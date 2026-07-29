using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.YearlyArchive;

public sealed partial class ArchiveInventoryRegisterService
{
    private async Task<List<YearlyArchiveInventoryRegisterItem>> BuildElectronicItemsAsync(
        string registerKind,
        IReadOnlyList<ArchiveInventoryRegisterItemDraft> drafts,
        int? excludeRecordId,
        DateTime now)
    {
        if (drafts == null || drafts.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一块袋内介质。");
        }

        var selectable = await _repository.GetSelectableElectronicMediaAsync(excludeMedia: null, excludeRecordId);

        int sort = 1;
        var items = new List<YearlyArchiveInventoryRegisterItem>();
        var seenMediaKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in drafts)
        {
            string mediumKind = draft.MediumKind?.Trim() ?? string.Empty;
            if (!ArchiveInventoryRegisterDomainValues.IsValidMediumKind(mediumKind) || draft.MediumId <= 0)
            {
                throw new InvalidOperationException("所选电子介质无效。");
            }

            string mediaKey = $"{mediumKind}:{draft.MediumId}";
            if (!seenMediaKeys.Add(mediaKey))
            {
                throw new InvalidOperationException("所选电子介质存在重复。");
            }

            var candidate = selectable.FirstOrDefault(item =>
                string.Equals(item.MediumKind, mediumKind, StringComparison.Ordinal)
                && item.MediumId == draft.MediumId);

            if (candidate == null)
            {
                throw new InvalidOperationException($"介质【{mediumKind} #{draft.MediumId}】不可纳入盘库登记。");
            }

            if (await _repository.ExistsActiveArchiveInventoryForMediumAsync(mediumKind, draft.MediumId, excludeRecordId))
            {
                throw new InvalidOperationException($"介质【{candidate.MediumCode}】已存在未办结的盘库登记明细。");
            }

            if (string.Equals(mediumKind, ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, StringComparison.Ordinal))
            {
                if (await _repository.ExistsActiveHardDiskInventoryOrDisposalForMediumAsync(draft.MediumId))
                {
                    throw new InvalidOperationException($"硬盘【{candidate.MediumCode}】已存在未办结的硬盘盘库或离库处置单。");
                }

                var hardDisk = await _repository.GetHardDiskWithLedgerAsync(draft.MediumId);
                ValidateHardDiskLock(hardDisk, excludeRecordId, candidate.MediumCode);
            }

            items.Add(new YearlyArchiveInventoryRegisterItem
            {
                SortOrder = sort++,
                MediumKind = mediumKind,
                MediumId = draft.MediumId,
                MediumCode = candidate.MediumCode,
                ElectronicArchiveUnitId = candidate.ElectronicArchiveUnitId,
                ElectronicArchiveNo = candidate.ElectronicArchiveNo,
                BeforeMediaStatus = candidate.BeforeMediaStatus,
                BeforeStorageLocation = candidate.BeforeStorageLocation,
                MaterialName = candidate.MaterialName,
                ItemName = candidate.ItemName,
                CreatedAt = now,
            });
        }

