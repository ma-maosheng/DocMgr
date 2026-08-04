using DocMgr.Data;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive;

/// <summary>
/// 年度资料盘库登记仓储。
/// </summary>
public sealed class ArchiveInventoryRegisterRepository : IArchiveInventoryRegisterRepository
{
    private static readonly int[] ActiveRegisterStatuses =
    [
        YearlyArchiveInventoryRegisterRecord.StatusDraft
    ];

    private static readonly int[] ActiveHardDiskInventoryStatuses =
    [
        HardDiskInventoryRegisterRecord.StatusDraft
    ];

    private static readonly int[] ActiveDisposalStatuses =
    [
        HardDiskDisposalRecord.StatusDraft,
        HardDiskDisposalRecord.StatusSubmitted,
        HardDiskDisposalRecord.StatusApproved,
        HardDiskDisposalRecord.StatusSignedUploaded
    ];

    private readonly AppDbContext _dbContext;

    public ArchiveInventoryRegisterRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<YearlyArchiveInventoryRegisterRecord>> SearchRecordsAsync(
        string? mediaKind,
        string? keyword,
        int? status,
        int? applyYear)
    {
        IQueryable<YearlyArchiveInventoryRegisterRecord> query = _dbContext.YearlyArchiveInventoryRegisterRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(mediaKind))
        {
            string trimmedMediaKind = mediaKind.Trim();
            query = query.Where(item => item.MediaKind == trimmedMediaKind);
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
                item.RegisterNo.Contains(trimmed)
                || item.ApplicantName.Contains(trimmed)
                || item.RegisterKind.Contains(trimmed)
                || item.Reason.Contains(trimmed)
                || item.Items.Any(detail =>
                    detail.MediumCode.Contains(trimmed)
                    || detail.MediumKind.Contains(trimmed)
                    || detail.ItemName.Contains(trimmed)
                    || detail.MaterialName.Contains(trimmed)
                    || detail.ContainerCode.Contains(trimmed)
                    || detail.ElectronicArchiveNo.Contains(trimmed)
                    || detail.BeforeStorageLocation.Contains(trimmed)));
        }

        return await query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<YearlyArchiveInventoryRegisterRecord?> GetRecordByIdAsync(int recordId)
    {
        return _dbContext.YearlyArchiveInventoryRegisterRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public Task<YearlyArchiveInventoryRegisterRecord?> GetRecordByIdForUpdateAsync(int recordId)
    {
        return _dbContext.YearlyArchiveInventoryRegisterRecords
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public async Task<string?> GetLastRegisterNoByPrefixAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        string trimmed = prefix.Trim();
        return await _dbContext.YearlyArchiveInventoryRegisterRecords
            .AsNoTracking()
            .Where(item => item.RegisterNo.StartsWith(trimmed))
            .OrderByDescending(item => item.RegisterNo)
            .Select(item => item.RegisterNo)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ArchiveInventorySelectableSimulatedFact>> GetSelectableSimulatedFilingFactsAsync(
        IReadOnlyCollection<int>? excludeFactIds = null)
    {
        HashSet<int> excluded = excludeFactIds == null
            ? new HashSet<int>()
            : excludeFactIds.Where(id => id > 0).ToHashSet();

        var activeFactIds = await _dbContext.YearlyArchiveInventoryRegisterItems
            .AsNoTracking()
            .Where(item => ActiveRegisterStatuses.Contains(item.RegisterRecord!.Status)
                           && item.FilingFactId > 0)
            .Select(item => item.FilingFactId)
            .Distinct()
            .ToListAsync();

        HashSet<int> busyFactIds = activeFactIds.ToHashSet();
        busyFactIds.ExceptWith(excluded);

        var facts = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated
                           && fact.LifecycleStatus == FilingFactLifecycleStatus.InArchive
                           && !busyFactIds.Contains(fact.Id))
            .OrderBy(fact => fact.ContainerCode)
            .ThenBy(fact => fact.ItemName)
            .ToListAsync();

        if (facts.Count == 0)
        {
            return new List<ArchiveInventorySelectableSimulatedFact>();
        }

        var snapshots = await LoadCopySnapshotsAsync(facts.Select(fact => fact.Id).ToList());

        List<int> projectIds = facts
            .Where(fact => fact.ProjectId.HasValue && fact.ProjectId.Value > 0)
            .Select(fact => fact.ProjectId!.Value)
            .Distinct()
            .ToList();

        Dictionary<int, string> projectYearById = await GetProjectImplementYearsByIdsAsync(projectIds);

        return facts
            .Select(fact =>
            {
                var snapshot = snapshots.GetValueOrDefault(fact.Id) ?? new SimulatedFilingFactCopyCountSnapshot
                {
                    InventoryLostCopyCount = Math.Max(0, fact.InventoryLostCopyCount),
                    InventoryScrapCopyCount = Math.Max(0, fact.InventoryScrapCopyCount)
                };
                int current = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                    fact.ContentCount,
                    snapshot);
                int available = SimulatedInArchiveCopyCountSupport.ResolveAvailableCopyCount(
                    current,
                    snapshot.InventoryScrapCopyCount);
                return new { Fact = fact, Available = available };
            })
            .Where(row => row.Available > 0)
            .Select(row => new ArchiveInventorySelectableSimulatedFact
            {
                FilingFactId = row.Fact.Id,
                ProjectName = row.Fact.ProjectName?.Trim() ?? string.Empty,
                Year = row.Fact.ProjectId.HasValue
                    ? projectYearById.GetValueOrDefault(row.Fact.ProjectId.Value, string.Empty).Trim()
                    : string.Empty,
                MaterialName = row.Fact.MaterialName?.Trim() ?? string.Empty,
                ItemName = row.Fact.ItemName?.Trim() ?? string.Empty,
                ContainerCode = !string.IsNullOrWhiteSpace(row.Fact.CurrentContainerCode)
                    ? row.Fact.CurrentContainerCode.Trim()
                    : row.Fact.ContainerCode?.Trim() ?? string.Empty,
                StorageLocation = !string.IsNullOrWhiteSpace(row.Fact.CurrentStorageLocation)
                    ? row.Fact.CurrentStorageLocation.Trim()
                    : row.Fact.StorageLocation?.Trim() ?? string.Empty,
                AvailableCopyCount = row.Available
            })
            .ToList();
    }

    public async Task<List<ArchiveInventorySelectableElectronicMedia>> GetSelectableElectronicMediaAsync(
        IReadOnlyCollection<ArchiveInventoryElectronicMediumKey>? excludeMedia = null,
        int? excludeRecordId = null)
    {
        HashSet<string> excludedKeys = excludeMedia == null
            ? new HashSet<string>(StringComparer.Ordinal)
            : excludeMedia
                .Where(item => item.MediumId > 0 && !string.IsNullOrWhiteSpace(item.MediumKind))
                .Select(item => $"{item.MediumKind.Trim()}:{item.MediumId}")
                .ToHashSet(StringComparer.Ordinal);

        var activeItemsQuery = _dbContext.YearlyArchiveInventoryRegisterItems
            .AsNoTracking()
            .Where(item => ActiveRegisterStatuses.Contains(item.RegisterRecord!.Status)
                           && item.MediumId > 0);

        if (excludeRecordId.HasValue && excludeRecordId.Value > 0)
        {
            int recordId = excludeRecordId.Value;
            activeItemsQuery = activeItemsQuery.Where(item => item.RegisterRecordId != recordId);
        }

        var activeItems = await activeItemsQuery
            .Select(item => new { item.MediumKind, item.MediumId })
            .ToListAsync();

        HashSet<string> busyKeys = activeItems
            .Select(item => $"{item.MediumKind}:{item.MediumId}")
            .ToHashSet(StringComparer.Ordinal);
        busyKeys.ExceptWith(excludedKeys);

        var hardDiskRows = await (
            from link in _dbContext.YearlyElectronicArchiveUnitMediumLinks.AsNoTracking()
            join unit in _dbContext.YearlyElectronicArchiveUnits.AsNoTracking()
                on link.YearlyElectronicArchiveUnitId equals unit.Id
            join medium in _dbContext.HardDiskMedia.AsNoTracking()
                on link.HardDiskMediumId equals medium.Id
            join ledger in _dbContext.HardDiskLedgers.AsNoTracking()
                on medium.Id equals ledger.MediumId
            where !medium.IsDeleted
                  && unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                  && ledger.MediaStatus == HardDiskMedium.StatusInStockData
            select new
            {
                MediumId = medium.Id,
                MediumCode = medium.DiskCode,
                unit.Id,
                unit.ElectronicArchiveNo,
                ledger.MediaStatus,
                ledger.StorageLocation,
                unit.ProjectName,
                unit.Year,
                unit.ContentSummary,
            }).ToListAsync();

        var opticalDiscRows = await (
            from link in _dbContext.YearlyElectronicArchiveUnitDiscLinks.AsNoTracking()
            join unit in _dbContext.YearlyElectronicArchiveUnits.AsNoTracking()
                on link.YearlyElectronicArchiveUnitId equals unit.Id
            join medium in _dbContext.OpticalDiscMedia.AsNoTracking()
                on link.OpticalDiscMediumId equals medium.Id
            join ledger in _dbContext.OpticalDiscLedgers.AsNoTracking()
                on medium.Id equals ledger.MediumId
            where !medium.IsDeleted
                  && unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                  && ledger.MediaStatus == OpticalDiscMedium.StatusInStock
            select new
            {
                MediumId = medium.Id,
                MediumCode = medium.DiscCode,
                unit.Id,
                unit.ElectronicArchiveNo,
                ledger.MediaStatus,
                ledger.StorageLocation,
                unit.ProjectName,
                unit.Year,
                unit.ContentSummary,
            }).ToListAsync();

        var results = new List<ArchiveInventorySelectableElectronicMedia>();

        foreach (var row in hardDiskRows)
        {
            string key = $"{ArchiveInventoryRegisterDomainValues.MediumKindHardDisk}:{row.MediumId}";
            if (busyKeys.Contains(key))
            {
                continue;
            }

            if (await ExistsActiveHardDiskInventoryOrDisposalForMediumAsync(row.MediumId))
            {
                continue;
            }

            results.Add(new ArchiveInventorySelectableElectronicMedia
            {
                MediumKind = ArchiveInventoryRegisterDomainValues.MediumKindHardDisk,
                MediumId = row.MediumId,
                MediumCode = row.MediumCode?.Trim() ?? string.Empty,
                ElectronicArchiveUnitId = row.Id,
                ElectronicArchiveNo = row.ElectronicArchiveNo?.Trim() ?? string.Empty,
                ProjectName = row.ProjectName?.Trim() ?? string.Empty,
                Year = row.Year?.Trim() ?? string.Empty,
                BeforeMediaStatus = row.MediaStatus?.Trim() ?? string.Empty,
                BeforeStorageLocation = row.StorageLocation?.Trim() ?? string.Empty,
                MaterialName = row.ProjectName?.Trim() ?? string.Empty,
                ItemName = row.ContentSummary?.Trim() ?? string.Empty,
            });
        }

        foreach (var row in opticalDiscRows)
        {
            string key = $"{ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc}:{row.MediumId}";
            if (busyKeys.Contains(key))
            {
                continue;
            }

            results.Add(new ArchiveInventorySelectableElectronicMedia
            {
                MediumKind = ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc,
                MediumId = row.MediumId,
                MediumCode = row.MediumCode?.Trim() ?? string.Empty,
                ElectronicArchiveUnitId = row.Id,
                ElectronicArchiveNo = row.ElectronicArchiveNo?.Trim() ?? string.Empty,
                ProjectName = row.ProjectName?.Trim() ?? string.Empty,
                Year = row.Year?.Trim() ?? string.Empty,
                BeforeMediaStatus = row.MediaStatus?.Trim() ?? string.Empty,
                BeforeStorageLocation = row.StorageLocation?.Trim() ?? string.Empty,
                MaterialName = row.ProjectName?.Trim() ?? string.Empty,
                ItemName = row.ContentSummary?.Trim() ?? string.Empty,
            });
        }

        return results
            .OrderBy(item => item.ElectronicArchiveNo, StringComparer.Ordinal)
            .ThenBy(item => item.MediumCode, StringComparer.Ordinal)
            .ToList();
    }

    public Task<bool> ExistsActiveArchiveInventoryForMediumAsync(
        string mediumKind,
        int mediumId,
        int? excludeRecordId = null)
    {
        string normalizedKind = mediumKind?.Trim() ?? string.Empty;
        IQueryable<YearlyArchiveInventoryRegisterItem> query = _dbContext.YearlyArchiveInventoryRegisterItems
            .AsNoTracking()
            .Where(item => item.MediumId == mediumId
                           && item.MediumKind == normalizedKind
                           && ActiveRegisterStatuses.Contains(item.RegisterRecord!.Status));

        if (excludeRecordId.HasValue && excludeRecordId.Value > 0)
        {
            int excludeId = excludeRecordId.Value;
            query = query.Where(item => item.RegisterRecordId != excludeId);
        }

        return query.AnyAsync();
    }

    public Task<bool> ExistsActiveArchiveInventoryForFilingFactAsync(
        int filingFactId,
        int? excludeRecordId = null)
    {
        IQueryable<YearlyArchiveInventoryRegisterItem> query = _dbContext.YearlyArchiveInventoryRegisterItems
            .AsNoTracking()
            .Where(item => item.FilingFactId == filingFactId
                           && ActiveRegisterStatuses.Contains(item.RegisterRecord!.Status));

        if (excludeRecordId.HasValue && excludeRecordId.Value > 0)
        {
            int excludeId = excludeRecordId.Value;
            query = query.Where(item => item.RegisterRecordId != excludeId);
        }

        return query.AnyAsync();
    }

    public async Task<bool> ExistsActiveHardDiskInventoryOrDisposalForMediumAsync(int mediumId)
    {
        bool hasInventory = await _dbContext.HardDiskInventoryRegisterItems
            .AsNoTracking()
            .AnyAsync(item => item.MediumId == mediumId
                              && ActiveHardDiskInventoryStatuses.Contains(item.RegisterRecord!.Status));

        if (hasInventory)
        {
            return true;
        }

        return await _dbContext.HardDiskDisposalItems
            .AsNoTracking()
            .AnyAsync(item => item.MediumId == mediumId
                              && ActiveDisposalStatuses.Contains(item.DisposalRecord!.Status));
    }

    public async Task<List<YearlyArchiveFilingFact>> GetFactsWithDetailsAsync(IReadOnlyCollection<int> filingFactIds)
    {
        if (filingFactIds == null || filingFactIds.Count == 0)
        {
            return [];
        }

        List<int> ids = filingFactIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.YearlyArchiveFilingFacts
            .Where(fact => ids.Contains(fact.Id))
            .ToListAsync();
    }

    public async Task<Dictionary<int, string>> GetProjectImplementYearsByIdsAsync(IReadOnlyCollection<int> projectIds)
    {
        if (projectIds == null || projectIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        List<int> ids = projectIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _dbContext.ProjectInfos
            .AsNoTracking()
            .Where(project => ids.Contains(project.Id))
            .ToDictionaryAsync(
                project => project.Id,
                project => project.ImplementYear?.Trim() ?? string.Empty);
    }

    public async Task<List<YearlyArchiveFilingFact>> GetElectronicFilingFactsByMediumAsync(
        string mediumKind,
        int mediumId,
        int electronicArchiveUnitId)
    {
        string normalizedKind = mediumKind?.Trim() ?? string.Empty;
        string? mediumCode = null;

        if (string.Equals(normalizedKind, ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, StringComparison.Ordinal))
        {
            mediumCode = await _dbContext.HardDiskMedia
                .AsNoTracking()
                .Where(medium => medium.Id == mediumId && !medium.IsDeleted)
                .Select(medium => medium.DiskCode)
                .FirstOrDefaultAsync();
        }
        else if (string.Equals(normalizedKind, ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc, StringComparison.Ordinal))
        {
            mediumCode = await _dbContext.OpticalDiscMedia
                .AsNoTracking()
                .Where(medium => medium.Id == mediumId && !medium.IsDeleted)
                .Select(medium => medium.DiscCode)
                .FirstOrDefaultAsync();
        }

        if (string.IsNullOrWhiteSpace(mediumCode))
        {
            return [];
        }

        string trimmedCode = mediumCode.Trim();
        return await _dbContext.YearlyArchiveFilingFacts
            .Where(fact => fact.MediumCode == trimmedCode
                           && fact.ContainerId == electronicArchiveUnitId
                           && fact.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic
                           && fact.LifecycleStatus == FilingFactLifecycleStatus.InArchive)
            .ToListAsync();
    }

    public Task<HardDiskMedium?> GetHardDiskWithLedgerAsync(int mediumId)
    {
        return _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.Id == mediumId);
    }

    public Task<OpticalDiscMedium?> GetOpticalDiscWithLedgerAsync(int mediumId)
    {
        return _dbContext.OpticalDiscMedia
            .Include(item => item.Ledger)
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.Id == mediumId);
    }

    public async Task<List<HardDiskMedium>> GetHardDisksWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds)
    {
        if (mediumIds == null || mediumIds.Count == 0)
        {
            return [];
        }

        List<int> ids = mediumIds.Where(id => id > 0).Distinct().ToList();
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

    public void AddRecord(YearlyArchiveInventoryRegisterRecord record)
    {
        _dbContext.YearlyArchiveInventoryRegisterRecords.Add(record);
    }

    public void RemoveItems(IEnumerable<YearlyArchiveInventoryRegisterItem> items)
    {
        _dbContext.YearlyArchiveInventoryRegisterItems.RemoveRange(items);
    }

    public void AddMaterialTransaction(YearlyArchiveMaterialTransaction transaction)
    {
        _dbContext.YearlyArchiveMaterialTransactions.Add(transaction);
    }

    public void AddHardDiskTransaction(HardDiskMediaTransaction transaction)
    {
        _dbContext.HardDiskMediaTransactions.Add(transaction);
    }

    public void AddOpticalDiscTransaction(OpticalDiscMediaTransaction transaction)
    {
        _dbContext.OpticalDiscMediaTransactions.Add(transaction);
    }

    public void RemoveRegisterLock(HardDiskRegisterLock lockItem)
    {
        _dbContext.HardDiskRegisterLocks.Remove(lockItem);
    }

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();

    private async Task<IReadOnlyDictionary<int, SimulatedFilingFactCopyCountSnapshot>> LoadCopySnapshotsAsync(
        IReadOnlyCollection<int> filingFactIds)
    {
        var factIdList = filingFactIds.Distinct().ToList();

        var outboundRows = await (
            from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
            join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                on item.OutboundRecordId equals record.Id
            where factIdList.Contains(item.FilingFactId)
                && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                && record.Status == YearlyArchiveOutboundRecord.Completed
            select new
            {
                item.FilingFactId,
                item.NeedReturn,
                item.ReservationStatus,
                item.CopyCount,
            }).ToListAsync();

        var pendingReturnByFactId = outboundRows
            .Where(row => row.NeedReturn
                && string.Equals(row.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed, StringComparison.Ordinal))
            .GroupBy(row => row.FilingFactId)
            .ToDictionary(group => group.Key, group => group.Sum(row => Math.Max(1, row.CopyCount ?? 1)));

        var noReturnByFactId = outboundRows
            .Where(row => !row.NeedReturn
                && !string.Equals(row.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseReturned, StringComparison.Ordinal))
            .GroupBy(row => row.FilingFactId)
            .ToDictionary(group => group.Key, group => group.Sum(row => Math.Max(1, row.CopyCount ?? 1)));

        var returnRows = await (
            from returnItem in _dbContext.YearlyArchiveReturnItems.AsNoTracking()
            join returnRecord in _dbContext.YearlyArchiveReturnRecords.AsNoTracking()
                on returnItem.ReturnRecordId equals returnRecord.Id
            where factIdList.Contains(returnItem.FilingFactId)
                && returnRecord.Status == YearlyArchiveReturnRecord.Completed
            select new
            {
                returnItem.FilingFactId,
                returnItem.LossCopyCount,
                returnItem.ReturnCopyCount,
                returnItem.ItemCondition,
            }).ToListAsync();

        var lostByFactId = returnRows
            .GroupBy(row => row.FilingFactId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.LossCopyCount > 0
                    ? Math.Max(0, row.LossCopyCount)
                    : ArchiveReturnDomainValues.IsLossCondition(row.ItemCondition)
                        ? Math.Max(1, row.ReturnCopyCount)
                        : 0));

        var inventoryCountsByFactId = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => factIdList.Contains(fact.Id))
            .Select(fact => new { fact.Id, fact.InventoryLostCopyCount, fact.InventoryScrapCopyCount })
            .ToDictionaryAsync(
                item => item.Id,
                item => (Lost: Math.Max(0, item.InventoryLostCopyCount), Scrap: Math.Max(0, item.InventoryScrapCopyCount)));

        return factIdList.ToDictionary(
            factId => factId,
            factId =>
            {
                var counts = inventoryCountsByFactId.GetValueOrDefault(factId);
                return new SimulatedFilingFactCopyCountSnapshot
                {
                    PendingReturnCopyCount = pendingReturnByFactId.GetValueOrDefault(factId),
                    NoReturnCopyCount = noReturnByFactId.GetValueOrDefault(factId),
                    LostCopyCount = lostByFactId.GetValueOrDefault(factId),
                    InventoryLostCopyCount = counts.Lost,
                    InventoryScrapCopyCount = counts.Scrap,
                };
            });
    }
}
