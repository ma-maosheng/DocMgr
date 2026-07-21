using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 流转台账三级结构：容器 → 业务单 → 明细时间线。
    /// </summary>
    internal static class CirculationLedgerHierarchySupport
    {
        public static IReadOnlyList<CirculationContainerMasterRow> BuildContainerMasters(
            IReadOnlyList<MaterialTransactionLedgerRow> transactions,
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> processNodes,
            IReadOnlyList<CirculationContainerMasterRow> neverCirculatedMasters,
            ArchiveContainerKind containerKind)
        {
            var keys = new Dictionary<CirculationContainerKey, ContainerMasterBuilder>();

            foreach (var row in transactions.Where(item => item.ContainerKind == containerKind))
            {
                RegisterContainerKey(keys, new CirculationContainerKey(row.ContainerCode, row.ContainerKind), row);
                if (TryCreateContainerKey(row.BeforeContainerCode, containerKind, out var beforeKey))
                {
                    RegisterContainerKey(keys, beforeKey, row);
                }

                if (TryCreateContainerKey(row.AfterContainerCode, containerKind, out var afterKey))
                {
                    RegisterContainerKey(keys, afterKey, row);
                }
            }

            foreach (var row in processNodes.Where(item => item.ContainerKind == containerKind))
            {
                if (!TryCreateContainerKey(row.ContainerCode, containerKind, out var key))
                {
                    continue;
                }

                if (!keys.TryGetValue(key, out var builder))
                {
                    builder = ContainerMasterBuilder.FromProcessNodeSnapshot(row);
                    keys[key] = builder;
                }

                builder.ProcessNodeCount++;
                builder.TouchActivity(row.OperatedAt, row.NodeCategoryDisplay, row.FilingFactId);
            }

            var circulated = keys.Values
                .Select(builder => builder.Build())
                .OrderByDescending(row => row.HasCirculationActivity)
                .ThenByDescending(row => row.LatestOperatedAt)
                .ThenBy(row => row.ContainerCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return CirculationLedgerGroupingSupport.MergeNeverCirculatedContainers(
                circulated,
                neverCirculatedMasters,
                containerKind);
        }

        public static IReadOnlyList<CirculationLedgerBusinessRow> BuildBusinessRows(
            CirculationContainerMasterRow? container,
            IReadOnlyList<MaterialTransactionLedgerRow> transactions,
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> processNodes)
        {
            if (container == null || string.IsNullOrWhiteSpace(container.ContainerCode))
            {
                return Array.Empty<CirculationLedgerBusinessRow>();
            }

            var groups = new Dictionary<CirculationLedgerBusinessKey, BusinessGroupBuilder>();

            foreach (var row in CirculationLedgerGroupingSupport.FilterCirculationDetails(transactions, container))
            {
                var key = ResolveTransactionBusinessKey(row);
                if (!groups.TryGetValue(key, out var builder))
                {
                    builder = new BusinessGroupBuilder(key);
                    groups[key] = builder;
                }

                builder.AddTransaction(row);
            }

            foreach (var row in CirculationLedgerGroupingSupport.FilterProcessNodeDetails(processNodes, container))
            {
                var key = new CirculationLedgerBusinessKey(
                    CirculationLedgerBusinessKind.Outbound,
                    row.OutboundNo.Trim());
                if (!groups.TryGetValue(key, out var builder))
                {
                    builder = new BusinessGroupBuilder(key);
                    groups[key] = builder;
                }

                builder.AddProcessNode(row);
            }

            return groups.Values
                .Select(builder => builder.Build())
                .OrderByDescending(row => row.LatestOperatedAt)
                .ThenBy(row => row.BusinessNo, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<CirculationLedgerSubItemRow> BuildSubItemRows(
            CirculationContainerMasterRow? container,
            CirculationLedgerBusinessRow? business,
            IReadOnlyList<MaterialTransactionLedgerRow> transactions,
            IReadOnlyList<MaterialOutboundProcessNodeSearchRow> processNodes)
        {
            if (container == null || business == null)
            {
                return Array.Empty<CirculationLedgerSubItemRow>();
            }

            var items = new List<CirculationLedgerSubItemRow>();

            foreach (var row in CirculationLedgerGroupingSupport.FilterCirculationDetails(transactions, container))
            {
                if (!MatchesBusiness(row, business))
                {
                    continue;
                }

                items.Add(MapTransactionSubItem(row));
            }

            if (string.Equals(business.BusinessKind, CirculationLedgerBusinessKind.Outbound, StringComparison.Ordinal))
            {
                foreach (var row in CirculationLedgerGroupingSupport.FilterProcessNodeDetails(
                             processNodes,
                             container))
                {
                    if (!string.Equals(row.OutboundNo, business.BusinessNo, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    items.Add(MapProcessNodeSubItem(row));
                }
            }

            return items
                .OrderByDescending(row => row.OperatedAt)
                .ThenByDescending(row => row.ItemId)
                .ToList();
        }

        private static CirculationLedgerBusinessKey ResolveTransactionBusinessKey(MaterialTransactionLedgerRow row)
        {
            if (string.Equals(row.TransactionType, MaterialTransactionDomainValues.TypeReturn, StringComparison.Ordinal))
            {
                return new CirculationLedgerBusinessKey(CirculationLedgerBusinessKind.Return, row.BusinessNo.Trim());
            }

            return new CirculationLedgerBusinessKey(CirculationLedgerBusinessKind.Outbound, row.BusinessNo.Trim());
        }

        private static bool MatchesBusiness(MaterialTransactionLedgerRow row, CirculationLedgerBusinessRow business)
        {
            var key = ResolveTransactionBusinessKey(row);
            return string.Equals(key.Kind, business.BusinessKind, StringComparison.Ordinal)
                && string.Equals(key.BusinessNo, business.BusinessNo, StringComparison.OrdinalIgnoreCase);
        }

        private static CirculationLedgerSubItemRow MapTransactionSubItem(MaterialTransactionLedgerRow row)
        {
            return new CirculationLedgerSubItemRow
            {
                ItemId = row.TransactionId,
                Kind = CirculationLedgerSubItemKind.PhysicalTransaction,
                OperatedAt = row.OperatedAt,
                CategoryDisplay = row.TransactionTypeDisplay,
                DetailDisplay = row.Summary,
                FilingFactNo = row.FilingFactNo,
                MaterialName = row.MaterialName,
                ItemName = row.ItemName,
                LocationChangeDisplay = row.LocationChangeDisplay,
                LifecycleChangeDisplay = row.LifecycleChangeDisplay,
                OperatorName = row.OperatorName,
                Remark = row.Remark,
                FilingFactId = row.FilingFactId
            };
        }

        private static CirculationLedgerSubItemRow MapProcessNodeSubItem(MaterialOutboundProcessNodeSearchRow row)
        {
            return new CirculationLedgerSubItemRow
            {
                ItemId = row.SyncEntryId,
                Kind = CirculationLedgerSubItemKind.ProcessNode,
                OperatedAt = row.OperatedAt,
                CategoryDisplay = row.NodeCategoryDisplay,
                DetailDisplay = row.ProcessNodeDisplay,
                FilingFactNo = row.FilingFactNo,
                MaterialName = row.MaterialName,
                ItemName = row.ItemName,
                OutboundStatusDisplay = row.OutboundStatusDisplay,
                UsageModeDisplay = row.UsageModeDisplay,
                ApplicantName = row.ApplicantName,
                OperatorName = row.OperatorName,
                Remark = row.Remark,
                FilingFactId = row.FilingFactId
            };
        }

        private static void RegisterContainerKey(
            Dictionary<CirculationContainerKey, ContainerMasterBuilder> keys,
            CirculationContainerKey key,
            MaterialTransactionLedgerRow row)
        {
            if (!TryCreateContainerKey(key.ContainerCode, key.ContainerKind, out var normalizedKey))
            {
                return;
            }

            if (!keys.TryGetValue(normalizedKey, out var builder))
            {
                builder = ContainerMasterBuilder.FromTransactionSnapshot(row, normalizedKey.ContainerCode);
                keys[normalizedKey] = builder;
            }

            builder.PhysicalTransactionCount++;
            builder.TouchActivity(row.OperatedAt, row.TransactionTypeDisplay, row.FilingFactId);
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

        private sealed class ContainerMasterBuilder
        {
            private readonly CirculationContainerKey _key;
            private readonly HashSet<int> _filingFactIds = new();
            private string _year = string.Empty;
            private string _projectName = string.Empty;
            private string _locationDisplay = string.Empty;
            private string _statusDisplay = string.Empty;

            public int PhysicalTransactionCount { get; set; }

            public int ProcessNodeCount { get; set; }

            public DateTime LatestOperatedAt { get; private set; }

            public string LatestActivityDisplay { get; private set; } = string.Empty;

            public int RepresentativeFilingFactId { get; private set; }

            private ContainerMasterBuilder(CirculationContainerKey key)
            {
                _key = key;
            }

            public static ContainerMasterBuilder FromTransactionSnapshot(
                MaterialTransactionLedgerRow row,
                string containerCode)
            {
                return new ContainerMasterBuilder(new CirculationContainerKey(containerCode, row.ContainerKind))
                {
                    _year = row.ContainerYear,
                    _projectName = row.ContainerProjectName,
                    _locationDisplay = row.ContainerLocationDisplay,
                    _statusDisplay = row.ContainerStatusDisplay
                };
            }

            public static ContainerMasterBuilder FromProcessNodeSnapshot(MaterialOutboundProcessNodeSearchRow row)
            {
                return new ContainerMasterBuilder(new CirculationContainerKey(row.ContainerCode, row.ContainerKind))
                {
                    _year = row.ContainerYear,
                    _projectName = row.ContainerProjectName,
                    _locationDisplay = row.ContainerLocationDisplay,
                    _statusDisplay = row.ContainerStatusDisplay
                };
            }

            public void TouchActivity(DateTime operatedAt, string activityDisplay, int filingFactId)
            {
                if (filingFactId > 0)
                {
                    _filingFactIds.Add(filingFactId);
                }

                if (LatestOperatedAt == default || operatedAt >= LatestOperatedAt)
                {
                    LatestOperatedAt = operatedAt;
                    LatestActivityDisplay = activityDisplay;
                    RepresentativeFilingFactId = filingFactId;
                }
            }

            public CirculationContainerMasterRow Build()
            {
                return new CirculationContainerMasterRow
                {
                    ContainerCode = _key.ContainerCode,
                    ContainerKind = _key.ContainerKind,
                    Year = _year,
                    ProjectName = _projectName,
                    LocationDisplay = _locationDisplay,
                    ContainerStatusDisplay = _statusDisplay,
                    MaterialCount = _filingFactIds.Count,
                    TransactionCount = PhysicalTransactionCount,
                    ProcessNodeCount = ProcessNodeCount,
                    LatestOperatedAt = LatestOperatedAt,
                    LatestTransactionTypeDisplay = LatestActivityDisplay,
                    RepresentativeFilingFactId = RepresentativeFilingFactId
                };
            }
        }

        private sealed class BusinessGroupBuilder
        {
            private readonly CirculationLedgerBusinessKey _key;

            public BusinessGroupBuilder(CirculationLedgerBusinessKey key)
            {
                _key = key;
            }

            private int SubItemCount { get; set; }

            private DateTime LatestOperatedAt { get; set; }

            private string LatestSummary { get; set; } = string.Empty;

            private string OutboundStatusDisplay { get; set; } = string.Empty;

            private string ApplicantName { get; set; } = string.Empty;

            private int RepresentativeFilingFactId { get; set; }

            public void AddTransaction(MaterialTransactionLedgerRow row)
            {
                SubItemCount++;
                Touch(row.OperatedAt, row.Summary, row.FilingFactId);
            }

            public void AddProcessNode(MaterialOutboundProcessNodeSearchRow row)
            {
                SubItemCount++;
                OutboundStatusDisplay = row.OutboundStatusDisplay;
                ApplicantName = row.ApplicantName;
                Touch(row.OperatedAt, row.ProcessNodeDisplay, row.FilingFactId);
            }

            private void Touch(DateTime operatedAt, string summary, int filingFactId)
            {
                if (LatestOperatedAt == default || operatedAt >= LatestOperatedAt)
                {
                    LatestOperatedAt = operatedAt;
                    LatestSummary = summary;
                    RepresentativeFilingFactId = filingFactId;
                }
            }

            public CirculationLedgerBusinessRow Build()
            {
                return new CirculationLedgerBusinessRow
                {
                    BusinessKind = _key.Kind,
                    BusinessNo = _key.BusinessNo,
                    LatestOperatedAt = LatestOperatedAt,
                    LatestSummary = LatestSummary,
                    SubItemCount = SubItemCount,
                    OutboundStatusDisplay = OutboundStatusDisplay,
                    ApplicantName = ApplicantName,
                    RepresentativeFilingFactId = RepresentativeFilingFactId
                };
            }
        }
    }

    internal readonly record struct CirculationLedgerBusinessKey(string Kind, string BusinessNo);
}
