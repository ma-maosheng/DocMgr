using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 电子介质袋占格同步：全部不还或灭失后标记已清空并离档；有待还时保留离柜位置、不标清空。
    /// </summary>
    public sealed class ArchiveElectronicBagSlotSyncService : IArchiveElectronicBagSlotSyncService
    {
        private readonly IArchiveOutboundRepository _outboundRepository;

        public ArchiveElectronicBagSlotSyncService(IArchiveOutboundRepository outboundRepository)
        {
            _outboundRepository = outboundRepository;
        }

        public async Task<IReadOnlyList<EmptiedArchiveBagHint>> SyncUnitsByIdsAsync(
            IReadOnlyCollection<int> unitIds,
            DateTime operatedAt)
        {
            if (unitIds == null || unitIds.Count == 0)
            {
                return [];
            }

            var emptied = new List<EmptiedArchiveBagHint>();
            foreach (int unitId in unitIds.Where(id => id > 0).Distinct())
            {
                var hint = await SyncUnitAsync(unitId, operatedAt);
                if (hint != null)
                {
                    emptied.Add(hint);
                }
            }

            return emptied;
        }

        private async Task<EmptiedArchiveBagHint?> SyncUnitAsync(int unitId, DateTime operatedAt)
        {
            var unit = await _outboundRepository.GetElectronicArchiveUnitByIdForUpdateAsync(unitId);
            if (unit == null
                || !string.Equals(
                    unit.UnitLifecycleStatus,
                    ArchiveContainerLifecycleStatus.InUse,
                    StringComparison.Ordinal))
            {
                return null;
            }

            var rows = await _outboundRepository.GetElectronicArchiveUnitMediaItemRowsForSyncAsync(unit);
            var totals = ArchiveSimulatedBoxSlotOccupancySupport.AggregateElectronicRows(rows);
            if (!totals.ShouldReleaseSlot)
            {
                return null;
            }

            return ReleaseBagSlot(unit, rows, operatedAt);
        }

        private static EmptiedArchiveBagHint? ReleaseBagSlot(
            YearlyElectronicArchiveUnit unit,
            IReadOnlyList<YearlyArchiveBoxMediaItemRow> rows,
            DateTime operatedAt)
        {
            string lastLocation = unit.StorageLocation?.Trim() ?? string.Empty;
            string archiveNo = unit.ElectronicArchiveNo?.Trim() ?? string.Empty;

            unit.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.Emptied;
            unit.StorageLocation = string.Empty;

            foreach (var row in rows)
            {
                if (!string.Equals(
                        row.Fact.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindElectronic,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var breakdown = ArchiveBoxMediaItemCopyCountSupport.Resolve(
                    row.Fact,
                    row.PendingReturnCopyCount,
                    row.NoReturnCopyCount,
                    row.LostCopyCount,
                    row.InventoryLostCopyCount > 0 ? row.InventoryLostCopyCount : row.Fact.InventoryLostCopyCount,
                    row.InventoryScrapCopyCount > 0 ? row.InventoryScrapCopyCount : row.Fact.InventoryScrapCopyCount);
                ArchiveEmptiedContainerFactLifecycleSupport.ApplyOnContainerEmptied(
                    row.Fact,
                    breakdown,
                    operatedAt);
            }

            return new EmptiedArchiveBagHint(unit.Id, archiveNo, lastLocation);
        }
    }
}
