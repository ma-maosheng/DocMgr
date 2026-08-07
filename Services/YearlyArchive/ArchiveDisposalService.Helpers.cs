using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.YearlyArchive;

/// <summary>
/// 资料离库处置：校验、明细构建、征用锁。
/// </summary>
public sealed partial class ArchiveDisposalService
{
    private static void EnsureValidMediaKind(string? mediaKind)
    {
        if (!ArchiveInventoryRegisterDomainValues.IsValidMediaKind(mediaKind))
        {
            throw new InvalidOperationException("介质类别无效，须为「模拟」或「电子」。");
        }
    }

    private static void EnsureArchiveAdmin(User? currentUser)
    {
        if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
        {
            throw new InvalidOperationException("仅资料室资料管理员可办理资料离库处置。");
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

    private static void ValidateHeader(string? reason, IEnumerable<YearlyArchiveDisposalItem>? items)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("请填写申请说明。");
        }

        if (items == null || !items.Any())
        {
            throw new InvalidOperationException("请至少选择一条待处置明细。");
        }
    }

    private static void ValidateItemMethods(string mediaKind, IEnumerable<YearlyArchiveDisposalItem> items)
    {
        foreach (var item in items)
        {
            if (!ArchiveDisposalDomainValues.IsValidReason(item.DisposalReason))
            {
                throw new InvalidOperationException($"明细「{ResolveItemDisplay(item)}」离库原因无效。");
            }

            if (!ArchiveDisposalDomainValues.IsValidDispositionMethod(item.DispositionMethod))
            {
                throw new InvalidOperationException($"明细「{ResolveItemDisplay(item)}」处置方式无效。");
            }

            string? mismatch = ArchiveDisposalDomainValues.TryGetReasonAndMethodMismatchMessage(
                mediaKind,
                item.DisposalReason,
                item.MediumKind,
                item.DispositionMethod);
            if (!string.IsNullOrWhiteSpace(mismatch))
            {
                throw new InvalidOperationException($"明细「{ResolveItemDisplay(item)}」：{mismatch}");
            }

            if (ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod)
                && string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation))
            {
                // 低格留盘目标档口在办结前录入，草稿/提交阶段允许暂时为空
            }
        }
    }

    private static string ResolveItemDisplay(YearlyArchiveDisposalItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.MediumCode))
        {
            return $"{item.MediumKind} {item.MediumCode}";
        }

        return string.IsNullOrWhiteSpace(item.ItemName) ? item.MaterialName : item.ItemName;
    }

    private async Task<List<YearlyArchiveDisposalItem>> BuildItemsAsync(
        string mediaKind,
        IReadOnlyList<YearlyArchiveDisposalItem> requested,
        DateTime now,
        int? excludeRecordId)
    {
        if (string.Equals(mediaKind.Trim(), ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
        {
            return await BuildSimulatedItemsAsync(requested, now, excludeRecordId);
        }

        return await BuildElectronicItemsAsync(requested, now, excludeRecordId);
    }

    private async Task<List<YearlyArchiveDisposalItem>> BuildSimulatedItemsAsync(
        IReadOnlyList<YearlyArchiveDisposalItem> requested,
        DateTime now,
        int? excludeRecordId)
    {
        var factIds = requested.Select(item => item.FilingFactId).Where(id => id > 0).Distinct().ToList();
        if (factIds.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一条模拟资料明细。");
        }

        var facts = await _repository.GetFilingFactsByIdsAsync(factIds);
        if (facts.Count != factIds.Count)
        {
            throw new InvalidOperationException("部分立档事实不存在，无法保存。");
        }

        var selectable = (await _repository.GetSelectableSimulatedItemsAsync(excludeRecordId))
            .ToDictionary(item => item.FilingFactId);

        var result = new List<YearlyArchiveDisposalItem>();
        int sort = 1;
        foreach (var req in requested.OrderBy(item => item.SortOrder))
        {
            var fact = facts.First(item => item.Id == req.FilingFactId);
            if (!selectable.ContainsKey(fact.Id)
                && !(excludeRecordId.HasValue && excludeRecordId.Value > 0))
            {
                // 当前草稿已包含的事实在 selectable 中可能因 exclude 仍可选；若完全不在则拒绝
            }

            if (await _repository.ExistsActiveDisposalForFilingFactAsync(fact.Id, excludeRecordId))
            {
                throw new InvalidOperationException($"资料「{fact.ItemName}」已在其他未办结离库处置单中。");
            }

            if (fact.InventoryLostCopyCount <= 0 && fact.InventoryScrapCopyCount <= 0)
            {
                throw new InvalidOperationException($"资料「{fact.ItemName}」无盘库丢失/拟销份数，不可纳入离库处置。");
            }

            if (string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.Disposed, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"资料「{fact.ItemName}」已处置，不可重复纳入。");
            }

            string registerKind = fact.InventoryScrapCopyCount > 0
                ? ArchiveInventoryRegisterDomainValues.KindScrap
                : ArchiveInventoryRegisterDomainValues.KindLost;
            string reason = ArchiveDisposalDomainValues.ResolveReasonFromRegisterKind(registerKind);
            string method = string.IsNullOrWhiteSpace(req.DispositionMethod)
                ? ArchiveDisposalDomainValues.ResolveDefaultMethod(
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    reason,
                    null)
                : ArchiveDisposalDomainValues.NormalizeDispositionMethod(req.DispositionMethod);

            result.Add(new YearlyArchiveDisposalItem
            {
                SortOrder = sort++,
                FilingFactId = fact.Id,
                ContainerId = fact.ContainerId,
                ContainerCode = fact.ContainerCode ?? string.Empty,
                BeforeStorageLocation = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? (fact.StorageLocation ?? string.Empty)
                    : fact.CurrentStorageLocation,
                SourceRegisterKind = registerKind,
                DisposalReason = reason,
                DispositionMethod = method,
                MaterialName = fact.MaterialName ?? string.Empty,
                ItemName = fact.ItemName ?? string.Empty,
                FormNo = fact.FormNo ?? string.Empty,
                InventoryLostCopyCount = fact.InventoryLostCopyCount,
                InventoryScrapCopyCount = fact.InventoryScrapCopyCount,
                BeforeLifecycleStatus = fact.LifecycleStatus ?? string.Empty,
                CreatedAt = now
            });
        }

        return result;
    }

    private async Task<List<YearlyArchiveDisposalItem>> BuildElectronicItemsAsync(
        IReadOnlyList<YearlyArchiveDisposalItem> requested,
        DateTime now,
        int? excludeRecordId)
    {
        if (requested.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一条电子介质明细。");
        }

        var selectable = (await _repository.GetSelectableElectronicItemsAsync(excludeRecordId))
            .ToDictionary(item => item.SelectionKey, StringComparer.Ordinal);

        var result = new List<YearlyArchiveDisposalItem>();
        int sort = 1;
        foreach (var req in requested.OrderBy(item => item.SortOrder))
        {
            string key = $"M:{req.MediumKind}:{req.MediumId}";
            if (!selectable.TryGetValue(key, out var candidate))
            {
                throw new InvalidOperationException(
                    $"介质「{req.MediumKind} {req.MediumCode}」当前不可选（可能已被占用或状态已变）。");
            }

            if (await _repository.ExistsActiveDisposalForMediumAsync(req.MediumKind, req.MediumId, excludeRecordId))
            {
                throw new InvalidOperationException($"介质「{candidate.MediumCode}」已在其他未办结离库处置单中。");
            }

            string method = string.IsNullOrWhiteSpace(req.DispositionMethod)
                ? ArchiveDisposalDomainValues.ResolveDefaultMethod(
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    candidate.DisposalReason,
                    candidate.MediumKind)
                : ArchiveDisposalDomainValues.NormalizeDispositionMethod(req.DispositionMethod);

            string targetBlank = req.TargetBlankSlotLocation?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(targetBlank))
            {
                targetBlank = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetBlank);
            }

            result.Add(new YearlyArchiveDisposalItem
            {
                SortOrder = sort++,
                ContainerId = candidate.ContainerId,
                ContainerCode = candidate.ContainerCode,
                BeforeStorageLocation = candidate.BeforeStorageLocation,
                SourceRegisterKind = candidate.SourceRegisterKind,
                DisposalReason = candidate.DisposalReason,
                DispositionMethod = method,
                MediumKind = candidate.MediumKind,
                MediumId = candidate.MediumId,
                MediumCode = candidate.MediumCode,
                ElectronicArchiveUnitId = candidate.ElectronicArchiveUnitId,
                ElectronicArchiveNo = candidate.ElectronicArchiveNo,
                BeforeMediaStatus = candidate.BeforeMediaStatus,
                TargetBlankSlotLocation = targetBlank,
                CreatedAt = now
            });
        }

        return result;
    }

    private async Task EnsureItemsStillSelectableAsync(YearlyArchiveDisposalRecord existing)
    {
        if (existing.IsSimulated)
        {
            var selectableIds = (await _repository.GetSelectableSimulatedItemsAsync(existing.Id))
                .Select(item => item.FilingFactId)
                .ToHashSet();
            // 当前单据内的事实在 exclude 后应仍可选；若库内状态已变则 selectable 不含
            foreach (var item in existing.Items)
            {
                if (!selectableIds.Contains(item.FilingFactId))
                {
                    // 重新校验事实状态
                    var facts = await _repository.GetFilingFactsByIdsAsync([item.FilingFactId]);
                    var fact = facts.FirstOrDefault();
                    if (fact == null
                        || (fact.InventoryLostCopyCount <= 0 && fact.InventoryScrapCopyCount <= 0)
                        || string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.Disposed, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"明细「{ResolveItemDisplay(item)}」已不可处置，请退回草稿调整。");
                    }
                }
            }

            return;
        }

        var selectableKeys = (await _repository.GetSelectableElectronicItemsAsync(existing.Id))
            .Select(item => item.SelectionKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var item in existing.Items)
        {
            string key = $"M:{item.MediumKind}:{item.MediumId}";
            if (!selectableKeys.Contains(key))
            {
                throw new InvalidOperationException($"明细「{ResolveItemDisplay(item)}」已不可处置，请退回草稿调整。");
            }
        }
    }

    /// <summary>
    /// 办结前校验低格留盘目标档口：必须已由用户录入，不做自动推荐与预占用。
    /// </summary>
    private static void ValidateBlankSlotsForFormatRetain(YearlyArchiveDisposalRecord existing)
    {
        foreach (var item in existing.Items.Where(i =>
                     ArchiveDisposalDomainValues.IsFormatRetainMethod(i.DispositionMethod)))
        {
            string target = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.TargetBlankSlotLocation);
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidOperationException(
                    $"硬盘「{item.MediumCode}」为低格留盘，办结前须填写目标空盘档口。");
            }

            item.TargetBlankSlotLocation = target;
            item.DispositionMethod = ArchiveDisposalDomainValues.NormalizeDispositionMethod(item.DispositionMethod);
        }
    }

    private async Task LockHardDiskMediaAsync(YearlyArchiveDisposalRecord existing, DateTime now)
    {
        var hdItems = existing.Items
            .Where(item => string.Equals(
                item.MediumKind,
                ArchiveInventoryRegisterDomainValues.MediumKindHardDisk,
                StringComparison.Ordinal))
            .ToList();
        if (hdItems.Count == 0)
        {
            return;
        }

        var media = await _repository.GetHardDiskMediaWithLedgerByIdsAsync(hdItems.Select(i => i.MediumId).ToList());
        foreach (var item in hdItems)
        {
            var medium = media.FirstOrDefault(m => m.Id == item.MediumId)
                ?? throw new InvalidOperationException($"未找到硬盘「{item.MediumCode}」。");

            if (medium.RegisterLock != null
                && !(string.Equals(
                         medium.RegisterLock.BusinessType,
                         HardDiskRegisterLock.BusinessTypeArchiveDisposal,
                         StringComparison.Ordinal)
                     && medium.RegisterLock.BusinessRecordId == existing.Id))
            {
                throw new InvalidOperationException(
                    $"硬盘「{medium.DiskCode}」已被其他业务征用，无法提交。");
            }

            if (medium.RegisterLock == null)
            {
                _repository.AddRegisterLock(new HardDiskRegisterLock
                {
                    MediumId = medium.Id,
                    BusinessType = HardDiskRegisterLock.BusinessTypeArchiveDisposal,
                    BusinessRecordId = existing.Id,
                    BusinessNo = existing.DisposalNo,
                    PreviousStatus = medium.Ledger?.MediaStatus ?? item.BeforeMediaStatus,
                    LockedTime = now
                });
            }
        }
    }

    private async Task UnlockHardDiskMediaIfOwnedAsync(YearlyArchiveDisposalRecord existing)
    {
        var hdIds = existing.Items
            .Where(item => string.Equals(
                item.MediumKind,
                ArchiveInventoryRegisterDomainValues.MediumKindHardDisk,
                StringComparison.Ordinal))
            .Select(item => item.MediumId)
            .Distinct()
            .ToList();
        if (hdIds.Count == 0)
        {
            return;
        }

        var media = await _repository.GetHardDiskMediaWithLedgerByIdsAsync(hdIds);
        foreach (var medium in media)
        {
            if (medium.RegisterLock != null
                && string.Equals(
                    medium.RegisterLock.BusinessType,
                    HardDiskRegisterLock.BusinessTypeArchiveDisposal,
                    StringComparison.Ordinal)
                && medium.RegisterLock.BusinessRecordId == existing.Id)
            {
                _repository.RemoveRegisterLock(medium.RegisterLock);
                medium.RegisterLock = null;
            }
        }
    }
}
