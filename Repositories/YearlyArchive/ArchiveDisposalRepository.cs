using DocMgr.Data;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive;

/// <summary>
/// 年度资料离库处置仓储。
/// </summary>
public sealed class ArchiveDisposalRepository : IArchiveDisposalRepository
{
    private static readonly int[] ActiveStatuses =
    [
        YearlyArchiveDisposalRecord.StatusDraft,
        YearlyArchiveDisposalRecord.StatusSubmitted,
        YearlyArchiveDisposalRecord.StatusApproved,
        YearlyArchiveDisposalRecord.StatusSignedUploaded
    ];

    private readonly AppDbContext _dbContext;

    public ArchiveDisposalRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<YearlyArchiveDisposalRecord>> SearchRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear,
        string? mediaKind)
    {
        IQueryable<YearlyArchiveDisposalRecord> query = _dbContext.YearlyArchiveDisposalRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(mediaKind))
        {
            string kind = mediaKind.Trim();
            query = query.Where(item => item.MediaKind == kind);
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (applyYear.HasValue)
        {
            int year = applyYear.Value;
            query = query.Where(item => item.ApplyTime.Year == year);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string trimmed = keyword.Trim();
            query = query.Where(item =>
                item.DisposalNo.Contains(trimmed)
                || item.ApplicantName.Contains(trimmed)
                || item.DisposalReason.Contains(trimmed)
                || item.DispositionMethod.Contains(trimmed)
                || item.Items.Any(detail =>
                    detail.ContainerCode.Contains(trimmed)
                    || detail.MaterialName.Contains(trimmed)
                    || detail.ItemName.Contains(trimmed)
                    || detail.MediumCode.Contains(trimmed)
                    || detail.ElectronicArchiveNo.Contains(trimmed)
                    || detail.FormNo.Contains(trimmed)));
        }

        return await query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<YearlyArchiveDisposalRecord?> GetRecordByIdAsync(int recordId)
    {
        return _dbContext.YearlyArchiveDisposalRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public Task<YearlyArchiveDisposalRecord?> GetRecordByIdForUpdateAsync(int recordId)
    {
        return _dbContext.YearlyArchiveDisposalRecords
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public async Task<string?> GetLastDisposalNoByPrefixAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        string trimmed = prefix.Trim();
        return await _dbContext.YearlyArchiveDisposalRecords
            .AsNoTracking()
            .Where(item => item.DisposalNo.StartsWith(trimmed))
            .OrderByDescending(item => item.DisposalNo)
            .Select(item => item.DisposalNo)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ArchiveDisposalSelectableItem>> GetSelectableSimulatedItemsAsync(int? excludeRecordId = null)
    {
        var activeFactIds = await _dbContext.YearlyArchiveDisposalItems
            .AsNoTracking()
            .Where(item => ActiveStatuses.Contains(item.DisposalRecord!.Status)
                           && item.FilingFactId > 0
                           && (excludeRecordId == null
                               || excludeRecordId.Value <= 0
                               || item.DisposalRecordId != excludeRecordId.Value))
            .Select(item => item.FilingFactId)
            .Distinct()
            .ToListAsync();

        HashSet<int> busy = activeFactIds.ToHashSet();

        var facts = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(item => item.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated)
            .Where(item => item.LifecycleStatus != FilingFactLifecycleStatus.Disposed)
            .Where(item => item.InventoryLostCopyCount > 0 || item.InventoryScrapCopyCount > 0)
            .Where(item => !busy.Contains(item.Id))
            .OrderBy(item => item.ContainerCode)
            .ThenBy(item => item.ItemName)
            .ToListAsync();

        return facts.Select(fact =>
        {
            string registerKind = fact.InventoryScrapCopyCount > 0
                ? ArchiveInventoryRegisterDomainValues.KindScrap
                : ArchiveInventoryRegisterDomainValues.KindLost;
            return new ArchiveDisposalSelectableItem
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                FilingFactId = fact.Id,
                ContainerId = fact.ContainerId,
                ContainerCode = fact.ContainerCode ?? string.Empty,
                BeforeStorageLocation = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? (fact.StorageLocation ?? string.Empty)
                    : fact.CurrentStorageLocation,
                SourceRegisterKind = registerKind,
                DisposalReason = ArchiveDisposalDomainValues.ResolveReasonFromRegisterKind(registerKind),
                MaterialName = fact.MaterialName ?? string.Empty,
                ItemName = fact.ItemName ?? string.Empty,
                FormNo = fact.FormNo ?? string.Empty,
                InventoryLostCopyCount = fact.InventoryLostCopyCount,
                InventoryScrapCopyCount = fact.InventoryScrapCopyCount,
                BeforeLifecycleStatus = fact.LifecycleStatus ?? string.Empty
            };
        }).ToList();
    }

    public async Task<List<ArchiveDisposalSelectableItem>> GetSelectableElectronicItemsAsync(int? excludeRecordId = null)
    {
        var activeMediumKeys = await _dbContext.YearlyArchiveDisposalItems
            .AsNoTracking()
            .Where(item => ActiveStatuses.Contains(item.DisposalRecord!.Status)
                           && item.MediumId > 0
                           && (excludeRecordId == null
                               || excludeRecordId.Value <= 0
                               || item.DisposalRecordId != excludeRecordId.Value))
            .Select(item => new { item.MediumKind, item.MediumId })
            .ToListAsync();

        HashSet<string> busyKeys = activeMediumKeys
            .Select(item => $"{item.MediumKind}:{item.MediumId}")
            .ToHashSet(StringComparer.Ordinal);

        var hdActiveDisposalIds = await _dbContext.HardDiskDisposalItems
            .AsNoTracking()
            .Where(item => item.DisposalRecord!.Status == HardDiskDisposalRecord.StatusDraft
                           || item.DisposalRecord.Status == HardDiskDisposalRecord.StatusSubmitted
                           || item.DisposalRecord.Status == HardDiskDisposalRecord.StatusApproved
                           || item.DisposalRecord.Status == HardDiskDisposalRecord.StatusSignedUploaded)
            .Select(item => item.MediumId)
            .Distinct()
            .ToListAsync();
        HashSet<int> hdBusy = hdActiveDisposalIds.ToHashSet();

        var result = new List<ArchiveDisposalSelectableItem>();

        var hdRows = await (
            from link in _dbContext.YearlyElectronicArchiveUnitMediumLinks.AsNoTracking()
            join unit in _dbContext.YearlyElectronicArchiveUnits.AsNoTracking()
                on link.YearlyElectronicArchiveUnitId equals unit.Id
            join medium in _dbContext.HardDiskMedia.AsNoTracking()
                on link.HardDiskMediumId equals medium.Id
            join ledger in _dbContext.HardDiskLedgers.AsNoTracking()
                on medium.Id equals ledger.MediumId
            where unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                  && !medium.IsDeleted
                  && (ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged
                      || ledger.MediaStatus == HardDiskMedium.StatusInStockLost
                      || ledger.MediaStatus == HardDiskMedium.StatusInStockScrap)
            select new
            {
                Unit = unit,
                Medium = medium,
                Ledger = ledger
            }).ToListAsync();

        foreach (var row in hdRows)
        {
            string key = $"{ArchiveInventoryRegisterDomainValues.MediumKindHardDisk}:{row.Medium.Id}";
            if (busyKeys.Contains(key) || hdBusy.Contains(row.Medium.Id))
            {
                continue;
            }

            var lockItem = await _dbContext.HardDiskRegisterLocks
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.MediumId == row.Medium.Id);
            if (lockItem != null)
            {
                bool ownedByCurrent = excludeRecordId.HasValue
                    && excludeRecordId.Value > 0
                    && string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeArchiveDisposal, StringComparison.Ordinal)
                    && lockItem.BusinessRecordId == excludeRecordId.Value;
                if (!ownedByCurrent)
                {
                    continue;
                }
            }

            string reason = ArchiveDisposalDomainValues.ResolveReasonFromMediaStatus(row.Ledger.MediaStatus);
            string registerKind = ResolveRegisterKindFromReason(reason);
            result.Add(new ArchiveDisposalSelectableItem
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                ContainerId = row.Unit.Id,
                ContainerCode = row.Unit.ElectronicArchiveNo ?? string.Empty,
                BeforeStorageLocation = row.Ledger.StorageLocation ?? row.Unit.StorageLocation ?? string.Empty,
                SourceRegisterKind = registerKind,
                DisposalReason = reason,
                MediumKind = ArchiveInventoryRegisterDomainValues.MediumKindHardDisk,
                MediumId = row.Medium.Id,
                MediumCode = row.Medium.DiskCode ?? string.Empty,
                ElectronicArchiveUnitId = row.Unit.Id,
                ElectronicArchiveNo = row.Unit.ElectronicArchiveNo ?? string.Empty,
                BeforeMediaStatus = row.Ledger.MediaStatus ?? string.Empty
            });
        }

        var odRows = await (
            from link in _dbContext.YearlyElectronicArchiveUnitDiscLinks.AsNoTracking()
            join unit in _dbContext.YearlyElectronicArchiveUnits.AsNoTracking()
                on link.YearlyElectronicArchiveUnitId equals unit.Id
            join medium in _dbContext.OpticalDiscMedia.AsNoTracking()
                on link.OpticalDiscMediumId equals medium.Id
            join ledger in _dbContext.OpticalDiscLedgers.AsNoTracking()
                on medium.Id equals ledger.MediumId
            where unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                  && !medium.IsDeleted
                  && (ledger.MediaStatus == OpticalDiscMedium.StatusDamaged
                      || ledger.MediaStatus == OpticalDiscMedium.StatusLost
                      || ledger.MediaStatus == OpticalDiscMedium.StatusScrap)
            select new
            {
                Unit = unit,
                Medium = medium,
                Ledger = ledger
            }).ToListAsync();

        foreach (var row in odRows)
        {
            string key = $"{ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc}:{row.Medium.Id}";
            if (busyKeys.Contains(key))
            {
                continue;
            }

            string reason = ArchiveDisposalDomainValues.ResolveReasonFromMediaStatus(row.Ledger.MediaStatus);
            string registerKind = ResolveRegisterKindFromReason(reason);
            result.Add(new ArchiveDisposalSelectableItem
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                ContainerId = row.Unit.Id,
                ContainerCode = row.Unit.ElectronicArchiveNo ?? string.Empty,
                BeforeStorageLocation = row.Ledger.StorageLocation ?? row.Unit.StorageLocation ?? string.Empty,
                SourceRegisterKind = registerKind,
                DisposalReason = reason,
                MediumKind = ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc,
                MediumId = row.Medium.Id,
                MediumCode = row.Medium.DiscCode ?? string.Empty,
                ElectronicArchiveUnitId = row.Unit.Id,
                ElectronicArchiveNo = row.Unit.ElectronicArchiveNo ?? string.Empty,
                BeforeMediaStatus = row.Ledger.MediaStatus ?? string.Empty
            });
        }

        return result
            .OrderBy(item => item.ElectronicArchiveNo, StringComparer.Ordinal)
            .ThenBy(item => item.MediumKind, StringComparer.Ordinal)
            .ThenBy(item => item.MediumCode, StringComparer.Ordinal)
            .ToList();
    }

    private static string ResolveRegisterKindFromReason(string reason)
    {
        if (string.Equals(reason, ArchiveDisposalDomainValues.ReasonDamaged, StringComparison.Ordinal))
        {
            return ArchiveInventoryRegisterDomainValues.KindDamage;
        }

        if (string.Equals(reason, ArchiveDisposalDomainValues.ReasonScrap, StringComparison.Ordinal))
        {
            return ArchiveInventoryRegisterDomainValues.KindScrap;
        }

        return ArchiveInventoryRegisterDomainValues.KindLost;
    }

    public async Task<List<YearlyArchiveFilingFact>> GetFilingFactsByIdsAsync(IReadOnlyCollection<int> filingFactIds)
    {
        List<int> ids = filingFactIds?.Where(id => id > 0).Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.YearlyArchiveFilingFacts
            .Where(item => ids.Contains(item.Id))
            .ToListAsync();
    }

    public async Task<List<YearlyArchiveBox>> GetBoxesByIdsAsync(IReadOnlyCollection<int> boxIds)
    {
        List<int> ids = boxIds?.Where(id => id > 0).Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.YearlyArchiveBoxes
            .Where(item => ids.Contains(item.Id))
            .ToListAsync();
    }

    public async Task<List<YearlyElectronicArchiveUnit>> GetElectronicUnitsByIdsAsync(IReadOnlyCollection<int> unitIds)
    {
        List<int> ids = unitIds?.Where(id => id > 0).Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.YearlyElectronicArchiveUnits
            .Where(item => ids.Contains(item.Id))
            .ToListAsync();
    }

    public async Task<List<HardDiskMedium>> GetHardDiskMediaWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds)
    {
        List<int> ids = mediumIds?.Where(id => id > 0).Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted && ids.Contains(item.Id))
            .ToListAsync();
    }

    public async Task<List<OpticalDiscMedium>> GetOpticalDiscMediaWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds)
    {
        List<int> ids = mediumIds?.Where(id => id > 0).Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.OpticalDiscMedia
            .Include(item => item.Ledger)
            .Where(item => !item.IsDeleted && ids.Contains(item.Id))
            .ToListAsync();
    }

    public Task<List<YearlyArchiveFilingFact>> GetFilingFactsByContainerIdAsync(int containerId, string mediaKind)
    {
        string kind = mediaKind?.Trim() ?? string.Empty;
        return _dbContext.YearlyArchiveFilingFacts
            .Where(item => item.ContainerId == containerId && item.MediaKind == kind)
            .ToListAsync();
    }

    public Task<List<YearlyElectronicArchiveUnitMediumLink>> GetHardDiskLinksByUnitIdAsync(int unitId)
    {
        return _dbContext.YearlyElectronicArchiveUnitMediumLinks
            .Where(item => item.YearlyElectronicArchiveUnitId == unitId)
            .ToListAsync();
    }

    public Task<List<YearlyElectronicArchiveUnitDiscLink>> GetDiscLinksByUnitIdAsync(int unitId)
    {
        return _dbContext.YearlyElectronicArchiveUnitDiscLinks
            .Where(item => item.YearlyElectronicArchiveUnitId == unitId)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveFilingFact>> GetFilingFactsByElectronicUnitIdAsync(int unitId)
    {
        return _dbContext.YearlyArchiveFilingFacts
            .Where(item => item.ContainerId == unitId
                           && item.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic)
            .ToListAsync();
    }

    public Task<bool> ExistsActiveDisposalForFilingFactAsync(int filingFactId, int? excludeRecordId = null)
    {
        IQueryable<YearlyArchiveDisposalItem> query = _dbContext.YearlyArchiveDisposalItems
            .AsNoTracking()
            .Where(item => item.FilingFactId == filingFactId
                           && ActiveStatuses.Contains(item.DisposalRecord!.Status));

        if (excludeRecordId.HasValue && excludeRecordId.Value > 0)
        {
            int excludeId = excludeRecordId.Value;
            query = query.Where(item => item.DisposalRecordId != excludeId);
        }

        return query.AnyAsync();
    }

    public Task<bool> ExistsActiveDisposalForMediumAsync(string mediumKind, int mediumId, int? excludeRecordId = null)
    {
        string kind = mediumKind?.Trim() ?? string.Empty;
        IQueryable<YearlyArchiveDisposalItem> query = _dbContext.YearlyArchiveDisposalItems
            .AsNoTracking()
            .Where(item => item.MediumId == mediumId
                           && item.MediumKind == kind
                           && ActiveStatuses.Contains(item.DisposalRecord!.Status));

        if (excludeRecordId.HasValue && excludeRecordId.Value > 0)
        {
            int excludeId = excludeRecordId.Value;
            query = query.Where(item => item.DisposalRecordId != excludeId);
        }

        return query.AnyAsync();
    }

    public Task<List<YearlyArchiveDisposalRecord>> GetPendingRecordsForToDoAsync(int takeCount)
    {
        int take = Math.Max(1, takeCount);
        return _dbContext.YearlyArchiveDisposalRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .Where(item => item.Status == YearlyArchiveDisposalRecord.StatusSubmitted
                           || item.Status == YearlyArchiveDisposalRecord.StatusApproved
                           || item.Status == YearlyArchiveDisposalRecord.StatusSignedUploaded)
            .OrderBy(item => item.SubmittedAt)
            .ThenBy(item => item.Id)
            .Take(take)
            .ToListAsync();
    }

    public Task<List<SystemAttachment>> GetAttachmentsAsync(string disposalNo)
    {
        string trimmed = disposalNo?.Trim() ?? string.Empty;
        return _dbContext.SystemAttachments
            .AsNoTracking()
            .Where(item => item.BusinessType == ArchiveDisposalDomainValues.AttachmentBusinessType
                           && item.BusinessNo == trimmed)
            .OrderByDescending(item => item.UploadTime)
            .ToListAsync();
    }

    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
    {
        return _dbContext.SystemAttachments
            .FirstOrDefaultAsync(item => item.Id == attachmentId);
    }

    public void AddRecord(YearlyArchiveDisposalRecord record) => _dbContext.YearlyArchiveDisposalRecords.Add(record);

    public void RemoveItems(IEnumerable<YearlyArchiveDisposalItem> items) =>
        _dbContext.YearlyArchiveDisposalItems.RemoveRange(items);

    public void AddMaterialTransaction(YearlyArchiveMaterialTransaction transaction) =>
        _dbContext.YearlyArchiveMaterialTransactions.Add(transaction);

    public void AddHardDiskTransaction(HardDiskMediaTransaction transaction) =>
        _dbContext.HardDiskMediaTransactions.Add(transaction);

    public void AddOpticalDiscTransaction(OpticalDiscMediaTransaction transaction) =>
        _dbContext.OpticalDiscMediaTransactions.Add(transaction);

    public void AddRegisterLock(HardDiskRegisterLock lockItem) =>
        _dbContext.HardDiskRegisterLocks.Add(lockItem);

    public void RemoveRegisterLock(HardDiskRegisterLock lockItem) =>
        _dbContext.HardDiskRegisterLocks.Remove(lockItem);

    public void RemoveArchiveBoxPlacementByBoxCode(string boxCode)
    {
        string trimmed = boxCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var placement = _dbContext.CabinetArchiveBoxPlacements
            .FirstOrDefault(item => item.BoxCode == trimmed);
        if (placement != null)
        {
            _dbContext.CabinetArchiveBoxPlacements.Remove(placement);
        }
    }

    public void RemoveHardDiskMediumLink(YearlyElectronicArchiveUnitMediumLink link) =>
        _dbContext.YearlyElectronicArchiveUnitMediumLinks.Remove(link);

    public void RemoveDiscLink(YearlyElectronicArchiveUnitDiscLink link) =>
        _dbContext.YearlyElectronicArchiveUnitDiscLinks.Remove(link);

    public void AddAttachment(SystemAttachment attachment) => _dbContext.SystemAttachments.Add(attachment);

    public void RemoveAttachment(SystemAttachment attachment) => _dbContext.SystemAttachments.Remove(attachment);

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
}
