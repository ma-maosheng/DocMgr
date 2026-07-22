using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia;

/// <summary>
/// 硬盘离库处置业务服务。
/// </summary>
public sealed class HardDiskDisposalService : IHardDiskDisposalService
{
    private readonly IHardDiskDisposalRepository _repository;
    private readonly IBusinessRuleService _businessRuleService;

    public HardDiskDisposalService(
        IHardDiskDisposalRepository repository,
        IBusinessRuleService businessRuleService)
    {
        _repository = repository;
        _businessRuleService = businessRuleService;
    }

    public async Task<IReadOnlyList<HardDiskDisposalRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear)
    {
        return await _repository.SearchRecordsAsync(keyword, status, applyYear);
    }

    public Task<HardDiskDisposalRecord?> GetRecordByIdAsync(int recordId)
    {
        return _repository.GetRecordByIdAsync(recordId);
    }

    public async Task<IReadOnlyList<HardDiskMedium>> GetSelectableMediaAsync(int? currentRecordId = null)
    {
        IReadOnlyList<int>? excludeIds = null;
        if (currentRecordId.HasValue && currentRecordId.Value > 0)
        {
            var current = await _repository.GetRecordByIdAsync(currentRecordId.Value);
            if (current?.Items != null && current.Items.Count > 0)
            {
                excludeIds = current.Items.Select(item => item.MediumId).ToList();
            }
        }

        return await _repository.GetSelectableInStockMediaAsync(excludeIds);
    }

    /// <inheritdoc />
    public Task<string> GenerateNextDisposalNoAsync()
    {
        return _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.DiskDisposalApply);
    }

    public async Task<HardDiskDisposalRecord> CreateDraftAsync(
        HardDiskDisposalRecord draft,
        IReadOnlyList<int> mediumIds,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);

        ValidateHeader(draft.DisposalReason, draft.DispositionMethod, draft.OtherRemark, draft.Reason);
        var media = await LoadAndValidateMediaAsync(mediumIds, excludeRecordId: null);

        DateTime now = DateTime.Now;
        string disposalNo = string.IsNullOrWhiteSpace(draft.DisposalNo)
            ? await _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.DiskDisposalApply)
            : draft.DisposalNo.Trim();

        var record = new HardDiskDisposalRecord
        {
            DisposalNo = disposalNo,
            Status = HardDiskDisposalRecord.StatusDraft,
            DisposalReason = draft.DisposalReason.Trim(),
            DispositionMethod = draft.DispositionMethod.Trim(),
            OtherRemark = draft.OtherRemark?.Trim() ?? string.Empty,
            Reason = draft.Reason?.Trim() ?? string.Empty,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            ApplicantUserId = currentUser.Id,
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            ApplyTime = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items = BuildItems(media, now)
        };

        _repository.AddRecord(record);
        await _repository.SaveChangesAsync();
        return (await _repository.GetRecordByIdAsync(record.Id))!;
    }

    public async Task<HardDiskDisposalRecord> UpdateDraftAsync(
        HardDiskDisposalRecord draft,
        IReadOnlyList<int> mediumIds,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.Id <= 0)
        {
            throw new InvalidOperationException("处置单无效，无法保存。");
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(draft.Id)
            ?? throw new InvalidOperationException("未找到离库处置单。");

        if (existing.Status != HardDiskDisposalRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态的处置单可编辑。");
        }

        ValidateHeader(draft.DisposalReason, draft.DispositionMethod, draft.OtherRemark, draft.Reason);
        var media = await LoadAndValidateMediaAsync(mediumIds, excludeRecordId: existing.Id);

        DateTime now = DateTime.Now;
        existing.DisposalReason = draft.DisposalReason.Trim();
        existing.DispositionMethod = draft.DispositionMethod.Trim();
        existing.OtherRemark = draft.OtherRemark?.Trim() ?? string.Empty;
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;

        if (existing.Items.Count > 0)
        {
            _repository.RemoveItems(existing.Items.ToList());
            existing.Items.Clear();
        }

        foreach (var item in BuildItems(media, now))
        {
            existing.Items.Add(item);
        }

        await _repository.SaveChangesAsync();
        return (await _repository.GetRecordByIdAsync(existing.Id))!;
    }

    public async Task SubmitAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到离库处置单。");

        if (existing.Status != HardDiskDisposalRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可提交。");
        }

        ValidateHeader(existing.DisposalReason, existing.DispositionMethod, existing.OtherRemark, existing.Reason);
        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一块待处置硬盘。");
        }

        var media = await LoadAndValidateMediaAsync(
            existing.Items.Select(item => item.MediumId).ToList(),
            excludeRecordId: existing.Id);

        DateTime now = DateTime.Now;
        LockMedia(existing, media, now);

        existing.Status = HardDiskDisposalRecord.StatusSubmitted;
        existing.SubmittedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task ApproveAsync(int recordId, string approvalOpinion, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到离库处置单。");

        if (existing.Status != HardDiskDisposalRecord.StatusSubmitted)
        {
            throw new InvalidOperationException("仅已提交状态可审批。");
        }

        DateTime now = DateTime.Now;
        existing.Status = HardDiskDisposalRecord.StatusApproved;
        existing.ApprovedBy = ResolveUserDisplayName(currentUser);
        existing.ApprovedTime = now;
        existing.ApprovalOpinion = string.IsNullOrWhiteSpace(approvalOpinion) ? "同意" : approvalOpinion.Trim();
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task ConfirmReadyForUploadAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到离库处置单。");

        if (existing.Status != HardDiskDisposalRecord.StatusApproved)
        {
            throw new InvalidOperationException("请先完成审批后再确认可上传签批单。");
        }

        DateTime now = DateTime.Now;
        existing.Status = HardDiskDisposalRecord.StatusSignedUploaded;
        existing.ConfirmedBy = ResolveUserDisplayName(currentUser);
        existing.ConfirmedTime = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task CompleteAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到离库处置单。");

        if (existing.Status != HardDiskDisposalRecord.StatusSignedUploaded)
        {
            throw new InvalidOperationException("请先确认可上传签批单后再办结。");
        }

        var attachments = await _repository.GetAttachmentsAsync(existing.DisposalNo);
        bool hasSignedForm = attachments.Any(item =>
            string.Equals(item.FileCategory, HardDiskDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal));
        bool hasDiskPhoto = attachments.Any(item =>
            string.Equals(item.FileCategory, HardDiskDisposalDomainValues.AttachmentCategoryDiskPhoto, StringComparison.Ordinal));

        if (!hasSignedForm)
        {
            throw new InvalidOperationException("办结前须上传签批单附件。");
        }

        if (!hasDiskPhoto)
        {
            throw new InvalidOperationException("办结前须上传待处置硬盘照片。");
        }

        var media = await _repository.GetMediaWithLedgerByIdsAsync(existing.Items.Select(item => item.MediumId).ToList());
        if (media.Count != existing.Items.Count)
        {
            throw new InvalidOperationException("部分关联硬盘不存在或已删除，无法办结。");
        }

        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);
        string afterStatus = HardDiskDisposalDomainValues.ResolveTerminalMediaStatus(existing.DisposalReason);
        string transactionType = HardDiskDisposalDomainValues.ResolveTransactionType(existing.DisposalReason);
        string holder = ResolveHolderAfterComplete(existing.DispositionMethod);

        foreach (var item in existing.Items.OrderBy(detail => detail.SortOrder))
        {
            var medium = media.First(m => m.Id == item.MediumId);
            var ledger = EnsureLedger(medium, now);
            string beforeStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
            string beforeLocation = ledger.StorageLocation?.Trim() ?? string.Empty;

            if (!IsInStockStatus(beforeStatus))
            {
                throw new InvalidOperationException($"硬盘【{medium.DiskCode}】当前状态为“{beforeStatus}”，无法办结离库处置。");
            }

            medium.UpdatedTime = now;
            ledger.UpdatedTime = now;
            ledger.DiskCode = medium.DiskCode;
            ledger.MediaStatus = afterStatus;
            ledger.NeedReturn = false;
            ledger.StorageLocation = string.Empty;
            ledger.HolderOrOrganization = holder;
            ledger.Remark = AppendDisposalRemark(ledger.Remark, existing);

            _repository.AddTransaction(new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                ApplicationId = null,
                TransactionType = transactionType,
                BeforeStatus = beforeStatus,
                AfterStatus = afterStatus,
                BeforeLocation = beforeLocation,
                AfterLocation = string.Empty,
                OperatorName = operatorName,
                OperateTime = now,
                RelatedPerson = existing.ApplicantName,
                TargetOrganization = holder,
                NeedReturn = false,
                RelatedBatch = existing.DisposalNo,
                Description = BuildTransactionDescription(existing),
                Remark = existing.Remark
            });

            UnlockMediaIfOwned(existing, medium);
        }

        existing.Status = HardDiskDisposalRecord.StatusCompleted;
        existing.CompletedAt = now;
        existing.CompletedBy = operatorName;
        existing.SignedAttachmentUploaded = true;
        existing.DiskPhotoUploaded = true;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task WithdrawAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到离库处置单。");

        if (existing.Status is HardDiskDisposalRecord.StatusCompleted
            or HardDiskDisposalRecord.StatusWithdrawn
            or HardDiskDisposalRecord.StatusForceWithdrawn)
        {
            throw new InvalidOperationException("当前状态不可撤回作废。");
        }

        var media = await _repository.GetMediaWithLedgerByIdsAsync(existing.Items.Select(item => item.MediumId).ToList());
        foreach (var medium in media)
        {
            UnlockMediaIfOwned(existing, medium);
        }

        DateTime now = DateTime.Now;
        existing.Status = HardDiskDisposalRecord.StatusWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = reason?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task RecordPrintAsync(int recordId)
    {
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到离库处置单。");

        if (existing.Status is HardDiskDisposalRecord.StatusDraft
            or HardDiskDisposalRecord.StatusWithdrawn
            or HardDiskDisposalRecord.StatusForceWithdrawn)
        {
            throw new InvalidOperationException("当前状态不可打印签批单。");
        }

        DateTime now = DateTime.Now;
        existing.PrintCount += 1;
        existing.LastPrintedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task<HardDiskDisposalPrintData> BuildPrintDataAsync(int recordId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到离库处置单。");

        return new HardDiskDisposalPrintData
        {
            DisposalNo = record.DisposalNo,
            ApplyDateText = record.ApplyTime.ToString("yyyy-MM-dd"),
            DisposalReason = record.DisposalReason,
            DispositionMethod = record.DispositionMethod,
            OtherRemark = record.OtherRemark,
            Reason = record.Reason,
            Remark = record.Remark,
            ApplicantName = record.ApplicantName,
            ApplicantDept = record.ApplicantDept,
            ApprovedBy = record.ApprovedBy,
            ApprovedDateText = record.ApprovedTime?.ToString("yyyy-MM-dd") ?? string.Empty,
            ApprovalOpinion = record.ApprovalOpinion,
            CompletedBy = record.CompletedBy,
            CompletedDateText = record.CompletedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
            IsCompleted = record.IsCompleted,
            Items = record.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new HardDiskDisposalPrintItemData
                {
                    SortOrder = item.SortOrder,
                    DiskCode = item.DiskCode,
                    SerialNumber = item.SerialNumber,
                    BeforeMediaStatus = item.BeforeMediaStatus,
                    BeforeStorageLocation = item.BeforeStorageLocation
                })
                .ToList()
        };
    }

    public async Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string disposalNo)
    {
        return await _repository.GetAttachmentsAsync(disposalNo);
    }

    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
    {
        return _repository.GetAttachmentByIdAsync(attachmentId);
    }

    public async Task<(bool Ok, string Message, SystemAttachment? Attachment)> UploadAttachmentAsync(
        int recordId,
        string fileCategory,
        string fileName,
        string extension,
        long fileSize,
        byte[] fileContent,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);

        if (string.IsNullOrWhiteSpace(fileName) || fileContent == null || fileContent.Length == 0)
        {
            return (false, "附件内容为空，无法上传。", null);
        }

        string? formatError = SystemAttachmentUploadSupport.ValidateUploadFormat(fileName, extension, fileContent);
        if (!string.IsNullOrWhiteSpace(formatError))
        {
            return (false, formatError, null);
        }

        string category = fileCategory?.Trim() ?? string.Empty;
        if (!HardDiskDisposalDomainValues.AttachmentCategoryOptions.Contains(category, StringComparer.Ordinal))
        {
            return (false, "附件分类无效。", null);
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId);
        if (existing == null)
        {
            return (false, "未找到离库处置单。", null);
        }

        if (existing.Status is HardDiskDisposalRecord.StatusDraft
            or HardDiskDisposalRecord.StatusSubmitted
            or HardDiskDisposalRecord.StatusWithdrawn
            or HardDiskDisposalRecord.StatusForceWithdrawn
            or HardDiskDisposalRecord.StatusCompleted)
        {
            return (false, "当前状态不允许上传附件（请在审批通过并确认可上传后操作）。", null);
        }

        if (existing.Status == HardDiskDisposalRecord.StatusApproved
            && !string.Equals(category, HardDiskDisposalDomainValues.AttachmentCategoryOther, StringComparison.Ordinal))
        {
            // 允许审批后先确认；附件主流程在 SignedUploaded，但 Approved 也可先传其他附件。
            // 签批单与照片要求进入 SignedUploaded。
            return (false, "请先确认可上传签批单，再上传签批单或硬盘照片。", null);
        }

        DateTime now = DateTime.Now;
        var attachment = new SystemAttachment
        {
            BusinessType = HardDiskDisposalDomainValues.AttachmentBusinessType,
            BusinessNo = existing.DisposalNo,
            BusinessId = existing.Id,
            FileName = fileName,
            Extension = extension ?? string.Empty,
            FileSize = fileSize,
            FileContent = fileContent,
            FileCategory = category,
            UploadTime = now,
            UploaderName = ResolveUserDisplayName(currentUser)
        };

        _repository.AddAttachment(attachment);

        if (string.Equals(category, HardDiskDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal))
        {
            existing.SignedAttachmentUploaded = true;
            existing.SignedAttachmentUploadedTime = now;
            existing.SignedAttachmentUploader = attachment.UploaderName;
        }
        else if (string.Equals(category, HardDiskDisposalDomainValues.AttachmentCategoryDiskPhoto, StringComparison.Ordinal))
        {
            existing.DiskPhotoUploaded = true;
        }

        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
        return (true, "附件上传成功。", attachment);
    }

    public async Task<(bool Ok, string Message)> DeleteAttachmentAsync(int attachmentId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var attachment = await _repository.GetAttachmentByIdAsync(attachmentId);
        if (attachment == null)
        {
            return (false, "附件不存在。");
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(attachment.BusinessId);
        if (existing == null)
        {
            return (false, "未找到关联处置单。");
        }

        if (existing.Status == HardDiskDisposalRecord.StatusCompleted)
        {
            return (false, "已办结单据不可删除附件。");
        }

        string category = attachment.FileCategory?.Trim() ?? string.Empty;
        var remainingBefore = await _repository.GetAttachmentsAsync(existing.DisposalNo);
        var remaining = remainingBefore.Where(item => item.Id != attachmentId).ToList();
        _repository.RemoveAttachment(attachment);

        if (string.Equals(category, HardDiskDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal))
        {
            bool stillHas = remaining.Any(item =>
                string.Equals(item.FileCategory, HardDiskDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal));
            if (!stillHas)
            {
                existing.SignedAttachmentUploaded = false;
                existing.SignedAttachmentUploadedTime = null;
                existing.SignedAttachmentUploader = string.Empty;
            }
        }
        else if (string.Equals(category, HardDiskDisposalDomainValues.AttachmentCategoryDiskPhoto, StringComparison.Ordinal))
        {
            bool stillHas = remaining.Any(item =>
                string.Equals(item.FileCategory, HardDiskDisposalDomainValues.AttachmentCategoryDiskPhoto, StringComparison.Ordinal));
            if (!stillHas)
            {
                existing.DiskPhotoUploaded = false;
            }
        }

        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
        return (true, "附件已删除。");
    }

    private async Task<List<HardDiskMedium>> LoadAndValidateMediaAsync(IReadOnlyList<int> mediumIds, int? excludeRecordId)
    {
        if (mediumIds == null || mediumIds.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一块待处置硬盘。");
        }

        List<int> ids = mediumIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一块待处置硬盘。");
        }

        var media = await _repository.GetMediaWithLedgerByIdsAsync(ids);
        if (media.Count != ids.Count)
        {
            throw new InvalidOperationException("部分所选硬盘不存在或已删除。");
        }

        foreach (var medium in media)
        {
            string status = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty;
            if (!IsInStockStatus(status))
            {
                throw new InvalidOperationException($"硬盘【{medium.DiskCode}】当前状态为“{status}”，仅「在库(空盘)」「在库(损坏)」可离库处置。");
            }

            if (await _repository.ExistsActiveDisposalForMediumAsync(medium.Id, excludeRecordId))
            {
                throw new InvalidOperationException($"硬盘【{medium.DiskCode}】已存在未办结的离库处置单。");
            }

            if (medium.RegisterLock != null)
            {
                bool ownedByCurrent = excludeRecordId.HasValue
                    && string.Equals(medium.RegisterLock.BusinessType, HardDiskRegisterLock.BusinessTypeDisposal, StringComparison.Ordinal)
                    && medium.RegisterLock.BusinessRecordId == excludeRecordId.Value;

                if (!ownedByCurrent)
                {
                    string lockOwner = string.IsNullOrWhiteSpace(medium.RegisterLock.BusinessNo)
                        ? medium.RegisterLock.BusinessType
                        : $"{medium.RegisterLock.BusinessType}（{medium.RegisterLock.BusinessNo.Trim()}）";
                    throw new InvalidOperationException(
                        $"硬盘【{medium.DiskCode}】已被其他业务征用：{lockOwner}，不可纳入离库处置。");
                }
            }
        }

        return media;
    }

    private static List<HardDiskDisposalItem> BuildItems(IReadOnlyList<HardDiskMedium> media, DateTime now)
    {
        int sort = 1;
        return media
            .OrderBy(item => item.DiskCode, StringComparer.Ordinal)
            .Select(medium => new HardDiskDisposalItem
            {
                SortOrder = sort++,
                MediumId = medium.Id,
                DiskCode = medium.DiskCode?.Trim() ?? string.Empty,
                SerialNumber = medium.SerialNumber?.Trim() ?? string.Empty,
                BeforeMediaStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty,
                BeforeStorageLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty,
                BeforeMediaNature = medium.Ledger?.MediaNature?.Trim() ?? string.Empty,
                CreatedAt = now
            })
            .ToList();
    }

    /// <summary>
    /// 提交后征用：写入 HardDiskRegisterLock（业务类型=硬盘离库处置），阻止其他业务再占用。
    /// </summary>
    private static void LockMedia(HardDiskDisposalRecord record, IReadOnlyList<HardDiskMedium> media, DateTime now)
    {
        foreach (var medium in media)
        {
            if (medium.RegisterLock != null)
            {
                bool ownedByCurrent =
                    string.Equals(medium.RegisterLock.BusinessType, HardDiskRegisterLock.BusinessTypeDisposal, StringComparison.Ordinal)
                    && medium.RegisterLock.BusinessRecordId == record.Id;

                if (!ownedByCurrent)
                {
                    string lockOwner = string.IsNullOrWhiteSpace(medium.RegisterLock.BusinessNo)
                        ? medium.RegisterLock.BusinessType
                        : $"{medium.RegisterLock.BusinessType}（{medium.RegisterLock.BusinessNo.Trim()}）";
                    throw new InvalidOperationException(
                        $"硬盘【{medium.DiskCode}】已被其他业务征用：{lockOwner}，无法提交离库处置。");
                }

                medium.RegisterLock.BusinessNo = record.DisposalNo;
                medium.RegisterLock.PreviousStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty;
                medium.RegisterLock.LockedTime = now;
                medium.UpdatedTime = now;
                continue;
            }

            medium.RegisterLock = new HardDiskRegisterLock
            {
                MediumId = medium.Id,
                BusinessType = HardDiskRegisterLock.BusinessTypeDisposal,
                BusinessRecordId = record.Id,
                BusinessNo = record.DisposalNo,
                PreviousStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty,
                LockedTime = now
            };
            medium.UpdatedTime = now;
        }
    }

    private void UnlockMediaIfOwned(HardDiskDisposalRecord record, HardDiskMedium medium)
    {
        var lockItem = medium.RegisterLock;
        if (lockItem == null)
        {
            return;
        }

        if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeDisposal, StringComparison.Ordinal)
            || lockItem.BusinessRecordId != record.Id)
        {
            return;
        }

        _repository.RemoveRegisterLock(lockItem);
        medium.RegisterLock = null;
        medium.UpdatedTime = DateTime.Now;
    }

    private static void ValidateHeader(string? reason, string? method, string? otherRemark, string? applyReason)
    {
        if (!HardDiskDisposalDomainValues.IsValidReason(reason))
        {
            throw new InvalidOperationException("请选择离库原因（淘汰/损毁/盘失/其他）。");
        }

        if (!HardDiskDisposalDomainValues.IsValidDispositionMethod(method))
        {
            throw new InvalidOperationException("请选择离库后处置方式（直接销毁/退还办公室/其他）。");
        }

        if (HardDiskDisposalDomainValues.RequiresOtherRemark(reason, method)
            && string.IsNullOrWhiteSpace(otherRemark))
        {
            throw new InvalidOperationException("离库原因或处置方式为「其他」时，须填写说明。");
        }

        if (string.IsNullOrWhiteSpace(applyReason))
        {
            throw new InvalidOperationException("请填写申请说明。");
        }
    }

    private static void EnsureArchiveAdmin(User? currentUser)
    {
        if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
        {
            throw new InvalidOperationException("仅资料室资料管理员可办理硬盘离库处置。");
        }
    }

    private static string ResolveUserDisplayName(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.RealName))
        {
            return user.RealName.Trim();
        }

        return user.LoginName?.Trim() ?? string.Empty;
    }

    private static bool IsInStockStatus(string? status)
    {
        string normalized = status?.Trim() ?? string.Empty;
        return string.Equals(normalized, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal)
            || string.Equals(normalized, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal);
    }

    private static HardDiskLedger EnsureLedger(HardDiskMedium medium, DateTime now)
    {
        if (medium.Ledger != null)
        {
            return medium.Ledger;
        }

        medium.Ledger = new HardDiskLedger
        {
            MediumId = medium.Id,
            DiskCode = medium.DiskCode,
            MediaStatus = string.Empty,
            MediaNature = string.Empty,
            StorageLocation = string.Empty,
            HolderOrOrganization = string.Empty,
            NeedReturn = false,
            RegisterPerson = medium.RegisterPerson,
            RegisterDate = medium.RegisterDate,
            Remark = medium.Remark,
            CreatedTime = now,
            UpdatedTime = now
        };
        return medium.Ledger;
    }

    private static string ResolveHolderAfterComplete(string? dispositionMethod)
    {
        string method = dispositionMethod?.Trim() ?? string.Empty;
        if (string.Equals(method, HardDiskDisposalDomainValues.MethodReturnOffice, StringComparison.Ordinal))
        {
            return HardDiskDisposalDomainValues.HolderOffice;
        }

        if (string.Equals(method, HardDiskDisposalDomainValues.MethodDirectDestroy, StringComparison.Ordinal))
        {
            return HardDiskDisposalDomainValues.HolderDestroyed;
        }

        return string.Empty;
    }

    private static string BuildTransactionDescription(HardDiskDisposalRecord record)
    {
        return $"离库处置：{record.DisposalReason} / {record.DispositionMethod}";
    }

    private static string AppendDisposalRemark(string? existingRemark, HardDiskDisposalRecord record)
    {
        string prefix = string.IsNullOrWhiteSpace(existingRemark) ? string.Empty : existingRemark.Trim() + "；";
        return $"{prefix}离库处置单 {record.DisposalNo}（{record.DisposalReason}/{record.DispositionMethod}）";
    }
}
