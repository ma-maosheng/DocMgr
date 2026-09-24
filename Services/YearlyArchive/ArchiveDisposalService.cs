using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.YearlyArchive;

/// <summary>
/// 年度资料离库处置业务服务（工作流主文件）。
/// </summary>
public sealed partial class ArchiveDisposalService : IArchiveDisposalService
{
    private readonly IArchiveDisposalRepository _repository;
    private readonly IBusinessRuleService _businessRuleService;
    private readonly IHardDiskMediaService _hardDiskMediaService;
    private readonly IUserService _userService;
    private readonly IApprovalWorkflowService _approvalWorkflowService;
    private readonly IBusinessLogicSettingsService _businessLogicSettingsService;

    public ArchiveDisposalService(
        IArchiveDisposalRepository repository,
        IBusinessRuleService businessRuleService,
        IHardDiskMediaService hardDiskMediaService,
        IUserService userService,
        IApprovalWorkflowService approvalWorkflowService,
        IBusinessLogicSettingsService businessLogicSettingsService)
    {
        _repository = repository;
        _businessRuleService = businessRuleService;
        _hardDiskMediaService = hardDiskMediaService;
        _userService = userService;
        _approvalWorkflowService = approvalWorkflowService;
        _businessLogicSettingsService = businessLogicSettingsService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<YearlyArchiveDisposalRecord>> SearchRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear,
        string mediaKind)
    {
        EnsureValidMediaKind(mediaKind);
        return await _repository.SearchRecordsAsync(keyword, status, applyYear, mediaKind.Trim());
    }

