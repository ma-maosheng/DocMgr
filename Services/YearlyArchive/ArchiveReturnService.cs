using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还服务：对已办结出库的提档(借出原件)项收回入库，办结时在单一事务内反向冲销立档台账影响。
    /// </summary>
    public sealed partial class ArchiveReturnService : IArchiveReturnService
    {
        private readonly IArchiveReturnRepository _returnRepository;
        private readonly IArchiveOutboundRepository _outboundRepository;
        private readonly IArchiveFilingFactRepository _filingFactRepository;
        private readonly IArchiveFilingRepository _filingRepository;
        private readonly IHardDiskMediaRepository _hardDiskMediaRepository;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IBusinessRuleService _businessRuleService;
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;
        private readonly IArchiveMaterialTransactionWriter _materialTransactionWriter;
        private readonly IArchiveSimulatedBoxSlotSyncService _simulatedBoxSlotSyncService;
        private readonly IArchiveElectronicBagSlotSyncService _electronicBagSlotSyncService;
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private readonly IUserService _userService;

        public ArchiveReturnService(
            IArchiveReturnRepository returnRepository,
            IArchiveOutboundRepository outboundRepository,
            IArchiveFilingFactRepository filingFactRepository,
            IArchiveFilingRepository filingRepository,
            IHardDiskMediaRepository hardDiskMediaRepository,
            IArchiveRegisterService archiveRegisterService,
            IBusinessRuleService businessRuleService,
            IBusinessLogicSettingsService businessLogicSettingsService,
            IArchiveMaterialTransactionWriter materialTransactionWriter,
            IArchiveSimulatedBoxSlotSyncService simulatedBoxSlotSyncService,
            IArchiveElectronicBagSlotSyncService electronicBagSlotSyncService,
            IApprovalWorkflowService approvalWorkflowService,
            IUserService userService)
        {
            _returnRepository = returnRepository;
            _outboundRepository = outboundRepository;
            _filingFactRepository = filingFactRepository;
            _filingRepository = filingRepository;
            _hardDiskMediaRepository = hardDiskMediaRepository;
            _archiveRegisterService = archiveRegisterService;
            _businessRuleService = businessRuleService;
            _businessLogicSettingsService = businessLogicSettingsService;
            _materialTransactionWriter = materialTransactionWriter;
            _simulatedBoxSlotSyncService = simulatedBoxSlotSyncService;
            _electronicBagSlotSyncService = electronicBagSlotSyncService;
            _approvalWorkflowService = approvalWorkflowService;
            _userService = userService;
        }

        /// <summary>解析归还单签批链（按是否灭失等字段匹配规则）。</summary>
        public async Task<ApprovalChainResolution> ResolveApprovalChainAsync(
            YearlyArchiveReturnRecord record,
            string? applicantDept = null)
        {
            ArgumentNullException.ThrowIfNull(record);
            string? sourceDestinationKind = null;
            if (record.SourceOutboundRecordId > 0)
            {
                var outbound = await _outboundRepository.GetByIdWithDetailsAsync(record.SourceOutboundRecordId);
                sourceDestinationKind = outbound?.DestinationKind;
            }

            return await _approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveReturn,
                    ApplicantDept = applicantDept ?? record.BorrowerDept,
                    FieldValues = ApprovalChainApplySupport.BuildReturnFieldValues(record, sourceDestinationKind)
                },
                _userService.GetAllUsers());
        }

        public bool IsArchiveAdminUser(User? user) => _archiveRegisterService.IsArchiveAdminUser(user);

        public bool IsDepartmentArchiveAdmin(User? user) => _archiveRegisterService.IsDepartmentArchiveAdmin(user);

        public bool CanSubmitApplication(User? user) => _archiveRegisterService.CanSubmitApplication(user);

        public Task<List<YearlyArchiveOutboundRecord>> GetReturnableOutboundsAsync(int year) =>
            _returnRepository.GetReturnableOutboundsAsync(year);

        public async Task<List<YearlyArchiveReturnRecord>> ListReturnsAsync(int year, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var records = await _returnRepository.ListByYearAsync(year);
            if (IsArchiveAdminUser(user))
            {
                return records;
            }

            return records.Where(record => record.RegisteredByUserId == user.Id).ToList();
        }

        public async Task<YearlyArchiveReturnRecord?> GetReturnAsync(int id)
        {
            var record = await _returnRepository.GetByIdWithDetailsAsync(id);
            if (record == null)
            {
                return null;
            }

            var outbound = await _outboundRepository.GetByIdWithDetailsAsync(record.SourceOutboundRecordId);
            ArchiveReturnItemDisplaySupport.EnrichFromOutbound(record, outbound);
            foreach (var item in record.Items)
            {
                ArchiveReturnDomainValues.NormalizeReturnCopyCounts(item);
            }

            await EnrichContainerAssessmentsAsync(record);
            return record;
        }

        public Task<string> GenerateNextReturnNoAsync() =>
            _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.AssetReturnRegister);

        public async Task<YearlyArchiveReturnRecord> CreateDraftFromOutboundAsync(int outboundRecordId, User registrar)
        {
            ArgumentNullException.ThrowIfNull(registrar);

            if (!CanSubmitApplication(registrar))
            {
                throw new InvalidOperationException("仅部门资料员可发起资料归还申请。");
            }

            var outbound = await _outboundRepository.GetByIdWithDetailsAsync(outboundRecordId)
                ?? throw new InvalidOperationException("未找到指定的出库申请单。");

            if (outbound.Status != YearlyArchiveOutboundRecord.Completed)
            {
                throw new InvalidOperationException("只有“已办结出库”的申请单才能办理资料归还。");
            }

            if (await _returnRepository.HasActiveReturnForOutboundAsync(outbound.Id))
            {
                throw new InvalidOperationException($"出库单 {outbound.OutboundNo} 已存在未作废的归还单，请勿重复发起。");
            }

            var returnableItems = outbound.Items
                .Where(ArchiveReturnItemDisplaySupport.IsReturnableOutboundItem)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .ToList();

            if (returnableItems.Count == 0)
            {
                throw new InvalidOperationException("该出库单没有需归还的提档明细。");
            }

            DateTime now = DateTime.Now;
            var record = new YearlyArchiveReturnRecord
            {
                ReturnNo = await GenerateNextReturnNoAsync(),
                Status = YearlyArchiveReturnRecord.Draft,
                SourceOutboundRecordId = outbound.Id,
                SourceOutboundNo = outbound.OutboundNo,
                ArchiveYear = outbound.ArchiveYear,
                ProjectId = outbound.ProjectId,
                ProjectName = outbound.ProjectName,
                BorrowerName = outbound.ApplicantName,
                BorrowerDept = outbound.ApplicantDept,
                RegisteredByUserId = registrar.Id,
                RegisteredByName = ResolveUserName(registrar),
                RegisteredByDept = registrar.Department?.Trim() ?? string.Empty,
                ReturnDate = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            int sortOrder = 0;
            foreach (var item in returnableItems)
            {
                int registerMediaId = await ResolveRegisterMediaIdAsync(item.FilingFactId);
                var returnItem = new YearlyArchiveReturnItem
                {
                    SortOrder = sortOrder++,
                    SourceOutboundItemId = item.Id,
                    FilingFactId = item.FilingFactId,
                    RegisterMediaId = registerMediaId,
                    MediaKind = item.MediaKind,
                    UsageMode = item.UsageMode,
                    ReturnCopyCount = Math.Max(1, item.CopyCount ?? 1),
                    IntactReturnCopyCount = Math.Max(1, item.CopyCount ?? 1),
                    LossCopyCount = 0,
                    MaterialName = item.MaterialName,
                    ItemName = item.ItemName,
                    ContainerCode = item.ContainerCode,
                    StorageLocation = item.StorageLocation,
                    ItemCondition = ArchiveReturnDomainValues.ConditionComplete,
                    CreatedAt = now
                };
                ArchiveReturnItemDisplaySupport.ApplyOutboundSnapshot(returnItem, item);
                record.Items.Add(returnItem);
            }

            await EnrichContainerAssessmentsAsync(record);
            return record;
        }

        public async Task<ArchiveReturnFlowResult> SaveReturnFlowAsync(SaveReturnRequest request, User user)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(user);

            if (!CanSubmitApplication(user))
            {
                return ArchiveReturnFlowResult.Fail("仅部门资料员可保存或提交资料归还申请。");
            }

            var record = request.Record;
            if (record.SourceOutboundRecordId <= 0)
            {
                return ArchiveReturnFlowResult.Fail("归还单缺少源出库单信息。");
            }

            if (request.Items.Count == 0)
            {
                return ArchiveReturnFlowResult.Fail("请至少保留一条归还明细。");
            }

            if (record.Id > 0)
            {
                var existing = await _returnRepository.GetByIdWithDetailsAsync(record.Id);
                if (existing == null)
                {
                    return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
                }

                if (existing.Status != YearlyArchiveReturnRecord.Draft)
                {
                    return ArchiveReturnFlowResult.Fail(
                        existing.Status == YearlyArchiveReturnRecord.Submitted
                            ? "已提交的归还申请不可再修改，请前往审批或作废。"
                            : "当前状态的归还单不可修改。");
                }
            }
            else if (await _returnRepository.HasActiveReturnForOutboundAsync(record.SourceOutboundRecordId))
            {
                return ArchiveReturnFlowResult.Fail("该出库单已存在未作废的归还单，请勿重复发起。");
            }

            if (request.SubmitForRegistration)
            {
                string? registrationValidation = await ValidateForRegistrationAsync(record, request.Items);
                if (registrationValidation != null)
                {
                    return ArchiveReturnFlowResult.Fail(registrationValidation);
                }
            }

            foreach (var item in request.Items)
            {
                ArchiveReturnDomainValues.NormalizeReturnCopyCounts(item);
            }

            DateTime now = DateTime.Now;
            if (string.IsNullOrWhiteSpace(record.ReturnNo))
            {
                record.ReturnNo = await GenerateNextReturnNoAsync();
            }

            if (record.ReturnDate == default)
            {
                record.ReturnDate = now;
            }

            if (request.SubmitForRegistration)
            {
                record.MarkAsSubmitted();
            }
            else
            {
                record.MarkAsDraft();
            }

            record.UpdatedAt = now;
            if (record.CreatedAt == default)
            {
                record.CreatedAt = now;
            }

            record.Items = request.Items.OrderBy(item => item.SortOrder).ToList();
            for (int index = 0; index < record.Items.Count; index++)
            {
                record.Items[index].SortOrder = index;
                if (record.Items[index].CreatedAt == default)
                {
                    record.Items[index].CreatedAt = now;
                }
            }

            int recordId = await _returnRepository.SaveOrUpdateRecordGraphAsync(record);
            if (recordId > 0)
            {
                await _returnRepository.LinkOrphanAttachmentsToRecordAsync(
                    record.ReturnNo,
                    ArchiveReturnDomainValues.BusinessTypeAttachment,
                    recordId);
            }

            if (request.SubmitForRegistration)
            {
                return ArchiveReturnFlowResult.Ok(
                    $"归还申请已提交，当前状态：{record.StatusStr}。请等待资料室审批。",
                    recordId);
            }

            return ArchiveReturnFlowResult.Ok($"草稿已保存，当前状态：{record.StatusStr}。", recordId);
        }

        public async Task<ArchiveReturnFlowResult> ApproveReturnFlowAsync(
            int recordId,
            User admin,
            ArchiveReturnApprovalInput? approvalInput = null)
        {
            ArgumentNullException.ThrowIfNull(admin);

            if (!IsArchiveAdminUser(admin))
            {
                return ArchiveReturnFlowResult.Fail("仅资料管理员可审批归还申请。");
            }

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
            }

            var gate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    record.Status,
                    record.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
                OfflineApprovalLifecycleSupport.Action.ApprovePass);
            if (!gate.Allowed)
            {
                return ArchiveReturnFlowResult.Fail(gate.DenyMessage ?? "当前状态不允许审批通过。");
            }

            DateTime now = DateTime.Now;
            var input = approvalInput ?? new ArchiveReturnApprovalInput();

            // 先把 UI 输入写回记录，再按签批链校验必填节点。
            record.DeptHead = input.DeptHead?.Trim() ?? string.Empty;
            record.DeptHeadDate = input.DeptHeadDate;
            record.ArchiveRoomHead = input.ArchiveRoomHead?.Trim() ?? string.Empty;
            record.ArchiveRoomHeadDate = input.ArchiveRoomHeadDate;
            record.ProductionHead = input.ProductionHeadName?.Trim() ?? string.Empty;
            record.ProductionHeadDate = input.ProductionHeadDate;
            record.ArchiveDeputyPresident = input.ArchiveDeputyPresidentName?.Trim() ?? string.Empty;
            record.ArchiveDeputyPresidentDate = input.ArchiveDeputyPresidentDate;
            record.ProductionVicePresident = input.ProductionVicePresidentName?.Trim() ?? string.Empty;
            record.ProductionVicePresidentDate = input.ProductionVicePresidentDate;

            var chain = await ResolveApprovalChainAsync(record);
            var missing = ApprovalChainApplySupport.CollectMissingSignerErrors(
                chain,
                nodeKey => ApprovalChainApplySupport.ReadReturnSigner(record, nodeKey));
            if (missing.Count > 0)
            {
                return ArchiveReturnFlowResult.Fail(string.Join(Environment.NewLine, missing));
            }

            record.Status = gate.NextStatus ?? YearlyArchiveReturnRecord.Approved;
            record.ApprovedAt = now;
            if (chain.DeptHead.IsEnabled)
            {
                record.DeptHeadDate ??= now;
            }
            else
            {
                record.DeptHead = string.Empty;
                record.DeptHeadDate = null;
            }

            if (chain.ArchiveRoomHead.IsEnabled)
            {
                record.ArchiveRoomHeadDate ??= now;
                record.ApprovedAt ??= record.ArchiveRoomHeadDate ?? now;
            }
            else
            {
                record.ArchiveRoomHead = string.Empty;
                record.ArchiveRoomHeadDate = null;
                record.ApprovedAt = input.DeptHeadDate ?? now;
            }

            if (chain.ProductionHead.IsEnabled)
            {
                record.ProductionHeadDate ??= now;
            }
            else
            {
                record.ProductionHead = string.Empty;
                record.ProductionHeadDate = null;
            }

            if (chain.ArchiveDeputyPresident.IsEnabled)
            {
                record.ArchiveDeputyPresidentDate ??= now;
            }
            else
            {
                record.ArchiveDeputyPresident = string.Empty;
                record.ArchiveDeputyPresidentDate = null;
            }

            if (chain.ProductionVicePresident.IsEnabled)
            {
                record.ProductionVicePresidentDate ??= now;
            }
            else
            {
                record.ProductionVicePresident = string.Empty;
                record.ProductionVicePresidentDate = null;
            }

            record.ApprovalOpinion = string.IsNullOrWhiteSpace(input.ApprovalOpinion)
                ? "同意"
                : input.ApprovalOpinion.Trim();
            record.UpdatedAt = now;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);

            return ArchiveReturnFlowResult.Ok(
                $"审批信息录入成功。当前状态：{record.StatusStr}。请办理实物交接。",
                record.Id);
        }

        public async Task<ArchiveReturnFlowResult> ConfirmHandoverFlowAsync(
            int recordId,
            User admin,
            ArchiveReturnApprovalInput? handoverInput = null)
        {
            ArgumentNullException.ThrowIfNull(admin);

            if (!IsArchiveAdminUser(admin))
            {
                return ArchiveReturnFlowResult.Fail("仅资料管理员可确认实物交接。");
            }

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
            }

            var gate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    record.Status,
                    record.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
                OfflineApprovalLifecycleSupport.Action.ConfirmMidStep);
            if (!gate.Allowed)
            {
                return ArchiveReturnFlowResult.Fail(gate.DenyMessage ?? "当前状态不允许确认实物交接。");
            }

            var abnormalGate = await ValidateAbnormalReturnGateAsync(record);
            if (!abnormalGate.Success)
            {
                return abnormalGate;
            }

            var input = handoverInput ?? new ArchiveReturnApprovalInput();
            string handoverAdmin = input.HandoverAdmin?.Trim() ?? string.Empty;
            string handoverApplicant = input.HandoverApplicant?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(handoverAdmin))
            {
                return ArchiveReturnFlowResult.Fail("请填写办理交接人（资料管理员）。");
            }

            if (!input.HandoverDate.HasValue)
            {
                return ArchiveReturnFlowResult.Fail("请填写办理交接日期。");
            }

            if (string.IsNullOrWhiteSpace(handoverApplicant))
            {
                handoverApplicant = record.BorrowerName?.Trim()
                    ?? record.RegisteredByName?.Trim()
                    ?? string.Empty;
            }

            DateTime now = DateTime.Now;
            record.HandoverApplicant = handoverApplicant;
            record.HandoverAdmin = handoverAdmin;
            record.HandoverDate = input.HandoverDate.Value;
            record.Status = gate.NextStatus ?? YearlyArchiveReturnRecord.SignedUploaded;
            record.SignedUploadedAt = now;
            record.UpdatedAt = now;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);

            return ArchiveReturnFlowResult.Ok(
                $"实物交接确认成功。当前状态：{record.StatusStr}。请上传签批交接单。",
                record.Id);
        }

        public async Task<ArchiveReturnFlowResult> CompleteReturnFlowAsync(int recordId, User admin)
        {
            ArgumentNullException.ThrowIfNull(admin);

            if (!IsArchiveAdminUser(admin))
            {
                return ArchiveReturnFlowResult.Fail("仅资料管理员可办结资料归还。");
            }

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
            }

            var gate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    record.Status,
                    record.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
                OfflineApprovalLifecycleSupport.Action.Complete);
            if (!gate.Allowed)
            {
                return ArchiveReturnFlowResult.Fail(gate.DenyMessage ?? "当前状态不允许办结。");
            }

            if (record.PrintCount <= 0)
            {
                return ArchiveReturnFlowResult.Fail("请先打印交接单后再确认办结。");
            }

            var abnormalGate = await ValidateAbnormalReturnGateAsync(record);
            if (!abnormalGate.Success)
            {
                return abnormalGate;
            }

            var outbound = await _outboundRepository.GetByIdWithDetailsAsync(record.SourceOutboundRecordId);
            if (outbound == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到对应的源出库单。");
            }

            string operatorName = ResolveUserName(admin);
            DateTime now = DateTime.Now;

            await using var transaction = await _returnRepository.BeginTransactionAsync();
            try
            {
                var lifecycleUpdates = new List<FilingFactLifecycleUpdate>();
                var factIds = record.Items.Select(item => item.FilingFactId).Where(id => id > 0).Distinct().ToList();
                var factsById = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(factIds);
                var copySnapshotsBeforeReturn = await _outboundRepository
                    .GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(factIds);

                await ApplyReturnContainerRehomeAsync(record, factsById, operatorName, now);

                var returnEffectsByFactId = record.Items
                    .Where(item => item.FilingFactId > 0)
                    .GroupBy(item => item.FilingFactId)
                    .ToDictionary(
                        group => group.Key,
                        group => (
                            Borrowed: group.Sum(ArchiveReturnDomainValues.ResolveBorrowedCopyCount),
                            Intact: group.Sum(ArchiveReturnDomainValues.ResolveIntactReturnCopyCount),
                            Loss: group.Sum(ArchiveReturnDomainValues.ResolveLossCopyCount)));

                foreach (var item in record.Items)
                {
                    var outboundItem = outbound.Items.FirstOrDefault(o => o.Id == item.SourceOutboundItemId);
                    if (outboundItem == null)
                    {
                        throw new InvalidOperationException($"归还明细对应的出库明细已不存在（明细：{item.ItemName}）。");
                    }

                    int intactCopyCount = ArchiveReturnDomainValues.ResolveIntactReturnCopyCount(item);
                    int lossCopyCount = ArchiveReturnDomainValues.ResolveLossCopyCount(item);

                    outboundItem.ReservationStatus = ArchiveOutboundDomainValues.SyncEntryPhaseReturned;
                    outboundItem.ContainerStatusHint = ArchiveOutboundDomainValues.ContainerStatusHintNone;
                    outbound.SyncEntries.Add(new YearlyArchiveOutboundSyncEntry
                    {
                        OutboundRecordId = outbound.Id,
                        OutboundItemId = outboundItem.Id,
                        FilingFactId = item.FilingFactId,
                        EntryKind = ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReturned,
                        Phase = ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed,
                        OperatedBy = operatorName,
                        Remark = BuildReturnSyncRemark(record, item, intactCopyCount, lossCopyCount),
                        CreatedAt = now
                    });

                    if (intactCopyCount > 0
                        && factsById.TryGetValue(item.FilingFactId, out var returnFact))
                    {
                        if (RequiresFiledHardDiskReturnSync(item, outboundItem, returnFact))
                        {
                            await CompleteFiledHardDiskReturnAsync(
                                record,
                                item,
                                outboundItem,
                                returnFact,
                                operatorName,
                                now);
                        }

                        if (RequiresFiledOpticalDiscReturnSync(item, outboundItem, returnFact))
                        {
                            await CompleteFiledOpticalDiscReturnAsync(
                                record,
                                item,
                                outboundItem,
                                returnFact,
                                operatorName,
                                now);
                        }
                    }
                }

                foreach (var pair in returnEffectsByFactId)
                {
                    if (!factsById.TryGetValue(pair.Key, out var fact))
                    {
                        continue;
                    }

                    var snapshot = copySnapshotsBeforeReturn.GetValueOrDefault(pair.Key)
                        ?? new SimulatedFilingFactCopyCountSnapshot();
                    lifecycleUpdates.Add(BuildReturnLifecycleUpdate(
                        record,
                        fact,
                        snapshot,
                        pair.Value.Borrowed,
                        pair.Value.Intact,
                        pair.Value.Loss));
                }

                await _filingFactRepository.UpdateFilingFactLifecyclesAsync(lifecycleUpdates, operatorName, "资料归还");

                outbound.UpdatedAt = now;
                record.Status = gate.NextStatus ?? YearlyArchiveReturnRecord.Completed;
                record.CompletedAt = now;
                record.HandlerName = operatorName;
                record.UpdatedAt = now;

                await _returnRepository.SaveChangesAsync();

                var emptiedBoxes = await SyncSimulatedArchiveBoxSlotsAfterReturnAsync(record, factsById, now);
                await SyncElectronicArchiveBagSlotsAfterReturnAsync(record, factsById, now);
                var lossBoxIds = ResolveLossRelatedSimulatedBoxIds(record, factsById);
                var emptiedByLoss = emptiedBoxes
                    .Where(box => lossBoxIds.Contains(box.BoxId))
                    .ToList();

                var afterLifecycleByFactId = lifecycleUpdates.ToDictionary(
                    update => update.FilingFactId,
                    update => update.LifecycleStatus);
                await _materialTransactionWriter.AppendReturnCompletionTransactionsAsync(
                    record,
                    outbound,
                    afterLifecycleByFactId);
                await _returnRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                return ArchiveReturnFlowResult.Ok(
                    BuildCompleteSuccessMessage(record, emptiedByLoss),
                    record.Id);
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return ArchiveReturnFlowResult.Fail(ex.Message);
            }
        }

        public async Task<ArchiveReturnFlowResult> VoidReturnFlowAsync(int recordId, string? reason, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
            }

            bool isRoomAdmin = IsArchiveAdminUser(user);
            bool isApplicantSide = CanSubmitApplication(user) && record.RegisteredByUserId == user.Id;
            if (!isRoomAdmin && !isApplicantSide)
            {
                return ArchiveReturnFlowResult.Fail("仅登记人（部门资料员）或资料管理员可作废该归还单。");
            }

            if (isRoomAdmin)
            {
                DateTime applyTime = record.SubmittedAt ?? record.RegisteredAt ?? record.CreatedAt;
                string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
                bool isOverdue = _businessLogicSettingsService.IsEligibleForAdminForceVoid(applyTime, settingCode);

                var forceGate = OfflineApprovalLifecycleSupport.TryTransition(
                    new OfflineApprovalLifecycleSupport.GateContext(
                        record.Status,
                        record.SignedAttachmentUploaded,
                        OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                        OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin,
                        forceVoidEligible: isOverdue),
                    OfflineApprovalLifecycleSupport.Action.ForceVoid);
                if (!forceGate.Allowed)
                {
                    if (!isOverdue
                        && record.Status is not (
                            YearlyArchiveReturnRecord.Completed
                            or YearlyArchiveReturnRecord.WithdrawnVoid
                            or YearlyArchiveReturnRecord.ForceVoided
                            or YearlyArchiveReturnRecord.Approved
                            or YearlyArchiveReturnRecord.SignedUploaded))
                    {
                        return ArchiveReturnFlowResult.Fail(
                            _businessLogicSettingsService.BuildNotEligibleMessage(settingCode));
                    }

                    return ArchiveReturnFlowResult.Fail(
                        forceGate.DenyMessage ?? "当前归还单不允许强制撤回作废。");
                }

                string forceReason = string.IsNullOrWhiteSpace(reason) ? "资料管理员强制撤回作废" : reason.Trim();
                DateTime now = DateTime.Now;
                record.Status = forceGate.NextStatus ?? YearlyArchiveReturnRecord.ForceVoided;
                record.ForceVoidedAt = now;
                record.VoidedAt = now;
                record.ForceVoidReason = forceReason;
                record.VoidReason = forceReason;
                record.UpdatedAt = now;
                await _returnRepository.SaveOrUpdateRecordGraphAsync(record);
                return ArchiveReturnFlowResult.Ok($"归还单 {record.ReturnNo} 已强制作废。", record.Id);
            }

            var withdrawGate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    record.Status,
                    record.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.Applicant),
                OfflineApprovalLifecycleSupport.Action.Withdraw);
            if (!withdrawGate.Allowed)
            {
                return ArchiveReturnFlowResult.Fail(
                    withdrawGate.DenyMessage ?? "审批后的归还单不可由申请人撤回作废。");
            }

            string withdrawReason = reason?.Trim() ?? string.Empty;
            DateTime withdrawNow = DateTime.Now;
            record.Status = withdrawGate.NextStatus ?? YearlyArchiveReturnRecord.WithdrawnVoid;
            record.WithdrawnAt = withdrawNow;
            record.VoidedAt = withdrawNow;
            record.VoidReason = withdrawReason;
            record.UpdatedAt = withdrawNow;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);
            return ArchiveReturnFlowResult.Ok($"归还单 {record.ReturnNo} 已撤回作废。", record.Id);
        }

        private static FilingFactLifecycleUpdate BuildReturnLifecycleUpdate(
            YearlyArchiveReturnRecord record,
            YearlyArchiveFilingFact fact,
            SimulatedFilingFactCopyCountSnapshot snapshotBeforeReturn,
            int borrowedCopyCount,
            int intactCopyCount,
            int lossCopyCount)
        {
            int pendingAfter = Math.Max(0, snapshotBeforeReturn.PendingReturnCopyCount - Math.Max(0, borrowedCopyCount));
            int lostAfter = Math.Max(0, snapshotBeforeReturn.LostCopyCount) + Math.Max(0, lossCopyCount);
            int currentAfter = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                fact.ContentCount,
                pendingAfter,
                snapshotBeforeReturn.NoReturnCopyCount,
                lostAfter,
                snapshotBeforeReturn.InventoryLostCopyCount,
                snapshotBeforeReturn.InventoryScrapCopyCount);

            string copySummary = $"完好 {intactCopyCount} 份、灭失 {lossCopyCount} 份";
            string remark = lossCopyCount > 0
                ? $"归还单 {record.ReturnNo}：{copySummary}"
                : $"归还单 {record.ReturnNo} 完好入库";

            // 库内与待还均为 0，且本单含灭失：资料已无实物可管，标为已销毁。
            if (lossCopyCount > 0 && currentAfter <= 0 && pendingAfter <= 0)
            {
                return new FilingFactLifecycleUpdate(
                    fact.Id,
                    FilingFactLifecycleStatus.Destroyed,
                    FilingFactBorrowHintLevel.None,
                    string.Empty,
                    remark);
            }

            if (lossCopyCount > 0)
            {
                string hint = currentAfter > 0
                    ? $"部分灭失后库内 {currentAfter} 份（{copySummary}）"
                    : $"灭失 {lossCopyCount} 份，仍有待还 {pendingAfter} 份";
                string status = pendingAfter > 0 && currentAfter <= 0
                    ? FilingFactLifecycleStatus.Borrowed
                    : FilingFactLifecycleStatus.InArchive;
                string hintLevel = pendingAfter > 0 && currentAfter > 0
                    ? FilingFactBorrowHintLevel.PartialAvailable
                    : FilingFactBorrowHintLevel.None;
                return new FilingFactLifecycleUpdate(
                    fact.Id,
                    status,
                    hintLevel,
                    hint,
                    remark);
            }

            string intactStatus = pendingAfter > 0 && currentAfter <= 0
                ? FilingFactLifecycleStatus.Borrowed
                : FilingFactLifecycleStatus.InArchive;
            string intactHintLevel = pendingAfter > 0 && currentAfter > 0
                ? FilingFactBorrowHintLevel.PartialAvailable
                : pendingAfter > 0
                    ? FilingFactBorrowHintLevel.OriginalBorrowed
                    : FilingFactBorrowHintLevel.None;
            string intactHint = pendingAfter > 0 && currentAfter > 0
                ? $"归还后库内 {currentAfter} 份，仍有待还 {pendingAfter} 份"
                : pendingAfter > 0
                    ? $"归还后仍有待还 {pendingAfter} 份"
                    : string.Empty;

            return new FilingFactLifecycleUpdate(
                fact.Id,
                intactStatus,
                intactHintLevel,
                intactHint,
                remark);
        }

        private static string BuildReturnSyncRemark(
            YearlyArchiveReturnRecord record,
            YearlyArchiveReturnItem item,
            int intactCopyCount,
            int lossCopyCount)
        {
            string summary = ArchiveReturnDomainValues.BuildReturnCopyCountSummary(item);
            return $"资料归还办结（{record.ReturnNo}）：{summary}";
        }

        private async Task<int> ResolveRegisterMediaIdAsync(int filingFactId)
        {
            if (filingFactId <= 0)
            {
                return 0;
            }

            var fact = await _outboundRepository.GetFilingFactByIdAsync(filingFactId);
            return fact?.RegisterMediaId ?? 0;
        }

        private static string ResolveUserName(User user) =>
            string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName : user.RealName.Trim();
    }
}
