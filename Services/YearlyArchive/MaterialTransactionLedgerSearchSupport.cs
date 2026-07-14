using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 迁档/流转台账：MaterialTransaction 行映射与筛选辅助。
    /// </summary>
    internal static class MaterialTransactionLedgerSearchSupport
    {
        public const int DefaultMaxResults = 5000;

        public static MaterialTransactionLedgerRow MapRow(
            YearlyArchiveMaterialTransaction transaction,
            YearlyArchiveFilingFact fact,
            string relocationMode,
            YearlyArchiveBox? archiveBox = null,
            YearlyElectronicArchiveUnit? electronicUnit = null)
        {
            ResolveContainerPresentation(fact, archiveBox, electronicUnit, out var containerContext);

            return new MaterialTransactionLedgerRow
            {
                TransactionId = transaction.Id,
                FilingFactId = fact.Id,
                OperatedAt = transaction.OperatedAt,
                TransactionType = transaction.TransactionType,
                BusinessNo = transaction.BusinessNo,
                RelocationMode = relocationMode,
                FilingFactNo = fact.FilingFactNo,
                FormNo = fact.FormNo,
                MediaKind = fact.MediaKind,
                MaterialName = fact.MaterialName,
                ItemName = fact.ItemName,
                ProjectName = fact.ProjectName,
                Summary = transaction.Summary,
                LocationChangeDisplay = BuildLocationChangeDisplay(transaction),
                LifecycleChangeDisplay = BuildLifecycleChangeDisplay(transaction),
                OperatorName = transaction.OperatorName,
                Remark = transaction.Remark,
                ContainerCode = containerContext.ContainerCode,
                ContainerKind = containerContext.ContainerKind,
                BeforeContainerCode = transaction.BeforeContainerCode?.Trim() ?? string.Empty,
                AfterContainerCode = transaction.AfterContainerCode?.Trim() ?? string.Empty,
                ContainerYear = containerContext.Year,
                ContainerProjectName = containerContext.ProjectName,
                ContainerLocationDisplay = containerContext.LocationDisplay,
                ContainerStatusDisplay = containerContext.StatusDisplay
            };
        }

        public static MaterialOutboundProcessNodeSearchRow MapProcessNodeSearchRow(
            YearlyArchiveOutboundSyncEntry entry,
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item,
            YearlyArchiveFilingFact fact,
            YearlyArchiveBox? archiveBox = null,
            YearlyElectronicArchiveUnit? electronicUnit = null)
        {
            var row = ArchiveOutboundProcessNodeSupport.MapProcessNode(entry, record, item);
            ResolveContainerPresentation(fact, archiveBox, electronicUnit, out var containerContext);

            return new MaterialOutboundProcessNodeSearchRow
            {
                SyncEntryId = entry.Id,
                FilingFactId = fact.Id,
                OperatedAt = row.OperatedAt,
                OutboundNo = row.OutboundNo,
                OutboundStatusDisplay = row.OutboundStatusDisplay,
                NodeCategoryDisplay = row.NodeCategoryDisplay,
                ProcessNodeDisplay = row.ProcessNodeDisplay,
                UsageModeDisplay = row.UsageModeDisplay,
                FilingFactNo = fact.FilingFactNo,
                FormNo = fact.FormNo,
                MaterialName = fact.MaterialName,
                ItemName = fact.ItemName,
                ApplicantName = row.ApplicantName,
                OperatorName = row.OperatorName,
                Remark = row.Remark,
                ContainerCode = containerContext.ContainerCode,
                ContainerKind = containerContext.ContainerKind,
                ContainerYear = containerContext.Year,
                ContainerProjectName = containerContext.ProjectName,
                ContainerLocationDisplay = containerContext.LocationDisplay,
                ContainerStatusDisplay = containerContext.StatusDisplay
            };
        }

        public static void ResolveContainerPresentation(
            YearlyArchiveFilingFact fact,
            YearlyArchiveBox? archiveBox,
            YearlyElectronicArchiveUnit? electronicUnit,
            out ContainerLedgerPresentation presentation)
        {
            string containerCode = !string.IsNullOrWhiteSpace(fact.CurrentContainerCode)
                ? fact.CurrentContainerCode.Trim()
                : fact.ContainerCode.Trim();

            if (fact.ContainerKind == ArchiveContainerKind.ArchiveBox && archiveBox != null)
            {
                presentation = new ContainerLedgerPresentation
                {
                    ContainerCode = !string.IsNullOrWhiteSpace(archiveBox.ArchiveSequenceNo)
                        ? archiveBox.ArchiveSequenceNo.Trim()
                        : containerCode,
                    ContainerKind = ArchiveContainerKind.ArchiveBox,
                    Year = archiveBox.Year?.Trim() ?? string.Empty,
                    ProjectName = archiveBox.ProjectName?.Trim() ?? fact.ProjectName.Trim(),
                    LocationDisplay = ResolveArchiveBoxLocationDisplay(archiveBox),
                    StatusDisplay = CirculationLedgerDisplayValues.MapContainerStatusDisplay(archiveBox.ContainerLifecycleStatus)
                };
                return;
            }

            if (fact.ContainerKind == ArchiveContainerKind.ElectronicBag && electronicUnit != null)
            {
                presentation = new ContainerLedgerPresentation
                {
                    ContainerCode = !string.IsNullOrWhiteSpace(electronicUnit.ElectronicArchiveNo)
                        ? electronicUnit.ElectronicArchiveNo.Trim()
                        : containerCode,
                    ContainerKind = ArchiveContainerKind.ElectronicBag,
                    Year = electronicUnit.Year?.Trim() ?? string.Empty,
                    ProjectName = electronicUnit.ProjectName?.Trim() ?? fact.ProjectName.Trim(),
                    LocationDisplay = ResolveElectronicUnitLocationDisplay(electronicUnit, fact),
                    StatusDisplay = CirculationLedgerDisplayValues.MapContainerStatusDisplay(electronicUnit.UnitLifecycleStatus)
                };
                return;
            }

            presentation = new ContainerLedgerPresentation
            {
                ContainerCode = containerCode,
                ContainerKind = fact.ContainerKind,
                Year = string.Empty,
                ProjectName = fact.ProjectName.Trim(),
                LocationDisplay = !string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? fact.CurrentStorageLocation.Trim()
                    : fact.StorageLocation.Trim(),
                StatusDisplay = CirculationLedgerDisplayValues.MapContainerStatusDisplay(ArchiveContainerLifecycleStatus.InUse)
            };
        }

        public static string NormalizeKeyword(string? keyword) => keyword?.Trim() ?? string.Empty;

        public static bool MatchesKeyword(string keyword, params string?[] fields)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return true;
            }

            return fields.Any(field =>
                !string.IsNullOrWhiteSpace(field)
                && field.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        public static string? ResolveNodeCategoryPhase(string nodeCategory) => nodeCategory switch
        {
            OutboundProcessNodeCategoryFilter.Reservation => ArchiveOutboundDomainValues.SyncEntryPhaseActive,
            OutboundProcessNodeCategoryFilter.Cancelled => ArchiveOutboundDomainValues.SyncEntryPhaseCancelled,
            OutboundProcessNodeCategoryFilter.Confirmed => ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed,
            _ => null
        };

        internal static string ResolveArchiveBoxLocationDisplay(YearlyArchiveBox box)
        {
            if (ArchiveContainerLifecycleStatus.OccupiesCabinet(box.ContainerLifecycleStatus)
                && !string.IsNullOrWhiteSpace(box.BoxLocationCode))
            {
                return box.BoxLocationCode.Trim();
            }

            if (!string.IsNullOrWhiteSpace(box.LastStorageLocation))
            {
                return box.LastStorageLocation.Trim();
            }

            return box.BoxLocationCode?.Trim() ?? string.Empty;
        }

        private static string ResolveElectronicUnitLocationDisplay(
            YearlyElectronicArchiveUnit unit,
            YearlyArchiveFilingFact fact)
        {
            if (!string.IsNullOrWhiteSpace(unit.StorageLocation))
            {
                return unit.StorageLocation.Trim();
            }

            return !string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                ? fact.CurrentStorageLocation.Trim()
                : fact.StorageLocation.Trim();
        }

        private static string BuildLocationChangeDisplay(YearlyArchiveMaterialTransaction transaction)
        {
            bool containerChanged = !string.IsNullOrWhiteSpace(transaction.AfterContainerCode)
                && !string.Equals(transaction.BeforeContainerCode?.Trim(), transaction.AfterContainerCode.Trim(), StringComparison.OrdinalIgnoreCase);
            bool locationChanged = !string.IsNullOrWhiteSpace(transaction.AfterStorageLocation)
                && !string.Equals(transaction.BeforeStorageLocation?.Trim(), transaction.AfterStorageLocation.Trim(), StringComparison.OrdinalIgnoreCase);

            if (containerChanged && locationChanged)
            {
                return $"{transaction.BeforeContainerCode} / {transaction.BeforeStorageLocation} → {transaction.AfterContainerCode} / {transaction.AfterStorageLocation}";
            }

            if (containerChanged)
            {
                return $"{transaction.BeforeContainerCode} → {transaction.AfterContainerCode}";
            }

            if (locationChanged)
            {
                return $"{transaction.BeforeStorageLocation} → {transaction.AfterStorageLocation}";
            }

            return "—";
        }

        private static string BuildLifecycleChangeDisplay(YearlyArchiveMaterialTransaction transaction)
        {
            if (string.IsNullOrWhiteSpace(transaction.BeforeLifecycleStatus)
                && string.IsNullOrWhiteSpace(transaction.AfterLifecycleStatus))
            {
                return "—";
            }

            if (string.IsNullOrWhiteSpace(transaction.BeforeLifecycleStatus))
            {
                return MaterialTransactionDomainValues.MapLifecycleStatusDisplay(transaction.AfterLifecycleStatus);
            }

            if (string.IsNullOrWhiteSpace(transaction.AfterLifecycleStatus)
                || string.Equals(transaction.BeforeLifecycleStatus, transaction.AfterLifecycleStatus, StringComparison.Ordinal))
            {
                return MaterialTransactionDomainValues.MapLifecycleStatusDisplay(transaction.BeforeLifecycleStatus);
            }

            return $"{MaterialTransactionDomainValues.MapLifecycleStatusDisplay(transaction.BeforeLifecycleStatus)} → {MaterialTransactionDomainValues.MapLifecycleStatusDisplay(transaction.AfterLifecycleStatus)}";
        }

        internal sealed class ContainerLedgerPresentation
        {
            public string ContainerCode { get; init; } = string.Empty;

            public ArchiveContainerKind ContainerKind { get; init; }

            public string Year { get; init; } = string.Empty;

            public string ProjectName { get; init; } = string.Empty;

            public string LocationDisplay { get; init; } = string.Empty;

            public string StatusDisplay { get; init; } = string.Empty;
        }
    }

    /// <summary>
    /// 出库流程节点类别筛选值。
    /// </summary>
    public static class OutboundProcessNodeCategoryFilter
    {
        public const string Reservation = "Reservation";
        public const string Cancelled = "Cancelled";
        public const string Confirmed = "Confirmed";

        public static string MapDisplay(string value) => value switch
        {
            Reservation => "流程预订",
            Cancelled => "流程撤销",
            Confirmed => "办结同步",
            _ => "全部"
        };
    }
}
