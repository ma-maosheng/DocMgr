using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料出库办结后，电子介质袋占格同步。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        private async Task SyncElectronicArchiveBagSlotsAfterOutboundAsync(
            YearlyArchiveOutboundRecord record,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
            DateTime operatedAt)
        {
            var unitIds = record.Items
                .Where(item => string.Equals(
                        item.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindElectronic,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.UsageMode,
                        ArchiveOutboundDomainValues.UsageModeWithdrawal,
                        StringComparison.Ordinal))
                .Select(item => item.FilingFactId)
                .Where(id => id > 0)
                .Distinct()
                .Select(id => factsById.TryGetValue(id, out var fact) ? fact : null)
                .Where(fact => fact != null
                    && fact.ContainerKind == ArchiveContainerKind.ElectronicBag
                    && fact.ContainerId > 0)
                .Select(fact => fact!.ContainerId)
                .Distinct()
                .ToList();

            if (unitIds.Count == 0)
            {
                return;
            }

            _ = await _electronicBagSlotSyncService.SyncUnitsByIdsAsync(unitIds, operatedAt);
        }
    }
}
