using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive
{
    public sealed partial class ArchiveMaterialTransactionRepository
    {
        public async Task<IReadOnlyList<MaterialTransactionLedgerRow>> SearchRelocationLedgerAsync(
            RelocationLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            var query = BuildRelocationLedgerQuery(criteria);
            var rows = await query
                .OrderByDescending(item => item.tx.OperatedAt)
                .ThenByDescending(item => item.tx.Id)
                .Take(MaterialTransactionLedgerSearchSupport.DefaultMaxResults)
                .ToListAsync();

            var containerLookup = await LoadContainerLookupAsync(rows.Select(item => item.fact));
            return rows
                .Select(item => MaterialTransactionLedgerSearchSupport.MapRow(
                    item.tx,
                    item.fact,
                    item.RelocationMode,
                    containerLookup.TryGetArchiveBox(item.fact),
                    containerLookup.TryGetElectronicUnit(item.fact)))
                .ToList();
        }

        private async Task<CirculationContainerLookup> LoadContainerLookupAsync(IEnumerable<YearlyArchiveFilingFact> facts)
        {
            var factList = facts.ToList();
            var boxIds = factList
                .Where(fact => fact.ContainerKind == ArchiveContainerKind.ArchiveBox && fact.ContainerId > 0)
                .Select(fact => fact.ContainerId)
                .Distinct()
                .ToList();
            var unitIds = factList
                .Where(fact => fact.ContainerKind == ArchiveContainerKind.ElectronicBag && fact.ContainerId > 0)
                .Select(fact => fact.ContainerId)
                .Distinct()
                .ToList();

            var boxesById = boxIds.Count == 0
                ? new Dictionary<int, YearlyArchiveBox>()
                : await _dbContext.YearlyArchiveBoxes
                    .AsNoTracking()
                    .Where(box => boxIds.Contains(box.Id))
                    .ToDictionaryAsync(box => box.Id);

            var unitsById = unitIds.Count == 0
                ? new Dictionary<int, YearlyElectronicArchiveUnit>()
                : await _dbContext.YearlyElectronicArchiveUnits
                    .AsNoTracking()
                    .Where(unit => unitIds.Contains(unit.Id))
                    .ToDictionaryAsync(unit => unit.Id);

            return new CirculationContainerLookup(boxesById, unitsById);
        }

        private sealed class CirculationContainerLookup
        {
            private readonly IReadOnlyDictionary<int, YearlyArchiveBox> _boxesById;
            private readonly IReadOnlyDictionary<int, YearlyElectronicArchiveUnit> _unitsById;

            public CirculationContainerLookup(
                IReadOnlyDictionary<int, YearlyArchiveBox> boxesById,
                IReadOnlyDictionary<int, YearlyElectronicArchiveUnit> unitsById)
            {
                _boxesById = boxesById;
                _unitsById = unitsById;
            }

            public YearlyArchiveBox? TryGetArchiveBox(YearlyArchiveFilingFact fact)
            {
                return fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                    && _boxesById.TryGetValue(fact.ContainerId, out var box)
                    ? box
                    : null;
            }

            public YearlyElectronicArchiveUnit? TryGetElectronicUnit(YearlyArchiveFilingFact fact)
            {
                return fact.ContainerKind == ArchiveContainerKind.ElectronicBag
                    && _unitsById.TryGetValue(fact.ContainerId, out var unit)
                    ? unit
                    : null;
            }
        }

        public async Task<IReadOnlyList<MaterialTransactionLedgerRow>> SearchCirculationLedgerAsync(
            CirculationLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            var query = BuildCirculationLedgerQuery(criteria);
            var rows = await query
                .OrderByDescending(item => item.tx.OperatedAt)
                .ThenByDescending(item => item.tx.Id)
                .Take(MaterialTransactionLedgerSearchSupport.DefaultMaxResults)
                .ToListAsync();

            var containerLookup = await LoadContainerLookupAsync(rows.Select(item => item.fact));
            return rows
                .Select(item => MaterialTransactionLedgerSearchSupport.MapRow(
                    item.tx,
                    item.fact,
                    string.Empty,
                    containerLookup.TryGetArchiveBox(item.fact),
                    containerLookup.TryGetElectronicUnit(item.fact)))
                .ToList();
        }

        public async Task<IReadOnlyList<MaterialOutboundProcessNodeSearchRow>> SearchOutboundProcessNodeLedgerAsync(
            OutboundProcessNodeLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            var query = BuildOutboundProcessNodeLedgerQuery(criteria);
            var rows = await query
                .OrderByDescending(item => item.entry.UpdatedAt ?? item.entry.CreatedAt)
                .ThenByDescending(item => item.entry.Id)
                .Take(MaterialTransactionLedgerSearchSupport.DefaultMaxResults)
                .ToListAsync();

            var containerLookup = await LoadContainerLookupAsync(rows.Select(item => item.fact));
            return rows
                .Select(item => MaterialTransactionLedgerSearchSupport.MapProcessNodeSearchRow(
                    item.entry,
                    item.record,
                    item.item,
                    item.fact,
                    containerLookup.TryGetArchiveBox(item.fact),
                    containerLookup.TryGetElectronicUnit(item.fact)))
                .ToList();
        }

        private IQueryable<RelocationLedgerQueryRow> BuildRelocationLedgerQuery(RelocationLedgerSearchCriteria criteria)
        {
            string keyword = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.Keyword);
            string businessNo = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.BusinessNo);
            string operatorName = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.OperatorName);
            string mediaKind = criteria.MediaKind?.Trim() ?? string.Empty;
            string relocationMode = criteria.RelocationMode?.Trim() ?? string.Empty;

            var query =
                from tx in _dbContext.YearlyArchiveMaterialTransactions.AsNoTracking()
                join fact in _dbContext.YearlyArchiveFilingFacts.AsNoTracking() on tx.FilingFactId equals fact.Id
                join record in _dbContext.YearlyArchiveRelocationRecords.AsNoTracking()
                    on tx.BusinessNo equals record.RelocationNo into records
                from record in records.DefaultIfEmpty()
                where tx.TransactionType == MaterialTransactionDomainValues.TypeRelocation
                select new RelocationLedgerQueryRow
                {
                    tx = tx,
                    fact = fact,
                    RelocationMode = record != null ? record.RelocationMode : string.Empty
                };

            if (criteria.OperatedFrom.HasValue)
            {
                DateTime from = criteria.OperatedFrom.Value.Date;
                query = query.Where(item => item.tx.OperatedAt >= from);
            }

            if (criteria.OperatedTo.HasValue)
            {
                DateTime to = criteria.OperatedTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(item => item.tx.OperatedAt <= to);
            }

            if (!string.IsNullOrWhiteSpace(mediaKind))
            {
                query = query.Where(item => item.fact.MediaKind == mediaKind);
            }

            if (!string.IsNullOrWhiteSpace(relocationMode))
            {
                query = query.Where(item => item.RelocationMode == relocationMode);
            }

            if (!string.IsNullOrWhiteSpace(businessNo))
            {
                query = query.Where(item => item.tx.BusinessNo.Contains(businessNo));
            }

            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                query = query.Where(item => item.tx.OperatorName.Contains(operatorName));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(item =>
                    item.fact.FilingFactNo.Contains(keyword)
                    || item.fact.FormNo.Contains(keyword)
                    || item.fact.MaterialName.Contains(keyword)
                    || item.fact.ItemName.Contains(keyword)
                    || item.fact.ProjectName.Contains(keyword)
                    || item.tx.Summary.Contains(keyword)
                    || item.tx.BeforeContainerCode.Contains(keyword)
                    || item.tx.AfterContainerCode.Contains(keyword)
                    || item.tx.BeforeStorageLocation.Contains(keyword)
                    || item.tx.AfterStorageLocation.Contains(keyword));
            }

            return query;
        }

        private IQueryable<CirculationLedgerQueryRow> BuildCirculationLedgerQuery(CirculationLedgerSearchCriteria criteria)
        {
            string keyword = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.Keyword);
            string businessNo = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.BusinessNo);
            string operatorName = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.OperatorName);
            string mediaKind = criteria.MediaKind?.Trim() ?? string.Empty;
            string transactionType = criteria.TransactionType?.Trim() ?? string.Empty;

            var allowedTypes = new[]
            {
                MaterialTransactionDomainValues.TypeOutbound,
                MaterialTransactionDomainValues.TypeReturn
            };

            var query =
                from tx in _dbContext.YearlyArchiveMaterialTransactions.AsNoTracking()
                join fact in _dbContext.YearlyArchiveFilingFacts.AsNoTracking() on tx.FilingFactId equals fact.Id
                where allowedTypes.Contains(tx.TransactionType)
                select new CirculationLedgerQueryRow
                {
                    tx = tx,
                    fact = fact
                };

            if (criteria.OperatedFrom.HasValue)
            {
                DateTime from = criteria.OperatedFrom.Value.Date;
                query = query.Where(item => item.tx.OperatedAt >= from);
            }

            if (criteria.OperatedTo.HasValue)
            {
                DateTime to = criteria.OperatedTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(item => item.tx.OperatedAt <= to);
            }

            if (!string.IsNullOrWhiteSpace(transactionType))
            {
                query = query.Where(item => item.tx.TransactionType == transactionType);
            }

            if (!string.IsNullOrWhiteSpace(mediaKind))
            {
                query = query.Where(item => item.fact.MediaKind == mediaKind);
            }

            if (!string.IsNullOrWhiteSpace(businessNo))
            {
                query = query.Where(item => item.tx.BusinessNo.Contains(businessNo));
            }

            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                query = query.Where(item => item.tx.OperatorName.Contains(operatorName));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(item =>
                    item.fact.FilingFactNo.Contains(keyword)
                    || item.fact.FormNo.Contains(keyword)
                    || item.fact.MaterialName.Contains(keyword)
                    || item.fact.ItemName.Contains(keyword)
                    || item.fact.ProjectName.Contains(keyword)
                    || item.tx.Summary.Contains(keyword)
                    || item.tx.Remark.Contains(keyword));
            }

            return query;
        }

        private IQueryable<OutboundProcessNodeLedgerQueryRow> BuildOutboundProcessNodeLedgerQuery(
            OutboundProcessNodeLedgerSearchCriteria criteria)
        {
            string keyword = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.Keyword);
            string outboundNo = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.OutboundNo);
            string operatorName = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.OperatorName);
            string applicantName = MaterialTransactionLedgerSearchSupport.NormalizeKeyword(criteria.ApplicantName);
            string nodeCategory = criteria.NodeCategory?.Trim() ?? string.Empty;

            var query =
                from entry in _dbContext.YearlyArchiveOutboundSyncEntries.AsNoTracking()
                join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                    on entry.OutboundRecordId equals record.Id
                join item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
                    on entry.OutboundItemId equals item.Id
                join fact in _dbContext.YearlyArchiveFilingFacts.AsNoTracking()
                    on entry.FilingFactId equals fact.Id
                select new OutboundProcessNodeLedgerQueryRow
                {
                    entry = entry,
                    record = record,
                    item = item,
                    fact = fact
                };

            if (criteria.OperatedFrom.HasValue)
            {
                DateTime from = criteria.OperatedFrom.Value.Date;
                query = query.Where(item => (item.entry.UpdatedAt ?? item.entry.CreatedAt) >= from);
            }

            if (criteria.OperatedTo.HasValue)
            {
                DateTime to = criteria.OperatedTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(item => (item.entry.UpdatedAt ?? item.entry.CreatedAt) <= to);
            }

            if (!string.IsNullOrWhiteSpace(outboundNo))
            {
                query = query.Where(item => item.record.OutboundNo.Contains(outboundNo));
            }

            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                query = query.Where(item => item.entry.OperatedBy.Contains(operatorName));
            }

            if (!string.IsNullOrWhiteSpace(applicantName))
            {
                query = query.Where(item => item.record.ApplicantName.Contains(applicantName));
            }

            if (string.Equals(nodeCategory, OutboundProcessNodeCategoryFilter.Reservation, StringComparison.Ordinal))
            {
                query = query.Where(item =>
                    item.entry.Phase == ArchiveOutboundDomainValues.SyncEntryPhaseActive
                    || item.entry.Phase == ArchiveOutboundDomainValues.SyncEntryPhasePending);
            }
            else if (string.Equals(nodeCategory, OutboundProcessNodeCategoryFilter.Cancelled, StringComparison.Ordinal))
            {
                query = query.Where(item => item.entry.Phase == ArchiveOutboundDomainValues.SyncEntryPhaseCancelled);
            }
            else if (string.Equals(nodeCategory, OutboundProcessNodeCategoryFilter.Confirmed, StringComparison.Ordinal))
            {
                query = query.Where(item => item.entry.Phase == ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(item =>
                    item.fact.FilingFactNo.Contains(keyword)
                    || item.fact.FormNo.Contains(keyword)
                    || item.fact.MaterialName.Contains(keyword)
                    || item.fact.ItemName.Contains(keyword)
                    || item.entry.Remark.Contains(keyword));
            }

            return query;
        }

        private sealed class RelocationLedgerQueryRow
        {
            public YearlyArchiveMaterialTransaction tx { get; init; } = null!;

            public YearlyArchiveFilingFact fact { get; init; } = null!;

            public string RelocationMode { get; init; } = string.Empty;
        }

        private sealed class CirculationLedgerQueryRow
        {
            public YearlyArchiveMaterialTransaction tx { get; init; } = null!;

            public YearlyArchiveFilingFact fact { get; init; } = null!;
        }

        private sealed class OutboundProcessNodeLedgerQueryRow
        {
            public YearlyArchiveOutboundSyncEntry entry { get; init; } = null!;

            public YearlyArchiveOutboundRecord record { get; init; } = null!;

            public YearlyArchiveOutboundItem item { get; init; } = null!;

            public YearlyArchiveFilingFact fact { get; init; } = null!;
        }
    }
}
