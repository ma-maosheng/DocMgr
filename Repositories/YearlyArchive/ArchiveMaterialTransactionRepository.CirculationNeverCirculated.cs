using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive
{
    public sealed partial class ArchiveMaterialTransactionRepository
    {
        public async Task<IReadOnlyList<CirculationContainerMasterRow>> SearchNeverCirculatedContainersAsync(
            CirculationLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            if (!CirculationLedgerNeverCirculatedSupport.CanIncludeNeverCirculated(criteria))
            {
                return Array.Empty<CirculationContainerMasterRow>();
            }

            var everCirculatedKeys = await LoadEverCirculatedContainerKeysAsync();
            string keyword = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.Keyword);

            var archiveBoxes = await BuildNeverCirculatedArchiveBoxQuery(criteria, keyword)
                .Take(MaterialTransactionLedgerSearchSupport.DefaultMaxResults)
                .ToListAsync();
            var electronicUnits = await BuildNeverCirculatedElectronicUnitQuery(criteria, keyword)
                .Take(MaterialTransactionLedgerSearchSupport.DefaultMaxResults)
                .ToListAsync();

            var boxIds = archiveBoxes.Select(box => box.Id).ToList();
            var unitIds = electronicUnits.Select(unit => unit.Id).ToList();

            var factStats = await LoadInArchiveFactStatsByContainerAsync(boxIds, unitIds);

            var rows = new List<CirculationContainerMasterRow>();

            foreach (var box in archiveBoxes)
            {
                var key = new CirculationContainerKey(box.ArchiveSequenceNo.Trim(), ArchiveContainerKind.ArchiveBox);
                if (everCirculatedKeys.Contains(key) || !factStats.TryGetValue((ArchiveContainerKind.ArchiveBox, box.Id), out var stats))
                {
                    continue;
                }

                rows.Add(CirculationLedgerNeverCirculatedSupport.MapArchiveBoxMasterRow(
                    box,
                    stats.MaterialCount,
                    stats.RepresentativeFilingFactId));
            }

            foreach (var unit in electronicUnits)
            {
                var key = new CirculationContainerKey(unit.ElectronicArchiveNo.Trim(), ArchiveContainerKind.ElectronicBag);
                if (everCirculatedKeys.Contains(key) || !factStats.TryGetValue((ArchiveContainerKind.ElectronicBag, unit.Id), out var stats))
                {
                    continue;
                }

                rows.Add(CirculationLedgerNeverCirculatedSupport.MapElectronicUnitMasterRow(
                    unit,
                    stats.MaterialCount,
                    stats.RepresentativeFilingFactId));
            }

            return rows
                .OrderByDescending(row => row.LatestOperatedAt)
                .ThenBy(row => row.ContainerCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<HashSet<CirculationContainerKey>> LoadEverCirculatedContainerKeysAsync()
        {
            var transactionRows = await (
                from tx in _dbContext.YearlyArchiveMaterialTransactions.AsNoTracking()
                join fact in _dbContext.YearlyArchiveFilingFacts.AsNoTracking() on tx.FilingFactId equals fact.Id
                where tx.TransactionType == MaterialTransactionDomainValues.TypeOutbound
                    || tx.TransactionType == MaterialTransactionDomainValues.TypeReturn
                select new
                {
                    fact.ContainerCode,
                    fact.ContainerKind,
                    tx.BeforeContainerCode,
                    tx.AfterContainerCode
                }).ToListAsync();

            var keys = new HashSet<CirculationContainerKey>();
            foreach (var row in transactionRows)
            {
                AddContainerKey(keys, row.ContainerCode, row.ContainerKind);
                AddContainerKey(keys, row.BeforeContainerCode, row.ContainerKind);
                AddContainerKey(keys, row.AfterContainerCode, row.ContainerKind);
            }

            return keys;
        }

        private static void AddContainerKey(
            HashSet<CirculationContainerKey> keys,
            string? containerCode,
            ArchiveContainerKind containerKind)
        {
            if (string.IsNullOrWhiteSpace(containerCode))
            {
                return;
            }

            keys.Add(new CirculationContainerKey(containerCode.Trim(), containerKind));
        }

        private IQueryable<YearlyArchiveBox> BuildNeverCirculatedArchiveBoxQuery(
            CirculationLedgerSearchCriteria criteria,
            string keyword)
        {
            var query = _dbContext.YearlyArchiveBoxes
                .AsNoTracking()
                .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                    && !string.IsNullOrWhiteSpace(box.ArchiveSequenceNo));

            if (criteria.OperatedFrom.HasValue)
            {
                DateTime from = criteria.OperatedFrom.Value.Date;
                query = query.Where(box => box.ArchivedDate >= from);
            }

            if (criteria.OperatedTo.HasValue)
            {
                DateTime to = criteria.OperatedTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(box => box.ArchivedDate <= to);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(box =>
                    box.ArchiveSequenceNo.Contains(keyword)
                    || box.ProjectName.Contains(keyword)
                    || box.Year.Contains(keyword)
                    || box.BoxLocationCode.Contains(keyword));
            }

            return query;
        }

        private IQueryable<YearlyElectronicArchiveUnit> BuildNeverCirculatedElectronicUnitQuery(
            CirculationLedgerSearchCriteria criteria,
            string keyword)
        {
            var query = _dbContext.YearlyElectronicArchiveUnits
                .AsNoTracking()
                .Where(unit => unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                    && !string.IsNullOrWhiteSpace(unit.ElectronicArchiveNo));

            if (criteria.OperatedFrom.HasValue)
            {
                DateTime from = criteria.OperatedFrom.Value.Date;
                query = query.Where(unit => unit.ArchivedDate >= from);
            }

            if (criteria.OperatedTo.HasValue)
            {
                DateTime to = criteria.OperatedTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(unit => unit.ArchivedDate <= to);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(unit =>
                    unit.ElectronicArchiveNo.Contains(keyword)
                    || unit.ProjectName.Contains(keyword)
                    || unit.Year.Contains(keyword)
                    || unit.StorageLocation.Contains(keyword));
            }

            return query;
        }

        private async Task<Dictionary<(ArchiveContainerKind Kind, int ContainerId), ContainerFactStats>> LoadInArchiveFactStatsByContainerAsync(
            IReadOnlyCollection<int> boxIds,
            IReadOnlyCollection<int> unitIds)
        {
            var stats = new Dictionary<(ArchiveContainerKind, int), ContainerFactStats>();

            if (boxIds.Count > 0)
            {
                var boxFacts = await _dbContext.YearlyArchiveFilingFacts
                    .AsNoTracking()
                    .Where(fact => fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                        && boxIds.Contains(fact.ContainerId)
                        && fact.LifecycleStatus == FilingFactLifecycleStatus.InArchive)
                    .GroupBy(fact => fact.ContainerId)
                    .Select(group => new
                    {
                        ContainerId = group.Key,
                        MaterialCount = group.Count(),
                        RepresentativeFilingFactId = group.Min(fact => fact.Id)
                    })
                    .ToListAsync();

                foreach (var item in boxFacts)
                {
                    stats[(ArchiveContainerKind.ArchiveBox, item.ContainerId)] = new ContainerFactStats(
                        item.MaterialCount,
                        item.RepresentativeFilingFactId);
                }
            }

            if (unitIds.Count > 0)
            {
                var unitFacts = await _dbContext.YearlyArchiveFilingFacts
                    .AsNoTracking()
                    .Where(fact => fact.ContainerKind == ArchiveContainerKind.ElectronicBag
                        && unitIds.Contains(fact.ContainerId)
                        && fact.LifecycleStatus == FilingFactLifecycleStatus.InArchive)
                    .GroupBy(fact => fact.ContainerId)
                    .Select(group => new
                    {
                        ContainerId = group.Key,
                        MaterialCount = group.Count(),
                        RepresentativeFilingFactId = group.Min(fact => fact.Id)
                    })
                    .ToListAsync();

                foreach (var item in unitFacts)
                {
                    stats[(ArchiveContainerKind.ElectronicBag, item.ContainerId)] = new ContainerFactStats(
                        item.MaterialCount,
                        item.RepresentativeFilingFactId);
                }
            }

            return stats;
        }

        private readonly record struct ContainerFactStats(int MaterialCount, int RepresentativeFilingFactId);
    }
}