        return items;
    }

    private async Task CompleteElectronicAsync(
        YearlyArchiveInventoryRegisterRecord record,
        string operatorName,
        DateTime now)
    {
        foreach (var item in record.Items.OrderBy(detail => detail.SortOrder))
        {
            if (string.Equals(item.MediumKind, ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, StringComparison.Ordinal))
            {
                await CompleteElectronicHardDiskAsync(record, item, operatorName, now);
            }
            else if (string.Equals(item.MediumKind, ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc, StringComparison.Ordinal))
            {
                await CompleteElectronicOpticalDiscAsync(record, item, operatorName, now);
            }
            else
            {
                throw new InvalidOperationException($"不支持的电子介质类别：{item.MediumKind}");
            }

            var relatedFacts = await _repository.GetElectronicFilingFactsByMediumAsync(
                item.MediumKind,
                item.MediumId,
                item.ElectronicArchiveUnitId);

            foreach (var fact in relatedFacts)
            {
                string beforeLifecycle = fact.LifecycleStatus?.Trim() ?? string.Empty;
                fact.LifecycleStatus = FilingFactLifecycleStatus.Destroyed;
                fact.BorrowHintLevel = FilingFactBorrowHintLevel.None;
                fact.BorrowHintText = string.Empty;
                fact.BorrowHintUpdatedAt = now;
                fact.LifecycleUpdatedAt = now;
                fact.LifecycleRemark = $"盘库登记 {record.RegisterNo}：袋内介质已{record.RegisterKind}";

                _repository.AddMaterialTransaction(BuildInventoryMaterialTransaction(
                    record,
                    item,
                    fact,
                    beforeLifecycle,
                    fact.LifecycleStatus,
                    operatorName,
                    now,
                    $"{item.MediumCode} {record.RegisterKind}"));
            }
        }
    }

    private async Task CompleteElectronicHardDiskAsync(
        YearlyArchiveInventoryRegisterRecord record,
        YearlyArchiveInventoryRegisterItem item,
        string operatorName,
        DateTime now)
    {
        var medium = await _repository.GetHardDiskWithLedgerAsync(item.MediumId)
            ?? throw new InvalidOperationException($"硬盘【{item.MediumCode}】不存在。");

        var ledger = EnsureHardDiskLedger(medium, now);
        string beforeStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
        string beforeLocation = ledger.StorageLocation?.Trim() ?? string.Empty;
        string afterStatus = ArchiveInventoryRegisterDomainValues.ResolveHardDiskAfterMediaStatus(record.RegisterKind, beforeStatus);

        medium.UpdatedTime = now;
        ledger.UpdatedTime = now;
        ledger.DiskCode = medium.DiskCode;
        ledger.MediaStatus = afterStatus;
        ledger.NeedReturn = false;
        ledger.StorageLocation = beforeLocation;

        _repository.AddHardDiskTransaction(new HardDiskMediaTransaction
        {
            MediumId = medium.Id,
            ApplicationId = null,
            TransactionType = ArchiveInventoryRegisterDomainValues.ResolveHardDiskTransactionType(record.RegisterKind),
            BeforeStatus = beforeStatus,
            AfterStatus = afterStatus,
            BeforeLocation = beforeLocation,
            AfterLocation = beforeLocation,
            OperatorName = operatorName,
            OperateTime = now,
            RelatedPerson = record.ApplicantName,
            TargetOrganization = "资料室",
            NeedReturn = false,
            RelatedBatch = record.RegisterNo,
            Description = $"资料盘库登记：{record.RegisterKind}",
            Remark = record.Remark
        });
    }

    private async Task CompleteElectronicOpticalDiscAsync(
        YearlyArchiveInventoryRegisterRecord record,
        YearlyArchiveInventoryRegisterItem item,
        string operatorName,
        DateTime now)
    {
        var medium = await _repository.GetOpticalDiscWithLedgerAsync(item.MediumId)
            ?? throw new InvalidOperationException($"光盘【{item.MediumCode}】不存在。");

        var ledger = EnsureOpticalDiscLedger(medium, now);
        string beforeStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
        string beforeLocation = ledger.StorageLocation?.Trim() ?? string.Empty;
        string afterStatus = ArchiveInventoryRegisterDomainValues.ResolveOpticalDiscAfterMediaStatus(record.RegisterKind, beforeStatus);

        medium.UpdatedTime = now;
        ledger.UpdatedTime = now;
        ledger.DiscCode = medium.DiscCode;
        ledger.MediaStatus = afterStatus;
        ledger.NeedReturn = false;
        ledger.StorageLocation = beforeLocation;

        _repository.AddOpticalDiscTransaction(new OpticalDiscMediaTransaction
        {
            MediumId = medium.Id,
            ApplicationId = null,
            TransactionType = ArchiveInventoryRegisterDomainValues.ResolveOpticalDiscTransactionType(record.RegisterKind),
            BusinessNo = record.RegisterNo,
            BeforeStatus = beforeStatus,
            AfterStatus = afterStatus,
            BeforeLocation = beforeLocation,
            AfterLocation = beforeLocation,
            OperatorName = operatorName,
            OperateTime = now,
            RelatedPerson = record.ApplicantName,
            TargetOrganization = "资料室",
            NeedReturn = false,
            RelatedBatch = record.RegisterNo,
            Description = $"资料盘库登记：{record.RegisterKind}",
            Remark = record.Remark
        });
    }

    private async Task<List<HardDiskMedium>> LoadHardDisksForElectronicDraftAsync(
        string mediaKind,
        IReadOnlyList<ArchiveInventoryRegisterItemDraft> drafts)
    {
        if (!string.Equals(mediaKind?.Trim(), ArchiveInventoryRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
        {
            return [];
        }

        List<int> ids = drafts
            .Where(item => string.Equals(item.MediumKind, ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, StringComparison.Ordinal))
            .Select(item => item.MediumId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        return ids.Count == 0 ? [] : await _repository.GetHardDisksWithLedgerByIdsAsync(ids);
    }

    private async Task<List<HardDiskMedium>> LoadHardDisksFromExistingItemsAsync(YearlyArchiveInventoryRegisterRecord record)
    {
        List<int> ids = record.Items
            .Where(item => string.Equals(item.MediumKind, ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, StringComparison.Ordinal))
            .Select(item => item.MediumId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        return ids.Count == 0 ? [] : await _repository.GetHardDisksWithLedgerByIdsAsync(ids);
    }

    private static void ValidateHardDiskLock(HardDiskMedium? medium, int? excludeRecordId, string mediumCode)
    {
        if (medium?.RegisterLock == null)
        {
            return;
        }

        bool ownedByCurrent = excludeRecordId.HasValue
            && string.Equals(medium.RegisterLock.BusinessType, HardDiskRegisterLock.BusinessTypeArchiveInventoryRegister, StringComparison.Ordinal)
            && medium.RegisterLock.BusinessRecordId == excludeRecordId.Value;

        if (!ownedByCurrent)
        {
            string lockOwner = string.IsNullOrWhiteSpace(medium.RegisterLock.BusinessNo)
                ? medium.RegisterLock.BusinessType
                : $"{medium.RegisterLock.BusinessType}（{medium.RegisterLock.BusinessNo.Trim()}）";
            throw new InvalidOperationException(
                $"硬盘【{mediumCode}】已被其他业务征用：{lockOwner}，不可纳入盘库登记。");
        }
    }

    private static void LockHardDisksIfNeeded(
        YearlyArchiveInventoryRegisterRecord record,
        IReadOnlyList<HardDiskMedium> media,
        DateTime now)
    {
        foreach (var medium in media)
        {
            if (medium.RegisterLock != null)
            {
                if (!string.Equals(medium.RegisterLock.BusinessType, HardDiskRegisterLock.BusinessTypeArchiveInventoryRegister, StringComparison.Ordinal)
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
                BusinessType = HardDiskRegisterLock.BusinessTypeArchiveInventoryRegister,
                BusinessRecordId = record.Id,
                BusinessNo = record.RegisterNo,
                PreviousStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty,
                LockedTime = now
            };
            medium.UpdatedTime = now;
        }
    }

    private void UnlockHardDisksIfOwned(YearlyArchiveInventoryRegisterRecord record, IReadOnlyList<HardDiskMedium> media)
    {
        foreach (var medium in media)
        {
            var lockItem = medium.RegisterLock;
            if (lockItem == null)
            {
                continue;
            }

            if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeArchiveInventoryRegister, StringComparison.Ordinal)
                || lockItem.BusinessRecordId != record.Id)
            {
                continue;
            }

            _repository.RemoveRegisterLock(lockItem);
            medium.RegisterLock = null;
            medium.UpdatedTime = DateTime.Now;
        }
    }

    private static HardDiskLedger EnsureHardDiskLedger(HardDiskMedium medium, DateTime now)
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

    private static OpticalDiscLedger EnsureOpticalDiscLedger(OpticalDiscMedium medium, DateTime now)
    {
        if (medium.Ledger != null)
        {
            return medium.Ledger;
        }

        medium.Ledger = new OpticalDiscLedger
        {
            MediumId = medium.Id,
            DiscCode = medium.DiscCode,
            MediaStatus = OpticalDiscMedium.StatusInStock,
            StorageLocation = string.Empty,
            HolderOrOrganization = string.Empty,
            NeedReturn = false,
            RegisterPerson = medium.RegisterPerson,
            RegisterDate = medium.RegisterDate,
            Remark = medium.Remarks,
            CreatedTime = now,
            UpdatedTime = now
        };
        return medium.Ledger;
    }
}
