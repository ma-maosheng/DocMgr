using DocMgr.Models.HistoryArchive;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HistoryArchive;

/// <summary>
/// 历史存档离库处置：七态办理、混放整组纳入、办结撤柜。
/// </summary>
public sealed class HistoryArchiveDisposalService : IHistoryArchiveDisposalService
{
    private readonly IHistoryArchiveDisposalRepository _repository;
    private readonly IBusinessRuleService _businessRuleService;
    private readonly IUserService _userService;

    public HistoryArchiveDisposalService(
        IHistoryArchiveDisposalRepository repository,
        IBusinessRuleService businessRuleService,
        IUserService userService)
    {
        _repository = repository;
        _businessRuleService = businessRuleService;
        _userService = userService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryArchiveDisposalRecord>> SearchRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear) =>
        await _repository.SearchRecordsAsync(keyword, status, applyYear);

    /// <inheritdoc />
    public Task<HistoryArchiveDisposalRecord?> GetRecordByIdAsync(int recordId) =>
        _repository.GetRecordByIdAsync(recordId);

    /// <inheritdoc />
    public Task<string> GenerateNextDisposalNoAsync() =>
        _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.HistoryArchiveDisposalApply);

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryArchiveDisposalBoxCandidate>> GetSelectableBoxesAsync(
        string materialKind,
        int? currentRecordId = null)
    {
        string kind = HistoryArchiveDisposalDomainValues.NormalizeMaterialKind(materialKind);
        if (!HistoryArchiveDisposalDomainValues.IsValidMaterialKind(kind))
        {
            return Array.Empty<HistoryArchiveDisposalBoxCandidate>();
        }

        return await BuildCandidatesAsync(kind, currentRecordId);
    }

    /// <inheritdoc />
    public async Task<HistoryArchiveDisposalRecord> CreateDraftAsync(
        HistoryArchiveDisposalRecord draft,
        IReadOnlyList<HistoryArchiveDisposalItem> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);

        DateTime now = DateTime.Now;
        string kind = HistoryArchiveDisposalDomainValues.NormalizeMaterialKind(draft.MaterialKind);
        var builtItems = await BuildItemsAsync(kind, items, currentRecordId: 0);

        var record = new HistoryArchiveDisposalRecord
        {
            DisposalNo = string.IsNullOrWhiteSpace(draft.DisposalNo)
                ? await GenerateNextDisposalNoAsync()
                : draft.DisposalNo.Trim(),
            Status = HistoryArchiveDisposalRecord.StatusDraft,
            MaterialKind = kind,
            DispositionMethod = draft.DispositionMethod?.Trim() ?? string.Empty,
            TransferTarget = draft.TransferTarget?.Trim() ?? string.Empty,
            OtherRemark = draft.OtherRemark?.Trim() ?? string.Empty,
            Reason = draft.Reason?.Trim() ?? string.Empty,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            ApplicantUserId = currentUser.Id,
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            ApplyTime = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items = builtItems
        };

        if (record.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一个档案盒。");
        }

        _repository.AddRecord(record);
        await _repository.SaveChangesAsync();
        return (await _repository.GetRecordByIdAsync(record.Id))!;
    }

    /// <inheritdoc />
    public async Task<HistoryArchiveDisposalRecord> UpdateDraftAsync(
        HistoryArchiveDisposalRecord draft,
        IReadOnlyList<HistoryArchiveDisposalItem> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        var existing = await _repository.GetRecordByIdAsync(draft.Id, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");

        if (existing.Status != HistoryArchiveDisposalRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态的处置单可编辑。");
        }

        string kind = HistoryArchiveDisposalDomainValues.NormalizeMaterialKind(
            string.IsNullOrWhiteSpace(draft.MaterialKind) ? existing.MaterialKind : draft.MaterialKind);
        var builtItems = await BuildItemsAsync(kind, items, existing.Id);

        existing.MaterialKind = kind;
        existing.DispositionMethod = draft.DispositionMethod?.Trim() ?? string.Empty;
        existing.TransferTarget = draft.TransferTarget?.Trim() ?? string.Empty;
        existing.OtherRemark = draft.OtherRemark?.Trim() ?? string.Empty;
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.UpdatedAt = DateTime.Now;

        if (existing.Items.Count > 0)
        {
            _repository.RemoveItems(existing.Items.ToList());
            existing.Items.Clear();
        }

        foreach (var item in builtItems)
        {
            existing.Items.Add(item);
        }

        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一个档案盒。");
        }

        await _repository.SaveChangesAsync();
        return (await _repository.GetRecordByIdAsync(existing.Id))!;
    }

    /// <inheritdoc />
    public async Task SubmitAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");

        if (existing.Status != HistoryArchiveDisposalRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可提交。");
        }

        IReadOnlyList<HistoryArchiveDisposalBoxCandidate> candidates =
            await BuildCandidatesAsync(existing.MaterialKind, existing.Id);
        Dictionary<string, HistoryArchiveDisposalBoxCandidate> selectable = candidates
            .Where(item => item.IsSelectable)
            .ToDictionary(item => item.BoxCode, StringComparer.OrdinalIgnoreCase);

        HistoryArchiveDisposalValidationSupport.EnsureValidForSubmit(
            existing.MaterialKind,
            existing.DispositionMethod,
            existing.TransferTarget,
            existing.OtherRemark,
            existing.Reason,
            existing.Items.ToList(),
            selectable);

        await ApplyLedgerLifecycleAsync(existing, HistoryArchiveDisposalDomainValues.LifecycleLocked, writeLastLocation: false);

        DateTime now = DateTime.Now;
        existing.Status = HistoryArchiveDisposalRecord.StatusSubmitted;
        existing.SubmittedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task ApproveAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");

        if (existing.Status != HistoryArchiveDisposalRecord.StatusSubmitted)
        {
            throw new InvalidOperationException("仅已提交状态可审批。");
        }

        ArchiveDisposalDefaultApprovers approvers = ArchiveDisposalDefaultApproverSupport.Resolve(_userService.GetAllUsers());
        if (string.IsNullOrWhiteSpace(approvers.ArchiveRoomHead)
            || string.IsNullOrWhiteSpace(approvers.ArchiveDeputyPresident))
        {
            throw new InvalidOperationException("未找到资料室负责人或分管资料副院长，请先在用户管理中维护对应角色后再审批通过。");
        }

        DateTime now = DateTime.Now;
        existing.ApprovedBy = ResolveUserDisplayName(currentUser);
        existing.ApprovedTime = now;
        existing.ApprovalOpinion = string.Empty;
        existing.ArchiveRoomHead = approvers.ArchiveRoomHead.Trim();
        existing.ArchiveRoomHeadDate = now.Date;
        existing.ArchiveDeputyPresident = approvers.ArchiveDeputyPresident.Trim();
        existing.ArchiveDeputyPresidentDate = now.Date;
        existing.Status = HistoryArchiveDisposalRecord.StatusApproved;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateReviewSignersAsync(
        int recordId,
        string? archiveRoomHead,
        string? archiveDeputyPresident,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");

        if (existing.Status is not (HistoryArchiveDisposalRecord.StatusApproved
            or HistoryArchiveDisposalRecord.StatusSignedUploaded))
        {
            throw new InvalidOperationException("仅已审批或已确认可上传状态可修改审核审批人。");
        }

        existing.ArchiveRoomHead = archiveRoomHead?.Trim() ?? string.Empty;
        existing.ArchiveDeputyPresident = archiveDeputyPresident?.Trim() ?? string.Empty;
        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task ConfirmReadyForUploadAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");

        if (existing.Status != HistoryArchiveDisposalRecord.StatusApproved)
        {
            throw new InvalidOperationException("仅已审批状态可确认可上传。");
        }

        DateTime now = DateTime.Now;
        existing.ConfirmedBy = ResolveUserDisplayName(currentUser);
        existing.ConfirmedTime = now;
        existing.Status = HistoryArchiveDisposalRecord.StatusSignedUploaded;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task CompleteAsync(int recordId, User currentUser, bool physicalRemovalConfirmed)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");

        if (existing.Status != HistoryArchiveDisposalRecord.StatusSignedUploaded)
        {
            throw new InvalidOperationException("请先确认可上传并上传签批单后再办结。");
        }

        var attachments = await _repository.GetAttachmentsAsync(existing.DisposalNo);
        HistoryArchiveDisposalValidationSupport.EnsureValidForComplete(
            existing.MaterialKind,
            existing.DispositionMethod,
            existing.TransferTarget,
            existing.OtherRemark,
            existing.Reason,
            existing.Items.ToList(),
            existing.ArchiveRoomHead,
            existing.ArchiveRoomHeadDate,
            existing.ArchiveDeputyPresident,
            existing.ArchiveDeputyPresidentDate,
            attachments,
            physicalRemovalConfirmed);

        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);
        HashSet<string> disposedBoxCodes = existing.Items
            .Select(item => item.BoxCode?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await ApplyLedgerLifecycleAsync(
            existing,
            HistoryArchiveDisposalDomainValues.LifecycleDisposed,
            writeLastLocation: true,
            disposedBoxCodes);

        foreach (string boxCode in disposedBoxCodes)
        {
            _repository.RemoveArchiveBoxPlacementByBoxCode(boxCode);
        }

        existing.PhysicalRemovalConfirmed = true;
        existing.PhysicalRemovalConfirmedAt = now;
        existing.PhysicalRemovalConfirmedBy = operatorName;
        existing.SignedAttachmentUploaded = attachments.Any(item =>
            string.Equals(
                item.FileCategory,
                HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal));
        existing.ScenePhotoUploaded = attachments.Any(item =>
            HistoryArchiveDisposalDomainValues.IsScenePhotoCategory(item.FileCategory));
        existing.Status = HistoryArchiveDisposalRecord.StatusCompleted;
        existing.CompletedAt = now;
        existing.CompletedBy = operatorName;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task WithdrawAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");

        if (existing.Status is not (HistoryArchiveDisposalRecord.StatusDraft
            or HistoryArchiveDisposalRecord.StatusSubmitted))
        {
            throw new InvalidOperationException("仅草稿或已提交状态可撤回。");
        }

        if (existing.Status == HistoryArchiveDisposalRecord.StatusSubmitted)
        {
            await ApplyLedgerLifecycleAsync(existing, HistoryArchiveDisposalDomainValues.LifecycleInStock, writeLastLocation: false);
        }

        DateTime now = DateTime.Now;
        existing.Status = HistoryArchiveDisposalRecord.StatusWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = reason?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task RecordPrintAsync(int recordId)
    {
        var existing = await _repository.GetRecordByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");
        if (existing.Status is HistoryArchiveDisposalRecord.StatusDraft
            or HistoryArchiveDisposalRecord.StatusWithdrawn
            or HistoryArchiveDisposalRecord.StatusForceWithdrawn)
        {
            throw new InvalidOperationException("当前状态不可打印签批单。");
        }

        existing.PrintCount += 1;
        existing.LastPrintedAt = DateTime.Now;
        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<HistoryArchiveDisposalPrintData> BuildPrintDataAsync(int recordId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");
        if (record.Status is HistoryArchiveDisposalRecord.StatusDraft
            or HistoryArchiveDisposalRecord.StatusWithdrawn
            or HistoryArchiveDisposalRecord.StatusForceWithdrawn)
        {
            throw new InvalidOperationException("请先提交后再打印签批单。");
        }

        bool completed = record.Status == HistoryArchiveDisposalRecord.StatusCompleted;
        return new HistoryArchiveDisposalPrintData
        {
            DisposalNo = record.DisposalNo,
            ApplyDateText = record.ApplyTime.ToString("yyyy-MM-dd"),
            MaterialKindDisplay = HistoryArchiveDisposalDomainValues.ToMaterialKindDisplay(record.MaterialKind),
            DispositionMethod = record.DispositionMethod,
            TransferTarget = record.TransferTarget,
            OtherRemark = record.OtherRemark,
            Reason = record.Reason,
            Remark = record.Remark,
            ApplicantName = record.ApplicantName,
            ApplicantDept = record.ApplicantDept,
            ArchiveRoomHead = record.ArchiveRoomHead,
            ArchiveRoomHeadDateText = FormatDate(record.ArchiveRoomHeadDate),
            ArchiveDeputyPresident = record.ArchiveDeputyPresident,
            ArchiveDeputyPresidentDateText = FormatDate(record.ArchiveDeputyPresidentDate),
            CompletedBy = record.CompletedBy,
            CompletedDateText = FormatDate(record.CompletedAt),
            IsCompleted = completed,
            PrintCount = record.PrintCount,
            Items = record.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new HistoryArchiveDisposalPrintItemData
                {
                    SortOrder = item.SortOrder,
                    BoxCode = item.BoxCode,
                    BoxSpecification = item.BoxSpecification,
                    StorageLocation = item.BeforeStorageLocation,
                    ContentSummary = item.ContentSummary,
                    MixedPlacementText = item.IsMixedPlacement ? "混放" : string.Empty,
                    DispositionMethod = record.DispositionMethod
                })
                .ToList()
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string disposalNo) =>
        await _repository.GetAttachmentsAsync(disposalNo);

    /// <inheritdoc />
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
        var existing = await _repository.GetRecordByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到历史存档离库处置单。");

        if (existing.Status is not (HistoryArchiveDisposalRecord.StatusApproved
            or HistoryArchiveDisposalRecord.StatusSignedUploaded))
        {
            return (false, "仅已审批或已确认可上传状态可上传附件。", null);
        }

        if (string.IsNullOrWhiteSpace(fileCategory))
        {
            return (false, "请选择附件分类。", null);
        }

        if (fileContent == null || fileContent.Length == 0)
        {
            return (false, "附件内容为空。", null);
        }

        var attachment = new SystemAttachment
        {
            BusinessType = HistoryArchiveDisposalDomainValues.AttachmentBusinessType,
            BusinessNo = existing.DisposalNo,
            BusinessId = existing.Id,
            FileName = fileName?.Trim() ?? string.Empty,
            Extension = extension?.Trim() ?? string.Empty,
            FileSize = fileSize,
            FileContent = fileContent,
            FileCategory = fileCategory.Trim(),
            UploadTime = DateTime.Now,
            UploaderName = ResolveUserDisplayName(currentUser)
        };
        _repository.AddAttachment(attachment);

        if (string.Equals(
                attachment.FileCategory,
                HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal))
        {
            existing.SignedAttachmentUploaded = true;
            existing.SignedAttachmentUploadedTime = attachment.UploadTime;
            existing.SignedAttachmentUploader = attachment.UploaderName;
        }

        if (HistoryArchiveDisposalDomainValues.IsScenePhotoCategory(attachment.FileCategory))
        {
            existing.ScenePhotoUploaded = true;
        }

        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
        return (true, "附件已上传。", attachment);
    }

    /// <inheritdoc />
    public async Task<(bool Ok, string Message)> DeleteAttachmentAsync(int attachmentId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var attachment = await _repository.GetAttachmentByIdAsync(attachmentId);
        if (attachment == null)
        {
            return (false, "未找到附件。");
        }

        _repository.RemoveAttachment(attachment);
        await _repository.SaveChangesAsync();
        return (true, "附件已删除。");
    }

    private async Task<List<HistoryArchiveDisposalItem>> BuildItemsAsync(
        string materialKind,
        IReadOnlyList<HistoryArchiveDisposalItem> requested,
        int currentRecordId)
    {
        IReadOnlyList<HistoryArchiveDisposalBoxCandidate> candidates =
            await BuildCandidatesAsync(materialKind, currentRecordId);
        Dictionary<string, HistoryArchiveDisposalBoxCandidate> byCode = candidates
            .ToDictionary(item => item.BoxCode, StringComparer.OrdinalIgnoreCase);

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (HistoryArchiveDisposalItem row in requested ?? Array.Empty<HistoryArchiveDisposalItem>())
        {
            string code = row.BoxCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code) || !byCode.TryGetValue(code, out var candidate))
            {
                continue;
            }

            foreach (string related in HistoryArchiveBoxCodeSupport.ResolveRelatedGroup(
                         candidates.ToDictionary(
                             item => item.BoxCode,
                             item => item.RelatedBoxCodes,
                             StringComparer.OrdinalIgnoreCase),
                         code))
            {
                selected.Add(related);
            }

            foreach (string related in candidate.RelatedBoxCodes)
            {
                selected.Add(related);
            }

            selected.Add(code);
        }

        DateTime now = DateTime.Now;
        int sort = 1;
        var items = new List<HistoryArchiveDisposalItem>();
        foreach (string boxCode in selected.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            if (!byCode.TryGetValue(boxCode, out HistoryArchiveDisposalBoxCandidate? candidate)
                || !candidate.IsSelectable)
            {
                continue;
            }

            items.Add(new HistoryArchiveDisposalItem
            {
                SortOrder = sort++,
                BoxCode = candidate.BoxCode,
                BoxSpecification = candidate.BoxSpecification,
                CabinetName = candidate.CabinetName,
                FaceCode = candidate.FaceCode,
                SlotCode = candidate.SlotCode,
                BeforeStorageLocation = candidate.StorageLocation,
                ContentSummary = candidate.ContentSummary,
                LedgerRecordCount = candidate.LedgerRecordCount,
                SourceRecordKeys = candidate.SourceRecordKeys,
                IsMixedPlacement = candidate.IsMixedPlacement,
                RelatedBoxCodes = candidate.RelatedBoxCodesText,
                CreatedAt = now
            });
        }

        return items;
    }

    private async Task<IReadOnlyList<HistoryArchiveDisposalBoxCandidate>> BuildCandidatesAsync(
        string materialKind,
        int? currentRecordId)
    {
        string kind = HistoryArchiveDisposalDomainValues.NormalizeMaterialKind(materialKind);
        var placements = await _repository.GetHistoryPlacementsAsync();
        HashSet<string> lockedByOther = await _repository.GetLockedBoxCodesAsync(currentRecordId);

        var ledgerRows = new List<LedgerRow>();
        if (string.Equals(kind, HistoryArchiveDisposalDomainValues.MaterialKindTopoMap, StringComparison.Ordinal))
        {
            foreach (var map in await _repository.GetTopoMapsAsync())
            {
                if (HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(map.LifecycleStatus))
                {
                    continue;
                }

                ledgerRows.Add(new LedgerRow(
                    map.Id,
                    map.BoxNumber,
                    map.BoxSpecification,
                    HistoryArchiveDisposalDomainValues.BuildSourceRecordKey(kind, map.Id),
                    HistoryArchiveDisposalContentSummarySupport.BuildTopoMapSummary([map])));
            }
        }
        else if (string.Equals(kind, HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto, StringComparison.Ordinal))
        {
            foreach (var photo in await _repository.GetAerialPhotosAsync())
            {
                if (HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(photo.LifecycleStatus))
                {
                    continue;
                }

                ledgerRows.Add(new LedgerRow(
                    photo.Id,
                    photo.BoxNumber,
                    photo.BoxSpecification,
                    HistoryArchiveDisposalDomainValues.BuildSourceRecordKey(kind, photo.Id),
                    HistoryArchiveDisposalContentSummarySupport.BuildAerialPhotoSummary([photo])));
            }
        }
        else
        {
            foreach (var map in await _repository.GetOtherMapsAsync())
            {
                if (HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(map.LifecycleStatus))
                {
                    continue;
                }

                ledgerRows.Add(new LedgerRow(
                    map.Id,
                    map.BoxNumber,
                    map.BoxSpecification,
                    HistoryArchiveDisposalDomainValues.BuildSourceRecordKey(kind, map.Id),
                    HistoryArchiveDisposalContentSummarySupport.BuildOtherMapSummary([map])));
            }
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> groups =
            HistoryArchiveBoxCodeSupport.BuildRelatedGroups(ledgerRows.Select(item => item.BoxNumber));

        var rowsByBox = new Dictionary<string, List<LedgerRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ledgerRows)
        {
            foreach (string code in HistoryArchiveBoxCodeSupport.SplitBoxCodes(row.BoxNumber))
            {
                if (!HistoryArchiveBoxCodeSupport.TryParseBoxCode(code, out _, out _, out _, out string normalized))
                {
                    continue;
                }

                if (!rowsByBox.TryGetValue(normalized, out List<LedgerRow>? list))
                {
                    list = new List<LedgerRow>();
                    rowsByBox[normalized] = list;
                }

                list.Add(row);
            }
        }

        HashSet<string> crossTypeBoxes = placements
            .Where(item => string.Equals(
                item.SourceType?.Trim(),
                HistoryArchiveDisposalDomainValues.PlacementSourceMixed,
                StringComparison.OrdinalIgnoreCase))
            .Select(item => item.BoxCode?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string expectedSource = kind switch
        {
            HistoryArchiveDisposalDomainValues.MaterialKindTopoMap =>
                HistoryArchiveDisposalDomainValues.PlacementSourceTopoMap,
            HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto =>
                HistoryArchiveDisposalDomainValues.PlacementSourceAerialPhoto,
            _ => HistoryArchiveDisposalDomainValues.PlacementSourceOtherMap
        };

        Dictionary<string, CabinetArchiveBoxPlacement> placementByCode = placements
            .Where(item =>
                string.Equals(item.SourceType?.Trim(), expectedSource, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.SourceType?.Trim(), HistoryArchiveDisposalDomainValues.PlacementSourceMixed, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.BoxCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<HistoryArchiveDisposalBoxCandidate>();
        foreach (var pair in rowsByBox)
        {
            string boxCode = pair.Key;
            if (!placementByCode.TryGetValue(boxCode, out CabinetArchiveBoxPlacement? placement))
            {
                continue;
            }

            IReadOnlyList<string> related = HistoryArchiveBoxCodeSupport.ResolveRelatedGroup(groups, boxCode);
            bool mixed = related.Count > 1;
            bool crossType = related.Any(code => crossTypeBoxes.Contains(code));
            bool locked = related.Any(code => lockedByOther.Contains(code));

            string summary = BuildGroupSummary(kind, pair.Value);

            result.Add(new HistoryArchiveDisposalBoxCandidate
            {
                BoxCode = boxCode,
                BoxSpecification = FirstNonEmpty(
                    placement.BoxSpecification,
                    pair.Value.Select(item => item.BoxSpecification)),
                CabinetName = placement.CabinetName,
                FaceCode = placement.FaceCode,
                SlotCode = placement.SlotCode,
                StorageLocation = boxCode,
                ContentSummary = summary,
                LedgerRecordCount = pair.Value.Select(item => item.Id).Distinct().Count(),
                SourceRecordKeys = string.Join("|", pair.Value.Select(item => item.SourceKey).Distinct(StringComparer.Ordinal)),
                IsMixedPlacement = mixed,
                RelatedBoxCodes = related,
                IsCrossTypeMixed = crossType,
                IsLockedByOther = locked
            });
        }

        return result
            .OrderBy(item => item.CabinetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.BoxCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildGroupSummary(string kind, IReadOnlyList<LedgerRow> rows)
    {
        if (string.Equals(kind, HistoryArchiveDisposalDomainValues.MaterialKindTopoMap, StringComparison.Ordinal))
        {
            return rows.Count == 0
                ? "（无台账）"
                : JoinSummaryHints(rows);
        }

        return JoinSummaryHints(rows);
    }

    private static string JoinSummaryHints(IReadOnlyList<LedgerRow> rows)
    {
        var hints = rows
            .Select(item => item.SummaryHint?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (hints.Count == 0)
        {
            return rows.Count > 0 ? $"{rows.Count}条" : "（无台账）";
        }

        if (hints.Count == 1)
        {
            return hints[0];
        }

        return $"{hints[0]}等{rows.Count}条";
    }

    private async Task ApplyLedgerLifecycleAsync(
        HistoryArchiveDisposalRecord record,
        string targetStatus,
        bool writeLastLocation,
        HashSet<string>? disposedBoxCodes = null)
    {
        string kind = HistoryArchiveDisposalDomainValues.NormalizeMaterialKind(record.MaterialKind);
        List<(string Kind, int Id)> keys = ParseSourceKeys(record.Items.Select(item => item.SourceRecordKeys));
        IReadOnlyList<int> ids = keys.Select(item => item.Id).Distinct().ToList();
        HashSet<string> boxCodes = disposedBoxCodes
            ?? record.Items
                .Select(item => item.BoxCode?.Trim() ?? string.Empty)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (string.Equals(kind, HistoryArchiveDisposalDomainValues.MaterialKindTopoMap, StringComparison.Ordinal))
        {
            foreach (var map in await _repository.GetTopoMapsByIdsAsync(ids, tracking: true))
            {
                ApplyLedgerRow(map, targetStatus, writeLastLocation, boxCodes, map.BoxNumber, value => map.BoxNumber = value);
            }
        }
        else if (string.Equals(kind, HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto, StringComparison.Ordinal))
        {
            foreach (var photo in await _repository.GetAerialPhotosByIdsAsync(ids, tracking: true))
            {
                ApplyLedgerRow(photo, targetStatus, writeLastLocation, boxCodes, photo.BoxNumber, value => photo.BoxNumber = value);
            }
        }
        else
        {
            foreach (var map in await _repository.GetOtherMapsByIdsAsync(ids, tracking: true))
            {
                ApplyLedgerRow(map, targetStatus, writeLastLocation, boxCodes, map.BoxNumber, value => map.BoxNumber = value);
            }
        }
    }

    private static void ApplyLedgerRow(
        object entity,
        string targetStatus,
        bool writeLastLocation,
        HashSet<string> disposedBoxCodes,
        string boxNumber,
        Action<string> setBoxNumber)
    {
        IReadOnlyList<string> codes = HistoryArchiveBoxCodeSupport.SplitBoxCodes(boxNumber);
        bool mixed = codes.Count > 1;
        if (entity is TopoMap topo)
        {
            ApplyStatus(topo, targetStatus, writeLastLocation, disposedBoxCodes, mixed, codes, setBoxNumber);
        }
        else if (entity is AerialPhoto aerial)
        {
            ApplyStatus(aerial, targetStatus, writeLastLocation, disposedBoxCodes, mixed, codes, setBoxNumber);
        }
        else if (entity is OtherMap other)
        {
            ApplyStatus(other, targetStatus, writeLastLocation, disposedBoxCodes, mixed, codes, setBoxNumber);
        }
    }

    private static void ApplyStatus(
        TopoMap map,
        string targetStatus,
        bool writeLastLocation,
        HashSet<string> disposedBoxCodes,
        bool mixed,
        IReadOnlyList<string> codes,
        Action<string> setBoxNumber)
    {
        ApplyCommon(targetStatus, writeLastLocation, disposedBoxCodes, mixed, codes, setBoxNumber,
            () => map.LifecycleStatus, value => map.LifecycleStatus = value,
            () => map.LastStorageLocation, value => map.LastStorageLocation = value,
            map.BoxNumber);
    }

    private static void ApplyStatus(
        AerialPhoto photo,
        string targetStatus,
        bool writeLastLocation,
        HashSet<string> disposedBoxCodes,
        bool mixed,
        IReadOnlyList<string> codes,
        Action<string> setBoxNumber)
    {
        ApplyCommon(targetStatus, writeLastLocation, disposedBoxCodes, mixed, codes, setBoxNumber,
            () => photo.LifecycleStatus, value => photo.LifecycleStatus = value,
            () => photo.LastStorageLocation, value => photo.LastStorageLocation = value,
            photo.BoxNumber);
    }

    private static void ApplyStatus(
        OtherMap map,
        string targetStatus,
        bool writeLastLocation,
        HashSet<string> disposedBoxCodes,
        bool mixed,
        IReadOnlyList<string> codes,
        Action<string> setBoxNumber)
    {
        ApplyCommon(targetStatus, writeLastLocation, disposedBoxCodes, mixed, codes, setBoxNumber,
            () => map.LifecycleStatus, value => map.LifecycleStatus = value,
            () => map.LastStorageLocation, value => map.LastStorageLocation = value,
            map.BoxNumber);
    }

    private static void ApplyCommon(
        string targetStatus,
        bool writeLastLocation,
        HashSet<string> disposedBoxCodes,
        bool mixed,
        IReadOnlyList<string> codes,
        Action<string> setBoxNumber,
        Func<string> getLifecycle,
        Action<string> setLifecycle,
        Func<string> getLastLocation,
        Action<string> setLastLocation,
        string originalBoxNumber)
    {
        _ = getLifecycle;
        _ = getLastLocation;

        if (string.Equals(targetStatus, HistoryArchiveDisposalDomainValues.LifecycleDisposed, StringComparison.Ordinal)
            && writeLastLocation
            && !mixed)
        {
            IReadOnlyList<string> remaining = codes
                .Where(code => !disposedBoxCodes.Contains(code))
                .ToList();
            if (remaining.Count > 0)
            {
                setLastLocation(originalBoxNumber);
                setBoxNumber(string.Join("；", remaining));
                return;
            }
        }

        if (writeLastLocation)
        {
            setLastLocation(originalBoxNumber);
        }

        setLifecycle(targetStatus);
    }

    private static List<(string Kind, int Id)> ParseSourceKeys(IEnumerable<string?> blobs)
    {
        var result = new List<(string Kind, int Id)>();
        foreach (string? blob in blobs)
        {
            if (string.IsNullOrWhiteSpace(blob))
            {
                continue;
            }

            foreach (string token in blob.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = token.LastIndexOf(':');
                if (separator <= 0 || separator >= token.Length - 1)
                {
                    continue;
                }

                string kind = token[..separator];
                if (int.TryParse(token[(separator + 1)..], out int id) && id > 0)
                {
                    result.Add((kind, id));
                }
            }
        }

        return result;
    }

    private static string FirstNonEmpty(string? preferred, IEnumerable<string?> fallbacks)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred.Trim();
        }

        return fallbacks
            .Select(item => item?.Trim() ?? string.Empty)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
            ?? string.Empty;
    }

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd") : string.Empty;

    private static void EnsureArchiveAdmin(User currentUser)
    {
        if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
        {
            throw new InvalidOperationException("仅资料室资料管理员可办理历史存档离库处置。");
        }
    }

    private static string ResolveUserDisplayName(User currentUser)
    {
        if (!string.IsNullOrWhiteSpace(currentUser.RealName))
        {
            return currentUser.RealName.Trim();
        }

        return currentUser.LoginName?.Trim() ?? string.Empty;
    }

    private sealed record LedgerRow(
        int Id,
        string BoxNumber,
        string BoxSpecification,
        string SourceKey,
        string SummaryHint);
}
