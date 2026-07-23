using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia;

/// <summary>
/// 硬盘盘库登记业务服务（轻量草稿/即时办结）。
/// </summary>
public sealed class HardDiskInventoryRegisterService : IHardDiskInventoryRegisterService
{
    private readonly IHardDiskInventoryRegisterRepository _repository;
    private readonly IHardDiskMediaService _hardDiskMediaService;
    private readonly IBusinessRuleService _businessRuleService;

    public HardDiskInventoryRegisterService(
        IHardDiskInventoryRegisterRepository repository,
        IHardDiskMediaService hardDiskMediaService,
        IBusinessRuleService businessRuleService)
    {
        _repository = repository;
        _hardDiskMediaService = hardDiskMediaService;
        _businessRuleService = businessRuleService;
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

        ValidateHeader(draft.RegisterKind, draft.Reason);
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

        ValidateHeader(draft.RegisterKind, draft.Reason);
        var media = await LoadAndValidateMediaAsync(draft.RegisterKind, items, excludeRecordId: existing.Id);

        DateTime now = DateTime.Now;
        UnlockMediaIfOwned(existing, await _repository.GetMediaWithLedgerByIdsAsync(existing.Items.Select(item => item.MediumId).ToList()));

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

    public async Task CompleteAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status != HardDiskInventoryRegisterRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可办结。");
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

        var media = await LoadAndValidateMediaAsync(existing.RegisterKind, drafts, excludeRecordId: existing.Id);
        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);
        string transactionType = HardDiskInventoryRegisterDomainValues.ResolveTransactionType(existing.RegisterKind);
        bool clearLocation = HardDiskInventoryRegisterDomainValues.ClearsStorageLocation(existing.RegisterKind);

        foreach (var item in existing.Items.OrderBy(detail => detail.SortOrder))
        {
            var medium = media.First(m => m.Id == item.MediumId);
            var ledger = EnsureLedger(medium, now);
            string beforeStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
            string beforeLocation = ledger.StorageLocation?.Trim() ?? string.Empty;
            string afterStatus = HardDiskInventoryRegisterDomainValues.ResolveAfterMediaStatus(existing.RegisterKind, beforeStatus);
            string afterLocation = clearLocation
                ? string.Empty
                : (item.TargetStorageLocation?.Trim() ?? string.Empty);

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
                RelatedPerson = existing.ApplicantName,
                TargetOrganization = "资料室",
                NeedReturn = false,
                RelatedBatch = existing.RegisterNo,
                Description = $"盘库登记：{existing.RegisterKind}",
                Remark = existing.Remark
            });
        }

        UnlockMediaIfOwned(existing, media);
        existing.Status = HardDiskInventoryRegisterRecord.StatusCompleted;
        existing.CompletedAt = now;
        existing.CompletedBy = operatorName;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task WithdrawAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status is HardDiskInventoryRegisterRecord.StatusCompleted
            or HardDiskInventoryRegisterRecord.StatusWithdrawn)
        {
            throw new InvalidOperationException("当前状态不可撤回作废。");
        }

        var media = await _repository.GetMediaWithLedgerByIdsAsync(existing.Items.Select(item => item.MediumId).ToList());
        UnlockMediaIfOwned(existing, media);

        DateTime now = DateTime.Now;
        existing.Status = HardDiskInventoryRegisterRecord.StatusWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = reason?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    private async Task<List<HardDiskMedium>> LoadAndValidateMediaAsync(
        string registerKind,
        IReadOnlyList<HardDiskInventoryRegisterItemDraft> itemDrafts,
        int? excludeRecordId)
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
            ValidateMediumStatusForKind(registerKind, medium.DiskCode, status);

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

        int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
            CabinetHardDiskSlotCategoryAssignment.CategoryDamaged);
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

            int existing = optionBySlot[slotCode].ExistingMediumCount;
            int projected = existing + delta;
            if (projected > slotCapacity)
            {
                throw new InvalidOperationException(
                    $"损坏硬盘专用档口【{slotCode}】容量超限：现有 {existing} 盘，本单拟新增 {delta} 盘，合计 {projected} 盘（上限 {slotCapacity} 盘/档口）。请调整目标档口后重试。");
            }
        }
    }

    private static void ValidateMediumStatusForKind(string registerKind, string diskCode, string status)
    {
        string kind = registerKind?.Trim() ?? string.Empty;
        if (string.Equals(kind, HardDiskInventoryRegisterDomainValues.KindDamage, StringComparison.Ordinal))
        {
            if (!string.Equals(status, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"硬盘【{diskCode}】当前状态为“{status}”，损坏登记仅允许「在库(空盘)」。");
            }

            return;
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

    private static void ValidateHeader(string? registerKind, string? reason)
    {
        if (!HardDiskInventoryRegisterDomainValues.IsValidRegisterKind(registerKind))
        {
            throw new InvalidOperationException("请选择登记类型（损坏登记/盘失登记/损坏档口调整）。");
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
            throw new InvalidOperationException("仅资料室资料管理员可办理硬盘盘库登记。");
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
