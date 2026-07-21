using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocMgr.Repositories.YearlyArchive;

public class ArchiveFilingRepository : IArchiveFilingRepository
{
    private sealed class ArchiveFilingRepositoryTransaction : IArchiveFilingRepositoryTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public ArchiveFilingRepositoryTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync()
        {
            return _transaction.CommitAsync();
        }

        public Task RollbackAsync()
        {
            return _transaction.RollbackAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _transaction.DisposeAsync();
        }
    }

    private readonly AppDbContext _dbContext;

    public ArchiveFilingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync()
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync();
        return new ArchiveFilingRepositoryTransaction(transaction);
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetPendingRecordsAsync(int? year)
    {
        return BuildPendingRecordsQuery(year)
            .Where(record =>
                (record.MediaEntries.Any(media => media.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated
                    && media.Items.Any(item => !item.ArchiveBoxLinks.Any()))
                 && record.SimulatedArchiveStatus != YearlyArchiveRegisterRecord.TrackArchived)
                ||
                (record.MediaEntries.Any(media => media.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic
                    && media.Items.Any(item => !item.ElectronicArchiveUnitMediaItemLinks.Any()))
                 && record.ElectronicArchiveStatus != YearlyArchiveRegisterRecord.TrackArchived))
            .OrderByDescending(record => record.CreatedDate)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetPendingSimulatedRecordsAsync(int? year)
    {
        return BuildPendingRecordsQuery(year)
            .Where(record => record.MediaEntries.Any(media => media.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated
                && media.Items.Any(item => !item.ArchiveBoxLinks.Any()))
                && record.SimulatedArchiveStatus != YearlyArchiveRegisterRecord.TrackArchived)
            .OrderByDescending(record => record.CreatedDate)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetPendingElectronicRecordsAsync(int? year)
    {
        return BuildPendingRecordsQuery(year)
            .Where(record => record.MediaEntries.Any(media => media.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic
                && media.Items.Any(item => !item.ElectronicArchiveUnitMediaItemLinks.Any()))
                && record.ElectronicArchiveStatus != YearlyArchiveRegisterRecord.TrackArchived)
            .OrderByDescending(record => record.CreatedDate)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetCompletedUnfiledRecordsForToDoAsync(int takeCount)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Include(record => record.MediaEntries)
            .Where(record => record.Status == YearlyArchiveRegisterRecord.Completed)
            .Where(record =>
                (record.MediaEntries.Any(media => media.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated
                    && media.Items.Any(item => !item.ArchiveBoxLinks.Any()))
                 && record.SimulatedArchiveStatus != YearlyArchiveRegisterRecord.TrackArchived)
                ||
                (record.MediaEntries.Any(media => media.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic
                    && media.Items.Any(item => !item.ElectronicArchiveUnitMediaItemLinks.Any()))
                 && record.ElectronicArchiveStatus != YearlyArchiveRegisterRecord.TrackArchived))
            .OrderByDescending(record => record.AdminDate ?? record.DeliverDate ?? record.CreatedDate)
            .Take(takeCount)
            .ToListAsync();
    }

    public Task<int> GetFiledSimulatedRecordCountAsync(int? year)
    {
        return BuildCompletedRecordsQuery(year)
            .Where(record => record.MediaEntries.Any(media => media.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated)
                && record.SimulatedArchiveStatus == YearlyArchiveRegisterRecord.TrackArchived)
            .CountAsync();
    }

    public Task<int> GetFiledElectronicRecordCountAsync(int? year)
    {
        return BuildCompletedRecordsQuery(year)
            .Where(record => record.MediaEntries.Any(media => media.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic)
                && record.ElectronicArchiveStatus == YearlyArchiveRegisterRecord.TrackArchived)
            .CountAsync();
    }

    public Task<List<ArchiveBoxSpecification>> GetArchiveBoxSpecificationsAsync()
    {
        return _dbContext.ArchiveBoxSpecifications
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<CabinetSlotSpecification>> GetCabinetSlotSpecificationsAsync()
    {
        return _dbContext.CabinetSlotSpecifications
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<CabinetSlotSpecialRule>> GetEnabledCabinetSlotSpecialRulesBySpecificationAsync(string boxSpecification)
    {
        return _dbContext.CabinetSlotSpecialRules
            .AsNoTracking()
            .Where(item => item.IsEnabled)
            .Where(item => item.RequiredBoxSpecification == boxSpecification)
            .OrderBy(item => item.SortOrder)
            .ToListAsync();
    }

    public Task<List<Cabinet>> GetNonMagneticCabinetsAsync()
    {
        return _dbContext.Cabinets
            .AsNoTracking()
            .Where(item => item.Type != CabinetType.MagneticDisk)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveBox>> GetExistingYearlyArchiveBoxesWithCabinetAsync()
    {
        return _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(item => item.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Where(item => item.CabinetName != string.Empty)
            .ToListAsync();
    }

    public Task<List<CabinetArchiveBoxPlacement>> GetArchiveBoxPlacementsAsync()
    {
        return _dbContext.CabinetArchiveBoxPlacements
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<YearlyArchiveBox>> GetExistingBoxesForProjectAsync(string projectName, string year)
    {
        return _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(box => box.ProjectName == projectName && box.Year == year)
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .ToListAsync();
    }

    public Task<List<YearlyElectronicArchiveUnit>> GetExistingElectronicUnitsForProjectAsync(string projectName, string year)
    {
        return _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .Where(unit => unit.ProjectName == projectName && unit.Year == year)
            .ToListAsync();
    }

    public Task<YearlyArchiveBox?> GetLastArchiveBoxByPrefixAsync(string prefix)
    {
        return _dbContext.YearlyArchiveBoxes
            .Where(box => box.ArchiveSequenceNo.StartsWith(prefix))
            .OrderByDescending(box => box.ArchiveSequenceNo)
            .FirstOrDefaultAsync();
    }

    public Task<YearlyElectronicArchiveUnit?> GetLastElectronicUnitByPrefixAsync(string prefix)
    {
        return _dbContext.YearlyElectronicArchiveUnits
            .Where(unit => unit.ElectronicArchiveNo.StartsWith(prefix))
            .OrderByDescending(unit => unit.ElectronicArchiveNo)
            .FirstOrDefaultAsync();
    }

    public Task<int> CountElectronicUnitsInSlotAsync(string slotCode, string slotPrefix)
    {
        return _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .CountAsync(item => item.StorageLocation == slotCode || item.StorageLocation.StartsWith(slotPrefix));
    }

    public async Task<List<int>> GetElectronicUnitSequenceIndexesInSlotAsync(string slotCode, string slotPrefix, int? excludeUnitId = null)
    {
        var unitLocations = await _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .Where(item => excludeUnitId == null || item.Id != excludeUnitId.Value)
            .Where(item => item.StorageLocation == slotCode || item.StorageLocation.StartsWith(slotPrefix))
            .Select(item => item.StorageLocation)
            .ToListAsync();

        var opticalDiscLocations = await _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Where(item => item.Ledger != null
                && (item.Ledger!.MediaStatus == OpticalDiscMedium.StatusInStock
                    || item.Ledger!.MediaStatus == OpticalDiscMedium.StatusDamaged))
            .Where(item => item.Ledger!.StorageLocation == slotCode || item.Ledger!.StorageLocation.StartsWith(slotPrefix))
            .Where(item => excludeUnitId == null
                || !_dbContext.YearlyElectronicArchiveUnitDiscLinks.Any(link =>
                    link.YearlyElectronicArchiveUnitId == excludeUnitId.Value
                    && link.OpticalDiscMediumId == item.Id))
            .Select(item => item.Ledger!.StorageLocation)
            .ToListAsync();

        return MagneticDedicatedSlotOccupancySupport.CollectOccupiedSequenceIndexes(
            slotCode,
            unitLocations.Concat(opticalDiscLocations));
    }

    public async Task<List<string>> GetElectronicArchiveUnitStorageLocationsInSlotAsync(string slotCode, string slotPrefix)
    {
        if (string.IsNullOrWhiteSpace(slotCode))
        {
            return [];
        }

        return await _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .Where(item => item.StorageLocation == slotCode || item.StorageLocation.StartsWith(slotPrefix))
            .Select(item => item.StorageLocation)
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .ToListAsync();
    }

    public Task<bool> IsArchiveSequenceExistsAsync(string sequenceNo)
    {
        return _dbContext.YearlyArchiveBoxes.AnyAsync(box => box.ArchiveSequenceNo == sequenceNo);
    }

    public Task<bool> IsElectronicArchiveNoExistsAsync(string sequenceNo)
    {
        return _dbContext.YearlyElectronicArchiveUnits.AnyAsync(unit => unit.ElectronicArchiveNo == sequenceNo);
    }

    public Task<List<HardDiskElectronicArchiveLinkInfo>> GetElectronicArchiveLinkInfosAsync(IReadOnlyCollection<int> mediumIds)
    {
        return _dbContext.YearlyElectronicArchiveUnitMediumLinks
            .AsNoTracking()
            .Where(link => mediumIds.Contains(link.HardDiskMediumId))
            .Select(link => new HardDiskElectronicArchiveLinkInfo(
                link.HardDiskMediumId,
                link.HardDiskMedium.DiskCode,
                link.YearlyElectronicArchiveUnitId,
                link.ElectronicArchiveUnit.ElectronicArchiveNo))
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetRegisterRecordsForArchivingAsync(IReadOnlyCollection<int> recordIds)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ElectronicArchiveUnitMediaItemLinks)
                        .ThenInclude(link => link.ElectronicArchiveUnit)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.ElectronicArchiveUnitLinks)
            .Include(record => record.ArchiveBoxes)
            .Include(record => record.ElectronicArchiveUnits)
            .Where(record => recordIds.Contains(record.Id))
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterMediaItem>> GetSimulatedMediaItemsForArchivingAsync(IReadOnlyCollection<int> mediaItemIds)
    {
        return LoadSimulatedMediaItemsForArchivingCoreAsync(mediaItemIds);
    }

    private async Task<List<YearlyArchiveRegisterMediaItem>> LoadSimulatedMediaItemsForArchivingCoreAsync(IReadOnlyCollection<int> mediaItemIds)
    {
        var idList = mediaItemIds.Where(id => id > 0).Distinct().ToList();
        if (idList.Count == 0)
        {
            return new List<YearlyArchiveRegisterMediaItem>();
        }

        var items = await _dbContext.YearlyArchiveRegisterMediaItems
            .AsSplitQuery()
            .Include(item => item.MediaEntry)
                .ThenInclude(media => media!.RegisterRecord)
            .Include(item => item.ArchiveBoxLinks)
            .Where(item => idList.Contains(item.Id))
            .ToListAsync();

        await AttachMissingSimulatedMediaNavigationsAsync(items);
        return items;
    }

    private async Task AttachMissingSimulatedMediaNavigationsAsync(IReadOnlyCollection<YearlyArchiveRegisterMediaItem> items)
    {
        var brokenIds = items
            .Where(item => item.MediaEntry == null || item.MediaEntry.RegisterRecord == null)
            .Select(item => item.Id)
            .Distinct()
            .ToList();
        if (brokenIds.Count == 0)
        {
            return;
        }

        var resolvedRows = await (
            from item in _dbContext.YearlyArchiveRegisterMediaItems.AsNoTracking()
            join media in _dbContext.YearlyArchiveRegisterMedias.AsNoTracking()
                on item.YearlyArchiveRegisterMediaId equals media.Id into mediaJoin
            from media in mediaJoin.DefaultIfEmpty()
            join record in _dbContext.YearlyArchiveRegisterRecords.AsNoTracking()
                on media.YearlyArchiveRegisterRecordId equals record.Id into recordJoin
            from record in recordJoin.DefaultIfEmpty()
            where brokenIds.Contains(item.Id)
            select new
            {
                ItemId = item.Id,
                Media = media,
                Record = record
            }).ToListAsync();

        foreach (var row in resolvedRows)
        {
            var target = items.FirstOrDefault(item => item.Id == row.ItemId);
            if (target == null)
            {
                continue;
            }

            if (target.MediaEntry == null && row.Media != null)
            {
                target.MediaEntry = row.Media;
            }

            if (target.MediaEntry != null && target.MediaEntry.RegisterRecord == null && row.Record != null)
            {
                target.MediaEntry.RegisterRecord = row.Record;
            }
        }
    }

    public Task<List<YearlyArchiveRegisterMedia>> GetElectronicMediaEntriesForArchivingAsync(IReadOnlyCollection<int> mediaEntryIds)
    {
        return _dbContext.YearlyArchiveRegisterMedias
            .Include(media => media.RegisterRecord)
            .Include(media => media.Items)
            .Include(media => media.ElectronicArchiveUnitLinks)
                .ThenInclude(link => link.ElectronicArchiveUnit)
            .Where(media => mediaEntryIds.Contains(media.Id))
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterMediaItem>> GetElectronicMediaItemsForArchivingAsync(IReadOnlyCollection<int> mediaItemIds)
    {
        return _dbContext.YearlyArchiveRegisterMediaItems
            .Include(item => item.MediaEntry)
                .ThenInclude(media => media!.RegisterRecord)
            .Include(item => item.MediaEntry)
                .ThenInclude(media => media!.Items)
                    .ThenInclude(sibling => sibling.ElectronicArchiveUnitMediaItemLinks)
            .Include(item => item.ElectronicDetail!)
                .ThenInclude(detail => detail.Entries)
            .Include(item => item.ElectronicArchiveUnitMediaItemLinks)
            .Where(item => mediaItemIds.Contains(item.Id))
            .ToListAsync();
    }

    public Task<List<YearlyElectronicArchiveUnitMediaItemLink>> GetElectronicArchiveUnitMediaItemLinksByUnitIdAsync(int unitId)
    {
        return _dbContext.YearlyElectronicArchiveUnitMediaItemLinks
            .AsNoTracking()
            .Include(link => link.ElectronicArchiveUnit)
            .Include(link => link.MediaItem)
                .ThenInclude(item => item.MediaEntry)
                    .ThenInclude(media => media!.RegisterRecord)
            .Where(link => link.YearlyElectronicArchiveUnitId == unitId)
            .OrderBy(link => link.FormNo)
            .ThenBy(link => link.ItemName)
            .ToListAsync();
    }

    public Task<List<YearlyElectronicArchiveUnitMediaItemLink>> GetElectronicArchiveUnitMediaItemLinksByMediumCodeAsync(string mediumCode)
    {
        string normalized = mediumCode.Trim();
        return _dbContext.YearlyElectronicArchiveUnitMediaItemLinks
            .AsNoTracking()
            .Where(link => link.MediumCode == normalized)
            .ToListAsync();
    }

    public void AddElectronicArchiveUnitMediaItemLink(YearlyElectronicArchiveUnitMediaItemLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        _dbContext.YearlyElectronicArchiveUnitMediaItemLinks.Add(link);
    }

    public void AddRegisterMediaItem(YearlyArchiveRegisterMediaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _dbContext.YearlyArchiveRegisterMediaItems.Add(item);
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetRegisterRecordsForSimulatedArchivingAsync(IReadOnlyCollection<int> recordIds)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ArchiveBoxLinks)
            .Include(record => record.ArchiveBoxes)
            .Where(record => recordIds.Contains(record.Id))
            .ToListAsync();
    }

    public Task<YearlyElectronicArchiveUnit?> GetElectronicArchiveUnitWithDetailsAsync(int unitId)
    {
        return _dbContext.YearlyElectronicArchiveUnits
            .Include(item => item.RegisterRecords)
            .Include(item => item.MediumLinks)
            .Include(item => item.DiscLinks)
            .Include(item => item.MediaEntryLinks)
            .Include(item => item.MediaItemLinks)
                .ThenInclude(link => link.MediaItem)
            .FirstOrDefaultAsync(item => item.Id == unitId);
    }

    public Task<OpticalDiscMedium?> GetOpticalDiscMediumByCodeAsync(string discCode)
    {
        return _dbContext.OpticalDiscMedia
            .Include(item => item.Ledger)
            .Include(item => item.Transactions)
            .FirstOrDefaultAsync(item => item.DiscCode == discCode);
    }

    public void AddOpticalDiscMedium(OpticalDiscMedium medium)
    {
        ArgumentNullException.ThrowIfNull(medium);
        _dbContext.OpticalDiscMedia.Add(medium);
    }

    public Task<YearlyElectronicArchiveUnitDiscLink?> GetElectronicArchiveUnitDiscLinkAsync(int unitId, int opticalDiscMediumId, string discCode)
    {
        return _dbContext.YearlyElectronicArchiveUnitDiscLinks
            .Include(item => item.OpticalDiscMedium)
            .FirstOrDefaultAsync(item => item.YearlyElectronicArchiveUnitId == unitId
                && (item.OpticalDiscMediumId == opticalDiscMediumId
                    || (item.OpticalDiscMedium != null
                        && item.OpticalDiscMedium.DiscCode == discCode)));
    }

    public Task<HardDiskMedium?> GetHardDiskMediumByIdWithLedgerAsync(int mediumId)
    {
        return _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .FirstOrDefaultAsync(item => item.Id == mediumId && !item.IsDeleted);
    }

    public Task<HardDiskMedium?> GetHardDiskMediumByDiskCodeWithLedgerAsync(string diskCode)
    {
        return _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.DiskCode == diskCode);
    }

    public Task<List<HardDiskMedium>> GetHardDiskMediaByCodesWithLedgerAsync(IReadOnlyCollection<string> diskCodes)
    {
        return _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted && diskCodes.Contains(item.DiskCode))
            .ToListAsync();
    }

    public Task<bool> HasCompletedReturnApplicationAsync(int mediumId, int? sourceApplicationId)
    {
        return _dbContext.HardDiskMediaApplications
            .AnyAsync(item => item.MediumId == mediumId
                && item.SourceApplicationId == sourceApplicationId
                && item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted
                && (item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration
                    || item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration
                    || item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration));
    }

    public Task<bool> HasPendingRetainedRegisterEntriesForBorrowedDiskAsync(
        string diskCode,
        IReadOnlyCollection<int>? excludingMediaEntryIds = null)
    {
        if (string.IsNullOrWhiteSpace(diskCode))
        {
            return Task.FromResult(false);
        }

        string normalizedDiskCode = diskCode.Trim();
        var excludedMediaEntryIds = excludingMediaEntryIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList()
            ?? [];

        return _dbContext.YearlyArchiveRegisterMedias
            .AnyAsync(media =>
                media.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic
                && media.MediaType == ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk
                && media.Disposition == ArchiveRegisterDomainValues.ElectronicDispositionRetain
                && media.IsBorrowedHardDisk
                && media.BorrowedHardDiskCode == normalizedDiskCode
                && !media.ElectronicArchiveUnitLinks.Any()
                && (excludedMediaEntryIds.Count == 0 || !excludedMediaEntryIds.Contains(media.Id))
                && media.RegisterRecord != null
                && media.RegisterRecord.Status == YearlyArchiveRegisterRecord.Completed);
    }

    public Task<bool> HasPendingExternalRetainedRegisterEntriesOnRecordsAsync(IReadOnlyCollection<int> registerRecordIds)
    {
        ArgumentNullException.ThrowIfNull(registerRecordIds);

        var targetRecordIds = registerRecordIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (targetRecordIds.Count == 0)
        {
            return Task.FromResult(false);
        }

        return _dbContext.YearlyArchiveRegisterMedias
            .AnyAsync(media =>
                targetRecordIds.Contains(media.YearlyArchiveRegisterRecordId)
                && media.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic
                && media.MediaType == ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk
                && media.Disposition == ArchiveRegisterDomainValues.ElectronicDispositionRetain
                && !media.IsBorrowedHardDisk
                && !media.ElectronicArchiveUnitLinks.Any()
                && media.RegisterRecord != null
                && media.RegisterRecord.Status == YearlyArchiveRegisterRecord.Completed);
    }

    public Task<List<int>> GetRegisterRecordIdsForMediaEntriesAsync(IReadOnlyCollection<int> mediaEntryIds)
    {
        ArgumentNullException.ThrowIfNull(mediaEntryIds);

        var targetMediaEntryIds = mediaEntryIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (targetMediaEntryIds.Count == 0)
        {
            return Task.FromResult(new List<int>());
        }

        return _dbContext.YearlyArchiveRegisterMedias
            .AsNoTracking()
            .Where(media => targetMediaEntryIds.Contains(media.Id))
            .Select(media => media.YearlyArchiveRegisterRecordId)
            .Distinct()
            .ToListAsync();
    }

    public void AddHardDiskMediaApplication(HardDiskMediaApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _dbContext.HardDiskMediaApplications.Add(application);
    }

    public void AddHardDiskMediaTransaction(HardDiskMediaTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _dbContext.HardDiskMediaTransactions.Add(transaction);
    }

    public Task<List<int>> GetArchiveBoxLinkedMediaItemIdsAsync(int boxId)
    {
        return _dbContext.YearlyArchiveBoxMediaItemLinks
            .Where(link => link.YearlyArchiveBoxId == boxId)
            .Select(link => link.YearlyArchiveRegisterMediaItemId)
            .ToListAsync();
    }

    public List<int> GetArchiveBoxLinkedMediaItemIds(int boxId)
    {
        return _dbContext.YearlyArchiveBoxMediaItemLinks
            .Where(link => link.YearlyArchiveBoxId == boxId)
            .Select(link => link.YearlyArchiveRegisterMediaItemId)
            .ToList();
    }

    public void AddArchiveBoxMediaItemLink(YearlyArchiveBoxMediaItemLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        _dbContext.YearlyArchiveBoxMediaItemLinks.Add(link);
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetRecordsForSimulatedStatusUpdateAsync(IReadOnlyCollection<int> recordIds)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ArchiveBoxLinks)
            .Where(record => recordIds.Contains(record.Id))
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetRecordsForElectronicStatusUpdateAsync(IReadOnlyCollection<int> recordIds)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ElectronicArchiveUnitMediaItemLinks)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.ElectronicArchiveUnitLinks)
            .Where(record => recordIds.Contains(record.Id))
            .ToListAsync();
    }

    public Task<YearlyArchiveRegisterRecord?> GetRegisterRecordForDeletionAsync(int id)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
            .FirstOrDefaultAsync(record => record.Id == id);
    }

    public Task<YearlyArchiveBox?> GetArchiveBoxWithRegisterRecordsAsync(int boxId)
    {
        return _dbContext.YearlyArchiveBoxes
            .Include(item => item.RegisterRecords)
            .FirstOrDefaultAsync(item => item.Id == boxId);
    }

    public async Task<IReadOnlyList<YearlyArchiveBox>> GetArchiveBoxesBySequenceNosAsync(IReadOnlyCollection<string> sequenceNos)
    {
        if (sequenceNos == null || sequenceNos.Count == 0)
        {
            return Array.Empty<YearlyArchiveBox>();
        }

        var normalized = sequenceNos
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            return Array.Empty<YearlyArchiveBox>();
        }

        return await _dbContext.YearlyArchiveBoxes
            .Where(box => normalized.Contains(box.ArchiveSequenceNo))
            .ToListAsync();
    }

    public Task<CabinetArchiveBoxPlacement?> GetArchiveBoxPlacementByCodeAsync(string boxCode)
    {
        return _dbContext.CabinetArchiveBoxPlacements
            .FirstOrDefaultAsync(item => item.BoxCode == boxCode);
    }

    public CabinetArchiveBoxPlacement? GetArchiveBoxPlacementByCode(string boxCode)
    {
        return _dbContext.CabinetArchiveBoxPlacements
            .FirstOrDefault(item => item.BoxCode == boxCode);
    }

    public void AddArchiveBoxPlacement(CabinetArchiveBoxPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        _dbContext.CabinetArchiveBoxPlacements.Add(placement);
    }

    public void RemoveArchiveBoxPlacementByBoxCode(string boxCode)
    {
        if (string.IsNullOrWhiteSpace(boxCode))
        {
            return;
        }

        string normalized = boxCode.Trim();
        var placement = _dbContext.CabinetArchiveBoxPlacements
            .FirstOrDefault(item => item.BoxCode == normalized);
        if (placement != null)
        {
            _dbContext.CabinetArchiveBoxPlacements.Remove(placement);
        }
    }

    public CabinetSlotSpecialRule? GetCabinetSlotSpecialRule(string cabinetName, string slotCode, string boxSpecification, string sideCode)
    {
        return _dbContext.CabinetSlotSpecialRules
            .AsNoTracking()
            .Where(item => item.IsEnabled)
            .Where(item => item.CabinetName == cabinetName)
            .Where(item => item.SlotCode == slotCode)
            .Where(item => item.RequiredBoxSpecification == boxSpecification)
            .Where(item => string.IsNullOrEmpty(item.RequiredArchiveFaceCode) || item.RequiredArchiveFaceCode == sideCode)
            .OrderBy(item => item.SortOrder)
            .FirstOrDefault();
    }

    public Task<List<ArchiveContainerProjection>> GetArchiveContainerProjectionsAsync(string projectName, string year, ArchiveContainerKind containerKind)
    {
        return _dbContext.ArchiveContainerSummaries
            .AsNoTracking()
            .Where(item => item.Kind == containerKind)
            .Where(item => item.ProjectName == projectName)
            .Where(item => item.Year == year)
            .OrderBy(item => item.ContainerCode)
            .ToListAsync();
    }

    public Task<List<string>> GetYearlyArchiveBoxLocationCodesAsync()
    {
        return _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(item => item.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Where(item => !string.IsNullOrWhiteSpace(item.BoxLocationCode))
            .Select(item => item.BoxLocationCode)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveBox>> GetInUseYearlyArchiveBoxesInSlotAsync(
        string cabinetName,
        string side,
        int row,
        int column)
    {
        string normalizedCabinet = cabinetName.Trim();
        string normalizedSide = side.Trim();
        return _dbContext.YearlyArchiveBoxes
            .Include(box => box.MediaItemLinks)
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Where(box => box.CabinetName == normalizedCabinet && box.Side == normalizedSide && box.Row == row && box.Column == column)
            .OrderBy(box => box.BoxIndex)
            .ThenBy(box => box.BoxLocationCode)
            .ToListAsync();
    }

    public async Task<int> CountHistoryArchiveOccupanciesInSlotAsync(
        string cabinetName,
        string side,
        int row,
        int column)
    {
        string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, side, row, column);
        if (string.IsNullOrWhiteSpace(targetSlotKey))
        {
            return 0;
        }

        var sourceValues = new List<string>();
        sourceValues.AddRange(await GetTopoMapBoxNumbersAsync());
        sourceValues.AddRange(await GetAerialPhotoBoxNumbersAsync());
        sourceValues.AddRange(await GetOtherMapBoxNumbersAsync());

        return sourceValues
            .SelectMany(SplitArchiveBoxCodesForSlotCount)
            .Count(boxCode => string.Equals(
                ArchiveSlotLocationSupport.BuildSlotKey(boxCode),
                targetSlotKey,
                StringComparison.OrdinalIgnoreCase));
    }

    public Task<Cabinet?> GetMagneticDiskCabinetByNameAsync(string cabinetName)
    {
        string normalized = cabinetName.Trim();
        return _dbContext.Cabinets
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.Type == CabinetType.MagneticDisk
                && item.Name == normalized);
    }

    public Task<string?> GetMagneticDiskSlotCategoryNameAsync(int cabinetId, string faceCode, string slotCode)
    {
        string normalizedFace = faceCode.Trim();
        string normalizedSlot = slotCode.Trim();
        return _dbContext.CabinetHardDiskSlotCategoryAssignments
            .AsNoTracking()
            .Where(item => item.CabinetId == cabinetId)
            .Where(item => item.FaceCode == normalizedFace && item.SlotCode == normalizedSlot)
            .Select(item => item.CategoryName)
            .FirstOrDefaultAsync();
    }

    public Task<string?> GetArchiveSlotCategoryNameAsync(int cabinetId, string faceCode, string slotCode)
    {
        string normalizedFace = faceCode.Trim();
        string normalizedSlot = slotCode.Trim();
        return _dbContext.CabinetArchiveSlotCategoryAssignments
            .AsNoTracking()
            .Where(item => item.CabinetId == cabinetId)
            .Where(item => item.FaceCode == normalizedFace && item.SlotCode == normalizedSlot)
            .Select(item => item.CategoryName)
            .FirstOrDefaultAsync();
    }

    public async Task<Dictionary<string, string>> GetArchiveSlotCategoryLookupForCabinetsAsync(IReadOnlyCollection<int> cabinetIds)
    {
        if (cabinetIds == null || cabinetIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var idSet = cabinetIds.Where(id => id > 0).Distinct().ToList();
        if (idSet.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await _dbContext.CabinetArchiveSlotCategoryAssignments
            .AsNoTracking()
            .Where(item => idSet.Contains(item.CabinetId))
            .Select(item => new { item.CabinetId, item.FaceCode, item.SlotCode, item.CategoryName })
            .ToListAsync();

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            string key = $"{row.CabinetId}:{row.FaceCode.Trim()}:{row.SlotCode.Trim()}";
            lookup[key] = row.CategoryName;
        }

        return lookup;
    }

    public async Task<bool> IsMagneticDiskSlotFullyEmptyAsync(string slotCode, string slotPrefix)
    {
        if (string.IsNullOrWhiteSpace(slotCode))
        {
            return false;
        }

        int electronicCount = await CountElectronicUnitsInSlotAsync(slotCode, slotPrefix);
        if (electronicCount > 0)
        {
            return false;
        }

        string normalizedSlotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotCode);
        var hardDiskLocations = await _dbContext.HardDiskLedgers
            .AsNoTracking()
            .Where(item => item.MediaStatus == HardDiskMedium.StatusInStockBlank
                || item.MediaStatus == HardDiskMedium.StatusInStockData
                || item.MediaStatus == HardDiskMedium.StatusInStockDamaged)
            .Select(item => item.StorageLocation)
            .ToListAsync();

        if (hardDiskLocations.Any(location =>
                !string.IsNullOrWhiteSpace(location)
                && string.Equals(
                    HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location),
                    normalizedSlotCode,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var opticalDiscLocations = await _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Where(item => item.Ledger != null && item.Ledger.MediaStatus == OpticalDiscMedium.StatusInStock)
            .Select(item => item.Ledger!.StorageLocation)
            .ToListAsync();

        return !opticalDiscLocations.Any(location =>
            !string.IsNullOrWhiteSpace(location)
            && string.Equals(
                HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location),
                normalizedSlotCode,
                StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitArchiveBoxCodesForSlotCount(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Enumerable.Empty<string>();
        }

        return source
            .Split([';', '；', ',', '，', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(code => !string.IsNullOrWhiteSpace(code));
    }

    public Task<List<string>> GetTopoMapBoxNumbersAsync()
    {
        return _dbContext.TopoMaps
            .AsNoTracking()
            .Select(item => item.BoxNumber)
            .ToListAsync();
    }

    public Task<List<string>> GetAerialPhotoBoxNumbersAsync()
    {
        return _dbContext.AerialPhotos
            .AsNoTracking()
            .Select(item => item.BoxNumber)
            .ToListAsync();
    }

    public Task<List<string>> GetOtherMapBoxNumbersAsync()
    {
        return _dbContext.OtherMaps
            .AsNoTracking()
            .Select(item => item.BoxNumber)
            .ToListAsync();
    }

    public Task<List<SystemAttachment>> GetRegisterAttachmentsByBusinessIdAsync(int id)
    {
        return _dbContext.SystemAttachments
            .Where(attachment => attachment.BusinessId == id && attachment.BusinessType == "YearlyArchiveRegister")
            .ToListAsync();
    }

    public void RemoveAttachments(IEnumerable<SystemAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        _dbContext.SystemAttachments.RemoveRange(attachments);
    }

    public void RemoveRegisterRecord(YearlyArchiveRegisterRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _dbContext.YearlyArchiveRegisterRecords.Remove(record);
    }

    public void AddArchiveBox(YearlyArchiveBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        _dbContext.YearlyArchiveBoxes.Add(box);
    }

    public void AddElectronicArchiveUnit(YearlyElectronicArchiveUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        _dbContext.YearlyElectronicArchiveUnits.Add(unit);
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    private IQueryable<YearlyArchiveRegisterRecord> BuildCompletedRecordsQuery(int? year)
    {
        IQueryable<YearlyArchiveRegisterRecord> query = _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Where(record => record.Status == YearlyArchiveRegisterRecord.Completed);

        if (year.HasValue)
        {
            DateTime start = new DateTime(year.Value, 1, 1);
            DateTime end = start.AddYears(1);
            query = query.Where(record => record.CreatedDate >= start && record.CreatedDate < end);
        }

        return query;
    }

    private IQueryable<YearlyArchiveRegisterRecord> BuildPendingRecordsQuery(int? year)
    {
        IQueryable<YearlyArchiveRegisterRecord> query = _dbContext.YearlyArchiveRegisterRecords
            .Where(record => record.Status == YearlyArchiveRegisterRecord.Completed);

        if (year.HasValue)
        {
            DateTime start = new DateTime(year.Value, 1, 1);
            DateTime end = start.AddYears(1);
            query = query.Where(record => record.CreatedDate >= start && record.CreatedDate < end);
        }

        return query
            .AsNoTracking()
            .AsSplitQuery()
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ArchiveBoxLinks)
                        .ThenInclude(link => link.ArchiveBox)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ElectronicDetail!)
                        .ThenInclude(detail => detail.Entries)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ElectronicArchiveUnitMediaItemLinks)
                        .ThenInclude(link => link.ElectronicArchiveUnit)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.ElectronicArchiveUnitLinks)
                    .ThenInclude(link => link.ElectronicArchiveUnit);
    }
}
