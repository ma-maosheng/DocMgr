using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Repositories.Interfaces;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public partial class ArchiveRegisterService : IArchiveRegisterService
    {
        private const string RegisterRecordEntityName = nameof(YearlyArchiveRegisterRecord);
        private const string RegisterMediaEntityName = nameof(YearlyArchiveRegisterMedia);
        private const string RegisterMediaItemEntityName = nameof(YearlyArchiveRegisterMediaItem);
        private const string RegisterElectronicDetailEntityName = nameof(YearlyArchiveRegisterElectronicMediaItemDetail);
        private const string EmptyScope = "";
        private const string SimulatedMediaKindScope = "Template=Simulated";
        private const string DataItemTypeScope = "Template=Data";
        private const string ProofItemTypeScope = "Template=Proof";
        private const string MediaKindElectronicScope = "MediaKind=" + ArchiveRegisterDomainValues.MediaKindElectronic;
        private const string MediaKindSimulatedDataScope = "MediaKind=" + ArchiveRegisterDomainValues.MediaKindSimulated + ";ItemType=" + ArchiveRegisterDomainValues.ItemTypeData;
        private const string MediaKindSimulatedProofScope = "MediaKind=" + ArchiveRegisterDomainValues.MediaKindSimulated + ";ItemType=" + ArchiveRegisterDomainValues.ItemTypeProof;
        private const string MediaKindSimulatedScope = "MediaKind=" + ArchiveRegisterDomainValues.MediaKindSimulated;

        private static readonly string[] RegisterRecordDomainFields =
        [
            nameof(YearlyArchiveRegisterRecord.SourceType),
            nameof(YearlyArchiveRegisterRecord.ArchivePurpose),
            nameof(YearlyArchiveRegisterRecord.ProdDeptOpinion),
            nameof(YearlyArchiveRegisterRecord.RndDeptOpinion),
            nameof(YearlyArchiveRegisterRecord.DeputyOpinion)
        ];

        private static readonly string[] RegisterMediaDomainFields =
        [
            nameof(YearlyArchiveRegisterMedia.MediaKind),
            nameof(YearlyArchiveRegisterMedia.MediaType),
            nameof(YearlyArchiveRegisterMedia.Disposition)
        ];

        private static readonly string[] RegisterMediaItemDomainFields =
        [
            nameof(YearlyArchiveRegisterMediaItem.ItemType),
            nameof(YearlyArchiveRegisterMediaItem.ConfidentialLevel)
        ];

        private readonly IArchiveRegisterRepository _archiveRegisterRepository;
        private readonly IBusinessRuleService _businessRuleService;
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;

        public ArchiveRegisterService(
            IArchiveRegisterRepository archiveRegisterRepository,
            IBusinessRuleService businessRuleService,
            IBusinessLogicSettingsService businessLogicSettingsService)
        {
            _archiveRegisterRepository = archiveRegisterRepository;
            _businessRuleService = businessRuleService;
            _businessLogicSettingsService = businessLogicSettingsService;
        }

        public Task<ArchiveRegisterPageDomainOptions> GetPageDomainOptionsAsync()
        {
            var definitions = GetPageDomainDefinitions();
            var pageDomainOptions = CreatePageDomainOptions(definitions);

            if (!HasRequiredPageDomainOptions(pageDomainOptions))
            {
                _archiveRegisterRepository.SeedFieldDomainDefaults();
                definitions = GetPageDomainDefinitions();
                pageDomainOptions = CreatePageDomainOptions(definitions);
            }

            return Task.FromResult(pageDomainOptions);
        }

        /// <summary>
        /// 获取指定电子介质类型允许的处置方式列表。
        /// </summary>
        public IReadOnlyList<string> GetAllowedElectronicDispositions(string? mediaType, IReadOnlyCollection<string> allDispositionOptions)
        {
            return ArchiveRegisterBusinessRules.GetAllowedElectronicDispositions(mediaType, allDispositionOptions);
        }

        public bool IsExternalSourceType(string? sourceType)
        {
            return ArchiveRegisterBusinessRules.IsExternalSourceType(sourceType);
        }

        public bool IsAllowedDomainValue(string? value, IReadOnlyCollection<string> options)
        {
            return ArchiveRegisterBusinessRules.IsAllowedDomainValue(value, options);
        }

        public string NormalizeConfidentialLevel(string? value)
        {
            return ArchiveRegisterBusinessRules.NormalizeConfidentialLevel(value);
        }

        public async Task SaveOrUpdateAsync(YearlyArchiveRegisterRecord record)
        {
            int recordId = await _archiveRegisterRepository.SaveOrUpdateRecordGraphAsync(record);
            if (recordId > 0)
            {
                await _archiveRegisterRepository.LinkOrphanAttachmentsToRecordAsync(record.FormNo, recordId);
            }
        }

        // [新增] 提交申请专用保存方法
        public async Task SubmitApplicationAsync(YearlyArchiveRegisterRecord record)
        {
            // 1. 强制清空审批流程字段
            record.ProdDeptOpinion = string.Empty;
            record.ProdLeader = string.Empty;
            record.ProdDate = null;
            record.RndDeptOpinion = string.Empty;
            record.RndLeader = string.Empty;
            record.RndDate = null;
            record.DeputyOpinion = string.Empty;
            record.DeputyLeader = string.Empty;
            record.DeputyDate = null;
            record.Deliverer = string.Empty;
            record.DeliverDate = null;
            record.Administrator = string.Empty;
            record.AdminDate = null;

            // 复用 SaveOrUpdateAsync 的逻辑来处理从头保存，
            // 或者使用之前的逻辑，但要应用同样的 "Id=0" 修复
            // 为了代码复用和修复 Bug，这里建议直接调用修复后的 SaveOrUpdateAsync
            // 只要确保 record 的状态是正确的即可。

            await SaveOrUpdateAsync(record);
        }



        public async Task<YearlyArchiveRegisterRecord?> GetByFormNoAsync(string formNo)
        {
            if (string.IsNullOrWhiteSpace(formNo)) return null;
            return await _archiveRegisterRepository.GetByFormNoWithDetailsAsync(formNo);
        }

        public async Task<YearlyArchiveRegisterRecord?> GetByIdAsync(int id)
        {
            return await _archiveRegisterRepository.GetByIdWithDetailsAsync(id);
        }

        // [修复] 此方法替换掉原有引发错误的 SearchRecordsAsync
        public async Task<List<YearlyArchiveRegisterRecord>> SearchRecordsAsync(string keyword, int? year = null, int? status = null, int? projectId = null)
        {
            return await _archiveRegisterRepository.SearchRecordsAsync(keyword, year, status, projectId);
        }

        public YearlyArchiveRegisterRecord CreateDraftRecord(User? currentUser)
        {
            if (!ArchiveRegisterBusinessRules.CanSubmitApplication(currentUser))
            {
                throw new InvalidOperationException("仅部门资料管理员可发起资料登记申请。");
            }

            var record = new YearlyArchiveRegisterRecord
            {
                CreatedDate = DateTime.Now,
                ApplicantDate = DateTime.Now,
                Status = YearlyArchiveRegisterRecord.Draft
            };

            if (currentUser != null)
            {
                record.ApplicantName = currentUser.RealName;
                record.ApplicantDept = currentUser.Department;
            }

            return record;
        }

        public async Task<YearlyArchiveRegisterRecord> CreateDraftRecordWithNextFormNoAsync(User? currentUser)
        {
            var record = CreateDraftRecord(currentUser);
            record.FormNo = await GenerateFormNoByPurposeAsync(record.ArchivePurpose);
            return record;
        }

        private async Task EnsureFormNoAsync(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (!string.IsNullOrWhiteSpace(record.FormNo))
            {
                return;
            }

            record.FormNo = await GenerateFormNoByPurposeAsync(record.ArchivePurpose);
        }

        private Task<string> GenerateFormNoByPurposeAsync(string? archivePurpose)
        {
            BusinessNoCategory category = ResolveBusinessNoCategory(archivePurpose);
            return _businessRuleService.GenerateBusinessNoAsync(category);
        }

        private static BusinessNoCategory ResolveBusinessNoCategory(string? archivePurpose)
        {
            if (string.IsNullOrWhiteSpace(archivePurpose))
            {
                return BusinessNoCategory.AssetInboundApply;
            }

            if (archivePurpose.Contains("销", StringComparison.Ordinal))
            {
                return BusinessNoCategory.AssetDestroyApply;
            }

            if (archivePurpose.Contains("还", StringComparison.Ordinal) || archivePurpose.Contains("登", StringComparison.Ordinal))
            {
                return BusinessNoCategory.AssetReturnRegister;
            }

            if (archivePurpose.Contains("出", StringComparison.Ordinal)
                || archivePurpose.Contains("借", StringComparison.Ordinal)
                || archivePurpose.Contains("移交", StringComparison.Ordinal))
            {
                return BusinessNoCategory.AssetOutboundApply;
            }

            return BusinessNoCategory.AssetInboundApply;
        }

        private static bool IsBorrowedRetainedHardDisk(YearlyArchiveRegisterMedia media)
        {
            ArgumentNullException.ThrowIfNull(media);

            return string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)
                && string.Equals(media.MediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
                && string.Equals(media.Disposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase)
                && media.IsBorrowedHardDisk
                && !string.IsNullOrWhiteSpace(media.BorrowedHardDiskCode);
        }

        private static bool CanRestoreRegisterLockedStatus(string? status)
        {
            return string.Equals(status, HardDiskMedium.StatusOutTemporary, StringComparison.Ordinal)
                || string.Equals(status, HardDiskMedium.StatusOutLongTerm, StringComparison.Ordinal);
        }

        private async Task ApplyBorrowedHardDiskRegisterLockAsync(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var retainedCodes = record.MediaEntries
                .Where(IsBorrowedRetainedHardDisk)
                .Select(media => media.BorrowedHardDiskCode.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingLockedMedia = await _archiveRegisterRepository.GetHardDiskMediaByRegisterLockAsync(record.Id, record.FormNo, onlyNotDeleted: false);

            foreach (var medium in existingLockedMedia.Where(item => retainedCodes.All(code => !string.Equals(code, item.DiskCode, StringComparison.OrdinalIgnoreCase))))
            {
                ReleaseRegisterLock(medium);
            }

            if (retainedCodes.Count == 0)
            {
                return;
            }

            var targetMedia = await _archiveRegisterRepository.GetHardDiskMediaByDiskCodesAsync(retainedCodes);

            foreach (string code in retainedCodes)
            {
                var medium = targetMedia.FirstOrDefault(item => string.Equals(item.DiskCode, code, StringComparison.OrdinalIgnoreCase) && !item.IsDeleted);
                if (medium == null)
                {
                    throw new InvalidOperationException($"未找到借出硬盘 [{code}]。请刷新后重试。");
                }

                medium.Ledger ??= new HardDiskLedger
                {
                    MediumId = medium.Id,
                    DiskCode = medium.DiskCode,
                    MediaStatus = HardDiskMedium.StatusInStockBlank,
                    MediaNature = HardDiskMedium.NatureBlank,
                    StorageLocation = string.Empty,
                    HolderOrOrganization = "资料室",
                    NeedReturn = false,
                    RegisterPerson = medium.RegisterPerson,
                    RegisterDate = medium.RegisterDate,
                    Remark = medium.Remark,
                    CreatedTime = medium.CreatedTime,
                    UpdatedTime = medium.UpdatedTime
                };
                var ledger = medium.Ledger;

                var registerLock = medium.RegisterLock;

                if (registerLock != null)
                {
                    if (HardDiskRegisterLock.IsOwnedByArchiveRegisterRecord(registerLock, record.Id, record.FormNo))
                    {
                        continue;
                    }

                    string owner = string.IsNullOrWhiteSpace(registerLock.BusinessNo)
                        ? registerLock.BusinessType
                        : registerLock.BusinessNo.Trim();
                    throw new InvalidOperationException($"硬盘 [{code}] 已被【{owner}】占用，不能再被新的资料登记申请单使用。");
                }

                if (!CanRestoreRegisterLockedStatus(ledger.MediaStatus))
                {
                    throw new InvalidOperationException($"硬盘 [{code}] 当前状态为 [{ledger.MediaStatus}]，不能用于借出硬盘资料登记。");
                }

                medium.RegisterLock = new HardDiskRegisterLock
                {
                    MediumId = medium.Id,
                    BusinessType = HardDiskRegisterLock.BusinessTypeArchiveRegister,
                    BusinessRecordId = record.Id > 0 ? record.Id : null,
                    BusinessNo = record.FormNo?.Trim() ?? string.Empty,
                    PreviousStatus = ledger.MediaStatus,
                    LockedTime = DateTime.Now
                };

                medium.UpdatedTime = DateTime.Now;
            }
        }

        private void ReleaseRegisterLock(HardDiskMedium medium)
        {
            ArgumentNullException.ThrowIfNull(medium);
            medium.Ledger ??= new HardDiskLedger
            {
                MediumId = medium.Id,
                DiskCode = medium.DiskCode,
                MediaStatus = HardDiskMedium.StatusInStockBlank,
                MediaNature = HardDiskMedium.NatureBlank,
                StorageLocation = string.Empty,
                HolderOrOrganization = "资料室",
                NeedReturn = false,
                RegisterPerson = medium.RegisterPerson,
                RegisterDate = medium.RegisterDate,
                Remark = medium.Remark,
                CreatedTime = medium.CreatedTime,
                UpdatedTime = medium.UpdatedTime
            };
            var ledger = medium.Ledger;

            var registerLock = medium.RegisterLock;
            if (registerLock != null)
            {
                _archiveRegisterRepository.RemoveHardDiskRegisterLock(registerLock);
            }

            medium.RegisterLock = null;
            medium.UpdatedTime = DateTime.Now;
        }

        private async Task RestoreBorrowedHardDiskRegisterLocksAsync(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (record.Id <= 0 && string.IsNullOrWhiteSpace(record.FormNo))
            {
                return;
            }

            var lockedMedia = await _archiveRegisterRepository.GetHardDiskMediaByRegisterLockAsync(record.Id, record.FormNo, onlyNotDeleted: true);

            foreach (var medium in lockedMedia)
            {
                ReleaseRegisterLock(medium);
            }
        }

        private async Task<bool> ExceedsForceCleanupAgeAsync(YearlyArchiveRegisterRecord record)
        {
            string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
            DateTime applyDate = ApplicationOverdueSettingSupport.ResolveRegisterApplyDate(record);
            return _businessLogicSettingsService.IsEligibleForAdminForceVoid(applyDate, settingCode);
        }

        public async Task<ArchiveRegisterFlowResult> SaveDraftFlowAsync(
            YearlyArchiveRegisterRecord? record,
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries,
            User? operatorUser)
        {
            if (record == null)
            {
                return ArchiveRegisterFlowResult.Fail("当前记录为空，无法保存。");
            }

            if (!ArchiveRegisterBusinessRules.CanSubmitApplication(operatorUser))
            {
                return ArchiveRegisterFlowResult.Fail("仅部门资料管理员可保存资料登记申请草稿。");
            }

            await EnsureFormNoAsync(record);

            record.MarkAsDraft();
            record.MediaEntries = mediaEntries?.ToList() ?? new List<YearlyArchiveRegisterMedia>();
            await SaveOrUpdateAsync(record);
            await ApplyBorrowedHardDiskRegisterLockAsync(record);
            await _archiveRegisterRepository.SaveChangesAsync();

            return ArchiveRegisterFlowResult.Ok("保存成功，当前状态：未提交。");
        }

        public async Task<ArchiveRegisterFlowResult> SaveApprovalFlowAsync(YearlyArchiveRegisterRecord? record, IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries, IReadOnlyCollection<SystemAttachment> attachments, User? currentUser)
        {
            if (record == null)
            {
                return ArchiveRegisterFlowResult.Fail("当前记录为空，无法保存。");
            }

            if (record.Id <= 0)
            {
                return ArchiveRegisterFlowResult.Fail("未找到指定的登记申请单。");
            }

            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                return ArchiveRegisterFlowResult.Fail("仅资料室（资料管理员）可执行审批保存。");
            }

            var existing = await GetByIdAsync(record.Id);
            if (existing == null)
            {
                return ArchiveRegisterFlowResult.Fail("未找到指定的登记申请单。");
            }

            if (!existing.IsSubmitted)
            {
                return ArchiveRegisterFlowResult.Fail("只有“已提交”状态的记录可执行审批通过。");
            }

            if (existing.IsApprovedReceived || existing.IsSignedUploaded || existing.IsArchived)
            {
                return ArchiveRegisterFlowResult.Fail("当前状态不允许再次执行审批通过。");
            }

            // 审批流程仅要求签字人，不要求签署具体意见。
            var approvalErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(record.ProdLeader)) approvalErrors.Add("• 生产管理科负责人签字缺失");
            if (string.IsNullOrWhiteSpace(record.RndLeader)) approvalErrors.Add("• 科研开发室负责人签字缺失");
            if (string.IsNullOrWhiteSpace(record.DeputyLeader)) approvalErrors.Add("• 分管领导签字缺失");
            if (approvalErrors.Count > 0)
            {
                return ArchiveRegisterFlowResult.Fail("审批签字信息不完整，无法审批通过：\n\n" + string.Join(Environment.NewLine, approvalErrors));
            }

            ArchiveRegisterBusinessRules.MergeMediaItemConfidentialLevels(existing, mediaEntries);

            var pageDomainOptions = await GetPageDomainOptionsAsync();
            var confidentialValidation = ValidateMediaItemConfidentialLevels(existing.MediaEntries, pageDomainOptions);
            if (!confidentialValidation.IsValid)
            {
                return ArchiveRegisterFlowResult.Fail("资料子项密级不完整：\n\n" + confidentialValidation.ErrorMessage);
            }

            ArchiveRegisterBusinessRules.CopyRegisterApprovalFields(existing, record);
            existing.MarkAsApprovedReceived();
            await SaveOrUpdateAsync(existing);

            return ArchiveRegisterFlowResult.Ok("审批通过成功。下一步：确认实物交接。");
        }

        public async Task<ArchiveRegisterFlowResult> ConfirmPhysicalHandoverFlowAsync(YearlyArchiveRegisterRecord? record, User? currentUser)
        {
            if (record == null || record.Id <= 0)
            {
                return ArchiveRegisterFlowResult.Fail("当前记录为空，无法确认实物交接。");
            }

            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                return ArchiveRegisterFlowResult.Fail("仅资料室（资料管理员）可执行确认实物交接。");
            }

            var existing = await GetByIdAsync(record.Id);
            if (existing == null)
            {
                return ArchiveRegisterFlowResult.Fail("未找到指定的登记申请单。");
            }

            if (!existing.IsApprovedReceived)
            {
                return ArchiveRegisterFlowResult.Fail("只有“已审批”状态的记录可确认实物交接。");
            }

            var handoverErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(record.Deliverer)) handoverErrors.Add("• 移交人缺失");
            if (string.IsNullOrWhiteSpace(record.Administrator)) handoverErrors.Add("• 资料员缺失");
            if (string.IsNullOrWhiteSpace(record.DeptLeader)) handoverErrors.Add("• 部门负责人缺失");
            if (handoverErrors.Count > 0)
            {
                return ArchiveRegisterFlowResult.Fail("实物交接信息不完整，无法确认：\n\n" + string.Join(Environment.NewLine, handoverErrors));
            }

            ArchiveRegisterBusinessRules.CopyRegisterApprovalFields(existing, record);
            existing.MarkAsSignedUploaded();
            await SaveOrUpdateAsync(existing);

            return ArchiveRegisterFlowResult.Ok("实物交接确认成功，请上传签批交接单和资料照片。");
        }

        public async Task<ArchiveRegisterFlowResult> CompleteRegisterFlowAsync(YearlyArchiveRegisterRecord? record, IReadOnlyCollection<SystemAttachment> attachments, User? currentUser)
        {
            if (record == null)
            {
                return ArchiveRegisterFlowResult.Fail("当前记录为空，无法确认办结。");
            }

            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                return ArchiveRegisterFlowResult.Fail("仅资料室（资料管理员）可执行确认办结。");
            }

            if (!record.IsSignedUploaded)
            {
                return ArchiveRegisterFlowResult.Fail("请先确认实物交接并上传签批交接单后再确认办结。");
            }

            var approvalValidation = await ValidateApprovalAsync(record, attachments);
            if (!approvalValidation.IsValid)
            {
                return ArchiveRegisterFlowResult.Fail("附件或审批信息尚未满足办结要求：\n\n" + approvalValidation.ErrorMessage);
            }

            record.MarkAsCompleted();
            await SaveOrUpdateAsync(record);
            return ArchiveRegisterFlowResult.Ok("确认办结成功。下一步：打印交接单。");
        }

        public async Task<ArchiveRegisterFlowResult> SubmitApplicationFlowAsync(
            YearlyArchiveRegisterRecord? record,
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries,
            bool isExternalSource,
            User? operatorUser)
        {
            if (record == null)
            {
                return ArchiveRegisterFlowResult.Fail("当前记录为空，无法提交。");
            }

            if (!ArchiveRegisterBusinessRules.CanSubmitApplication(operatorUser))
            {
                return ArchiveRegisterFlowResult.Fail("仅部门资料管理员可提交资料登记申请。");
            }

            await EnsureFormNoAsync(record);

            var applicationValidation = await ValidateApplicationAsync(record, mediaEntries, isExternalSource);
            if (!applicationValidation.IsValid)
            {
                return ArchiveRegisterFlowResult.Fail("申请信息不完整，无法提交：\n\n" + applicationValidation.ErrorMessage);
            }

            if (!record.IsDraft)
            {
                return ArchiveRegisterFlowResult.Fail("只有“未提交”状态的记录才能提交申请。");
            }

            record.MarkAsSubmitted();
            record.MediaEntries = mediaEntries?.ToList() ?? new List<YearlyArchiveRegisterMedia>();
            await SubmitApplicationAsync(record);
            await ApplyBorrowedHardDiskRegisterLockAsync(record);
            await _archiveRegisterRepository.SaveChangesAsync();

            return ArchiveRegisterFlowResult.Ok("申请提交成功！");
        }

        /// <inheritdoc/>
        public async Task SyncBorrowedHardDiskRegisterLocksAsync(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (record.Id <= 0)
            {
                throw new InvalidOperationException("登记单尚未保存，无法同步借出硬盘登记锁。");
            }

            if (record.MediaEntries == null || record.MediaEntries.Count == 0)
            {
                var loaded = await GetByIdAsync(record.Id);
                if (loaded == null)
                {
                    throw new InvalidOperationException($"未找到登记单（Id={record.Id}），无法同步借出硬盘登记锁。");
                }

                record = loaded;
            }

            await ApplyBorrowedHardDiskRegisterLockAsync(record);
            await _archiveRegisterRepository.SaveChangesAsync();
        }

        public async Task<ArchiveRegisterFlowResult> CancelRegisterFlowAsync(YearlyArchiveRegisterRecord? record, User? currentUser)
        {
            if (record == null || record.Id == 0)
            {
                return ArchiveRegisterFlowResult.Fail("当前记录为空，无法撤回作废。");
            }

            if (!IsApplicantUser(currentUser))
            {
                return ArchiveRegisterFlowResult.Fail("仅申请人可执行撤回作废。");
            }

            string currentApplicantName = currentUser?.RealName?.Trim() ?? string.Empty;
            if (!string.Equals(record.ApplicantName?.Trim(), currentApplicantName, StringComparison.Ordinal))
            {
                return ArchiveRegisterFlowResult.Fail("仅申请人本人可撤销当前登记单。");
            }

            if (!record.CanCancelRegister)
            {
                return ArchiveRegisterFlowResult.Fail("当前登记单已录入审批信息或状态不允许撤回作废。");
            }

            await RestoreBorrowedHardDiskRegisterLocksAsync(record);
            record.MarkAsWithdrawnVoid();
            await SaveOrUpdateAsync(record);
            return ArchiveRegisterFlowResult.Ok("撤回作废成功。");
        }

        public async Task<ArchiveRegisterFlowResult> ForceCleanupRegisterFlowAsync(YearlyArchiveRegisterRecord? record, User? currentUser)
        {
            if (record == null || record.Id == 0)
            {
                return ArchiveRegisterFlowResult.Fail("当前记录为空，无法执行申请单强制作废。");
            }

            if (!IsArchiveAdminUser(currentUser))
            {
                return ArchiveRegisterFlowResult.Fail("仅资料室管理员可执行申请单强制作废。");
            }

            if (!record.CanForceCleanupRegister)
            {
                return ArchiveRegisterFlowResult.Fail("当前登记单已录入审批信息或状态不允许强制作废。");
            }

            if (!await ExceedsForceCleanupAgeAsync(record))
            {
                string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
                return ArchiveRegisterFlowResult.Fail(_businessLogicSettingsService.BuildNotEligibleMessage(settingCode));
            }

            await RestoreBorrowedHardDiskRegisterLocksAsync(record);
            record.MarkAsForceVoided();
            await SaveOrUpdateAsync(record);
            return ArchiveRegisterFlowResult.Ok("申请单强制作废成功。");
        }

        public async Task<ArchiveRegisterAttachmentFlowResult> UploadAttachmentFlowAsync(YearlyArchiveRegisterRecord? record, User? currentUser, string fileName, string extension, long fileSize, byte[] fileContent)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.FormNo))
            {
                return ArchiveRegisterAttachmentFlowResult.Fail("请先生成或输入表单编号。");
            }

            if (string.IsNullOrWhiteSpace(fileName) || fileContent == null || fileContent.Length == 0)
            {
                return ArchiveRegisterAttachmentFlowResult.Fail("附件内容为空，无法上传。");
            }

            string? formatError = SystemAttachmentUploadSupport.ValidateUploadFormat(fileName, extension, fileContent);
            if (!string.IsNullOrWhiteSpace(formatError))
            {
                return ArchiveRegisterAttachmentFlowResult.Fail(formatError);
            }

            var attachment = new SystemAttachment
            {
                BusinessType = "YearlyArchiveRegister",
                BusinessNo = record.FormNo,
                FileName = fileName,
                Extension = extension ?? string.Empty,
                FileSize = fileSize,
                UploadTime = DateTime.Now,
                UploaderName = currentUser?.RealName ?? string.Empty,
                FileContent = fileContent
            };

            if (record.Id > 0)
            {
                var persisted = await GetByIdAsync(record.Id);
                if (persisted != null)
                {
                    if (!persisted.IsApprovedReceived && !persisted.IsSignedUploaded)
                    {
                        return ArchiveRegisterAttachmentFlowResult.Fail("当前状态不允许上传签批交接单，请先执行“审批通过”并确认实物交接。");
                    }

                    await UploadAttachmentAsync(attachment);

                    if (persisted.IsApprovedReceived)
                    {
                        var currentAttachments = await GetAttachmentsByFormNoAsync(persisted.FormNo);
                        var validation = await ValidateMandatoryAttachmentsAsync(currentAttachments);
                        if (validation.IsValid)
                        {
                            persisted.MarkAsSignedUploaded();
                            await SaveOrUpdateAsync(persisted);
                        }
                    }

                    return ArchiveRegisterAttachmentFlowResult.Ok("上传成功", attachment);
                }
            }

            await UploadAttachmentAsync(attachment);
            return ArchiveRegisterAttachmentFlowResult.Ok("上传成功", attachment);
        }

        public async Task<ArchiveRegisterAttachmentFlowResult> DeleteAttachmentFlowAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return ArchiveRegisterAttachmentFlowResult.Fail("附件不存在，无法删除。");
            }

            await DeleteAttachmentAsync(attachment.Id);
            return ArchiveRegisterAttachmentFlowResult.Ok("删除成功");
        }

        public async Task<ArchiveRegisterAttachmentFlowResult> PrepareAttachmentViewFlowAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return ArchiveRegisterAttachmentFlowResult.Fail("附件不存在，无法查看。");
            }

            var full = await GetAttachmentByIdAsync(attachment.Id);
            if (full?.FileContent == null || full.FileContent.Length == 0)
            {
                return ArchiveRegisterAttachmentFlowResult.Fail("附件内容为空，无法查看。");
            }

            return ArchiveRegisterAttachmentFlowResult.Ok("附件已就绪", full);
        }

        public Task<ArchiveRegisterApprovalValidationResult> ValidateApprovalAsync(YearlyArchiveRegisterRecord record, IReadOnlyCollection<SystemAttachment> attachments)
        {
            ArgumentNullException.ThrowIfNull(record);

            var errors = new List<string>();
            var attachmentList = attachments ?? Array.Empty<SystemAttachment>();

            // 办结校验仅要求签字人；历史记录若仍有意见值则校验域值合法性。
            var pageDomainOptions = CreatePageDomainOptions(GetPageDomainDefinitions());
            var prodOpinionOptions = pageDomainOptions.ProdOpinionOptions;
            var rndOpinionOptions = pageDomainOptions.RndOpinionOptions;
            var deputyOpinionOptions = pageDomainOptions.DeputyOpinionOptions;

            if (!string.IsNullOrWhiteSpace(record.ProdDeptOpinion) && !IsAllowedDomainValue(record.ProdDeptOpinion, prodOpinionOptions))
                errors.Add($"• 生产管理科意见不在域值定义中（允许值：{string.Join("、", prodOpinionOptions)}）");
            if (!string.IsNullOrWhiteSpace(record.RndDeptOpinion) && !IsAllowedDomainValue(record.RndDeptOpinion, rndOpinionOptions))
                errors.Add($"• 科研开发室意见不在域值定义中（允许值：{string.Join("、", rndOpinionOptions)}）");
            if (!string.IsNullOrWhiteSpace(record.DeputyOpinion) && !IsAllowedDomainValue(record.DeputyOpinion, deputyOpinionOptions))
                errors.Add($"• 分管领导意见不在域值定义中（允许值：{string.Join("、", deputyOpinionOptions)}）");

            if (string.IsNullOrWhiteSpace(record.ProdLeader)) errors.Add("• 生产管理科负责人签字缺失");
            if (string.IsNullOrWhiteSpace(record.RndLeader)) errors.Add("• 科研开发室负责人签字缺失");
            if (string.IsNullOrWhiteSpace(record.DeputyLeader)) errors.Add("• 分管领导签字缺失");
            if (string.IsNullOrWhiteSpace(record.Deliverer)) errors.Add("• 移交人签字缺失");
            if (string.IsNullOrWhiteSpace(record.Administrator)) errors.Add("• 资料员签字缺失");
            if (string.IsNullOrWhiteSpace(record.DeptLeader)) errors.Add("• 部门负责人签字缺失");

            errors.AddRange(CollectMandatoryAttachmentErrors(attachmentList));

            return Task.FromResult(new ArchiveRegisterApprovalValidationResult(errors));
        }

        public Task<ArchiveRegisterApprovalValidationResult> ValidateMandatoryAttachmentsAsync(IReadOnlyCollection<SystemAttachment> attachments)
        {
            var errors = CollectMandatoryAttachmentErrors(attachments ?? Array.Empty<SystemAttachment>());
            return Task.FromResult(new ArchiveRegisterApprovalValidationResult(errors));
        }

        private static List<string> CollectMandatoryAttachmentErrors(IReadOnlyCollection<SystemAttachment> attachmentList)
        {
            var errors = new List<string>();
            var fileNames = attachmentList
                .Where(a => !string.IsNullOrWhiteSpace(a.FileName))
                .Select(a => a.FileName.Trim())
                .ToList();

            if (!fileNames.Any(name => name.Contains("登记申请单", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("• 缺少“登记申请单”附件（文件名需包含“登记申请单”）");
            }

            if (!fileNames.Any(name => name.Contains("资料照片", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("• 缺少“资料照片”附件（文件名需包含“资料照片”）");
            }

            var allowedKinds = new[] { "登记申请单", "资料照片" };
            var unexpectedFiles = fileNames
                .Where(name => !allowedKinds.Any(kind => name.Contains(kind, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (unexpectedFiles.Count > 0)
            {
                errors.Add("• 附件仅允许“登记申请单”“资料照片”两类，存在不允许文件：" + string.Join("、", unexpectedFiles));
            }

            int requiredAttachmentCount = fileNames.Count(name =>
                allowedKinds.Any(kind => name.Contains(kind, StringComparison.OrdinalIgnoreCase)));
            if (requiredAttachmentCount != 2)
            {
                errors.Add($"• 附件数量必须且只能为2个（登记申请单、资料照片各1个），当前匹配到 {requiredAttachmentCount} 个。");
            }

            return errors;
        }

        public async Task ApplyDefaultApprovalInfoAsync(YearlyArchiveRegisterRecord record, User currentUser)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(currentUser);

            if (!IsArchiveAdminUser(currentUser))
            {
                throw new InvalidOperationException("仅资料室（资料管理员）可执行该操作。");
            }

            var users = await _archiveRegisterRepository.GetUsersAsync();
            var now = DateTime.Now;

            var deptLeader = FindUserByDeptAndRole(users, record.ApplicantDept, "部门负责人");
            if (!string.IsNullOrWhiteSpace(deptLeader))
                record.DeptLeader = deptLeader;
            if (!record.DeptDate.HasValue)
                record.DeptDate = now;

            // 审批流程仅签字，不再预填意见。
            if (string.IsNullOrWhiteSpace(record.ProdLeader))
                record.ProdLeader = FindUserByRoleOrDept(users, "生产管理科");
            if (!record.ProdDate.HasValue)
                record.ProdDate = now;

            if (string.IsNullOrWhiteSpace(record.RndLeader))
                record.RndLeader = FindUserByRoleOrDept(users, "资料室");
            if (!record.RndDate.HasValue)
                record.RndDate = now;

            if (string.IsNullOrWhiteSpace(record.DeputyLeader))
                record.DeputyLeader = FindUserByRoleOrDept(users, "分管资料副院长");
            if (!record.DeputyDate.HasValue)
                record.DeputyDate = now;

            if (string.IsNullOrWhiteSpace(record.Deliverer))
                record.Deliverer = record.ApplicantName;
            if (!record.DeliverDate.HasValue)
                record.DeliverDate = now;

            if (string.IsNullOrWhiteSpace(record.Administrator))
                record.Administrator = currentUser.RealName;
            if (!record.AdminDate.HasValue)
                record.AdminDate = now;
        }

        public async Task ApplyDefaultOutboundApprovalInfoAsync(YearlyArchiveOutboundRecord record, User currentUser)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(currentUser);

            if (!IsArchiveAdminUser(currentUser))
            {
                throw new InvalidOperationException("仅资料室（资料管理员）可执行该操作。");
            }

            var users = await _archiveRegisterRepository.GetUsersAsync();
            var now = DateTime.Now;

            var deptLeader = FindUserByDeptAndRole(users, record.ApplicantDept, "部门负责人");
            if (string.IsNullOrWhiteSpace(record.DeptAuditor) && !string.IsNullOrWhiteSpace(deptLeader))
            {
                record.DeptAuditor = deptLeader;
            }

            if (!record.DeptAuditDate.HasValue)
            {
                record.DeptAuditDate = now;
            }

            // 审批流程仅签字，不再预填意见；清空残留意见，避免部分节点「同意」、部分空白。
            record.DeptAuditOpinion = string.Empty;
            record.ArchiveRoomHeadOpinion = string.Empty;
            record.ProductionHeadOpinion = string.Empty;
            record.VicePresidentOpinion = string.Empty;

            if (string.IsNullOrWhiteSpace(record.ProductionHead))
            {
                record.ProductionHead = FindUserByRoleOrDept(users, "生产管理科");
            }

            if (!record.ProductionHeadDate.HasValue)
            {
                record.ProductionHeadDate = now;
            }

            if (string.IsNullOrWhiteSpace(record.ArchiveRoomHead))
            {
                record.ArchiveRoomHead = FindUserByRoleOrDept(users, "资料室");
            }

            if (!record.ArchiveRoomHeadDate.HasValue)
            {
                record.ArchiveRoomHeadDate = now;
            }

            if (string.IsNullOrWhiteSpace(record.VicePresident))
            {
                record.VicePresident = FindUserByRoleOrDept(users, "分管生产副院长");
            }

            if (!record.VicePresidentDate.HasValue)
            {
                record.VicePresidentDate = now;
            }
        }

        public async Task<ArchiveRegisterApplicationValidationResult> ValidateApplicationAsync(YearlyArchiveRegisterRecord record, IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries, bool isExternalSource)
        {
            ArgumentNullException.ThrowIfNull(record);

            var errors = new List<string>();
            var entries = mediaEntries?.ToList() ?? new List<YearlyArchiveRegisterMedia>();
            var sourceType = string.IsNullOrWhiteSpace(record.SourceType) ? string.Empty : record.SourceType.Trim();

            var pageDomainOptions = await GetPageDomainOptionsAsync();

            var sourceTypeOptions = pageDomainOptions.SourceTypes;
            var archivePurposeOptions = pageDomainOptions.ArchivePurposes;
            var dataElectronicMediaTypeOptions = pageDomainOptions.DataElectronicMediaTypes;
            var dataSimulatedMediaTypeOptions = pageDomainOptions.DataSimulatedMediaTypes;
            var proofSimulatedMediaTypeOptions = pageDomainOptions.ProofSimulatedMediaTypes;
            var dataElectronicDispositionOptions = pageDomainOptions.DataElectronicDispositions;
            var dataSimulatedDispositionOptions = pageDomainOptions.DataSimulatedDispositions;

            if (string.IsNullOrWhiteSpace(record.FormNo)) errors.Add("• 表单编号未生成");
            if (string.IsNullOrWhiteSpace(record.MaterialName)) errors.Add("• 资料名称未填写");
            if (!IsAllowedDomainValue(sourceType, sourceTypeOptions))
                errors.Add($"• 资料来源不在域值定义中（允许值：{string.Join("、", sourceTypeOptions)}）");

            if (isExternalSource)
            {
                if (string.IsNullOrWhiteSpace(record.ProvideUnit)) errors.Add("• 资料来源为“外来”时，【提供单位】不能为空");
            }
            else
            {
                if (!record.ProjectId.HasValue) errors.Add("• 内部资料必须选择【所属项目】");
                if (string.IsNullOrWhiteSpace(record.ProvideUnit)) errors.Add("• 内部资料必须选择【提供部门】");
            }

            if (entries.Count == 0) errors.Add("• 【介质与内容明细】为空");
            else
            {
                if (entries.Any(m => string.IsNullOrWhiteSpace(m.MediaKind) || string.IsNullOrWhiteSpace(m.MediaType))) errors.Add("• 存在不完整的介质信息(类别或类型未填)");

                for (int i = 0; i < entries.Count; i++)
                {
                    var media = entries[i];
                    var seq = i + 1;
                    if (!string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"• 第{seq}条介质类型非法（仅允许：{ArchiveRegisterDomainValues.MediaKindElectronic}/{ArchiveRegisterDomainValues.MediaKindSimulated}）");
                        continue;
                    }

                    if (media.Items == null || media.Items.Count == 0)
                    {
                        errors.Add($"• 第{seq}条介质至少需要填写一条内容明细");
                        continue;
                    }

                    if (string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IsAllowedDomainValue(media.MediaType, dataElectronicMediaTypeOptions))
                            errors.Add($"• 第{seq}条电子介质类型不在域值定义中（允许值：{string.Join("、", dataElectronicMediaTypeOptions)}）");
                        if (!string.IsNullOrWhiteSpace(media.Disposition) && !IsAllowedDomainValue(media.Disposition, dataElectronicDispositionOptions))
                            errors.Add($"• 第{seq}条电子处置方式不在域值定义中（允许值：{string.Join("、", dataElectronicDispositionOptions)}）");
                        if (media.MediaCount != 1)
                            errors.Add($"• 第{seq}条电子介质数量必须为1");

                        for (int itemIndex = 0; itemIndex < media.Items.Count; itemIndex++)
                        {
                            errors.AddRange(CollectElectronicMediaItemValidationErrors(
                                media.Items[itemIndex],
                                seq,
                                itemIndex + 1,
                                pageDomainOptions));
                        }

                        continue;
                    }

                    var hasProofItem = media.Items.Any(x => string.Equals(x.ItemType, ArchiveRegisterDomainValues.ItemTypeProof, StringComparison.OrdinalIgnoreCase));
                    var simulatedTypeOptions = hasProofItem ? proofSimulatedMediaTypeOptions : dataSimulatedMediaTypeOptions;
                    if (!IsAllowedDomainValue(media.MediaType, simulatedTypeOptions))
                        errors.Add($"• 第{seq}条模拟介质类型不在域值定义中（允许值：{string.Join("、", simulatedTypeOptions)}）");

                    if (!hasProofItem && !string.IsNullOrWhiteSpace(media.Disposition) && !IsAllowedDomainValue(media.Disposition, dataSimulatedDispositionOptions))
                        errors.Add($"• 第{seq}条模拟处置方式不在域值定义中（允许值：{string.Join("、", dataSimulatedDispositionOptions)}）");
                }

                var electronicEntries = entries
                    .Where(m => string.Equals(m.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var electronicMediaTypes = electronicEntries
                    .Select(m => m.MediaType?.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (electronicMediaTypes.Count > 1)
                    errors.Add("• 电子介质只能使用同一种介质类型");

                var electronicDispositions = electronicEntries
                    .Select(m => m.Disposition?.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (electronicDispositions.Count > 1)
                    errors.Add("• 电子介质只能使用同一种处置方式");

                var simulatedEntries = entries
                    .Where(m => string.Equals(m.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var simulatedEntry in simulatedEntries)
                {
                    if (!string.IsNullOrWhiteSpace(simulatedEntry.Disposition)
                        && !string.Equals(simulatedEntry.Disposition.Trim(), ArchiveRegisterDomainValues.SimulatedDispositionRetain, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"• 模拟介质处置方式固定为“{ArchiveRegisterDomainValues.SimulatedDispositionRetain}”，不允许其他值");
                        break;
                    }
                }

                // 现有：硬盘 + 留存 + 借出时，编号必填
                foreach (var electronicEntry in electronicEntries.Where(entry =>
                         string.Equals(entry.MediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(entry.Disposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase)))
                {
                    if (electronicEntry.IsBorrowedHardDisk && string.IsNullOrWhiteSpace(electronicEntry.BorrowedHardDiskCode))
                    {
                        errors.Add("• 硬盘介质留存场景中，若登记为资料室借出硬盘，则必须填写借出硬盘介质编号");
                    }
                }

                // 新增：同一申请单内，借出硬盘编号不得重复
                var duplicatedBorrowedCodes = electronicEntries
                    .Where(entry =>
                        string.Equals(entry.MediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(entry.Disposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase)
                        && entry.IsBorrowedHardDisk
                        && !string.IsNullOrWhiteSpace(entry.BorrowedHardDiskCode))
                    .Select(entry => entry.BorrowedHardDiskCode!.Trim())
                    .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (duplicatedBorrowedCodes.Count > 0)
                {
                    errors.Add($"• 同一申请单内“资料室借出硬盘编号”不能重复：{string.Join("、", duplicatedBorrowedCodes)}");
                }
            }

            if (string.IsNullOrWhiteSpace(record.ArchivePurpose)) errors.Add("• 未选择【库管模式】");
            else if (!IsAllowedDomainValue(record.ArchivePurpose, archivePurposeOptions))
                errors.Add($"• 库管模式不在域值定义中（允许值：{string.Join("、", archivePurposeOptions)}）");

            if (string.IsNullOrWhiteSpace(record.ApplicantName)) errors.Add("• 申请人异常");

            errors.AddRange(ValidateMediaItemConfidentialLevels(entries, pageDomainOptions).Errors);

            return new ArchiveRegisterApplicationValidationResult(errors);
        }

        private static ArchiveRegisterApplicationValidationResult ValidateMediaItemConfidentialLevels(
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries,
            ArchiveRegisterPageDomainOptions pageDomainOptions)
        {
            var errors = new List<string>();
            var entries = mediaEntries?.ToList() ?? new List<YearlyArchiveRegisterMedia>();
            var confidentialOptions = pageDomainOptions.ConfidentialLevels;

            for (int mediaIndex = 0; mediaIndex < entries.Count; mediaIndex++)
            {
                var media = entries[mediaIndex];
                if (media.Items == null || media.Items.Count == 0)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < media.Items.Count; itemIndex++)
                {
                    var item = media.Items[itemIndex];
                    var prefix = $"• 第{mediaIndex + 1}条介质第{itemIndex + 1}个子项";
                    var confidentialLevel = ArchiveRegisterBusinessRules.NormalizeConfidentialLevel(item.ConfidentialLevel);
                    if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(confidentialLevel, confidentialOptions))
                    {
                        errors.Add($"{prefix}【密级】未选择或不在域值定义中（允许值：{string.Join("、", confidentialOptions)}）");
                    }
                    else
                    {
                        item.ConfidentialLevel = confidentialLevel;
                    }
                }
            }

            return new ArchiveRegisterApplicationValidationResult(errors);
        }

        public async Task<bool> TryAutoFillApprovalForArchiveAdminAsync(YearlyArchiveRegisterRecord record, User currentUser)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(currentUser);

            if (!IsArchiveAdminUser(currentUser) || !record.IsSubmitted)
            {
                return false;
            }

            var users = await _archiveRegisterRepository.GetUsersAsync();
            bool changed = false;

            string deptLeader = FindUserByDeptAndRole(users, record.ApplicantDept, "业务部门负责人");
            if (string.IsNullOrWhiteSpace(record.DeptLeader) && !string.IsNullOrWhiteSpace(deptLeader))
            {
                record.DeptLeader = deptLeader;
                record.DeptDate = DateTime.Now;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(record.Administrator))
            {
                record.Administrator = currentUser.RealName;
                record.AdminDate = DateTime.Now;
                changed = true;
            }

            return changed;
        }

        public ArchiveRegisterUiPermissionState ResolveUiPermissionState(User? user, YearlyArchiveRegisterRecord? currentRecord)
        {
            return ArchiveRegisterBusinessRules.ResolveUiPermissionState(user, currentRecord);
        }

    }
}
