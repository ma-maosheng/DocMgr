using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料出库办结后，模拟介质档案盒占格同步。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        private async Task SyncSimulatedArchiveBoxSlotsAfterOutboundAsync(
            YearlyArchiveOutboundRecord record,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
            DateTime operatedAt)
        {
            var boxIds = record.Items
                .Where(ArchiveSimulatedMediaItemStockSupport.IsSimulatedWithdrawalStockItem)
                .Select(item => item.FilingFactId)
                .Where(id => id > 0)
                .Distinct()
                .Select(id => factsById.TryGetValue(id, out var fact) ? fact : null)
                .Where(fact => fact != null
                    && fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                    && fact.ContainerId > 0)
                .Select(fact => fact!.ContainerId)
                .Distinct()
                .ToList();

            if (boxIds.Count == 0)
            {
                return;
            }

            await _simulatedBoxSlotSyncService.SyncBoxesByIdsAsync(boxIds, operatedAt);
        }
    }
}
