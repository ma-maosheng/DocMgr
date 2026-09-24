using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive;

/// <summary>
/// 年度资料盘库登记业务服务（B 流：草稿→提交→审批→确认可上传→签批办结）。
/// </summary>
public sealed partial class ArchiveInventoryRegisterService : IArchiveInventoryRegisterService
{
    private readonly IArchiveInventoryRegisterRepository _repository;
    private readonly IArchiveOutboundRepository _outboundRepository;
    private readonly IBusinessRuleService _businessRuleService;
    private readonly IUserService _userService;
    private readonly IApprovalWorkflowService _approvalWorkflowService;
    private readonly IBusinessLogicSettingsService _businessLogicSettingsService;

    public ArchiveInventoryRegisterService(
        IArchiveInventoryRegisterRepository repository,
        IArchiveOutboundRepository outboundRepository,
        IBusinessRuleService businessRuleService,
        IUserService userService,
        IApprovalWorkflowService approvalWorkflowService,
        IBusinessLogicSettingsService businessLogicSettingsService)
    {
        _repository = repository;
        _outboundRepository = outboundRepository;
        _businessRuleService = businessRuleService;
        _userService = userService;
        _approvalWorkflowService = approvalWorkflowService;
        _businessLogicSettingsService = businessLogicSettingsService;
    }

    public async Task<IReadOnlyList<YearlyArchiveInventoryRegisterRecord>> SearchRecordsAsync(
        string? mediaKind,
        string? keyword,
        int? status,
        int? applyYear)
    {
        return await _repository.SearchRecordsAsync(mediaKind, keyword, status, applyYear);
    }

    public Task<YearlyArchiveInventoryRegisterRecord?> GetRecordByIdAsync(int recordId)
    {
        return _repository.GetRecordByIdAsync(recordId);
    }

    public async Task<IReadOnlyList<ArchiveInventorySelectableSimulatedFact>> GetSelectableSimulatedFilingFactsAsync(int? currentRecordId = null)
    {
        IReadOnlyCollection<int>? excludeIds = null;
        if (currentRecordId.HasValue && currentRecordId.Value > 0)
        {
            var current = await _repository.GetRecordByIdAsync(currentRecordId.Value);
            if (current?.Items != null && current.Items.Count > 0)
            {
                excludeIds = current.Items
                    .Where(item => item.FilingFactId > 0)
                    .Select(item => item.FilingFactId)
                    .ToList();
            }
        }

        return await _repository.GetSelectableSimulatedFilingFactsAsync(excludeIds);
    }

    public async Task<IReadOnlyList<ArchiveInventorySelectableElectronicMedia>> GetSelectableElectronicMediaAsync(
        int? currentRecordId = null)
    {
        IReadOnlyCollection<ArchiveInventoryElectronicMediumKey>? excludeMedia = null;
        if (currentRecordId.HasValue && currentRecordId.Value > 0)
        {
            var current = await _repository.GetRecordByIdAsync(currentRecordId.Value);
            if (current?.Items != null && current.Items.Count > 0)
            {
                excludeMedia = current.Items
                    .Where(item => item.MediumId > 0 && !string.IsNullOrWhiteSpace(item.MediumKind))
                    .Select(item => new ArchiveInventoryElectronicMediumKey(item.MediumKind.Trim(), item.MediumId))
                    .ToList();
            }
        }

        return await _repository.GetSelectableElectronicMediaAsync(excludeMedia, currentRecordId);
    }

