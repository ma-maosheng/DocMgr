using DocMgr.Data;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
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
                .Include(box => box.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item.MediaEntry)
                            .ThenInclude(media => media!.RegisterRecord)
                .FirstOrDefaultAsync(box => box.ArchiveSequenceNo == normalized);
        }

        /// <inheritdoc />
        public async Task<List<HistoryArchiveLedgerReferenceGroup>> GetHistoryLedgerReferencesByBoxCodesAsync(
            IReadOnlyCollection<string> boxCodes)
        {
            if (boxCodes == null || boxCodes.Count == 0)
            {
                return [];
            }

            var normalizedCodes = new HashSet<string>(
                boxCodes.Select(code => code?.Trim() ?? string.Empty)
                    .Where(code => !string.IsNullOrWhiteSpace(code)),
                StringComparer.OrdinalIgnoreCase);
            if (normalizedCodes.Count == 0)
            {
                return [];
            }

            return await BuildReferenceGroupsAsync(links => links
                .Where(link => normalizedCodes.Contains(link.BoxCode)));
        }

        /// <inheritdoc />
        public async Task<List<HistoryArchiveLedgerReferenceGroup>> GetHistoryLedgerReferencesInSlotAsync(
            string cabinetName,
            string face,
            int row,
            int column)
        {
            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, face, row, column);
            string slotPrefix = slotKey + "-";

            var boxesInSlot = await _dbContext.HistoryArchiveBoxes
                .Where(box =>
                    (box.BoxCode == slotKey || box.BoxCode.StartsWith(slotPrefix))
                    && box.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
                .Select(box => box.BoxCode)
                .ToListAsync();
            var slotCodes = new HashSet<string>(boxesInSlot, StringComparer.OrdinalIgnoreCase);
            if (slotCodes.Count == 0)
            {
                return [];
            }

            return await BuildReferenceGroupsAsync(links => links
                .Where(link => slotCodes.Contains(link.BoxCode)));
        }

        /// <summary>
        /// 盒驱动构建台账引用组：遍历在库盒与全部链接（内存按盒号过滤），
        /// 按台账行聚合出盒号序列（与投影水合同构）。
        /// </summary>
        private async Task<List<HistoryArchiveLedgerReferenceGroup>> BuildReferenceGroupsAsync(
            Func<IEnumerable<(int BoxId, string BoxCode)>, IEnumerable<(int BoxId, string BoxCode)>> filterLinks)
        {
            var boxIdToCode = await _dbContext.HistoryArchiveBoxes
                .AsNoTracking()
                .Where(box => box.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
                .ToDictionaryAsync(box => box.Id, box => box.BoxCode);

            var boxCodesByBoxId = new Dictionary<int, string>();
            foreach (var (boxId, boxCode) in filterLinks(boxIdToCode.Select(pair => (pair.Key, pair.Value))))
            {
                boxCodesByBoxId[boxId] = boxCode;
            }

            if (boxCodesByBoxId.Count == 0)
            {
                return [];
            }

            var links = await _dbContext.HistoryArchiveBoxLedgerLinks
                .AsNoTracking()
                .Where(link => boxCodesByBoxId.Keys.Contains(link.HistoryArchiveBoxId))
                .OrderBy(link => link.Id)
                .ToListAsync();

            var inStockIdsByKind = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal)
            {
                [HistoryArchiveDisposalDomainValues.MaterialKindTopoMap] =
                    (await _dbContext.TopoMaps.AsNoTracking()
                        .Where(item => item.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
                        .Select(item => item.Id)
                        .ToListAsync())
                    .ToHashSet(),
                [HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto] =
                    (await _dbContext.AerialPhotos.AsNoTracking()
                        .Where(item => item.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
                        .Select(item => item.Id)
                        .ToListAsync())
                    .ToHashSet(),
                [HistoryArchiveDisposalDomainValues.MaterialKindOtherMap] =
                    (await _dbContext.OtherMaps.AsNoTracking()
                        .Where(item => item.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
                        .Select(item => item.Id)
                        .ToListAsync())
                    .ToHashSet()
            };

            var codesByRecord = new Dictionary<(string Kind, int RecordId), List<string>>();
            foreach (var link in links)
            {
                if (!inStockIdsByKind.TryGetValue(link.MaterialKind, out HashSet<int>? inStockIds)
                    || !inStockIds.Contains(link.RecordId)
                    || !boxCodesByBoxId.TryGetValue(link.HistoryArchiveBoxId, out string? boxCode))
                {
                    continue;
                }

                var key = (link.MaterialKind, link.RecordId);
                if (!codesByRecord.TryGetValue(key, out List<string>? codes))
                {
                    codes = new List<string>();
                    codesByRecord[key] = codes;
                }

                codes.Add(boxCode);
            }

            var groups = new List<HistoryArchiveLedgerReferenceGroup>();
            foreach (var pair in codesByRecord)
            {
                IReadOnlyList<string> codes = pair.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                groups.Add(new HistoryArchiveLedgerReferenceGroup
                {
                    MaterialKind = pair.Key.Kind,
                    RecordId = pair.Key.RecordId,
                    BoxCodes = codes,
                    BoxNumberText = string.Join("；", codes)
                });
            }

            return groups;
        }

        /// <inheritdoc />
        public Task<HashSet<string>> GetHistoryDisposalLockedBoxCodesAsync()
        {
            return GetHistoryDisposalLockedBoxCodesCoreAsync();
        }

        /// <inheritdoc />
        public async Task<List<HistoryArchiveBox>> GetHistoryArchiveBoxesByCodesForUpdateAsync(
            IReadOnlyCollection<string> boxCodes)
        {
            if (boxCodes == null || boxCodes.Count == 0)
            {
                return [];
            }

            var normalized = boxCodes
                .Select(code => code?.Trim() ?? string.Empty)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToList();
            if (normalized.Count == 0)
            {
                return [];
            }

            return await _dbContext.HistoryArchiveBoxes
                .Where(box => normalized.Contains(box.BoxCode))
                .ToListAsync();
        }

        /// <inheritdoc />
        public Task<List<HistoryArchiveBox>> GetHistoryArchiveBoxesInSlotForUpdateAsync(
            string cabinetName,
            string face,
            int row,
            int column)
        {
            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, face, row, column);
            string slotPrefix = slotKey + "-";
            return _dbContext.HistoryArchiveBoxes
                .Where(box =>
                    (box.BoxCode == slotKey || box.BoxCode.StartsWith(slotPrefix))
                    && box.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
                .ToListAsync();
        }

        private static readonly int[] HistoryDisposalActiveStatuses =
        [
            HistoryArchiveDisposalRecord.StatusDraft,
            HistoryArchiveDisposalRecord.StatusSubmitted,
            HistoryArchiveDisposalRecord.StatusApproved,
            HistoryArchiveDisposalRecord.StatusSignedUploaded
        ];

        private async Task<HashSet<string>> GetHistoryDisposalLockedBoxCodesCoreAsync()
        {
            List<string> codes = await _dbContext.HistoryArchiveDisposalItems
                .AsNoTracking()
                .Where(item => HistoryDisposalActiveStatuses.Contains(item.DisposalRecord!.Status))
                .Select(item => item.BoxCode)
                .ToListAsync();
            return codes
                .Select(item => item?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public Task<YearlyElectronicArchiveUnit?> GetElectronicUnitForRelocationAsync(int unitId)
        {
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
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item.MediaEntry)
                            .ThenInclude(media => media!.RegisterRecord)
                .FirstOrDefaultAsync(unit => unit.Id == unitId);
        }

        public Task<YearlyElectronicArchiveUnit?> GetElectronicUnitByArchiveNoAsync(string archiveNo)
        {
            string normalized = archiveNo.Trim();
            return _dbContext.YearlyElectronicArchiveUnits
                .Include(unit => unit.MediumLinks)
                    .ThenInclude(link => link.HardDiskMedium)
                        .ThenInclude(medium => medium.Ledger)
                .Include(unit => unit.DiscLinks)
                    .ThenInclude(link => link.OpticalDiscMedium)
                        .ThenInclude(disc => disc!.Ledger)
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