    /// <inheritdoc />
    public Task<YearlyArchiveDisposalRecord?> GetRecordByIdAsync(int recordId)
    {
        return _repository.GetRecordByIdAsync(recordId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ArchiveDisposalSelectableItem>> GetSelectableItemsAsync(
        string mediaKind,
        int? currentRecordId = null)
    {
        EnsureValidMediaKind(mediaKind);
        if (string.Equals(mediaKind.Trim(), ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
        {
            return await _repository.GetSelectableSimulatedItemsAsync(currentRecordId);
        }

        return await _repository.GetSelectableElectronicItemsAsync(currentRecordId);
    }

    /// <inheritdoc />
    public Task<string> GenerateNextDisposalNoAsync()
    {
        return _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.ArchiveDisposalApply);
    }

    /// <inheritdoc />
    public async Task<YearlyArchiveDisposalRecord> CreateDraftAsync(
        YearlyArchiveDisposalRecord draft,
        IReadOnlyList<YearlyArchiveDisposalItem> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        EnsureValidMediaKind(draft.MediaKind);
        ValidateHeader(draft.Reason, items);

        DateTime now = DateTime.Now;
        string disposalNo = string.IsNullOrWhiteSpace(draft.DisposalNo)
            ? await _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.ArchiveDisposalApply)
            : draft.DisposalNo.Trim();

        var builtItems = await BuildItemsAsync(draft.MediaKind, items, now, excludeRecordId: null);
        ValidateItemMethods(draft.MediaKind, builtItems);

        var record = new YearlyArchiveDisposalRecord
        {
            DisposalNo = disposalNo,
            MediaKind = draft.MediaKind.Trim(),
            Status = YearlyArchiveDisposalRecord.StatusDraft,
            DisposalReason = ArchiveDisposalDomainValues.BuildReasonSummary(builtItems.Select(i => i.DisposalReason)),
            DispositionMethod = ArchiveDisposalDomainValues.BuildDispositionMethodSummary(
                builtItems.Select(i => i.DispositionMethod)),
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

        _repository.AddRecord(record);
        await _repository.SaveChangesAsync();
        return (await _repository.GetRecordByIdAsync(record.Id))!;
    }

    /// <inheritdoc />
    public async Task<YearlyArchiveDisposalRecord> UpdateDraftAsync(
        YearlyArchiveDisposalRecord draft,
        IReadOnlyList<YearlyArchiveDisposalItem> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id <= 0)
        {
            throw new InvalidOperationException("处置单无效，无法保存。");
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(draft.Id)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        if (existing.Status != YearlyArchiveDisposalRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态的处置单可编辑。");
        }

        EnsureValidMediaKind(existing.MediaKind);
        ValidateHeader(draft.Reason, items);

        DateTime now = DateTime.Now;
        var builtItems = await BuildItemsAsync(existing.MediaKind, items, now, excludeRecordId: existing.Id);
        ValidateItemMethods(existing.MediaKind, builtItems);

        existing.DisposalReason = ArchiveDisposalDomainValues.BuildReasonSummary(builtItems.Select(i => i.DisposalReason));
        existing.DispositionMethod = ArchiveDisposalDomainValues.BuildDispositionMethodSummary(
            builtItems.Select(i => i.DispositionMethod));
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;

        if (existing.Items.Count > 0)
        {
            _repository.RemoveItems(existing.Items.ToList());
            existing.Items.Clear();
        }

        foreach (var item in builtItems)
        {
            existing.Items.Add(item);
        }

        await _repository.SaveChangesAsync();
        return (await _repository.GetRecordByIdAsync(existing.Id))!;
    }

    /// <inheritdoc />
    public async Task SubmitAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        if (existing.Status != YearlyArchiveDisposalRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可提交。");
        }

        ValidateHeader(existing.Reason, existing.Items);
        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一条待处置明细。");
        }

        ValidateItemMethods(existing.MediaKind, existing.Items);
        await EnsureItemsStillSelectableAsync(existing);

        DateTime now = DateTime.Now;
        await LockHardDiskMediaAsync(existing, now);

        existing.Status = YearlyArchiveDisposalRecord.StatusSubmitted;
        existing.SubmittedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task ApproveAsync(int recordId, string approvalOpinion, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        var gate = OfflineApprovalLifecycleSupport.TryTransition(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.ApprovePass);
        if (!gate.Allowed)
        {
            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不允许审批。");
        }

        DateTime now = DateTime.Now;
        var users = _userService.GetAllUsers();
        var chain = await _approvalWorkflowService.ResolveAsync(
            new ApprovalChainResolveRequest
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal,
                FieldValues = ApprovalChainApplySupport.BuildYearlyDisposalFieldValues(existing)
            },
            users);
        ApprovalChainApplySupport.ApplyToYearlyDisposal(existing, chain, now);

        var missing = ApprovalChainApplySupport.CollectMissingSignerErrors(
            chain,
            nodeKey => ApprovalChainApplySupport.ReadYearlyDisposalSigner(existing, nodeKey));
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, missing)
                + Environment.NewLine
                + "请在「审核审批」中配置或在用户管理中维护对应角色后再审批通过。");
        }

        existing.Status = gate.NextStatus ?? YearlyArchiveDisposalRecord.StatusApproved;
        existing.ApprovedBy = ResolveUserDisplayName(currentUser);
        existing.ApprovedTime = now;
        existing.ApprovalOpinion = string.IsNullOrWhiteSpace(approvalOpinion) ? "同意" : approvalOpinion.Trim();
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateReviewSignersAsync(
        int recordId,
        string? deptHead,
        DateTime? deptHeadDate,
        string? archiveRoomHead,
        DateTime? archiveRoomHeadDate,
        string? productionHead,
        DateTime? productionHeadDate,
        string? archiveDeputyPresident,
        DateTime? archiveDeputyPresidentDate,
        string? productionVicePresident,
        DateTime? productionVicePresidentDate,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        if (existing.Status is not (YearlyArchiveDisposalRecord.StatusApproved
            or YearlyArchiveDisposalRecord.StatusSignedUploaded))
        {
            throw new InvalidOperationException("仅已审批或已确认可上传状态可修改审核审批人。");
        }

        var minDate = ApprovalSignatureDateSupport.ResolveMinDate(existing.FirstPrintedAt, existing.LastPrintedAt, existing.PrintCount);
        ThrowIfSignatureDateInvalid(deptHeadDate, minDate, "部门审核日期");
        ThrowIfSignatureDateInvalid(archiveRoomHeadDate, minDate, "资料室签字日期");
        ThrowIfSignatureDateInvalid(productionHeadDate, minDate, "生产科签字日期");
        ThrowIfSignatureDateInvalid(archiveDeputyPresidentDate, minDate, "分管资料院长签字日期");
        ThrowIfSignatureDateInvalid(productionVicePresidentDate, minDate, "分管生产院长签字日期");

        existing.DeptHead = deptHead?.Trim() ?? string.Empty;
        existing.DeptHeadDate = ApprovalSignatureDateSupport.Clamp(deptHeadDate, minDate);
        existing.ArchiveRoomHead = archiveRoomHead?.Trim() ?? string.Empty;
        existing.ArchiveRoomHeadDate = ApprovalSignatureDateSupport.Clamp(archiveRoomHeadDate, minDate);
        existing.ProductionHead = productionHead?.Trim() ?? string.Empty;
        existing.ProductionHeadDate = ApprovalSignatureDateSupport.Clamp(productionHeadDate, minDate);
        existing.ArchiveDeputyPresident = archiveDeputyPresident?.Trim() ?? string.Empty;
        existing.ArchiveDeputyPresidentDate = ApprovalSignatureDateSupport.Clamp(archiveDeputyPresidentDate, minDate);
        existing.ProductionVicePresident = productionVicePresident?.Trim() ?? string.Empty;
        existing.ProductionVicePresidentDate = ApprovalSignatureDateSupport.Clamp(productionVicePresidentDate, minDate);
        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    private static void ThrowIfSignatureDateInvalid(DateTime? value, DateTime? minDate, string fieldLabel)
    {
        var error = ApprovalSignatureDateSupport.ValidateNotBeforePrint(value, minDate, fieldLabel);
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <inheritdoc />
    public async Task ConfirmReadyForUploadAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        var gate = OfflineApprovalLifecycleSupport.TryTransition(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.ConfirmMidStep);
        if (!gate.Allowed)
        {
            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不允许确认可上传。");
        }

        DateTime now = DateTime.Now;
        existing.Status = gate.NextStatus ?? YearlyArchiveDisposalRecord.StatusSignedUploaded;
        existing.ConfirmedBy = ResolveUserDisplayName(currentUser);
        existing.ConfirmedTime = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task WithdrawAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        var gate = OfflineApprovalLifecycleSupport.TryTransition(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.Withdraw);
        if (!gate.Allowed)
        {
            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不可撤回作废。");
        }

        await UnlockHardDiskMediaIfOwnedAsync(existing);

        DateTime now = DateTime.Now;
        existing.Status = gate.NextStatus ?? YearlyArchiveDisposalRecord.StatusWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = reason?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task ForceVoidAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
        bool isOverdue = _businessLogicSettingsService.IsEligibleForAdminForceVoid(existing.ApplyTime, settingCode);
        var gate = OfflineApprovalLifecycleSupport.TryTransition(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin,
                forceVoidEligible: isOverdue),
            OfflineApprovalLifecycleSupport.Action.ForceVoid);
        if (!gate.Allowed)
        {
            if (!isOverdue
                && existing.Status is YearlyArchiveDisposalRecord.StatusDraft
                    or YearlyArchiveDisposalRecord.StatusSubmitted)
            {
                throw new InvalidOperationException(_businessLogicSettingsService.BuildNotEligibleMessage(settingCode));
            }

            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不可强制作废。");
        }

        await UnlockHardDiskMediaIfOwnedAsync(existing);

        DateTime now = DateTime.Now;
        existing.Status = gate.NextStatus ?? YearlyArchiveDisposalRecord.StatusForceWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = string.IsNullOrWhiteSpace(reason) ? "资料管理员强制作废" : reason.Trim();
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task RecordPrintAsync(int recordId)
    {
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        if (existing.Status is YearlyArchiveDisposalRecord.StatusDraft
            or YearlyArchiveDisposalRecord.StatusWithdrawn
            or YearlyArchiveDisposalRecord.StatusForceWithdrawn)
        {
            throw new InvalidOperationException("当前状态不可打印签批单。");
        }

        DateTime now = DateTime.Now;
        int printCount = existing.PrintCount;
        DateTime? firstPrintedAt = existing.FirstPrintedAt;
        DateTime? lastPrintedAt = existing.LastPrintedAt;
        ApprovalSignatureDateSupport.RecordPrint(ref printCount, ref firstPrintedAt, ref lastPrintedAt, now);
        existing.PrintCount = printCount;
        existing.FirstPrintedAt = firstPrintedAt;
        existing.LastPrintedAt = lastPrintedAt;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<YearlyArchiveDisposalPrintData> BuildPrintDataAsync(int recordId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        var orderedItems = record.Items.OrderBy(item => item.SortOrder).ToList();
        Dictionary<int, YearlyArchiveFilingFact> factsById = new();
        Dictionary<int, YearlyArchiveBox> boxesById = new();
        Dictionary<int, YearlyElectronicArchiveUnit> unitsById = new();

        if (record.IsSimulated)
        {
            var factIds = orderedItems.Select(item => item.FilingFactId).Where(id => id > 0).Distinct().ToList();
            factsById = (await _repository.GetFilingFactsByIdsAsync(factIds))
                .ToDictionary(item => item.Id);
            var boxIds = orderedItems.Select(item => item.ContainerId).Where(id => id > 0).Distinct().ToList();
            boxesById = (await _repository.GetBoxesByIdsAsync(boxIds))
                .ToDictionary(item => item.Id);
        }
        else
        {
            var unitIds = orderedItems.Select(item => item.ElectronicArchiveUnitId).Where(id => id > 0).Distinct().ToList();
            unitsById = (await _repository.GetElectronicUnitsByIdsAsync(unitIds))
                .ToDictionary(item => item.Id);
        }

        var users = _userService.GetAllUsers();
        var chain = await _approvalWorkflowService.ResolveAsync(
            new ApprovalChainResolveRequest
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal,
                FieldValues = ApprovalChainApplySupport.BuildYearlyDisposalFieldValues(record)
            },
            users);
        var defaultApprovers = chain.MatchedRuleId == null
            ? ArchiveDisposalDefaultApproverSupport.Resolve(users)
            : ArchiveDisposalDefaultApproverSupport.FromChain(chain);

        return new YearlyArchiveDisposalPrintData
        {
            DisposalNo = record.DisposalNo,
            MediaKind = record.MediaKind,
            StatusDisplay = record.StatusDisplay,
            ApplyDateText = record.ApplyTime == default ? string.Empty : record.ApplyTime.ToString("yyyy-MM-dd"),
            DisposalReason = ArchiveDisposalDomainValues.BuildReasonSummary(
                orderedItems.Select(item => item.DisposalReason)),
            DispositionMethod = ArchiveDisposalDomainValues.BuildDispositionMethodSummary(
                orderedItems.Select(item => item.DispositionMethod)),
            Reason = record.Reason,
            Remark = record.Remark,
            ApplicantName = record.ApplicantName,
            ApplicantDept = record.ApplicantDept,
            ApplyTime = record.ApplyTime,
            ApprovedBy = record.ApprovedBy,
            ApprovedTime = record.ApprovedTime,
            ApprovedDateText = record.ApprovedTime?.ToString("yyyy-MM-dd") ?? string.Empty,
            ApprovalOpinion = record.ApprovalOpinion,
            CompletedBy = record.CompletedBy,
            CompletedDateText = record.CompletedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
            IsCompleted = record.IsCompleted,
            DeptHead = PreferNonEmpty(record.DeptHead, defaultApprovers.DeptHead),
            ArchiveRoomHead = PreferNonEmpty(record.ArchiveRoomHead, defaultApprovers.ArchiveRoomHead),
            ProductionHead = PreferNonEmpty(record.ProductionHead, defaultApprovers.ProductionHead),
            ArchiveDeputyPresident = PreferNonEmpty(record.ArchiveDeputyPresident, defaultApprovers.ArchiveDeputyPresident),
            ProductionVicePresident = PreferNonEmpty(record.ProductionVicePresident, defaultApprovers.ProductionVicePresident),
            EnableDeptHead = defaultApprovers.EnableDeptHead,
            EnableArchiveRoomHead = defaultApprovers.EnableArchiveRoomHead,
            EnableProductionHead = defaultApprovers.EnableProductionHead,
            EnableArchiveDeputyPresident = defaultApprovers.EnableArchiveDeputyPresident,
            EnableProductionVicePresident = defaultApprovers.EnableProductionVicePresident,
            PrintCount = record.PrintCount,
            Items = orderedItems
                .Select(item => BuildPrintItemRow(record, item, factsById, boxesById, unitsById))
                .ToList()
        };
    }

    private static YearlyArchiveDisposalPrintItemRow BuildPrintItemRow(
        YearlyArchiveDisposalRecord record,
        YearlyArchiveDisposalItem item,
        IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
        IReadOnlyDictionary<int, YearlyArchiveBox> boxesById,
        IReadOnlyDictionary<int, YearlyElectronicArchiveUnit> unitsById)
    {
        string projectYear = string.Empty;
        string projectName = string.Empty;
        string boxCode = string.Empty;
        string bagCode = string.Empty;
        string materialDetail;

        if (record.IsSimulated)
        {
            boxCode = item.ContainerCode?.Trim() ?? string.Empty;
            if (factsById.TryGetValue(item.FilingFactId, out var fact))
            {
                projectName = fact.ProjectName?.Trim() ?? string.Empty;
            }

            if (boxesById.TryGetValue(item.ContainerId, out var box))
            {
                if (string.IsNullOrWhiteSpace(projectName))
                {
                    projectName = box.ProjectName?.Trim() ?? string.Empty;
                }

                projectYear = box.Year?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(boxCode))
                {
                    boxCode = box.ArchiveSequenceNo?.Trim() ?? string.Empty;
                }
            }

            string itemName = string.IsNullOrWhiteSpace(item.ItemName) ? item.MaterialName : item.ItemName;
            materialDetail = string.IsNullOrWhiteSpace(item.FormNo)
                ? itemName
                : $"{itemName}（表单：{item.FormNo.Trim()}）";
        }
        else
        {
            bagCode = item.ElectronicArchiveNo?.Trim() ?? string.Empty;
            if (unitsById.TryGetValue(item.ElectronicArchiveUnitId, out var unit))
            {
                projectYear = unit.Year?.Trim() ?? string.Empty;
                projectName = unit.ProjectName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(bagCode))
                {
                    bagCode = unit.ElectronicArchiveNo?.Trim() ?? string.Empty;
                }
            }

            materialDetail = string.IsNullOrWhiteSpace(item.MediumCode)
                ? (string.IsNullOrWhiteSpace(item.ItemName) ? item.MaterialName : item.ItemName)
                : $"{item.MediumKind} {item.MediumCode}".Trim();
        }

        string displayName = string.IsNullOrWhiteSpace(item.MediumCode)
            ? (string.IsNullOrWhiteSpace(item.ItemName) ? item.MaterialName : item.ItemName)
            : $"{item.MediumKind} {item.MediumCode}";

        return new YearlyArchiveDisposalPrintItemRow
        {
            SortOrder = item.SortOrder,
            ProjectYear = projectYear,
            ProjectName = projectName,
            BoxCode = boxCode,
            BagCode = bagCode,
            ContainerCode = item.ContainerCode,
            DisplayName = displayName,
            MaterialDetail = materialDetail,
            SourceRegisterKind = item.SourceRegisterKind,
            DisposalReason = item.DisposalReason,
            DispositionMethod = ArchiveDisposalDomainValues.NormalizeDispositionMethod(item.DispositionMethod),
            BeforeStorageLocation = item.BeforeStorageLocation,
            MediumKind = item.MediumKind,
            MediumCode = item.MediumCode,
            TargetBlankSlotLocation = item.TargetBlankSlotLocation,
            FormNo = item.FormNo
        };
    }

    /// <inheritdoc />
    public async Task<bool> RequiresPhysicalRemovalConfirmationAsync(int recordId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");
        return await WillDisposeAnyContainerAsync(record);
    }

    /// <inheritdoc />
    public async Task<bool> RequiresFormatRetainConfirmationAsync(int recordId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");
        return ArchiveDisposalDomainValues.HasFormatRetainMethod(record.Items.Select(i => i.DispositionMethod));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string disposalNo)
    {
        return await _repository.GetAttachmentsAsync(disposalNo);
    }

    /// <inheritdoc />
    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId) =>
        _repository.GetAttachmentByIdAsync(attachmentId);

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
        if (!ArchiveDisposalDomainValues.AttachmentCategoryOptions.Contains(category, StringComparer.Ordinal))
        {
            return (false, "附件分类无效。", null);
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId);
        if (existing == null)
        {
            return (false, "未找到资料离库处置单。", null);
        }

        bool isOther = string.Equals(category, ArchiveDisposalDomainValues.AttachmentCategoryOther, StringComparison.Ordinal);
        var attachGate = OfflineApprovalLifecycleSupport.EvaluateAttachmentUpload(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            isOtherCategory: isOther,
            isArchiveAdmin: true,
            allowOtherWhileApproved: true);
        if (!attachGate.Allowed)
        {
            return (false, attachGate.DenyMessage ?? "当前状态不允许上传附件。", null);
        }

        DateTime now = DateTime.Now;
        var attachment = new SystemAttachment
        {
            BusinessType = ArchiveDisposalDomainValues.AttachmentBusinessType,
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

        if (string.Equals(category, ArchiveDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal))
        {
            existing.SignedAttachmentUploaded = true;
            existing.SignedAttachmentUploadedTime = now;
            existing.SignedAttachmentUploader = attachment.UploaderName;
        }
        else if (ArchiveDisposalDomainValues.IsScenePhotoCategory(category))
        {
            existing.ScenePhotoUploaded = true;
        }

        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
        return (true, "附件上传成功。", attachment);
    }

    /// <inheritdoc />
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

        var deleteGate = OfflineApprovalLifecycleSupport.EvaluateAttachmentDelete(existing.Status);
        if (!deleteGate.Allowed)
        {
            return (false, deleteGate.DenyMessage ?? "当前状态不允许删除附件。");
        }

        string category = attachment.FileCategory?.Trim() ?? string.Empty;
        var remainingBefore = await _repository.GetAttachmentsAsync(existing.DisposalNo);
        var remaining = remainingBefore.Where(item => item.Id != attachmentId).ToList();
        _repository.RemoveAttachment(attachment);

        if (string.Equals(category, ArchiveDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal))
        {
            bool stillHas = remaining.Any(item =>
                string.Equals(item.FileCategory, ArchiveDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal));
            if (!stillHas)
            {
                existing.SignedAttachmentUploaded = false;
                existing.SignedAttachmentUploadedTime = null;
                existing.SignedAttachmentUploader = string.Empty;
            }
        }
        else if (ArchiveDisposalDomainValues.IsScenePhotoCategory(category))
        {
            bool stillHas = remaining.Any(item =>
                ArchiveDisposalDomainValues.IsScenePhotoCategory(item.FileCategory));
            if (!stillHas)
            {
                existing.ScenePhotoUploaded = false;
            }
        }

        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
        return (true, "附件已删除。");
    }

    private static string PreferNonEmpty(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary) ? primary.Trim() : (fallback?.Trim() ?? string.Empty);
}