    public Task<string> GenerateNextRegisterNoAsync()
    {
        return _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.ArchiveInventoryRegister);
    }

    public async Task<YearlyArchiveInventoryRegisterRecord> CreateDraftAsync(
        YearlyArchiveInventoryRegisterRecord draft,
        IReadOnlyList<ArchiveInventoryRegisterItemDraft> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(items);

        ValidateHeader(draft.MediaKind, draft.RegisterKind, draft.Reason);

        DateTime now = DateTime.Now;
        string registerNo = string.IsNullOrWhiteSpace(draft.RegisterNo)
            ? await _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.ArchiveInventoryRegister)
            : draft.RegisterNo.Trim();

        var record = new YearlyArchiveInventoryRegisterRecord
        {
            RegisterNo = registerNo,
            Status = YearlyArchiveInventoryRegisterRecord.StatusDraft,
            MediaKind = draft.MediaKind.Trim(),
            RegisterKind = draft.RegisterKind.Trim(),
            Reason = draft.Reason?.Trim() ?? string.Empty,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            ApplicantUserId = currentUser.Id,
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            ApplyTime = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (IsSimulatedMediaKind(record.MediaKind))
        {
            record.Items = await BuildSimulatedItemsAsync(record.MediaKind, record.RegisterKind, items, excludeRecordId: null, now);
        }
        else
        {
            record.Items = await BuildElectronicItemsAsync(record.RegisterKind, items, excludeRecordId: null, now);
        }

        _repository.AddRecord(record);
        await _repository.SaveChangesAsync();

        if (!IsSimulatedMediaKind(record.MediaKind))
        {
            LockHardDisksIfNeeded(record, await LoadHardDisksForElectronicDraftAsync(record.MediaKind, items), now);
            await _repository.SaveChangesAsync();
        }

        return (await _repository.GetRecordByIdAsync(record.Id))!;
    }

    public async Task<YearlyArchiveInventoryRegisterRecord> UpdateDraftAsync(
        YearlyArchiveInventoryRegisterRecord draft,
        IReadOnlyList<ArchiveInventoryRegisterItemDraft> items,
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

        if (existing.Status != YearlyArchiveInventoryRegisterRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可修改。");
        }

        ValidateHeader(draft.MediaKind, draft.RegisterKind, draft.Reason);

        DateTime now = DateTime.Now;
        // 草稿改明细：先解除本单全部旧锁，再按新明细加锁。
        await UnlockAllOwnedHardDiskLocksAsync(existing);

        _repository.RemoveItems(existing.Items.ToList());
        existing.Items.Clear();

        existing.MediaKind = draft.MediaKind.Trim();
        existing.RegisterKind = draft.RegisterKind.Trim();
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;

        if (IsSimulatedMediaKind(existing.MediaKind))
        {
            foreach (var item in await BuildSimulatedItemsAsync(existing.MediaKind, existing.RegisterKind, items, existing.Id, now))
            {
                existing.Items.Add(item);
            }
        }
        else
        {
            foreach (var item in await BuildElectronicItemsAsync(existing.RegisterKind, items, existing.Id, now))
            {
                existing.Items.Add(item);
            }
        }

        await _repository.SaveChangesAsync();
        LockHardDisksIfNeeded(existing, await LoadHardDisksForElectronicDraftAsync(existing.MediaKind, items), now);
        await _repository.SaveChangesAsync();

        return (await _repository.GetRecordByIdAsync(existing.Id))!;
    }

    public async Task SubmitAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status != YearlyArchiveInventoryRegisterRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可提交。");
        }

        ValidateHeader(existing.MediaKind, existing.RegisterKind, existing.Reason);
        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少添加一条登记明细。");
        }

        DateTime now = DateTime.Now;
        existing.Status = YearlyArchiveInventoryRegisterRecord.StatusSubmitted;
        existing.SubmittedAt = now;
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

        ValidateHeader(existing.MediaKind, existing.RegisterKind, existing.Reason);
        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少添加一条登记明细。");
        }

        var attachments = await _repository.GetAttachmentsAsync(existing.RegisterNo);
        bool hasSignedForm = attachments.Any(item =>
            string.Equals(
                item.FileCategory,
                ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal));
        if (!hasSignedForm)
        {
            throw new InvalidOperationException("办结前须上传签批单附件。");
        }

        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);

        if (IsSimulatedMediaKind(existing.MediaKind))
        {
            await CompleteSimulatedAsync(existing, operatorName, now);
        }
        else
        {
            await CompleteElectronicAsync(existing, operatorName, now);
        }

        UnlockHardDisksIfOwned(existing, await LoadHardDisksFromExistingItemsAsync(existing));

        existing.Status = gate.NextStatus ?? YearlyArchiveInventoryRegisterRecord.StatusCompleted;
        existing.CompletedAt = now;
        existing.CompletedBy = operatorName;
        existing.SignedAttachmentUploaded = true;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task WithdrawAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status is YearlyArchiveInventoryRegisterRecord.StatusApproved
            or YearlyArchiveInventoryRegisterRecord.StatusSignedUploaded
            or YearlyArchiveInventoryRegisterRecord.StatusCompleted)
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

        UnlockHardDisksIfOwned(existing, await LoadHardDisksFromExistingItemsAsync(existing));

        DateTime now = DateTime.Now;
        existing.Status = gate.NextStatus ?? YearlyArchiveInventoryRegisterRecord.StatusWithdrawn;
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
                && existing.Status is YearlyArchiveInventoryRegisterRecord.StatusDraft
                    or YearlyArchiveInventoryRegisterRecord.StatusSubmitted)
            {
                throw new InvalidOperationException(_businessLogicSettingsService.BuildNotEligibleMessage(settingCode));
            }

            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不可强制作废。");
        }

        UnlockHardDisksIfOwned(existing, await LoadHardDisksFromExistingItemsAsync(existing));

        DateTime now = DateTime.Now;
        existing.Status = gate.NextStatus ?? YearlyArchiveInventoryRegisterRecord.StatusForceWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = string.IsNullOrWhiteSpace(reason) ? "资料管理员强制作废" : reason.Trim();
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    private static bool IsSimulatedMediaKind(string? mediaKind) =>
        string.Equals(mediaKind?.Trim(), ArchiveInventoryRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);

    private static void ValidateHeader(string? mediaKind, string? registerKind, string? reason)
    {
        if (!ArchiveInventoryRegisterDomainValues.IsValidMediaKind(mediaKind))
        {
            throw new InvalidOperationException("请选择介质轨（模拟/电子）。");
        }

        if (!ArchiveInventoryRegisterDomainValues.IsRegisterKindAllowedForMediaKind(mediaKind, registerKind))
        {
            throw new InvalidOperationException("登记类型无效：模拟轨仅支持盘失/拟销登记，电子轨仅支持盘失/损坏/拟销登记。");
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
            throw new InvalidOperationException("仅资料管理员可办理年度资料盘库登记。");
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
}
