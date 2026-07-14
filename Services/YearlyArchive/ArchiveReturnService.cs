using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

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
        private readonly IArchiveMaterialTransactionWriter _materialTransactionWriter;
        private readonly IArchiveSimulatedBoxSlotSyncService _simulatedBoxSlotSyncService;

        public ArchiveReturnService(
            IArchiveReturnRepository returnRepository,
            IArchiveOutboundRepository outboundRepository,
            IArchiveFilingFactRepository filingFactRepository,
            IArchiveFilingRepository filingRepository,
            IHardDiskMediaRepository hardDiskMediaRepository,
            IArchiveRegisterService archiveRegisterService,
            IBusinessRuleService businessRuleService,
            IArchiveMaterialTransactionWriter materialTransactionWriter,
            IArchiveSimulatedBoxSlotSyncService simulatedBoxSlotSyncService)
        {
            _returnRepository = returnRepository;
            _outboundRepository = outboundRepository;
            _filingFactRepository = filingFactRepository;
            _filingRepository = filingRepository;
            _hardDiskMediaRepository = hardDiskMediaRepository;
            _archiveRegisterService = archiveRegisterService;
            _businessRuleService = businessRuleService;
            _materialTransactionWriter = materialTransactionWriter;
            _simulatedBoxSlotSyncService = simulatedBoxSlotSyncService;
        }

        public bool IsArchiveAdminUser(User? user) => _archiveRegisterService.IsArchiveAdminUser(user);

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
                        existing.Status == YearlyArchiveReturnRecord.Registered
                            ? "已登记的归还单不可再修改，请直接办结入库或作废。"
                            : "已办结或已作废的归还单不可修改。");
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

            record.Status = request.SubmitForRegistration
                ? YearlyArchiveReturnRecord.Registered
                : YearlyArchiveReturnRecord.Draft;
            if (request.SubmitForRegistration)
            {
                record.RegisteredAt = now;
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

            string action = request.SubmitForRegistration ? "登记完成" : "草稿已保存";
            if (request.SubmitForRegistration)
            {
                return ArchiveReturnFlowResult.Ok(
                    $"{action}，当前状态：{record.StatusStr}。登记信息已锁定，可打印回执并办结入库。",
                    recordId);
            }

            return ArchiveReturnFlowResult.Ok($"{action}，当前状态：{record.StatusStr}。", recordId);
        }

        public async Task<ArchiveReturnFlowResult> CompleteReturnFlowAsync(int recordId, User admin)
        {
            ArgumentNullException.ThrowIfNull(admin);

            if (!IsArchiveAdminUser(admin))
            {
                return ArchiveReturnFlowResult.Fail("仅资料室管理员可办结资料归还。");
            }

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
            }

            if (record.Status != YearlyArchiveReturnRecord.Registered)
            {
                return ArchiveReturnFlowResult.Fail("只有“已登记”状态的归还单可办结入库。");
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

                await ApplyReturnContainerRehomeAsync(record, factsById, operatorName, now);

                foreach (var item in record.Items)
                {
                    var outboundItem = outbound.Items.FirstOrDefault(o => o.Id == item.SourceOutboundItemId);
                    if (outboundItem == null)
                    {
                        throw new InvalidOperationException($"归还明细对应的出库明细已不存在（明细：{item.ItemName}）。");
                    }

                    int intactCopyCount = ArchiveReturnDomainValues.ResolveIntactReturnCopyCount(item);
                    int lossCopyCount = ArchiveReturnDomainValues.ResolveLossCopyCount(item);

                    lifecycleUpdates.Add(BuildReturnLifecycleUpdate(record, item, intactCopyCount, lossCopyCount));

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
                }

                await _filingFactRepository.UpdateFilingFactLifecyclesAsync(lifecycleUpdates, operatorName, "资料归还");

                outbound.UpdatedAt = now;
                record.MarkAsCompleted(operatorName);
                record.UpdatedAt = now;

                await _returnRepository.SaveChangesAsync();

                await SyncSimulatedArchiveBoxSlotsAfterReturnAsync(record, factsById, now);

                await _materialTransactionWriter.AppendReturnCompletionTransactionsAsync(record, outbound);
                await _returnRepository.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return ArchiveReturnFlowResult.Fail(ex.Message);
            }

            return ArchiveReturnFlowResult.Ok($"资料归还办结完成，单据 {record.ReturnNo} 已入库。", record.Id);
        }

        public async Task<ArchiveReturnFlowResult> VoidReturnFlowAsync(int recordId, string? reason, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
            }

            if (record.RegisteredByUserId != user.Id && !IsArchiveAdminUser(user))
            {
                return ArchiveReturnFlowResult.Fail("仅登记人或资料室管理员可作废该归还单。");
            }

            if (record.Status is not (YearlyArchiveReturnRecord.Draft or YearlyArchiveReturnRecord.Registered))
            {
                return ArchiveReturnFlowResult.Fail(
                    record.Status == YearlyArchiveReturnRecord.Completed
                        ? "已办结的归还单不可作废。"
                        : "该归还单当前状态不可作废。");
            }

            record.MarkAsVoided(reason);
            record.UpdatedAt = DateTime.Now;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);

            return ArchiveReturnFlowResult.Ok($"归还单 {record.ReturnNo} 已作废。", record.Id);
        }

        private static FilingFactLifecycleUpdate BuildReturnLifecycleUpdate(
            YearlyArchiveReturnRecord record,
            YearlyArchiveReturnItem item,
            int intactCopyCount,
            int lossCopyCount)
        {
            string remark = lossCopyCount > 0
                ? $"归还单 {record.ReturnNo} 完好 {intactCopyCount} 份、灭失 {lossCopyCount} 份"
                : string.Empty;

            return new FilingFactLifecycleUpdate(
                item.FilingFactId,
                FilingFactLifecycleStatus.InArchive,
                FilingFactBorrowHintLevel.None,
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
