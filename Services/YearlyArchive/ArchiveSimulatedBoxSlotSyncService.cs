using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟介质档案盒占格同步：全部不还或灭失后释放占格；有待还时保留占格。
    /// </summary>
    public sealed class ArchiveSimulatedBoxSlotSyncService : IArchiveSimulatedBoxSlotSyncService
    {
        private readonly IArchiveOutboundRepository _outboundRepository;

        public ArchiveSimulatedBoxSlotSyncService(IArchiveOutboundRepository outboundRepository)
        {
            _outboundRepository = outboundRepository;
        }

        public async Task<IReadOnlyList<EmptiedArchiveBoxHint>> SyncBoxesByIdsAsync(
            IReadOnlyCollection<int> boxIds,
            DateTime operatedAt)
        {
            if (boxIds == null || boxIds.Count == 0)
            {
                return [];
            }

            var emptied = new List<EmptiedArchiveBoxHint>();
            foreach (int boxId in boxIds.Where(id => id > 0).Distinct())
            {
                var hint = await SyncBoxAsync(boxId, operatedAt);
                if (hint != null)
                {
                    emptied.Add(hint);
                }
            }

            return emptied;
        }

        private async Task<EmptiedArchiveBoxHint?> SyncBoxAsync(int boxId, DateTime operatedAt)
        {
            var box = await _outboundRepository.GetYearlyArchiveBoxByIdForUpdateAsync(boxId);
            if (box == null
                || !string.Equals(
                    box.ContainerLifecycleStatus,
                    ArchiveContainerLifecycleStatus.InUse,
                    StringComparison.Ordinal))
            {
                return null;
            }

            var rows = await _outboundRepository.GetYearlyArchiveBoxMediaItemRowsForSyncAsync(box);
            var totals = ArchiveSimulatedBoxSlotOccupancySupport.AggregateRows(rows);
            if (!totals.ShouldReleaseSlot)
            {
                return null;
            }

            return ReleaseBoxSlot(box, rows, operatedAt);
        }

        private EmptiedArchiveBoxHint? ReleaseBoxSlot(
            YearlyArchiveBox box,
            IReadOnlyList<YearlyArchiveBoxMediaItemRow> rows,
            DateTime operatedAt)
        {
            string lastLocation = box.BoxLocationCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lastLocation))
            {
                return null;
            }

            string archiveSequenceNo = box.ArchiveSequenceNo?.Trim() ?? string.Empty;

            _outboundRepository.RemoveArchiveBoxPlacementByBoxCode(lastLocation);

            box.LastStorageLocation = lastLocation;
            box.ContainerLifecycleStatus = ArchiveContainerLifecycleStatus.Emptied;
            box.BoxLocationCode = string.Empty;
            box.CabinetName = string.Empty;
            box.Side = string.Empty;
            box.Row = 0;
            box.Column = 0;
            box.BoxIndex = 0;

            foreach (var row in rows)
            {
                if (!string.Equals(
                        row.Fact.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindSimulated,
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

            return new EmptiedArchiveBoxHint(box.Id, archiveSequenceNo, lastLocation);
        }
    }
}
