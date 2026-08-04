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
    public async Task<IReadOnlyDictionary<int, string>> ResolveBeforeStorageLocationsAsync(IReadOnlyList<HardDiskMedium> media)
    {
        if (media == null || media.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var needLookupIds = media
            .Where(item => item != null
                           && (string.Equals(
                                   item.Ledger?.MediaStatus?.Trim(),
                                   HardDiskMedium.StatusInStockLost,
                                   StringComparison.Ordinal)
                               || string.Equals(
                                   item.Ledger?.MediaStatus?.Trim(),
                                   HardDiskMedium.StatusInStockScrap,
                                   StringComparison.Ordinal))
                           && string.IsNullOrWhiteSpace(item.Ledger?.StorageLocation))
            .Select(item => item.Id)
            .Distinct()
            .ToList();

        IReadOnlyDictionary<int, string> lostBeforeLocations = needLookupIds.Count == 0
            ? new Dictionary<int, string>()
            : await _repository.GetInventoryLostBeforeLocationsAsync(needLookupIds);

        var result = new Dictionary<int, string>(media.Count);
        foreach (var medium in media)
        {
            if (medium == null || result.ContainsKey(medium.Id))
            {
                continue;
            }

            lostBeforeLocations.TryGetValue(medium.Id, out string? lostBefore);
            result[medium.Id] = HardDiskDisposalDomainValues.ResolveBeforeStorageLocation(
                medium.Ledger?.MediaStatus,
                medium.Ledger?.StorageLocation,
                lostBefore);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, string>> GetInventoryLostBeforeLocationsAsync(IReadOnlyCollection<int> mediumIds)
    {
        return await _repository.GetInventoryLostBeforeLocationsAsync(mediumIds);
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

        ValidateHeader(draft.OtherRemark, draft.Reason, draft.Items);
        var media = await LoadAndValidateMediaAsync(mediumIds, excludeRecordId: null);

        DateTime now = DateTime.Now;
        string disposalNo = string.IsNullOrWhiteSpace(draft.DisposalNo)
            ? await _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.DiskDisposalApply)
            : draft.DisposalNo.Trim();

        var items = await BuildItemsAsync(media, now, draft.Items);
        ValidateItemReasons(items);
        ValidateItemDispositionMethods(items);

        var record = new HardDiskDisposalRecord
        {
            DisposalNo = disposalNo,
            Status = HardDiskDisposalRecord.StatusDraft,
            DisposalReason = HardDiskDisposalDomainValues.BuildReasonSummary(items.Select(item => item.DisposalReason)),
            DispositionMethod = HardDiskDisposalDomainValues.BuildDispositionMethodSummary(
                items.Select(item => item.DispositionMethod)),
            OtherRemark = draft.OtherRemark?.Trim() ?? string.Empty,
            Reason = draft.Reason?.Trim() ?? string.Empty,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            ApplicantUserId = currentUser.Id,
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            ApplyTime = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items = items
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

        ValidateHeader(draft.OtherRemark, draft.Reason, draft.Items);
        var media = await LoadAndValidateMediaAsync(mediumIds, excludeRecordId: existing.Id);

        DateTime now = DateTime.Now;
        var items = await BuildItemsAsync(media, now, draft.Items);
        ValidateItemReasons(items);
        ValidateItemDispositionMethods(items);

        existing.DisposalReason = HardDiskDisposalDomainValues.BuildReasonSummary(items.Select(item => item.DisposalReason));
        existing.DispositionMethod = HardDiskDisposalDomainValues.BuildDispositionMethodSummary(
            items.Select(item => item.DispositionMethod));
        existing.OtherRemark = draft.OtherRemark?.Trim() ?? string.Empty;
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;

        if (existing.Items.Count > 0)
        {
            _repository.RemoveItems(existing.Items.ToList());
            existing.Items.Clear();
        }

        foreach (var item in items)
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

        ValidateHeader(existing.OtherRemark, existing.Reason, existing.Items);
        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一块待处置硬盘。");
        }

        ValidateItemReasons(existing.Items);
        ValidateItemDispositionMethods(existing.Items);

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

        var lostMediumIdsNeedingLocation = existing.Items
            .Where(item =>
            {
                var medium = media.FirstOrDefault(m => m.Id == item.MediumId);
                string status = medium?.Ledger?.MediaStatus?.Trim() ?? item.BeforeMediaStatus?.Trim() ?? string.Empty;
                return (string.Equals(status, HardDiskMedium.StatusInStockLost, StringComparison.Ordinal)
                        || string.Equals(status, HardDiskMedium.StatusInStockScrap, StringComparison.Ordinal))
                       && string.IsNullOrWhiteSpace(medium?.Ledger?.StorageLocation)
                       && string.IsNullOrWhiteSpace(item.BeforeStorageLocation);
            })
            .Select(item => item.MediumId)
            .Distinct()
            .ToList();
        IReadOnlyDictionary<int, string> lostBeforeLocations = lostMediumIdsNeedingLocation.Count == 0
            ? new Dictionary<int, string>()
            : await _repository.GetInventoryLostBeforeLocationsAsync(lostMediumIdsNeedingLocation);

        foreach (var item in existing.Items.OrderBy(detail => detail.SortOrder))
        {
            var medium = media.First(m => m.Id == item.MediumId);
            var ledger = EnsureLedger(medium, now);
            string beforeStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
            lostBeforeLocations.TryGetValue(item.MediumId, out string? lostBefore);
            string beforeLocation = HardDiskDisposalDomainValues.ResolveBeforeStorageLocation(
                beforeStatus,
                ledger.StorageLocation,
                string.IsNullOrWhiteSpace(item.BeforeStorageLocation) ? lostBefore : item.BeforeStorageLocation);
            string itemReason = ResolveItemDisposalReason(item);
            string itemMethod = ResolveItemDispositionMethod(item, existing.DispositionMethod);
            string holder = ResolveHolderAfterComplete(itemMethod);

            if (!IsInStockStatus(beforeStatus))
            {
                throw new InvalidOperationException($"硬盘【{medium.DiskCode}】当前状态为“{beforeStatus}”，无法办结离库处置。");
            }

            string afterStatus = HardDiskDisposalDomainValues.ResolveTerminalMediaStatus(itemReason);
            string transactionType = HardDiskDisposalDomainValues.ResolveTransactionType(itemReason);

            medium.UpdatedTime = now;
            ledger.UpdatedTime = now;
            ledger.DiskCode = medium.DiskCode;
            ledger.MediaStatus = afterStatus;
            ledger.NeedReturn = false;
            ledger.StorageLocation = string.Empty;
            ledger.HolderOrOrganization = holder;
            ledger.Remark = AppendDisposalRemark(ledger.Remark, existing, itemReason, itemMethod);

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
                Description = BuildTransactionDescription(itemReason, itemMethod),
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
            DisposalReason = HardDiskDisposalDomainValues.BuildReasonSummary(
                record.Items.Select(item => ResolveItemDisposalReason(item))),
            DispositionMethod = HardDiskDisposalDomainValues.BuildDispositionMethodSummary(
                record.Items.Select(item => ResolveItemDispositionMethod(item, record.DispositionMethod))),
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
            PrintCount = record.PrintCount,
            Items = record.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new HardDiskDisposalPrintItemData
                {
                    SortOrder = item.SortOrder,
                    DiskCode = item.DiskCode,
                    SerialNumber = item.SerialNumber,
                    BeforeMediaStatus = item.BeforeMediaStatus,
                    BeforeMediaNature = item.BeforeMediaNature,
                    BeforeStorageLocation = item.BeforeStorageLocation,
                    DisposalReason = ResolveItemDisposalReason(item),
                    DispositionMethod = ResolveItemDispositionMethod(item, record.DispositionMethod)
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
                throw new InvalidOperationException($"硬盘【{medium.DiskCode}】当前状态为“{status}”，仅「在库(空盘)」「在库(损坏)」「在库(盘失)」「在库(拟销)」可离库处置。");
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

    private async Task<List<HardDiskDisposalItem>> BuildItemsAsync(
        IReadOnlyList<HardDiskMedium> media,
        DateTime now,
        IEnumerable<HardDiskDisposalItem>? draftItems)
    {
        Dictionary<int, string> methodsByMediumId = (draftItems ?? Array.Empty<HardDiskDisposalItem>())
            .Where(item => item.MediumId > 0)
            .GroupBy(item => item.MediumId)
            .ToDictionary(
                group => group.Key,
                group => group.Last().DispositionMethod?.Trim() ?? string.Empty);

        IReadOnlyDictionary<int, string> beforeLocations = await ResolveBeforeStorageLocationsAsync(media);

        int sort = 1;
        return media
            .OrderBy(item => item.DiskCode, StringComparer.Ordinal)
            .Select(medium =>
            {
                string beforeStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty;
                string reason = HardDiskDisposalDomainValues.ResolveReasonFromMediaStatus(beforeStatus);
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException(
                        $"硬盘【{medium.DiskCode}】当前状态为“{beforeStatus}”，无法自动确定离库原因。");
                }

                methodsByMediumId.TryGetValue(medium.Id, out string? draftMethod);
                string method = draftMethod?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(method))
                {
                    method = HardDiskDisposalDomainValues.ResolveDispositionMethodFromMediaStatus(beforeStatus);
                }

                beforeLocations.TryGetValue(medium.Id, out string? beforeLocation);

                return new HardDiskDisposalItem
                {
                    SortOrder = sort++,
                    MediumId = medium.Id,
                    DiskCode = medium.DiskCode?.Trim() ?? string.Empty,
                    SerialNumber = medium.SerialNumber?.Trim() ?? string.Empty,
                    BeforeMediaStatus = beforeStatus,
                    BeforeStorageLocation = beforeLocation ?? string.Empty,
                    BeforeMediaNature = medium.Ledger?.MediaNature?.Trim() ?? string.Empty,
                    DisposalReason = reason,
                    DispositionMethod = method,
                    CreatedAt = now
                };
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

    private static void ValidateHeader(
        string? otherRemark,
        string? applyReason,
        IEnumerable<HardDiskDisposalItem>? items)
    {
        bool requiresOtherRemark = (items ?? Array.Empty<HardDiskDisposalItem>())
            .Any(item => HardDiskDisposalDomainValues.RequiresOtherRemark(
                ResolveItemDispositionMethod(item, headerFallback: null)));

        if (requiresOtherRemark && string.IsNullOrWhiteSpace(otherRemark))
        {
            throw new InvalidOperationException("存在处置方式为「其他」的硬盘时，须填写说明。");
        }

        if (string.IsNullOrWhiteSpace(applyReason))
        {
            throw new InvalidOperationException("请填写申请说明。");
        }
    }

    private static void ValidateItemReasons(IEnumerable<HardDiskDisposalItem> items)
    {
        foreach (var item in items)
        {
            string reason = ResolveItemDisposalReason(item);
            if (!HardDiskDisposalDomainValues.IsValidReason(reason))
            {
                throw new InvalidOperationException(
                    $"硬盘【{item.DiskCode}】离库原因无效，请按介质状态重新选取后保存。");
            }
        }
    }

    private static void ValidateItemDispositionMethods(IEnumerable<HardDiskDisposalItem> items)
    {
        foreach (var item in items)
        {
            string reason = ResolveItemDisposalReason(item);
            string method = ResolveItemDispositionMethod(item, headerFallback: null);
            if (!HardDiskDisposalDomainValues.IsValidDispositionMethod(method))
            {
                throw new InvalidOperationException(
                    $"硬盘【{item.DiskCode}】未指定处置方式，请勾选后在上方选择处置方式。");
            }

            string? mismatch = HardDiskDisposalDomainValues.TryGetReasonAndDispositionMethodMismatchMessage(
                reason,
                method);
            if (!string.IsNullOrWhiteSpace(mismatch))
            {
                throw new InvalidOperationException($"硬盘【{item.DiskCode}】{mismatch}");
            }
        }
    }

    /// <summary>
    /// 解析明细离库原因：优先明细字段；空时按处置前状态回推（兼容迁移前数据）。
    /// </summary>
    private static string ResolveItemDisposalReason(HardDiskDisposalItem item)
    {
        string reason = item.DisposalReason?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            return string.Equals(reason, HardDiskDisposalDomainValues.LegacyReasonDamaged, StringComparison.Ordinal)
                ? HardDiskDisposalDomainValues.ReasonDamaged
                : reason;
        }

        return HardDiskDisposalDomainValues.ResolveReasonFromMediaStatus(item.BeforeMediaStatus);
    }

    /// <summary>
    /// 解析明细处置方式：优先明细字段；空时按盘失自动回推，再回退主表汇总/旧整单值。
    /// </summary>
    private static string ResolveItemDispositionMethod(HardDiskDisposalItem item, string? headerFallback)
    {
        string method = item.DispositionMethod?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(method))
        {
            return method;
        }

        method = HardDiskDisposalDomainValues.ResolveDispositionMethodFromMediaStatus(item.BeforeMediaStatus);
        if (!string.IsNullOrWhiteSpace(method))
        {
            return method;
        }

        // 兼容迁移前整单处置方式：主表仅有单一值时回填到明细。
        string header = headerFallback?.Trim() ?? string.Empty;
        if (HardDiskDisposalDomainValues.IsValidDispositionMethod(header)
            && !header.Contains('、', StringComparison.Ordinal))
        {
            return header;
        }

        return string.Empty;
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
            || string.Equals(normalized, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal)
            || string.Equals(normalized, HardDiskMedium.StatusInStockLost, StringComparison.Ordinal)
            || string.Equals(normalized, HardDiskMedium.StatusInStockScrap, StringComparison.Ordinal);
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

    private static string BuildTransactionDescription(string disposalReason, string dispositionMethod)
    {
        return $"离库处置：{disposalReason} / {dispositionMethod}";
    }

    private static string AppendDisposalRemark(
        string? existingRemark,
        HardDiskDisposalRecord record,
        string disposalReason,
        string dispositionMethod)
    {
        string prefix = string.IsNullOrWhiteSpace(existingRemark) ? string.Empty : existingRemark.Trim() + "；";
        return $"{prefix}离库处置单 {record.DisposalNo}（{disposalReason}/{dispositionMethod}）";
    }
}
