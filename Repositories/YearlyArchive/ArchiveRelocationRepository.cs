using DocMgr.Data;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocMgr.Repositories.YearlyArchive
{
    public sealed class ArchiveRelocationRepository : IArchiveRelocationRepository
    {
        private sealed class ArchiveRelocationRepositoryTransaction : IArchiveFilingRepositoryTransaction
        {
            private readonly IDbContextTransaction _transaction;

            public ArchiveRelocationRepositoryTransaction(IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public Task CommitAsync() => _transaction.CommitAsync();

            public Task RollbackAsync() => _transaction.RollbackAsync();

            public async ValueTask DisposeAsync() => await _transaction.DisposeAsync();
        }

        private readonly AppDbContext _dbContext;

        public ArchiveRelocationRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync()
        {
            var transaction = await _dbContext.Database.BeginTransactionAsync();
            return new ArchiveRelocationRepositoryTransaction(transaction);
        }

        public Task<string?> GetLastRelocationNoByPrefixAsync(string prefix)
        {
            return _dbContext.Set<YearlyArchiveRelocationRecord>()
                .AsNoTracking()
                .Where(record => record.RelocationNo.StartsWith(prefix))
                .OrderByDescending(record => record.RelocationNo)
                .Select(record => record.RelocationNo)
                .FirstOrDefaultAsync();
        }

        public void AddRelocationRecord(YearlyArchiveRelocationRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _dbContext.Set<YearlyArchiveRelocationRecord>().Add(record);
        }

        public Task<int> SaveChangesAsync() => _dbContext.SaveChangesAsync();

        public Task<YearlyArchiveBox?> GetArchiveBoxForRelocationAsync(int boxId)
        {
            return _dbContext.YearlyArchiveBoxes
                .Include(box => box.RegisterRecords)
                .Include(box => box.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item.MediaEntry)
                            .ThenInclude(media => media!.RegisterRecord)
                .FirstOrDefaultAsync(box => box.Id == boxId);
        }

        public Task<YearlyArchiveBox?> GetArchiveBoxBySequenceNoAsync(string sequenceNo)
        {
            string normalized = sequenceNo.Trim();
            return _dbContext.YearlyArchiveBoxes
                .Include(box => box.RegisterRecords)
                .Include(box => box.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item.MediaEntry)
                            .ThenInclude(media => media!.RegisterRecord)
                .FirstOrDefaultAsync(box => box.ArchiveSequenceNo == normalized);
        }

        public Task<YearlyElectronicArchiveUnit?> GetElectronicUnitForRelocationAsync(int unitId)
        {
            return _dbContext.YearlyElectronicArchiveUnits
                .Include(unit => unit.RegisterRecords)
                .Include(unit => unit.MediumLinks)
                    .ThenInclude(link => link.HardDiskMedium)
                        .ThenInclude(medium => medium!.RegisterLock)
                .Include(unit => unit.MediumLinks)
                    .ThenInclude(link => link.HardDiskMedium)
                        .ThenInclude(medium => medium!.Ledger)
                .Include(unit => unit.DiscLinks)
                    .ThenInclude(link => link.OpticalDiscMedium)
                        .ThenInclude(disc => disc!.Ledger)
                .Include(unit => unit.MediaEntryLinks)
                .Include(unit => unit.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item.MediaEntry)
                            .ThenInclude(media => media!.RegisterRecord)
                .FirstOrDefaultAsync(unit => unit.Id == unitId);
        }

        public Task<YearlyElectronicArchiveUnit?> GetElectronicUnitByArchiveNoAsync(string archiveNo)
        {
            string normalized = archiveNo.Trim();
            return _dbContext.YearlyElectronicArchiveUnits
                .Include(unit => unit.RegisterRecords)
                .Include(unit => unit.MediumLinks)
                    .ThenInclude(link => link.HardDiskMedium)
                        .ThenInclude(medium => medium.Ledger)
                .Include(unit => unit.DiscLinks)
                    .ThenInclude(link => link.OpticalDiscMedium)
                        .ThenInclude(disc => disc!.Ledger)
                .Include(unit => unit.MediaEntryLinks)
                .Include(unit => unit.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item.MediaEntry)
                            .ThenInclude(media => media!.RegisterRecord)
                .FirstOrDefaultAsync(unit => unit.ElectronicArchiveNo == normalized);
        }

        public async Task<List<YearlyArchiveBox>> GetSimulatedTargetBoxesAsync(string projectName, string year, int excludeBoxId)
        {
            var boxes = await _dbContext.YearlyArchiveBoxes
                .Include(box => box.MediaItemLinks)
                .Where(box => box.ProjectName == projectName && box.Year == year && box.Id != excludeBoxId)
                .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
                .OrderBy(box => box.ArchiveSequenceNo)
                .ToListAsync();

            return boxes;
        }

        public async Task<List<YearlyElectronicArchiveUnit>> GetElectronicTargetUnitsAsync(string projectName, string year, int excludeUnitId)
        {
            var units = await _dbContext.YearlyElectronicArchiveUnits
                .Include(unit => unit.MediaItemLinks)
                .Where(unit => unit.ProjectName == projectName && unit.Year == year && unit.Id != excludeUnitId)
                .OrderBy(unit => unit.ElectronicArchiveNo)
                .ToListAsync();

            return units;
        }

        public Task<List<YearlyArchiveFilingFact>> GetFilingFactsBySourceLinksAsync(
            string sourceLinkType,
            IReadOnlyCollection<int> sourceLinkIds)
        {
            if (sourceLinkIds == null || sourceLinkIds.Count == 0)
            {
                return Task.FromResult(new List<YearlyArchiveFilingFact>());
            }

            return _dbContext.YearlyArchiveFilingFacts
                .Where(fact => fact.SourceLinkType == sourceLinkType && sourceLinkIds.Contains(fact.SourceLinkId))
                .ToListAsync();
        }

        public Task<List<YearlyArchiveFilingFact>> GetFilingFactsByContainerAsync(string mediaKind, int containerId)
        {
            return _dbContext.YearlyArchiveFilingFacts
                .Where(fact => fact.MediaKind == mediaKind && fact.ContainerId == containerId)
                .ToListAsync();
        }

        public Task<HardDiskMedium?> GetHardDiskMediumByCodeWithLedgerAsync(string diskCode)
        {
            string normalized = diskCode.Trim();
            return _dbContext.HardDiskMedia
                .Include(medium => medium.Ledger)
                .FirstOrDefaultAsync(medium => medium.DiskCode == normalized);
        }

        public Task<List<YearlyElectronicArchiveUnitMediumLink>> GetElectronicUnitMediumLinksAsync(int unitId)
        {
            return _dbContext.YearlyElectronicArchiveUnitMediumLinks
                .Where(link => link.YearlyElectronicArchiveUnitId == unitId)
                .ToListAsync();
        }

        public Task<List<YearlyElectronicArchiveUnitMediumLink>> GetElectronicMediumLinksByMediumIdAsync(int mediumId)
        {
            return _dbContext.YearlyElectronicArchiveUnitMediumLinks
                .Include(link => link.ElectronicArchiveUnit)
                .Where(link => link.HardDiskMediumId == mediumId)
                .ToListAsync();
        }

        public Task<List<YearlyElectronicArchiveUnitDiscLink>> GetElectronicUnitDiscLinksAsync(int unitId)
        {
            return _dbContext.YearlyElectronicArchiveUnitDiscLinks
                .Include(link => link.OpticalDiscMedium)
                    .ThenInclude(disc => disc!.Ledger)
                .Where(link => link.YearlyElectronicArchiveUnitId == unitId)
                .ToListAsync();
        }

        public Task<List<YearlyArchiveBox>> GetSimulatedSourceCandidatesAsync(string projectName, string year)
        {
            string normalizedProject = projectName.Trim();
            string normalizedYear = year.Trim();

            return _dbContext.YearlyArchiveBoxes
                .AsNoTracking()
                .Include(box => box.MediaItemLinks)
                .Where(box => box.ProjectName == normalizedProject && box.Year == normalizedYear)
                .Where(box => box.MediaItemLinks.Count > 0)
                .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
                .OrderBy(box => box.ArchiveSequenceNo)
                .ToListAsync();
        }

        public Task<List<YearlyElectronicArchiveUnit>> GetElectronicSourceCandidatesAsync(string projectName, string year)
        {
            string normalizedProject = projectName.Trim();
            string normalizedYear = year.Trim();

            return _dbContext.YearlyElectronicArchiveUnits
                .AsNoTracking()
                .Include(unit => unit.MediaItemLinks)
                .Include(unit => unit.MediumLinks)
                    .ThenInclude(link => link.HardDiskMedium)
                        .ThenInclude(medium => medium!.Ledger)
                .Where(unit => unit.ProjectName == normalizedProject && unit.Year == normalizedYear)
                .Where(unit => unit.MediaItemLinks.Count > 0)
                .Where(unit => unit.UnitLifecycleStatus != ArchiveContainerLifecycleStatus.Disposed)
                .OrderBy(unit => unit.ElectronicArchiveNo)
                .ToListAsync();
        }

        public Task<List<YearlyElectronicArchiveUnit>> GetInUseElectronicArchiveUnitsInSlotForRelocationAsync(
            string cabinetName,
            string side,
            int row,
            int column)
        {
            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, side, row, column);
            string slotPrefix = slotKey + "-";

            return _dbContext.YearlyElectronicArchiveUnits
                .Include(unit => unit.MediumLinks)
                    .ThenInclude(link => link.HardDiskMedium)
                        .ThenInclude(medium => medium!.RegisterLock)
                .Include(unit => unit.MediumLinks)
                    .ThenInclude(link => link.HardDiskMedium)
                        .ThenInclude(medium => medium!.Ledger)
                .Include(unit => unit.DiscLinks)
                    .ThenInclude(link => link.OpticalDiscMedium)
                        .ThenInclude(disc => disc!.Ledger)
                .Include(unit => unit.MediaItemLinks)
                .Where(unit => unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
                .Where(unit =>
                    unit.StorageLocation == slotKey
                    || unit.StorageLocation.StartsWith(slotPrefix)
                    || unit.MediumLinks.Any(link =>
                        link.HardDiskMedium != null
                        && link.HardDiskMedium.Ledger != null
                        && (link.HardDiskMedium.Ledger.MediaStatus == HardDiskMedium.StatusInStockData
                            || link.HardDiskMedium.Ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged)
                        && (link.HardDiskMedium.Ledger.StorageLocation == slotKey
                            || link.HardDiskMedium.Ledger.StorageLocation.StartsWith(slotPrefix)))
                    || unit.DiscLinks.Any(link =>
                        link.OpticalDiscMedium != null
                        && link.OpticalDiscMedium.Ledger != null
                        && (link.OpticalDiscMedium.Ledger.MediaStatus == OpticalDiscMedium.StatusInStock
                            || link.OpticalDiscMedium.Ledger.MediaStatus == OpticalDiscMedium.StatusDamaged)
                        && (link.OpticalDiscMedium.Ledger.StorageLocation == slotKey
                            || link.OpticalDiscMedium.Ledger.StorageLocation.StartsWith(slotPrefix))))
                .ToListAsync();
        }
    }
}
