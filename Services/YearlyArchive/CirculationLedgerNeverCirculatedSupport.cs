using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 流转台账：未流转在库容器查询与展示辅助。
    /// </summary>
    internal static class CirculationLedgerNeverCirculatedSupport
    {
        public static bool CanIncludeNeverCirculated(CirculationLedgerSearchCriteria criteria)
        {
            return CanIncludeNeverCirculated(criteria, processNodeCriteria: null);
        }

        public static bool CanIncludeNeverCirculated(
            CirculationLedgerSearchCriteria criteria,
            OutboundProcessNodeLedgerSearchCriteria? processNodeCriteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            if (!CirculationLedgerListingMode.NeedsNeverCirculated(criteria.ListingMode))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(criteria.TransactionType)
                && string.IsNullOrWhiteSpace(criteria.BusinessNo)
                && string.IsNullOrWhiteSpace(criteria.OperatorName)
                && string.IsNullOrWhiteSpace(processNodeCriteria?.OutboundNo)
                && string.IsNullOrWhiteSpace(processNodeCriteria?.NodeCategory)
                && string.IsNullOrWhiteSpace(processNodeCriteria?.ApplicantName);
        }

        internal static CirculationContainerMasterRow MapArchiveBoxMasterRow(
            YearlyArchiveBox box,
            int materialCount,
            int representativeFilingFactId)
        {
            return new CirculationContainerMasterRow
            {
                ContainerCode = box.ArchiveSequenceNo.Trim(),
                ContainerKind = ArchiveContainerKind.ArchiveBox,
                Year = box.Year?.Trim() ?? string.Empty,
                ProjectName = box.ProjectName?.Trim() ?? string.Empty,
                LocationDisplay = MaterialTransactionLedgerSearchSupport.ResolveArchiveBoxLocationDisplay(box),
                ContainerStatusDisplay = CirculationLedgerDisplayValues.MapContainerStatusDisplay(box.ContainerLifecycleStatus),
                MaterialCount = materialCount,
                TransactionCount = 0,
                LatestOperatedAt = box.ArchivedDate,
                LatestTransactionTypeDisplay = CirculationLedgerDisplayValues.NeverCirculatedDisplay,
                RepresentativeFilingFactId = representativeFilingFactId
            };
        }

        internal static CirculationContainerMasterRow MapElectronicUnitMasterRow(
            YearlyElectronicArchiveUnit unit,
            int materialCount,
            int representativeFilingFactId)
        {
            return new CirculationContainerMasterRow
            {
                ContainerCode = unit.ElectronicArchiveNo.Trim(),
                ContainerKind = ArchiveContainerKind.ElectronicBag,
                Year = unit.Year?.Trim() ?? string.Empty,
                ProjectName = unit.ProjectName?.Trim() ?? string.Empty,
                LocationDisplay = string.IsNullOrWhiteSpace(unit.StorageLocation)
                    ? string.Empty
                    : unit.StorageLocation.Trim(),
                ContainerStatusDisplay = CirculationLedgerDisplayValues.MapContainerStatusDisplay(unit.UnitLifecycleStatus),
                MaterialCount = materialCount,
                TransactionCount = 0,
                LatestOperatedAt = unit.ArchivedDate,
                LatestTransactionTypeDisplay = CirculationLedgerDisplayValues.NeverCirculatedDisplay,
                RepresentativeFilingFactId = representativeFilingFactId
            };
        }
    }
}
