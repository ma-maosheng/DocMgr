using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 流转台账：按容器（档案盒 / 电子介质袋）聚合一级行与明细筛选。
    /// </summary>
    internal static class CirculationLedgerGroupingSupport
    {
        public static IReadOnlyList<CirculationContainerMasterRow> BuildCirculationMasters(
            IReadOnlyList<MaterialTransactionLedgerRow> rows,
            ArchiveContainerKind containerKind)
        {
            var groups = GroupCirculationRows(rows, containerKind);
            return groups
                .Select(group => BuildCirculationMasterRow(group.Key, group.Value))
                .OrderByDescending(row => row.LatestOperatedAt)
                .ThenBy(row => row.ContainerCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<CirculationContainerMasterRow> MergeNeverCirculatedContainers(
            IReadOnlyList<CirculationContainerMasterRow> circulationMasters,
            IReadOnlyList<CirculationContainerMasterRow> neverCirculatedMasters,
            ArchiveContainerKind containerKind)
        {
            var existingKeys = circulationMasters
                .Where(row => row.ContainerKind == containerKind)
                .Select(row => new CirculationContainerKey(row.ContainerCode, row.ContainerKind))
                .ToHashSet();

            var merged = circulationMasters
                .Where(row => row.ContainerKind == containerKind)
                .ToList();

            foreach (var row in neverCirculatedMasters.Where(item => item.ContainerKind == containerKind))
            {
                var key = new CirculationContainerKey(row.ContainerCode, row.ContainerKind);
                if (existingKeys.Contains(key))
                {
                    continue;
                }

                merged.Add(row);
                existingKeys.Add(key);
            }

            return merged
                .OrderByDescending(row => row.HasLoss)
                .ThenByDescending(row => row.HasCirculationActivity)
                .ThenByDescending(row => row.LatestOperatedAt)
                .ThenBy(row => row.ContainerCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<OutboundProcessNodeContainerMasterRow> BuildProcessNodeMasters(
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> rows,
            ArchiveContainerKind containerKind)
        {
            var groups = GroupProcessNodeRows(rows, containerKind);
            return groups
                .Select(group => BuildProcessNodeMasterRow(group.Key, group.Value))
                .OrderByDescending(row => row.LatestOperatedAt)
                .ThenBy(row => row.ContainerCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<MaterialTransactionLedgerRow> FilterCirculationDetails(
            IReadOnlyList<MaterialTransactionLedgerRow> rows,
            CirculationContainerMasterRow? master)
        {
            if (master == null || string.IsNullOrWhiteSpace(master.ContainerCode))
            {
                return Array.Empty<MaterialTransactionLedgerRow>();
            }

            var key = new CirculationContainerKey(master.ContainerCode, master.ContainerKind);
            return rows
                .Where(row => row.ContainerKind == key.ContainerKind && RowTouchesContainer(row, key))
                .OrderByDescending(row => row.OperatedAt)
                .ThenByDescending(row => row.TransactionId)
                .ToList();
        }

        public static IReadOnlyList<MaterialOutboundProcessNodeSearchRow> FilterProcessNodeDetails(
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> rows,
            OutboundProcessNodeContainerMasterRow? master)
        {
            if (master == null || string.IsNullOrWhiteSpace(master.ContainerCode))
            {
                return Array.Empty<MaterialOutboundProcessNodeSearchRow>();
            }

            return FilterProcessNodeDetails(rows, master.ContainerCode, master.ContainerKind);
        }

        public static IReadOnlyList<MaterialOutboundProcessNodeSearchRow> FilterProcessNodeDetails(
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> rows,
            CirculationContainerMasterRow? master)
        {
            if (master == null || string.IsNullOrWhiteSpace(master.ContainerCode))
            {
                return Array.Empty<MaterialOutboundProcessNodeSearchRow>();
            }

            return FilterProcessNodeDetails(rows, master.ContainerCode, master.ContainerKind);
        }

        private static IReadOnlyList<MaterialOutboundProcessNodeSearchRow> FilterProcessNodeDetails(
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> rows,
            string containerCode,
            ArchiveContainerKind containerKind)
        {
            return rows
                .Where(row =>
                    row.ContainerKind == containerKind
                    && string.Equals(row.ContainerCode, containerCode, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(row => row.OperatedAt)
                .ThenByDescending(row => row.SyncEntryId)
                .ToList();
        }

        private static Dictionary<CirculationContainerKey, List<MaterialTransactionLedgerRow>> GroupCirculationRows(
            IReadOnlyList<MaterialTransactionLedgerRow> rows,
            ArchiveContainerKind containerKind)
        {
            var groups = new Dictionary<CirculationContainerKey, List<MaterialTransactionLedgerRow>>();

            foreach (var row in rows.Where(item => item.ContainerKind == containerKind))
            {
                AddCirculationRowToGroup(groups, new CirculationContainerKey(row.ContainerCode, row.ContainerKind), row);

                if (TryCreateContainerKey(row.BeforeContainerCode, containerKind, out var beforeKey)
                    && !beforeKey.Equals(new CirculationContainerKey(row.ContainerCode, row.ContainerKind)))
                {
                    AddCirculationRowToGroup(groups, beforeKey, row);
                }

                if (TryCreateContainerKey(row.AfterContainerCode, containerKind, out var afterKey)
                    && !afterKey.Equals(new CirculationContainerKey(row.ContainerCode, row.ContainerKind)))
                {
                    AddCirculationRowToGroup(groups, afterKey, row);
                }
            }

            return groups;
        }

        private static Dictionary<CirculationContainerKey, List<MaterialOutboundProcessNodeSearchRow>> GroupProcessNodeRows(
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> rows,
            ArchiveContainerKind containerKind)
        {
            var groups = new Dictionary<CirculationContainerKey, List<MaterialOutboundProcessNodeSearchRow>>();

            foreach (var row in rows.Where(item => item.ContainerKind == containerKind))
            {
                if (!TryCreateContainerKey(row.ContainerCode, containerKind, out var key))
                {
                    continue;
                }

                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<MaterialOutboundProcessNodeSearchRow>();
                    groups[key] = list;
                }

                list.Add(row);
            }

            return groups;
        }

        private static CirculationContainerMasterRow BuildCirculationMasterRow(
            CirculationContainerKey key,
            IReadOnlyList<MaterialTransactionLedgerRow> groupRows)
        {
            var latest = groupRows
                .OrderByDescending(row => row.OperatedAt)
                .ThenByDescending(row => row.TransactionId)
                .First();
            var snapshot = groupRows
                .FirstOrDefault(row => string.Equals(row.ContainerCode, key.ContainerCode, StringComparison.OrdinalIgnoreCase))
                ?? latest;

            return new CirculationContainerMasterRow
            {
                ContainerCode = key.ContainerCode,
                ContainerKind = key.ContainerKind,
                Year = snapshot.ContainerYear,
                ProjectName = snapshot.ContainerProjectName,
                LocationDisplay = snapshot.ContainerLocationDisplay,
                ContainerStatusDisplay = snapshot.ContainerStatusDisplay,
                MaterialCount = groupRows.Select(row => row.FilingFactId).Distinct().Count(),
                TransactionCount = groupRows.Count,
                LatestOperatedAt = latest.OperatedAt,
                LatestTransactionTypeDisplay = latest.TransactionTypeDisplay,
                RepresentativeFilingFactId = latest.FilingFactId,
                HasLoss = groupRows.Any(row => row.HasLoss)
            };
        }

        private static OutboundProcessNodeContainerMasterRow BuildProcessNodeMasterRow(
            CirculationContainerKey key,
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> groupRows)
        {
            var latest = groupRows
                .OrderByDescending(row => row.OperatedAt)
                .ThenByDescending(row => row.SyncEntryId)
                .First();
            var snapshot = groupRows
                .FirstOrDefault(row => string.Equals(row.ContainerCode, key.ContainerCode, StringComparison.OrdinalIgnoreCase))
                ?? latest;

            var outboundNos = groupRows
                .Select(row => row.OutboundNo.Trim())
                .Where(no => !string.IsNullOrWhiteSpace(no))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(no => no, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new OutboundProcessNodeContainerMasterRow
            {
                ContainerCode = key.ContainerCode,
                ContainerKind = key.ContainerKind,
                Year = snapshot.ContainerYear,
                ProjectName = snapshot.ContainerProjectName,
                LocationDisplay = snapshot.ContainerLocationDisplay,
                ContainerStatusDisplay = snapshot.ContainerStatusDisplay,
                MaterialCount = groupRows.Select(row => row.FilingFactId).Distinct().Count(),
                NodeCount = groupRows.Count,
                RelatedOutboundSummary = BuildOutboundSummary(outboundNos),
                LatestOperatedAt = latest.OperatedAt,
                LatestNodeSummary = $"{latest.NodeCategoryDisplay} · {latest.ProcessNodeDisplay}",
                RepresentativeFilingFactId = latest.FilingFactId
            };
        }

        private static string BuildOutboundSummary(IReadOnlyList<string> outboundNos)
        {
            if (outboundNos.Count == 0)
            {
                return "—";
            }

            if (outboundNos.Count == 1)
            {
                return outboundNos[0];
            }

            return $"{outboundNos[0]} 等{outboundNos.Count}单";
        }

        private static void AddCirculationRowToGroup(
            Dictionary<CirculationContainerKey, List<MaterialTransactionLedgerRow>> groups,
            CirculationContainerKey key,
            MaterialTransactionLedgerRow row)
        {
            if (!TryCreateContainerKey(key.ContainerCode, key.ContainerKind, out var normalizedKey))
            {
                return;
            }

            if (!groups.TryGetValue(normalizedKey, out var list))
            {
                list = new List<MaterialTransactionLedgerRow>();
                groups[normalizedKey] = list;
            }

            list.Add(row);
        }

        private static bool RowTouchesContainer(MaterialTransactionLedgerRow row, CirculationContainerKey key)
        {
            return string.Equals(row.ContainerCode, key.ContainerCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.BeforeContainerCode, key.ContainerCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.AfterContainerCode, key.ContainerCode, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCreateContainerKey(
            string? containerCode,
            ArchiveContainerKind containerKind,
            out CirculationContainerKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(containerCode))
            {
                return false;
            }

            key = new CirculationContainerKey(containerCode.Trim(), containerKind);
            return true;
        }
    }
}
