using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia;

/// <summary>
/// 硬盘盘库登记业务服务（B 流：审批通过 → 确认可上传 → 上传签批单 → 办结）。
/// </summary>
public sealed class HardDiskInventoryRegisterService : IHardDiskInventoryRegisterService
{
    private readonly IHardDiskInventoryRegisterRepository _repository;
    private readonly IHardDiskMediaService _hardDiskMediaService;
    private readonly IBusinessRuleService _businessRuleService;
    private readonly IUserService _userService;
    private readonly IApprovalWorkflowService _approvalWorkflowService;
    private readonly IBusinessLogicSettingsService _businessLogicSettingsService;

    public HardDiskInventoryRegisterService(
        IHardDiskInventoryRegisterRepository repository,
        IHardDiskMediaService hardDiskMediaService,
        IBusinessRuleService businessRuleService,
        IUserService userService,
        IApprovalWorkflowService approvalWorkflowService,
        IBusinessLogicSettingsService businessLogicSettingsService)
    {
        _repository = repository;
        _hardDiskMediaService = hardDiskMediaService;
        _businessRuleService = businessRuleService;
        _userService = userService;
        _approvalWorkflowService = approvalWorkflowService;
        _businessLogicSettingsService = businessLogicSettingsService;
    }

    public async Task<IReadOnlyList<HardDiskInventoryRegisterRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear)
    {
        return await _repository.SearchRecordsAsync(keyword, status, applyYear);
    }

    public Task<HardDiskInventoryRegisterRecord?> GetRecordByIdAsync(int recordId)
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

    public Task<string> GenerateNextRegisterNoAsync()
    {
        return _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.DiskInventoryRegister);
    }

    public Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDamagedTargetLocationOptionsAsync()
    {
        return _hardDiskMediaService.GetDedicatedTargetLocationOptionsAsync(
            CabinetHardDiskSlotCategoryAssignment.CategoryDamaged);
    }

    public async Task<HardDiskInventoryRegisterRecord> CreateDraftAsync(
        HardDiskInventoryRegisterRecord draft,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(items);

        ValidateHeader(draft.RegisterKind, draft.Reason, requireCreatable: true);
        var media = await LoadAndValidateMediaAsync(draft.RegisterKind, items, excludeRecordId: null);

        DateTime now = DateTime.Now;
        string registerNo = string.IsNullOrWhiteSpace(draft.RegisterNo)
            ? await _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.DiskInventoryRegister)
            : draft.RegisterNo.Trim();

        var record = new HardDiskInventoryRegisterRecord
        {
            RegisterNo = registerNo,
            Status = HardDiskInventoryRegisterRecord.StatusDraft,
            RegisterKind = draft.RegisterKind.Trim(),
            Reason = draft.Reason?.Trim() ?? string.Empty,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            ApplicantUserId = currentUser.Id,
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            ApplyTime = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items = BuildItems(media, items, now)
        };

        _repository.AddRecord(record);
        await _repository.SaveChangesAsync();

        LockMedia(record, media, now);
        await _repository.SaveChangesAsync();

        return (await _repository.GetRecordByIdAsync(record.Id))!;
    }

    public async Task<HardDiskInventoryRegisterRecord> UpdateDraftAsync(
        HardDiskInventoryRegisterRecord draft,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(items);

        if (draft.Id <= 0)
        {
            throw new InvalidOperationException("登记单无效。");
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(draft.Id)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status != HardDiskInventoryRegisterRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可修改。");
        }

        bool keepLegacyRelocate =
            string.Equals(existing.RegisterKind, HardDiskInventoryRegisterDomainValues.KindRelocateDamaged, StringComparison.Ordinal)
            && string.Equals(draft.RegisterKind?.Trim(), HardDiskInventoryRegisterDomainValues.KindRelocateDamaged, StringComparison.Ordinal);
        ValidateHeader(draft.RegisterKind, draft.Reason, requireCreatable: !keepLegacyRelocate);
        var media = await LoadAndValidateMediaAsync(draft.RegisterKind, items, excludeRecordId: existing.Id);

        DateTime now = DateTime.Now;
        // 草稿改明细：先解除本单全部旧锁，再按新明细加锁（避免仅按旧 Items 遗漏孤儿锁）。
        await UnlockAllOwnedLocksAsync(existing);

        _repository.RemoveItems(existing.Items.ToList());
        existing.Items.Clear();

        existing.RegisterKind = draft.RegisterKind.Trim();
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        foreach (var item in BuildItems(media, items, now))
        {
            existing.Items.Add(item);
        }

        await _repository.SaveChangesAsync();
        LockMedia(existing, media, now);
        await _repository.SaveChangesAsync();

        return (await _repository.GetRecordByIdAsync(existing.Id))!;
    }

    public async Task SubmitAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status != HardDiskInventoryRegisterRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可提交。");
        }

        ValidateHeader(existing.RegisterKind, existing.Reason);
        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一块硬盘。");
        }

        var drafts = existing.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new HardDiskInventoryRegisterItemDraft
            {
                MediumId = item.MediumId,
                TargetStorageLocation = item.TargetStorageLocation
            })
            .ToList();

        // 草稿保存时已锁定介质，提交仅复核有效性，不再重复加锁。
        await LoadAndValidateMediaAsync(existing.RegisterKind, drafts, excludeRecordId: existing.Id);

        DateTime now = DateTime.Now;
        existing.Status = HardDiskInventoryRegisterRecord.StatusSubmitted;
        existing.SubmittedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task ApproveAsync(int recordId, string approvalOpinion, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

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
                BusinessType = ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister,
                ApplicantDept = existing.ApplicantDept,
                FieldValues = ApprovalChainApplySupport.BuildHardDiskInventoryRegisterFieldValues(existing)
            },
            users);
        ApprovalChainApplySupport.ApplyToHardDiskInventoryRegister(existing, chain, now);

        var missing = ApprovalChainApplySupport.CollectMissingSignerErrors(
            chain,
            nodeKey => ApprovalChainApplySupport.ReadHardDiskInventoryRegisterSigner(existing, nodeKey));
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, missing)
                + Environment.NewLine
                + "请在「审核审批」中配置或在用户管理中维护对应角色后再审批通过。");
        }

        existing.Status = gate.NextStatus ?? HardDiskInventoryRegisterRecord.StatusApproved;
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
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status is not (HardDiskInventoryRegisterRecord.StatusApproved
            or HardDiskInventoryRegisterRecord.StatusSignedUploaded))
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

    public async Task ConfirmReadyForUploadAsync(
        int recordId,
        User currentUser,
        bool damagedDiskRelocationConfirmed = false)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        var gate = OfflineApprovalLifecycleSupport.TryTransition(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.ConfirmMidStep);
        if (!gate.Allowed)
        {
            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不允许确认可上传附件信息。");
        }

        bool needsRelocationConfirm = HardDiskInventoryRegisterDomainValues.RequiresDamagedDiskRelocationConfirm(existing.RegisterKind);
        if (needsRelocationConfirm && !damagedDiskRelocationConfirmed)
        {
            throw new InvalidOperationException("损坏登记确认可上传附件信息前须勾选确认：已完成损坏硬盘迁档。");
        }

        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);

        if (needsRelocationConfirm)
        {
            ValidateHeader(existing.RegisterKind, existing.Reason);
            if (existing.Items.Count == 0)
            {
                throw new InvalidOperationException("请至少选择一块硬盘。");
            }

            var drafts = existing.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new HardDiskInventoryRegisterItemDraft
                {
                    MediumId = item.MediumId,
                    TargetStorageLocation = item.TargetStorageLocation
                })
                .ToList();

            var media = await LoadAndValidateMediaAsync(
                existing.RegisterKind,
                drafts,
                excludeRecordId: existing.Id,
                allowPostRelocationDamageStatus: false);

            ApplyInventoryRegisterLedgerEffects(
                existing,
                media,
                operatorName,
                now,
                descriptionPrefix: "盘库登记(迁档确认)");

            existing.DamagedDiskRelocationConfirmed = true;
            existing.DamagedDiskRelocationConfirmedAt = now;
            existing.DamagedDiskRelocationConfirmedBy = operatorName;
        }

        existing.Status = gate.NextStatus ?? HardDiskInventoryRegisterRecord.StatusSignedUploaded;
        existing.ConfirmedBy = operatorName;
        existing.ConfirmedTime = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task CompleteAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        var gate = OfflineApprovalLifecycleSupport.TryTransition(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.Complete);
        if (!gate.Allowed)
        {
            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不允许办结。");
        }

        var attachments = await _repository.GetAttachmentsAsync(existing.RegisterNo);
        bool hasSignedForm = attachments.Any(item =>
            string.Equals(
                item.FileCategory?.Trim(),
                HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal));
        if (!hasSignedForm)
        {
            throw new InvalidOperationException("办结前须上传签批单附件。");
        }

        bool damageRelocationAlreadyApplied =
            HardDiskInventoryRegisterDomainValues.RequiresDamagedDiskRelocationConfirm(existing.RegisterKind)
            && existing.DamagedDiskRelocationConfirmed;
        if (HardDiskInventoryRegisterDomainValues.RequiresDamagedDiskRelocationConfirm(existing.RegisterKind)
            && !existing.DamagedDiskRelocationConfirmed)
        {
            throw new InvalidOperationException("损坏登记办结前须已确认：已完成损坏硬盘迁档。");
        }

        ValidateHeader(existing.RegisterKind, existing.Reason);
        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一块硬盘。");
        }

        var drafts = existing.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new HardDiskInventoryRegisterItemDraft
            {
                MediumId = item.MediumId,
                TargetStorageLocation = item.TargetStorageLocation
            })
            .ToList();

        var media = await LoadAndValidateMediaAsync(
            existing.RegisterKind,
            drafts,
            excludeRecordId: existing.Id,
            allowPostRelocationDamageStatus: damageRelocationAlreadyApplied);

        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);

        // 损坏登记：档口/状态已在「确认可上传附件信息」时写入；此处幂等补齐并办结。
        ApplyInventoryRegisterLedgerEffects(
            existing,
            media,
            operatorName,
            now,
            descriptionPrefix: "盘库登记");

        UnlockMediaIfOwned(existing, media);
        existing.Status = gate.NextStatus ?? HardDiskInventoryRegisterRecord.StatusCompleted;
        existing.CompletedAt = now;
        existing.CompletedBy = operatorName;
        existing.SignedAttachmentUploaded = true;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <summary>
    /// 写入盘库登记台账效应（状态/档口/流转）。已与目标一致时跳过，保证确认可上传与办结可幂等复用。
    /// </summary>
    private void ApplyInventoryRegisterLedgerEffects(
        HardDiskInventoryRegisterRecord record,
        IReadOnlyList<HardDiskMedium> media,
        string operatorName,
        DateTime now,
        string descriptionPrefix)
    {
        string transactionType = HardDiskInventoryRegisterDomainValues.ResolveTransactionType(record.RegisterKind);
        bool clearLocation = HardDiskInventoryRegisterDomainValues.ClearsStorageLocation(record.RegisterKind);

        foreach (var item in record.Items.OrderBy(detail => detail.SortOrder))
        {
            var medium = media.First(m => m.Id == item.MediumId);
            var ledger = EnsureLedger(medium, now);
            string beforeStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
            string beforeLocation = ledger.StorageLocation?.Trim() ?? string.Empty;
            string afterStatus = HardDiskInventoryRegisterDomainValues.ResolveAfterMediaStatus(
                record.RegisterKind,
                beforeStatus);
            string afterLocation = clearLocation
                ? string.Empty
                : (item.TargetStorageLocation?.Trim() ?? string.Empty);

            bool statusSame = string.Equals(beforeStatus, afterStatus, StringComparison.Ordinal);
            bool locationSame = HardDiskLedgerSyncSupport.IsSameFullLocation(beforeLocation, afterLocation);
            if (statusSame && locationSame)
            {
                continue;
            }

            medium.UpdatedTime = now;
            ledger.UpdatedTime = now;
            ledger.DiskCode = medium.DiskCode;
            ledger.MediaStatus = afterStatus;
            ledger.NeedReturn = false;
            ledger.StorageLocation = afterLocation;
            if (string.Equals(afterStatus, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal)
                || string.Equals(afterStatus, HardDiskMedium.StatusInStockLost, StringComparison.Ordinal))
            {
                ledger.HolderOrOrganization = string.Equals(afterStatus, HardDiskMedium.StatusInStockLost, StringComparison.Ordinal)
                    ? string.Empty
                    : "资料室";
            }

            _repository.AddTransaction(new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                ApplicationId = null,
                TransactionType = transactionType,
                BeforeStatus = beforeStatus,
                AfterStatus = afterStatus,
                BeforeLocation = beforeLocation,
                AfterLocation = afterLocation,
                OperatorName = operatorName,
                OperateTime = now,
                RelatedPerson = record.ApplicantName,
                TargetOrganization = "资料室",
                NeedReturn = false,
                RelatedBatch = record.RegisterNo,
                Description = $"{descriptionPrefix}：{record.RegisterKind}",
                Remark = record.Remark
            });
        }
    }

    public async Task WithdrawAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status is HardDiskInventoryRegisterRecord.StatusApproved
            or HardDiskInventoryRegisterRecord.StatusSignedUploaded
            or HardDiskInventoryRegisterRecord.StatusCompleted)
        {
            throw new InvalidOperationException("审批通过后不可撤回作废。");
        }

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

        var media = await _repository.GetMediaWithLedgerByIdsAsync(existing.Items.Select(item => item.MediumId).ToList());
        UnlockMediaIfOwned(existing, media);

        DateTime now = DateTime.Now;
        existing.Status = gate.NextStatus ?? HardDiskInventoryRegisterRecord.StatusWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = reason?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task ForceVoidAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

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
                && existing.Status is HardDiskInventoryRegisterRecord.StatusDraft
                    or HardDiskInventoryRegisterRecord.StatusSubmitted)
            {
                throw new InvalidOperationException(_businessLogicSettingsService.BuildNotEligibleMessage(settingCode));
            }

            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不可强制作废。");
        }

        var media = await _repository.GetMediaWithLedgerByIdsAsync(existing.Items.Select(item => item.MediumId).ToList());
        UnlockMediaIfOwned(existing, media);

        DateTime now = DateTime.Now;
        existing.Status = gate.NextStatus ?? HardDiskInventoryRegisterRecord.StatusForceWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = string.IsNullOrWhiteSpace(reason) ? "资料管理员强制作废" : reason.Trim();
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task RecordPrintAsync(int recordId)
    {
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status is HardDiskInventoryRegisterRecord.StatusDraft
            or HardDiskInventoryRegisterRecord.StatusWithdrawn
            or HardDiskInventoryRegisterRecord.StatusForceWithdrawn)
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

    public async Task<HardDiskInventoryRegisterPrintData> BuildPrintDataAsync(int recordId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        var orderedItems = record.Items.OrderBy(item => item.SortOrder).ToList();
        var chain = await _approvalWorkflowService.ResolveAsync(
            new ApprovalChainResolveRequest
            {
                BusinessType = ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister,
                ApplicantDept = record.ApplicantDept,
                FieldValues = ApprovalChainApplySupport.BuildHardDiskInventoryRegisterFieldValues(record)
            },
            _userService.GetAllUsers());

        return new HardDiskInventoryRegisterPrintData
        {
            RegisterNo = record.RegisterNo,
            ApplyDateText = record.ApplyTime.ToString("yyyy-MM-dd"),
            RegisterKind = record.RegisterKind,
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
            DeptHead = record.DeptHead,
            DeptHeadDateText = record.DeptHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ArchiveRoomHead = record.ArchiveRoomHead,
            ArchiveRoomHeadDateText = record.ArchiveRoomHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ProductionHead = record.ProductionHead,
            ProductionHeadDateText = record.ProductionHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ArchiveDeputyPresident = record.ArchiveDeputyPresident,
            ArchiveDeputyPresidentDateText = record.ArchiveDeputyPresidentDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ProductionVicePresident = record.ProductionVicePresident,
            ProductionVicePresidentDateText = record.ProductionVicePresidentDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            EnableDeptHead = chain.DeptHead.IsEnabled,
            EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
            EnableProductionHead = chain.ProductionHead.IsEnabled,
            EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
            EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled,
            PrintCount = record.PrintCount,
            Items = orderedItems
                .Select(item => new HardDiskInventoryRegisterPrintItemData
                {
                    SortOrder = item.SortOrder,
                    DiskCode = item.DiskCode,
                    SerialNumber = item.SerialNumber,
                    BeforeMediaStatus = item.BeforeMediaStatus,
                    BeforeStorageLocation = item.BeforeStorageLocation,
                    TargetStorageLocation = item.TargetStorageLocation
                })
                .ToList()
        };
    }

    public async Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string registerNo)
    {
        return await _repository.GetAttachmentsAsync(registerNo);
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
        if (!HardDiskInventoryRegisterDomainValues.AttachmentCategoryOptions.Contains(category, StringComparer.Ordinal))
        {
            return (false, "附件分类无效。", null);
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId);
        if (existing == null)
        {
            return (false, "未找到盘库登记单。", null);
        }

        if (existing.Status is HardDiskInventoryRegisterRecord.StatusDraft
            or HardDiskInventoryRegisterRecord.StatusSubmitted
            or HardDiskInventoryRegisterRecord.StatusWithdrawn
            or HardDiskInventoryRegisterRecord.StatusForceWithdrawn)
        {
            return (false, "当前状态不允许上传附件（请在审批通过并确认可上传后操作）。", null);
        }

        bool isOther = string.Equals(category, HardDiskInventoryRegisterDomainValues.AttachmentCategoryOther, StringComparison.Ordinal);
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
            BusinessType = HardDiskInventoryRegisterDomainValues.AttachmentBusinessType,
            BusinessNo = existing.RegisterNo,
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

        if (string.Equals(category, HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal))
        {
            existing.SignedAttachmentUploaded = true;
            existing.SignedAttachmentUploadedTime = now;
            existing.SignedAttachmentUploader = attachment.UploaderName;
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
            return (false, "未找到关联盘库登记单。");
        }

        var deleteGate = OfflineApprovalLifecycleSupport.EvaluateAttachmentDelete(existing.Status);
        if (!deleteGate.Allowed)
        {
            return (false, deleteGate.DenyMessage ?? "当前状态不允许删除附件。");
        }

        string category = attachment.FileCategory?.Trim() ?? string.Empty;
        var remainingBefore = await _repository.GetAttachmentsAsync(existing.RegisterNo);
        var remaining = remainingBefore.Where(item => item.Id != attachmentId).ToList();
        _repository.RemoveAttachment(attachment);

        if (string.Equals(category, HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal))
        {
            bool stillHas = remaining.Any(item =>
                string.Equals(item.FileCategory, HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal));
            if (!stillHas)
            {
                existing.SignedAttachmentUploaded = false;
                existing.SignedAttachmentUploadedTime = null;
                existing.SignedAttachmentUploader = string.Empty;
            }
        }

        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
        return (true, "附件已删除。");
    }

    private async Task<List<HardDiskMedium>> LoadAndValidateMediaAsync(
        string registerKind,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> itemDrafts,
        int? excludeRecordId,
        bool allowPostRelocationDamageStatus = false)
    {
        if (itemDrafts == null || itemDrafts.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一块硬盘。");
        }

        List<int> ids = itemDrafts.Select(item => item.MediumId).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0 || ids.Count != itemDrafts.Count)
        {
            throw new InvalidOperationException("所选硬盘无效或存在重复。");
        }

        var media = await _repository.GetMediaWithLedgerByIdsAsync(ids);
        if (media.Count != ids.Count)
        {
            throw new InvalidOperationException("部分所选硬盘不存在或已删除。");
        }

        bool requiresTarget = HardDiskInventoryRegisterDomainValues.RequiresDamagedTargetLocation(registerKind);
        var locationByMediumId = itemDrafts.ToDictionary(item => item.MediumId, item => item.TargetStorageLocation?.Trim() ?? string.Empty);

        foreach (var medium in media)
        {
            string status = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty;
            ValidateMediumStatusForKind(
                registerKind,
                medium.DiskCode,
                status,
                allowPostRelocationDamageStatus);

            if (await _repository.ExistsActiveRegisterForMediumAsync(medium.Id, excludeRecordId))
            {
                throw new InvalidOperationException($"硬盘【{medium.DiskCode}】已存在未办结的盘库登记单。");
            }

            if (await _repository.ExistsActiveDisposalForMediumAsync(medium.Id))
            {
                throw new InvalidOperationException($"硬盘【{medium.DiskCode}】已存在未办结的离库处置单。");
            }

            if (medium.RegisterLock != null)
            {
                bool ownedByCurrent = excludeRecordId.HasValue
                    && string.Equals(medium.RegisterLock.BusinessType, HardDiskRegisterLock.BusinessTypeInventoryRegister, StringComparison.Ordinal)
                    && medium.RegisterLock.BusinessRecordId == excludeRecordId.Value;

                if (!ownedByCurrent)
                {
                    string lockOwner = string.IsNullOrWhiteSpace(medium.RegisterLock.BusinessNo)
                        ? medium.RegisterLock.BusinessType
                        : $"{medium.RegisterLock.BusinessType}（{medium.RegisterLock.BusinessNo.Trim()}）";
                    throw new InvalidOperationException(
                        $"硬盘【{medium.DiskCode}】已被其他业务征用：{lockOwner}，不可纳入盘库登记。");
                }
            }

            if (requiresTarget)
            {
                string target = locationByMediumId[medium.Id];
                if (string.IsNullOrWhiteSpace(target))
                {
                    throw new InvalidOperationException($"硬盘【{medium.DiskCode}】请指定损坏硬盘专用档口。");
                }
            }
        }

        if (requiresTarget)
        {
            await ValidateDamagedTargetLocationsAndCapacityAsync(media, locationByMediumId);
        }

        return media;
    }

    /// <summary>
    /// 核验目标位置均为损坏硬盘专用档口，且办结后各档口占用不超过容量上限。
    /// </summary>
    private async Task ValidateDamagedTargetLocationsAndCapacityAsync(
        IReadOnlyList<HardDiskMedium> media,
        IReadOnlyDictionary<int, string> locationByMediumId)
    {
        var options = await GetDamagedTargetLocationOptionsAsync();
        if (options.Count == 0)
        {
            throw new InvalidOperationException("未找到损坏硬盘专用档口，请先在磁盘柜开柜界面完成设置。");
        }

        var optionBySlot = options
            .GroupBy(option => HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(option.Location), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var netDeltaBySlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var medium in media)
        {
            string target = locationByMediumId[medium.Id];
            string targetSlot = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(target);
            if (string.IsNullOrWhiteSpace(targetSlot) || !optionBySlot.ContainsKey(targetSlot))
            {
                throw new InvalidOperationException(
                    $"硬盘【{medium.DiskCode}】的目标档口【{target}】不是损坏硬盘专用档口。");
            }

            string beforeSlot = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(medium.Ledger?.StorageLocation);
            if (string.Equals(beforeSlot, targetSlot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            netDeltaBySlot[targetSlot] = netDeltaBySlot.GetValueOrDefault(targetSlot) + 1;
            if (!string.IsNullOrWhiteSpace(beforeSlot) && optionBySlot.ContainsKey(beforeSlot))
            {
                netDeltaBySlot[beforeSlot] = netDeltaBySlot.GetValueOrDefault(beforeSlot) - 1;
            }
        }

        foreach (var (slotCode, delta) in netDeltaBySlot)
        {
            if (delta <= 0)
            {
                continue;
            }

            var option = optionBySlot[slotCode];
            int slotCapacity = option.SlotCapacity > 0
                ? option.SlotCapacity
                : CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
            int existing = option.ExistingMediumCount;
            int projected = existing + delta;
            if (projected > slotCapacity)
            {
                throw new InvalidOperationException(
                    $"损坏硬盘专用档口【{slotCode}】容量超限：现有 {existing} 盘，本单拟新增 {delta} 盘，合计 {projected} 盘（上限 {slotCapacity} 盘/档口）。请调整目标档口后重试。");
            }
        }
    }

    private static void ValidateMediumStatusForKind(
        string registerKind,
        string diskCode,
        string status,
        bool allowPostRelocationDamageStatus = false)
    {
        string kind = registerKind?.Trim() ?? string.Empty;
        if (string.Equals(kind, HardDiskInventoryRegisterDomainValues.KindDamage, StringComparison.Ordinal))
        {
            if (string.Equals(status, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                return;
            }

            if (allowPostRelocationDamageStatus
                && string.Equals(status, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                allowPostRelocationDamageStatus
                    ? $"硬盘【{diskCode}】当前状态为“{status}”，损坏登记办结仅允许「在库(空盘)」或已迁档后的「在库(损坏)」。"
                    : $"硬盘【{diskCode}】当前状态为“{status}”，损坏登记仅允许「在库(空盘)」。");
        }

        if (string.Equals(kind, HardDiskInventoryRegisterDomainValues.KindRelocateDamaged, StringComparison.Ordinal))
        {
            if (!string.Equals(status, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"硬盘【{diskCode}】当前状态为“{status}”，损坏档口调整仅允许「在库(损坏)」。");
            }

            return;
        }

        if (string.Equals(kind, HardDiskInventoryRegisterDomainValues.KindLost, StringComparison.Ordinal))
        {
            if (!string.Equals(status, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal)
                && !string.Equals(status, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"硬盘【{diskCode}】当前状态为“{status}”，盘失登记仅允许「在库(空盘)」或「在库(损坏)」。");
            }
        }
    }

    private static List<HardDiskInventoryRegisterItem> BuildItems(
        IReadOnlyList<HardDiskMedium> media,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> drafts,
        DateTime now)
    {
        var locationByMediumId = drafts.ToDictionary(item => item.MediumId, item => item.TargetStorageLocation?.Trim() ?? string.Empty);
        int sort = 1;
        return media
            .OrderBy(item => item.DiskCode, StringComparer.Ordinal)
            .Select(medium => new HardDiskInventoryRegisterItem
            {
                SortOrder = sort++,
                MediumId = medium.Id,
                DiskCode = medium.DiskCode?.Trim() ?? string.Empty,
                SerialNumber = medium.SerialNumber?.Trim() ?? string.Empty,
                BeforeMediaStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty,
                BeforeStorageLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty,
                BeforeMediaNature = medium.Ledger?.MediaNature?.Trim() ?? string.Empty,
                TargetStorageLocation = locationByMediumId.GetValueOrDefault(medium.Id, string.Empty),
                CreatedAt = now
            })
            .ToList();
    }

    private static void LockMedia(HardDiskInventoryRegisterRecord record, IReadOnlyList<HardDiskMedium> media, DateTime now)
    {
        foreach (var medium in media)
        {
            if (medium.RegisterLock != null)
            {
                if (!string.Equals(medium.RegisterLock.BusinessType, HardDiskRegisterLock.BusinessTypeInventoryRegister, StringComparison.Ordinal)
                    || medium.RegisterLock.BusinessRecordId != record.Id)
                {
                    throw new InvalidOperationException($"硬盘【{medium.DiskCode}】已被其他业务征用，无法锁定。");
                }

                medium.RegisterLock.BusinessNo = record.RegisterNo;
                medium.RegisterLock.PreviousStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty;
                medium.RegisterLock.LockedTime = now;
                continue;
            }

            medium.RegisterLock = new HardDiskRegisterLock
            {
                MediumId = medium.Id,
                BusinessType = HardDiskRegisterLock.BusinessTypeInventoryRegister,
                BusinessRecordId = record.Id,
                BusinessNo = record.RegisterNo,
                PreviousStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty,
                LockedTime = now
            };
            medium.UpdatedTime = now;
        }
    }

    private void UnlockMediaIfOwned(HardDiskInventoryRegisterRecord record, IReadOnlyList<HardDiskMedium> media)
    {
        foreach (var medium in media)
        {
            var lockItem = medium.RegisterLock;
            if (lockItem == null)
            {
                continue;
            }

            if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeInventoryRegister, StringComparison.Ordinal)
                || lockItem.BusinessRecordId != record.Id)
            {
                continue;
            }

            _repository.RemoveRegisterLock(lockItem);
            medium.RegisterLock = null;
            medium.UpdatedTime = DateTime.Now;
        }
    }

    /// <summary>解除本单占用的全部硬盘锁（保存草稿换明细时用）。</summary>
    private async Task UnlockAllOwnedLocksAsync(HardDiskInventoryRegisterRecord record)
    {
        var ownedLocks = await _repository.GetOwnedRegisterLocksAsync(record.Id);
        foreach (var lockItem in ownedLocks)
        {
            _repository.RemoveRegisterLock(lockItem);
        }
    }

    private static void ValidateHeader(string? registerKind, string? reason, bool requireCreatable = false)
    {
        if (requireCreatable)
        {
            if (!HardDiskInventoryRegisterDomainValues.IsCreatableRegisterKind(registerKind))
            {
                throw new InvalidOperationException(
                    "请选择登记类型（损坏登记/盘失登记）。损坏盘档口调整请在开柜界面使用交互式迁档。");
            }
        }
        else if (!HardDiskInventoryRegisterDomainValues.IsValidRegisterKind(registerKind))
        {
            throw new InvalidOperationException("请选择登记类型（损坏登记/盘失登记）。");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("请填写登记说明。");
        }
    }

    private static void EnsureArchiveAdmin(User? currentUser)
    {
        if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
        {
            throw new InvalidOperationException("仅资料管理员可办理硬盘盘库登记。");
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
}
