using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveOutboundService : IArchiveOutboundService
    {
        private readonly IArchiveOutboundRepository _outboundRepository;
        private readonly IArchiveFilingSearchService _searchService;
        private readonly IArchiveFilingFactRepository _filingFactRepository;
        private readonly IHardDiskMediaRepository _hardDiskMediaRepository;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IBusinessRuleService _businessRuleService;
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;
        private readonly IArchiveMaterialTransactionWriter _materialTransactionWriter;
        private readonly IArchiveMaterialTransactionRepository _materialTransactionRepository;
        private readonly IArchiveSimulatedBoxSlotSyncService _simulatedBoxSlotSyncService;

        public ArchiveOutboundService(
            IArchiveOutboundRepository outboundRepository,
            IArchiveFilingSearchService searchService,
            IArchiveFilingFactRepository filingFactRepository,
            IHardDiskMediaRepository hardDiskMediaRepository,
            IArchiveRegisterService archiveRegisterService,
            IBusinessRuleService businessRuleService,
            IBusinessLogicSettingsService businessLogicSettingsService,
            IArchiveMaterialTransactionWriter materialTransactionWriter,
            IArchiveMaterialTransactionRepository materialTransactionRepository,
            IArchiveSimulatedBoxSlotSyncService simulatedBoxSlotSyncService)
        {
            _outboundRepository = outboundRepository;
            _searchService = searchService;
            _filingFactRepository = filingFactRepository;
            _hardDiskMediaRepository = hardDiskMediaRepository;
            _archiveRegisterService = archiveRegisterService;
            _businessRuleService = businessRuleService;
            _businessLogicSettingsService = businessLogicSettingsService;
            _materialTransactionWriter = materialTransactionWriter;
            _materialTransactionRepository = materialTransactionRepository;
            _simulatedBoxSlotSyncService = simulatedBoxSlotSyncService;
        }

        public bool IsArchiveAdminUser(User? user) => _archiveRegisterService.IsArchiveAdminUser(user);

        public bool IsDepartmentArchiveAdmin(User? user) => _archiveRegisterService.IsDepartmentArchiveAdmin(user);

        public bool CanSubmitApplication(User? user) => _archiveRegisterService.CanSubmitApplication(user);

        public async Task<List<YearlyArchiveOutboundRecord>> ListRecordsAsync(OutboundListCriteria criteria, User user)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(criteria);

            List<YearlyArchiveOutboundRecord> source = IsArchiveAdminUser(user) && !criteria.OnlyMine
                ? await _outboundRepository.ListByYearAsync(criteria.Year)
                : await _outboundRepository.ListByApplicantUserIdAsync(user.Id, criteria.Year);

            IEnumerable<YearlyArchiveOutboundRecord> filtered = source;

            filtered = criteria.WorkspaceMode switch
            {
                ArchiveOutboundWorkspaceMode.Application => filtered,
                ArchiveOutboundWorkspaceMode.Approval => criteria.StatusFilter.HasValue
                    ? filtered.Where(r => r.Status == criteria.StatusFilter.Value)
                    : filtered,
                ArchiveOutboundWorkspaceMode.Handover => filtered.Where(r =>
                    r.Status == YearlyArchiveOutboundRecord.SignedUploaded
                        || r.Status == YearlyArchiveOutboundRecord.Completed),
                _ => filtered
            };

            if (criteria.StatusFilter.HasValue
                && criteria.WorkspaceMode != ArchiveOutboundWorkspaceMode.Approval)
            {
                filtered = filtered.Where(r => r.Status == criteria.StatusFilter.Value);
            }

            return filtered.OrderByDescending(r => r.OutboundNo).ToList();
        }

        public Task<List<int>> GetExistingApplyYearsAsync() =>
            _outboundRepository.GetExistingApplyYearsAsync();

        public async Task<YearlyArchiveOutboundRecord?> GetRecordAsync(int id)
        {
            var record = await _outboundRepository.GetByIdWithDetailsAsync(id);
            if (record != null)
            {
                await FillMissingOutboundItemArchivePurposesAsync(record.Items);
            }

            return record;
        }

        public async Task<YearlyArchiveOutboundRecord> CreateDraftRecordAsync(User applicant)
        {
            ArgumentNullException.ThrowIfNull(applicant);

            if (!CanSubmitApplication(applicant))
            {
                throw new InvalidOperationException("仅部门资料管理员可发起资料借出申请。");
            }

            DateTime now = DateTime.Now;
            return new YearlyArchiveOutboundRecord
            {
                OutboundNo = await GenerateNextOutboundNoAsync(),
                Status = YearlyArchiveOutboundRecord.Unsubmitted,
                ApplicantUserId = applicant.Id,
                ApplicantName = string.IsNullOrWhiteSpace(applicant.RealName) ? applicant.LoginName : applicant.RealName.Trim(),
                ApplicantDept = applicant.Department?.Trim() ?? string.Empty,
                ApplyDate = now,
                ArchiveYear = now.Year,
                DestinationKind = ArchiveOutboundDomainValues.DestinationInternal,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        public async Task<YearlyArchiveOutboundRecord> CreateDraftFromSearchPoolAsync(CreateOutboundFromPoolRequest request, User applicant)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(applicant);

            bool isAdmin = IsArchiveAdminUser(applicant);
            var resultSet = await _searchService.GetSearchPoolAsync(request.ResultSetId, applicant, isAdmin)
                ?? throw new InvalidOperationException("未找到指定的检索池。");

            var selectedIds = request.ResultSetItemIds?.Count > 0
                ? request.ResultSetItemIds.ToHashSet()
                : resultSet.Items.Select(item => item.Id).ToHashSet();

            var selectedItems = resultSet.Items
                .Where(item => selectedIds.Contains(item.Id))
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .ToList();

            if (selectedItems.Count == 0)
            {
                throw new InvalidOperationException("请至少选择一条检索池明细。");
            }

            var record = await CreateDraftRecordAsync(applicant);
            record.MaterialSummary = string.Empty;

            var currentLocations = await _searchService.GetCurrentStorageLocationsByFilingFactIdsAsync(
                selectedItems.Select(item => item.FilingFactId).Distinct().ToList());

            int sortOrder = 0;
            foreach (var poolItem in selectedItems)
            {
                var fact = await _outboundRepository.GetFilingFactByIdAsync(poolItem.FilingFactId);
                currentLocations.TryGetValue(poolItem.FilingFactId, out string? currentLocation);

                var outboundItem = MapPoolItemToOutboundItem(poolItem, fact, currentLocation, sortOrder++, resultSet.Id);
                await EnrichOutboundItemFromFactAsync(outboundItem, fact);
                record.Items.Add(outboundItem);
            }

            record.MaterialSummary = BuildMaterialSummaryFromOutboundItems(record.Items);
            return record;
        }

        public Task<string> GenerateNextOutboundNoAsync() =>
            _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.AssetOutboundApply);

        public async Task<ArchiveOutboundFlowResult> SaveDraftFlowAsync(SaveOutboundDraftRequest request, User user)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(user);

            if (!CanSubmitApplication(user))
            {
                return ArchiveOutboundFlowResult.Fail("仅部门资料管理员可保存资料借出申请草稿。");
            }

            var validation = ValidateDraft(request.Record, request.Items, user, requireSubmittedFields: false);
            if (!validation.Success)
            {
                return validation;
            }

            var record = request.Record;
            if (record.Id > 0)
            {
                var existing = await _outboundRepository.GetByIdWithDetailsAsync(record.Id);
                if (existing == null)
                {
                    return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
                }

                if (existing.Status != YearlyArchiveOutboundRecord.Unsubmitted)
                {
                    return ArchiveOutboundFlowResult.Fail("只有“未提交”状态的申请单才能保存。");
                }

                record.Status = existing.Status;
            }
            else if (record.Status != YearlyArchiveOutboundRecord.Unsubmitted)
            {
                return ArchiveOutboundFlowResult.Fail("只有“未提交”状态的申请单才能保存。");
            }
            if (string.Equals(record.DestinationKind, ArchiveOutboundDomainValues.DestinationInternal, StringComparison.Ordinal))
            {
                record.ExternalUnit = string.Empty;
            }

            record.ProofMaterialNote = NormalizeProofMaterialNote(record.ProofMaterialNote);

            if (string.IsNullOrWhiteSpace(record.OutboundNo))
            {
                record.OutboundNo = await GenerateNextOutboundNoAsync();
            }

            record.UpdatedAt = DateTime.Now;
            record.Items = request.Items.OrderBy(item => item.SortOrder).ToList();
            for (int index = 0; index < record.Items.Count; index++)
            {
                record.Items[index].SortOrder = index;
                record.Items[index].CreatedAt = record.Items[index].CreatedAt == default ? DateTime.Now : record.Items[index].CreatedAt;
            }

            record.MaterialSummary = record.Items.Count > 0
                ? ArchiveOutboundItemDescription.BuildMaterialSummary(record.Items)
                : string.Empty;

            ArchiveOutboundReturnSupport.SyncRecordExpectedReturnDate(record, record.Items);

            int recordId = await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
            await _outboundRepository.LinkOrphanAttachmentsToRecordAsync(
                record.OutboundNo,
                ArchiveOutboundDomainValues.BusinessTypeAttachment,
                recordId);

            return ArchiveOutboundFlowResult.Ok($"保存成功，当前状态：{record.StatusStr}。");
        }

        public async Task<ArchiveOutboundFlowResult> SubmitApplicationFlowAsync(int recordId, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (!CanSubmitApplication(user))
            {
                return ArchiveOutboundFlowResult.Fail("仅部门资料管理员可提交资料借出申请。");
            }

            if (record.ApplicantUserId != user.Id && !ArchiveRegisterBusinessRules.IsSystemAdministrator(user))
            {
                return ArchiveOutboundFlowResult.Fail("仅申请人本人可提交该申请。");
            }

            if (record.Status != YearlyArchiveOutboundRecord.Unsubmitted)
            {
                return ArchiveOutboundFlowResult.Fail("只有“未提交”状态的申请单才能提交。");
            }

            var validation = ValidateDraft(record, record.Items, user, requireSubmittedFields: true);
            if (!validation.Success)
            {
                return validation;
            }

            var simulatedStockErrors = await CollectSimulatedOutboundStockErrorsAsync(record);
            if (simulatedStockErrors.Count > 0)
            {
                return ArchiveOutboundFlowResult.Fail(
                    "提交申请校验未通过：\n\n" + string.Join(Environment.NewLine, simulatedStockErrors));
            }

            var electronicReservationErrors = await CollectElectronicWithdrawalReservationErrorsAsync(record);
            if (electronicReservationErrors.Count > 0)
            {
                return ArchiveOutboundFlowResult.Fail(
                    "提交申请校验未通过：\n\n" + string.Join(Environment.NewLine, electronicReservationErrors));
            }

            var electronicErrors = await CollectElectronicWithdrawalErrorsAsync(record.Items);
            if (electronicErrors.Count > 0)
            {
                return ArchiveOutboundFlowResult.Fail(
                    "申请信息不完整：\n\n" + string.Join(Environment.NewLine, electronicErrors));
            }

            var copyDiskCapacityErrors = await CollectCopyDiskCapacityErrorsAsync(record.Items);
            if (copyDiskCapacityErrors.Count > 0)
            {
                return ArchiveOutboundFlowResult.Fail(
                    "提交申请校验未通过：\n\n" + string.Join(Environment.NewLine, copyDiskCapacityErrors));
            }

            await using var transaction = await _outboundRepository.BeginTransactionAsync();
            try
            {
                record.MarkAsSubmitted();
                record.ApprovalDeadline = DateTime.Now.AddDays(ArchiveOutboundDomainValues.DefaultApprovalDeadlineDays);
                record.UpdatedAt = DateTime.Now;

                int savedId = await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
                record = await _outboundRepository.GetByIdWithDetailsAsync(savedId)
                    ?? throw new InvalidOperationException("提交后未能重新加载申请单。");

                await ApplySubmitSyncAsync(record, user);
                await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
                await transaction.CommitAsync();
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return ArchiveOutboundFlowResult.Fail(ex.Message);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                return ArchiveOutboundFlowResult.Fail(
                    "提交申请保存失败，请重启应用以完成数据库升级后重试。详情："
                    + (ex.InnerException?.Message ?? ex.Message));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return ArchiveOutboundFlowResult.Ok("提交成功。系统已执行提交同步，请打印申请单并等待审批。");
        }

        public async Task<ArchiveOutboundFlowResult> WithdrawApplicationFlowAsync(int recordId, string? reason, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (!CanSubmitApplication(user))
            {
                return ArchiveOutboundFlowResult.Fail("仅部门资料管理员可撤回资料借出申请。");
            }

            if (record.ApplicantUserId != user.Id
                && !ArchiveRegisterBusinessRules.IsSystemAdministrator(user))
            {
                return ArchiveOutboundFlowResult.Fail("仅申请人本人可撤回该申请。");
            }

            if (!record.CanApplicantWithdraw)
            {
                return ArchiveOutboundFlowResult.Fail("当前申请单已录入审批信息或状态不允许撤回作废。");
            }

            record.MarkAsWithdrawnVoid(reason);
            record.UpdatedAt = DateTime.Now;

            await using var transaction = await _outboundRepository.BeginTransactionAsync();
            try
            {
                await ApplyCancelSyncAsync(record, user, "撤回申请");
                await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return ArchiveOutboundFlowResult.Ok("撤回成功，相关预订与征用已注销。");
        }

        public async Task<ArchiveOutboundFlowResult> ForceVoidByAdminFlowAsync(int recordId, string reason, User admin)
        {
            ArgumentNullException.ThrowIfNull(admin);

            if (!IsArchiveAdminUser(admin))
            {
                return ArchiveOutboundFlowResult.Fail("仅资料室管理员可强制作废申请。");
            }

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (!record.CanForceVoid)
            {
                return ArchiveOutboundFlowResult.Fail("仅“已提交”且尚未审批的申请单可强制作废。");
            }

            string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
            DateTime applyDate = ApplicationOverdueSettingSupport.ResolveOutboundApplyDate(record);
            if (!_businessLogicSettingsService.IsEligibleForAdminForceVoid(applyDate, settingCode))
            {
                return ArchiveOutboundFlowResult.Fail(_businessLogicSettingsService.BuildNotEligibleMessage(settingCode));
            }

            record.MarkAsForceVoided(ArchiveOutboundDomainValues.ForceVoidKindAdminManual, reason);
            record.UpdatedAt = DateTime.Now;

            await using var transaction = await _outboundRepository.BeginTransactionAsync();
            try
            {
                await ApplyCancelSyncAsync(record, admin, "管理员强制作废");
                await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return ArchiveOutboundFlowResult.Ok("强制作废成功。");
        }

        public async Task<int> ProcessOverdueAutoForceVoidAsync(DateTime asOf)
        {
            var records = await _outboundRepository.GetSubmittedRecordsPastDeadlineAsync(asOf);
            int count = 0;

            foreach (var record in records)
            {
                if (!record.CanForceVoid)
                {
                    continue;
                }

                var detailed = await _outboundRepository.GetByIdWithDetailsAsync(record.Id);
                if (detailed == null)
                {
                    continue;
                }

                detailed.MarkAsForceVoided(ArchiveOutboundDomainValues.ForceVoidKindOverdueAuto, "提交后逾期未审批，系统自动强制作废。");
                detailed.UpdatedAt = DateTime.Now;

                await using var transaction = await _outboundRepository.BeginTransactionAsync();
                try
                {
                    await ApplyCancelSyncAsync(detailed, operatorName: "系统", remark: "逾期未审批自动强制作废");
                    await _outboundRepository.SaveOrUpdateRecordGraphAsync(detailed);
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                count++;
            }

            return count;
        }

        public async Task<ArchiveOutboundFlowResult> SaveApprovalFlowAsync(YearlyArchiveOutboundRecord record, User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(operatorUser);

            if (!IsArchiveAdminUser(operatorUser))
            {
                return ArchiveOutboundFlowResult.Fail("仅资料室管理员可录入审批信息。");
            }

            if (record.Status != YearlyArchiveOutboundRecord.Submitted)
            {
                return ArchiveOutboundFlowResult.Fail("只有「已提交」状态的申请单可审批通过。");
            }

            var existing = await _outboundRepository.GetByIdWithDetailsAsync(record.Id);
            if (existing == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            CopyApprovalFields(existing, record);
            existing.UpdatedAt = DateTime.Now;

            if (IsApprovalComplete(existing))
            {
                existing.MarkAsApproved();
            }

            await _outboundRepository.SaveOrUpdateRecordGraphAsync(existing);
            return ArchiveOutboundFlowResult.Ok(existing.IsApproved ? "审批信息已保存，申请单状态：已审批。" : "审批信息已保存。");
        }

        public Task ApplyDefaultApprovalInfoAsync(YearlyArchiveOutboundRecord record, User operatorUser) =>
            _archiveRegisterService.ApplyDefaultOutboundApprovalInfoAsync(record, operatorUser);

        public async Task<ArchiveOutboundFlowResult> UploadAttachmentFlowAsync(
            int recordId,
            string attachmentKind,
            SystemAttachment attachment,
            User user)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            ArgumentNullException.ThrowIfNull(user);

            string? formatError = SystemAttachmentUploadSupport.ValidateUploadFormat(
                attachment.FileName,
                attachment.Extension,
                attachment.FileContent);
            if (!string.IsNullOrWhiteSpace(formatError))
            {
                return ArchiveOutboundFlowResult.Fail(formatError);
            }

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (string.IsNullOrWhiteSpace(record.OutboundNo))
            {
                return ArchiveOutboundFlowResult.Fail("申请单号为空，无法上传附件。");
            }

            bool isProofMaterialScan = string.Equals(
                attachmentKind,
                ArchiveOutboundDomainValues.AttachmentKindProofMaterialScan,
                StringComparison.Ordinal);
            bool isSignedApproval = string.Equals(
                attachmentKind,
                ArchiveOutboundDomainValues.AttachmentKindSignedApprovalForm,
                StringComparison.Ordinal);

            if (isProofMaterialScan || isSignedApproval)
            {
                if (!IsArchiveAdminUser(user))
                {
                    return ArchiveOutboundFlowResult.Fail("仅资料室管理员可上传审批附件。");
                }

                if (isProofMaterialScan)
                {
                    if (record.Status is not (YearlyArchiveOutboundRecord.Submitted or YearlyArchiveOutboundRecord.Approved))
                    {
                        return ArchiveOutboundFlowResult.Fail("仅已提交或已审批通过的申请单可上传证明材料扫描件。");
                    }
                }
                else if (record.Status != YearlyArchiveOutboundRecord.SignedUploaded)
                {
                    return ArchiveOutboundFlowResult.Fail("请先确认实物交接后再上传签批交接单。");
                }
            }

            bool isHandoverAttachment = string.Equals(attachmentKind, ArchiveOutboundDomainValues.AttachmentKindSignedHandoverForm, StringComparison.Ordinal)
                || string.Equals(attachmentKind, ArchiveOutboundDomainValues.AttachmentKindMaterialPhoto, StringComparison.Ordinal);

            if (isHandoverAttachment)
            {
                if (!IsArchiveAdminUser(user))
                {
                    return ArchiveOutboundFlowResult.Fail("仅资料室管理员可上传出库附件。");
                }

                if (record.Status != YearlyArchiveOutboundRecord.SignedUploaded)
                {
                    return ArchiveOutboundFlowResult.Fail("只有「已办结审批」状态的申请单可上传交接附件。");
                }
            }

            attachment.BusinessType = ArchiveOutboundDomainValues.BusinessTypeAttachment;
            attachment.BusinessNo = record.OutboundNo;
            attachment.BusinessId = record.Id > 0 ? record.Id : 0;
            attachment.FileCategory = attachmentKind;
            attachment.UploaderName = string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName : user.RealName;
            attachment.UploadTime = DateTime.Now;

            _outboundRepository.AddAttachment(attachment);
            await _outboundRepository.SaveChangesAsync();

            if (record.Id > 0)
            {
                await _outboundRepository.LinkOrphanAttachmentsToRecordAsync(
                    record.OutboundNo,
                    ArchiveOutboundDomainValues.BusinessTypeAttachment,
                    record.Id);
            }

            return ArchiveOutboundFlowResult.Ok("附件上传成功。");
        }

        public async Task<ArchiveOutboundAttachmentFlowResult> DeleteAttachmentFlowAsync(
            int recordId,
            SystemAttachment attachment,
            User user)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            ArgumentNullException.ThrowIfNull(user);

            if (!IsArchiveAdminUser(user))
            {
                return ArchiveOutboundAttachmentFlowResult.Fail("仅资料室管理员可删除附件。");
            }

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundAttachmentFlowResult.Fail("未找到指定的出库申请单。");
            }

            var existing = await _outboundRepository.GetAttachmentByIdAsync(attachment.Id);
            if (existing == null || existing.BusinessId != recordId)
            {
                return ArchiveOutboundAttachmentFlowResult.Fail("附件不存在或不属于当前申请单。");
            }

            bool isProofMaterialScan = string.Equals(
                existing.FileCategory,
                ArchiveOutboundDomainValues.AttachmentKindProofMaterialScan,
                StringComparison.Ordinal);
            bool isSignedApproval = string.Equals(
                existing.FileCategory,
                ArchiveOutboundDomainValues.AttachmentKindSignedApprovalForm,
                StringComparison.Ordinal);
            bool isApprovalAttachment = isProofMaterialScan || isSignedApproval;
            bool isHandoverAttachment = existing.FileCategory is
                ArchiveOutboundDomainValues.AttachmentKindSignedHandoverForm
                or ArchiveOutboundDomainValues.AttachmentKindMaterialPhoto;

            if (!isApprovalAttachment && !isHandoverAttachment)
            {
                return ArchiveOutboundAttachmentFlowResult.Fail("该附件类型不允许删除。");
            }

            if (isProofMaterialScan
                && record.Status is not (YearlyArchiveOutboundRecord.Submitted or YearlyArchiveOutboundRecord.Approved))
            {
                return ArchiveOutboundAttachmentFlowResult.Fail("当前状态不允许删除证明材料扫描件。");
            }

            if (isSignedApproval && record.Status != YearlyArchiveOutboundRecord.SignedUploaded)
            {
                return ArchiveOutboundAttachmentFlowResult.Fail("请先确认实物交接后再删除附件。");
            }

            if (isHandoverAttachment && record.Status != YearlyArchiveOutboundRecord.SignedUploaded)
            {
                return ArchiveOutboundAttachmentFlowResult.Fail("当前状态不允许删除交接附件。");
            }

            _outboundRepository.RemoveAttachment(existing);
            await _outboundRepository.SaveChangesAsync();
            return ArchiveOutboundAttachmentFlowResult.Ok("附件已删除。");
        }

        public async Task<ArchiveOutboundAttachmentFlowResult> PrepareAttachmentViewFlowAsync(SystemAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);

            var full = await _outboundRepository.GetAttachmentByIdAsync(attachment.Id);
            if (full?.FileContent == null || full.FileContent.Length == 0)
            {
                return ArchiveOutboundAttachmentFlowResult.Fail("附件内容为空，无法查看。");
            }

            return ArchiveOutboundAttachmentFlowResult.Ok("附件已就绪", full);
        }

        public async Task<ArchiveOutboundApprovalValidationResult> ValidateApprovalPhaseAsync(YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            IReadOnlyList<SystemAttachment> attachments = record.Id > 0
                ? await _outboundRepository.GetAttachmentsByBusinessIdAsync(record.Id)
                : Array.Empty<SystemAttachment>();

            var errors = CollectApprovalPhaseErrors(record, attachments);
            return new ArchiveOutboundApprovalValidationResult(errors);
        }

        public async Task<ArchiveOutboundFlowResult> CompleteApprovalPhaseFlowAsync(YearlyArchiveOutboundRecord record, User user)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(user);

            if (!IsArchiveAdminUser(user))
            {
                return ArchiveOutboundFlowResult.Fail("仅资料室管理员可确认审批阶段办结。");
            }

            var existing = await _outboundRepository.GetByIdWithDetailsAsync(record.Id);
            if (existing == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (existing.Status != YearlyArchiveOutboundRecord.Approved)
            {
                return ArchiveOutboundFlowResult.Fail("请先审批通过后再办结审批阶段。");
            }

            CopyApprovalFields(existing, record);
            existing.UpdatedAt = DateTime.Now;

            var attachments = await _outboundRepository.GetAttachmentsByBusinessIdAsync(existing.Id);
            var errors = CollectApprovalPhaseErrors(existing, attachments);
            if (errors.Count > 0)
            {
                return ArchiveOutboundFlowResult.Fail("审批信息验证未通过：\n\n" + string.Join(Environment.NewLine, errors));
            }

            existing.MarkAsSignedUploaded();
            existing.UpdatedAt = DateTime.Now;
            await _outboundRepository.SaveOrUpdateRecordGraphAsync(existing);

            return ArchiveOutboundFlowResult.Ok("实物交接确认成功，请上传签批交接单。");
        }

        public async Task<ArchiveOutboundFlowResult> CompletePhysicalOutboundFlowAsync(int recordId, string handoverRemark, User admin)
        {
            ArgumentNullException.ThrowIfNull(admin);

            if (!IsArchiveAdminUser(admin))
            {
                return ArchiveOutboundFlowResult.Fail("仅资料室管理员可办理资料出库。");
            }

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (record.Status != YearlyArchiveOutboundRecord.SignedUploaded)
            {
                return ArchiveOutboundFlowResult.Fail("只有「已办结审批」状态的申请单可办理资料出库。");
            }

            var attachments = await _outboundRepository.GetAttachmentsByBusinessIdAsync(record.Id);
            bool hasHandover = attachments.Any(a =>
                string.Equals(a.FileCategory, ArchiveOutboundDomainValues.AttachmentKindSignedHandoverForm, StringComparison.Ordinal));
            bool hasPhoto = attachments.Any(a =>
                string.Equals(a.FileCategory, ArchiveOutboundDomainValues.AttachmentKindMaterialPhoto, StringComparison.Ordinal));

            if (!hasHandover || !hasPhoto)
            {
                return ArchiveOutboundFlowResult.Fail("请先上传“交接签字交接单”和“资料照片”。");
            }

            string operatorName = string.IsNullOrWhiteSpace(admin.RealName) ? admin.LoginName : admin.RealName.Trim();

            await using var transaction = await _outboundRepository.BeginTransactionAsync();
            try
            {
                await ApplyPhysicalCompletionSyncAsync(record, operatorName);
                record.HandoverRemark = handoverRemark?.Trim() ?? string.Empty;
                record.PhysicallyCompletedBy = operatorName;
                record.MarkAsCompleted();
                record.UpdatedAt = DateTime.Now;
                await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);

                var boxSyncFactIds = record.Items.Select(item => item.FilingFactId).Where(id => id > 0).Distinct().ToList();
                var boxSyncFactsById = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(boxSyncFactIds);
                await SyncSimulatedArchiveBoxSlotsAfterOutboundAsync(record, boxSyncFactsById, DateTime.Now);

                await _materialTransactionWriter.AppendOutboundCompletionTransactionsAsync(record);
                await _materialTransactionRepository.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return ArchiveOutboundFlowResult.Fail(ex.Message);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return ArchiveOutboundFlowResult.Ok("资料出库办结成功（已办结出库），台账与立档事实已同步。");
        }

        public Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(int recordId) =>
            _outboundRepository.GetAttachmentsByBusinessIdAsync(recordId).ContinueWith(
                task => (IReadOnlyList<SystemAttachment>)task.Result,
                TaskScheduler.Default);

        private async Task ApplySubmitSyncAsync(YearlyArchiveOutboundRecord record, User user)
        {
            string operatorName = string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName : user.RealName.Trim();
            DateTime now = DateTime.Now;
            record.SyncEntries ??= new List<YearlyArchiveOutboundSyncEntry>();

            foreach (var item in record.Items)
            {
                string phase = item.UsageMode switch
                {
                    ArchiveOutboundDomainValues.UsageModeWithdrawal => ArchiveOutboundDomainValues.SyncEntryPhaseActive,
                    ArchiveOutboundDomainValues.UsageModeCopy or ArchiveOutboundDomainValues.UsageModeDuplicate
                        => ArchiveOutboundDomainValues.SyncEntryPhasePending,
                    _ => ArchiveOutboundDomainValues.SyncEntryPhasePending
                };

                string entryKind = item.UsageMode switch
                {
                    ArchiveOutboundDomainValues.UsageModeWithdrawal => ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReservation,
                    ArchiveOutboundDomainValues.UsageModeCopy => ArchiveOutboundDomainValues.SyncEntryKindCopyLedger,
                    ArchiveOutboundDomainValues.UsageModeDuplicate => ArchiveOutboundDomainValues.SyncEntryKindDuplicateLedger,
                    _ => ArchiveOutboundDomainValues.SyncEntryKindCopyLedger
                };

                record.SyncEntries.Add(new YearlyArchiveOutboundSyncEntry
                {
                    OutboundRecordId = record.Id,
                    OutboundItemId = item.Id,
                    FilingFactId = item.FilingFactId,
                    EntryKind = entryKind,
                    Phase = phase,
                    OperatedBy = operatorName,
                    CreatedAt = now
                });

                item.ReservationStatus = phase;

                if (item.RequisitionedMediumId is int mediumId
                    && string.Equals(item.ElectronicMediaSource, ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank, StringComparison.Ordinal))
                {
                    await ApplyHardDiskRequisitionLockAsync(record, item, mediumId, operatorName);
                }
            }
        }

        private async Task ApplyHardDiskRequisitionLockAsync(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item,
            int mediumId,
            string operatorName)
        {
            var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(mediumId);
            if (medium == null)
            {
                throw new InvalidOperationException($"未找到库内空盘 [{item.RequisitionedDiskCode}]。");
            }

            if (medium.RegisterLock != null)
            {
                var lockItem = medium.RegisterLock;
                if (string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeArchiveOutboundRequisition, StringComparison.Ordinal)
                    && string.Equals(lockItem.BusinessNo, record.OutboundNo, StringComparison.OrdinalIgnoreCase))
                {
                    item.RequisitionedDiskCode = medium.DiskCode;
                    return;
                }

                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 已被【{lockItem.BusinessNo}】占用，无法征用。");
            }

            string currentStatus = medium.Ledger?.MediaStatus ?? string.Empty;
            if (!string.Equals(currentStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 当前状态为“{currentStatus}”，不可征用。");
            }

            medium.RegisterLock = new HardDiskRegisterLock
            {
                MediumId = medium.Id,
                BusinessType = HardDiskRegisterLock.BusinessTypeArchiveOutboundRequisition,
                BusinessRecordId = record.Id > 0 ? record.Id : null,
                BusinessNo = record.OutboundNo,
                PreviousStatus = currentStatus,
                LockedTime = DateTime.Now
            };
            medium.UpdatedTime = DateTime.Now;
            item.RequisitionedDiskCode = medium.DiskCode;
        }

        private async Task ApplyCancelSyncAsync(YearlyArchiveOutboundRecord record, User? user, string remark)
        {
            await ApplyCancelSyncAsync(record, user == null
                ? "系统"
                : string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName : user.RealName.Trim(), remark);
        }

        private async Task ApplyCancelSyncAsync(YearlyArchiveOutboundRecord record, string operatorName, string remark)
        {
            DateTime now = DateTime.Now;
            foreach (var entry in record.SyncEntries.Where(e =>
                         e.Phase is ArchiveOutboundDomainValues.SyncEntryPhaseActive
                             or ArchiveOutboundDomainValues.SyncEntryPhasePending))
            {
                entry.Phase = ArchiveOutboundDomainValues.SyncEntryPhaseCancelled;
                entry.UpdatedAt = now;
                entry.Remark = remark;
                entry.OperatedBy = operatorName;
            }

            foreach (var item in record.Items)
            {
                item.ReservationStatus = ArchiveOutboundDomainValues.SyncEntryPhaseCancelled;
            }

            foreach (var item in record.Items.Where(i => i.RequisitionedMediumId is int mediumId && mediumId > 0))
            {
                await TryReleaseHardDiskRequisitionLockAsync(item.RequisitionedMediumId!.Value, record.OutboundNo);
            }
        }

        private async Task TryReleaseHardDiskRequisitionLockAsync(int mediumId, string outboundNo)
        {
            var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(mediumId);
            if (medium?.RegisterLock == null)
            {
                return;
            }

            var lockItem = medium.RegisterLock;
            if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeArchiveOutboundRequisition, StringComparison.Ordinal)
                || !string.Equals(lockItem.BusinessNo, outboundNo, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            medium.RegisterLock = null;
            medium.UpdatedTime = DateTime.Now;
        }

        private static ArchiveOutboundFlowResult ValidateDraft(
            YearlyArchiveOutboundRecord record,
            IReadOnlyList<YearlyArchiveOutboundItem> items,
            User user,
            bool requireSubmittedFields)
        {
            _ = user;

            var errors = CollectSubmitValidationErrors(record, items, requireSubmittedFields);

            if (errors.Count > 0)
            {
                return ArchiveOutboundFlowResult.Fail("申请信息不完整：\n\n" + string.Join(Environment.NewLine, errors));
            }

            return ArchiveOutboundFlowResult.Ok(string.Empty);
        }

        private static void ValidateItem(YearlyArchiveOutboundItem item, List<string> errors, bool requireSubmittedFields)
        {
            if (item.FilingFactId <= 0)
            {
                errors.Add("• 存在未关联立档事实的明细。");
                return;
            }

            if (string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
            {
                if (item.UsageMode is not (ArchiveOutboundDomainValues.UsageModeWithdrawal or ArchiveOutboundDomainValues.UsageModeCopy))
                {
                    errors.Add($"• [{item.ItemName}] 模拟介质仅支持提档或复制。");
                }
            }
            else if (string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                if (item.UsageMode is not (ArchiveOutboundDomainValues.UsageModeWithdrawal or ArchiveOutboundDomainValues.UsageModeDuplicate))
                {
                    errors.Add($"• [{item.ItemName}] 电子介质仅支持提档或拷贝。");
                }

                if (requireSubmittedFields
                    && ArchiveOutboundDomainValues.IsLongTermElectronicArchivePurpose(item.ArchivePurpose)
                    && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal)
                {
                    errors.Add($"• [{item.ItemName}] 长期存档的电子介质资料不允许提档借出，请选择拷贝。");
                }
            }

            if (requireSubmittedFields
                && string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                && item.CopyCount != 1)
            {
                errors.Add($"• [{item.ItemName}] 电子介质份数只能为 1。");
            }

            if (requireSubmittedFields
                && string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
            {
                if (item.CopyCount is null or <= 0)
                {
                    errors.Add($"• [{item.ItemName}] 请填写份数。");
                }
            }

            if (requireSubmittedFields
                && item.UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate
                && string.Equals(item.ElectronicMediaSource, ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank, StringComparison.Ordinal)
                && item.RequisitionedMediumId is null or <= 0)
            {
                errors.Add($"• [{item.ItemName}] 拷贝使用库内空盘时，请选择硬盘编号。");
            }
        }

        private static List<string> ValidateContainerUnitConsistency(IReadOnlyList<YearlyArchiveOutboundItem> items)
        {
            var errors = new List<string>();

            foreach (var group in ArchiveOutboundContainerUnitSupport.GroupItems(items))
            {
                var unitItems = group.ToList();
                if (unitItems.Count <= 1)
                {
                    continue;
                }

                var sample = unitItems[0];
                string unitTitle = ArchiveOutboundContainerUnitSupport.FormatUnitTitle(sample.MediaKind, sample.ContainerCode);

                if (unitItems.Any(item => item.UsageMode != sample.UsageMode))
                {
                    errors.Add($"• {unitTitle}：同一盒/袋内资料的领用方式须一致。");
                }

                if (sample.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                    && unitItems.Any(item => item.NeedReturn != sample.NeedReturn))
                {
                    errors.Add($"• {unitTitle}：同一盒/袋内提档资料的归还选项须一致。");
                }

                if (sample.UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate
                    && string.Equals(sample.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
                {
                    if (unitItems.Any(item =>
                            !string.Equals(item.ElectronicMediaSource, sample.ElectronicMediaSource, StringComparison.Ordinal)
                            || !string.Equals(item.ElectronicMediumType, sample.ElectronicMediumType, StringComparison.Ordinal)))
                    {
                        errors.Add($"• {unitTitle}：同一介质袋内拷贝设置须一致。");
                    }

                    if (string.Equals(sample.ElectronicMediaSource, ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank, StringComparison.Ordinal)
                        && unitItems.Any(item =>
                            item.RequisitionedMediumId != sample.RequisitionedMediumId
                            || !string.Equals(item.RequisitionedDiskCode, sample.RequisitionedDiskCode, StringComparison.OrdinalIgnoreCase)
                            || item.RequisitionedDiskNeedReturn != sample.RequisitionedDiskNeedReturn))
                    {
                        errors.Add($"• {unitTitle}：同一介质袋内库内空盘选择及归还选项须一致。");
                    }
                }

                if (unitItems.Any(item => ArchiveOutboundReturnSupport.ItemRequiresExpectedReturnDate(item))
                    && unitItems.Any(item =>
                        item.ExpectedReturnDate != sample.ExpectedReturnDate))
                {
                    errors.Add($"• {unitTitle}：同一盒/袋内预计归还日期须一致。");
                }
            }

            return errors;
        }

        private static List<string> CollectSubmitValidationErrors(
            YearlyArchiveOutboundRecord record,
            IReadOnlyList<YearlyArchiveOutboundItem> items,
            bool requireSubmittedFields)
        {
            var errors = new List<string>();

            if (requireSubmittedFields && string.IsNullOrWhiteSpace(record.Reason))
            {
                errors.Add("• 请填写原由。");
            }

            if (requireSubmittedFields && items.Count == 0)
            {
                errors.Add("• 请至少添加一条资料明细。");
            }

            if (requireSubmittedFields
                && string.Equals(record.DestinationKind, ArchiveOutboundDomainValues.DestinationExternal, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(record.ExternalUnit))
            {
                errors.Add("• 外部去向请填写单位名称。");
            }

            foreach (var group in ArchiveOutboundContainerUnitSupport.GroupItems(items))
            {
                var unitItems = group.ToList();
                if (unitItems.Count == 0)
                {
                    continue;
                }

                var sample = unitItems[0];
                if (!ArchiveOutboundReturnSupport.ItemRequiresExpectedReturnDate(sample))
                {
                    continue;
                }

                string unitTitle = ArchiveOutboundContainerUnitSupport.FormatUnitTitle(sample.MediaKind, sample.ContainerCode);
                if (!sample.ExpectedReturnDate.HasValue && requireSubmittedFields)
                {
                    errors.Add($"• {unitTitle}：请填写预计归还日期。");
                }
            }

            foreach (var item in items)
            {
                ValidateItem(item, errors, requireSubmittedFields);
            }

            errors.AddRange(ValidateContainerUnitConsistency(items));
            errors.AddRange(ArchiveOutboundSharedDiskSettingsSupport.ValidateCrossUnitConsistency(items));
            return errors;
        }

        private static bool IsApprovalComplete(YearlyArchiveOutboundRecord record) =>
            !string.IsNullOrWhiteSpace(record.DeptAuditor)
            && record.DeptAuditDate.HasValue
            && !string.IsNullOrWhiteSpace(record.ArchiveRoomHead)
            && record.ArchiveRoomHeadDate.HasValue
            && !string.IsNullOrWhiteSpace(record.ProductionHead)
            && record.ProductionHeadDate.HasValue
            && !string.IsNullOrWhiteSpace(record.VicePresident)
            && VicePresidentDatePresent(record);

        private static bool VicePresidentDatePresent(YearlyArchiveOutboundRecord record) => record.VicePresidentDate.HasValue;

        private static List<string> CollectApprovalPhaseErrors(
            YearlyArchiveOutboundRecord record,
            IReadOnlyList<SystemAttachment> attachments)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(record.DeptAuditor))
            {
                errors.Add("• 请填写申请部门审核人。");
            }

            if (!record.DeptAuditDate.HasValue)
            {
                errors.Add("• 请填写申请部门审核日期。");
            }

            if (string.IsNullOrWhiteSpace(record.ArchiveRoomHead))
            {
                errors.Add("• 请填写资料室负责人。");
            }

            if (!record.ArchiveRoomHeadDate.HasValue)
            {
                errors.Add("• 请填写资料室负责人审核日期。");
            }

            if (string.IsNullOrWhiteSpace(record.ProductionHead))
            {
                errors.Add("• 请填写生产科负责人。");
            }

            if (!record.ProductionHeadDate.HasValue)
            {
                errors.Add("• 请填写生产科负责人审核日期。");
            }

            if (string.IsNullOrWhiteSpace(record.VicePresident))
            {
                errors.Add("• 请填写生产副院长。");
            }

            if (!record.VicePresidentDate.HasValue)
            {
                errors.Add("• 请填写生产副院长审核日期。");
            }

            if (ArchiveOutboundDomainValues.RequiresProofMaterialScan(record.ProofMaterialNote))
            {
                bool hasProofScan = attachments.Any(a =>
                    string.Equals(a.FileCategory, ArchiveOutboundDomainValues.AttachmentKindProofMaterialScan, StringComparison.Ordinal));
                if (!hasProofScan)
                {
                    errors.Add("• 申请时已声明有证明材料，请上传证明材料扫描件。");
                }
            }

            return errors;
        }

        private static void CopyApprovalFields(YearlyArchiveOutboundRecord target, YearlyArchiveOutboundRecord source)
        {
            target.DeptAuditOpinion = source.DeptAuditOpinion?.Trim() ?? string.Empty;
            target.DeptAuditor = source.DeptAuditor?.Trim() ?? string.Empty;
            target.DeptAuditDate = source.DeptAuditDate;
            target.ArchiveRoomHeadOpinion = source.ArchiveRoomHeadOpinion?.Trim() ?? string.Empty;
            target.ArchiveRoomHead = source.ArchiveRoomHead?.Trim() ?? string.Empty;
            target.ArchiveRoomHeadDate = source.ArchiveRoomHeadDate;
            target.ProductionHeadOpinion = source.ProductionHeadOpinion?.Trim() ?? string.Empty;
            target.ProductionHead = source.ProductionHead?.Trim() ?? string.Empty;
            target.ProductionHeadDate = source.ProductionHeadDate;
            target.VicePresidentOpinion = source.VicePresidentOpinion?.Trim() ?? string.Empty;
            target.VicePresident = source.VicePresident?.Trim() ?? string.Empty;
            target.VicePresidentDate = source.VicePresidentDate;
        }

        private static string NormalizeProofMaterialNote(string? proofMaterialNote)
        {
            string note = proofMaterialNote?.Trim() ?? string.Empty;
            return ArchiveOutboundDomainValues.HasProofMaterial(note)
                ? note
                : ArchiveOutboundDomainValues.ProofMaterialNoneText;
        }
    }
}
