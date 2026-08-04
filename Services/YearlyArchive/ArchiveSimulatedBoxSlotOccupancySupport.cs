using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟介质档案盒占格规则：有待还（含部分提档待还）保留占格并标识；库内与待还均为 0 且存在不还/归还灭失份数时释放占格。
    /// 盘库丢失与拟销均不触发释档。电子介质不存在部分提档，不参与本类份数汇总。
    /// </summary>
    internal static class ArchiveSimulatedBoxSlotOccupancySupport
    {
        public readonly record struct SimulatedBoxCopyCountTotals(
            int CurrentInArchive,
            int PendingReturn,
            int NoReturn,
            int Lost,
            int InventoryLost,
            int InventoryScrap)
        {
            /// <summary>
            /// 库内与待还均为 0，且存在不还或归还灭失份数时释放占格（盘库丢失/拟销不释档）。
            /// </summary>
            public bool ShouldReleaseSlot =>
                CurrentInArchive == 0
                && PendingReturn == 0
                && (NoReturn > 0 || Lost > 0);

            public bool HasPendingReturn => PendingReturn > 0;

            /// <summary>
            /// 盘库致空（仍占格）：可借库内因丢失/拟销耗尽。开柜角标不再显示「空」，改按份数分别标「失」「销」。
            /// </summary>
            public bool IsInventoryEmptyMark =>
                (InventoryLost > 0 || InventoryScrap > 0)
                && SimulatedInArchiveCopyCountSupport.ResolveAvailableCopyCount(CurrentInArchive, InventoryScrap) == 0
                && PendingReturn == 0
                && !ShouldReleaseSlot;

            /// <summary>是否存在盘库丢失份数（开柜右上「失」）。</summary>
            public bool IsInventoryLostMark => InventoryLost > 0;

            /// <summary>是否存在盘库拟销份数（开柜右上「销」，可与「失」并存）。</summary>
            public bool IsInventoryScrapMark => InventoryScrap > 0;
        }

        public static SimulatedBoxCopyCountTotals AggregateRows(IEnumerable<YearlyArchiveBoxMediaItemRow> rows)
        {
            return AggregateRows(rows, ArchiveRegisterDomainValues.MediaKindSimulated);
        }

        /// <summary>
        /// 汇总电子介质袋内资料子项份数（电子不存在部分提档，待还不还灭失按整件表达）。
        /// </summary>
        public static SimulatedBoxCopyCountTotals AggregateElectronicRows(
            IEnumerable<YearlyArchiveBoxMediaItemRow> rows)
        {
            return AggregateRows(rows, ArchiveRegisterDomainValues.MediaKindElectronic);
        }

        private static SimulatedBoxCopyCountTotals AggregateRows(
            IEnumerable<YearlyArchiveBoxMediaItemRow> rows,
            string mediaKind)
        {
            int currentInArchive = 0;
            int pendingReturn = 0;
            int noReturn = 0;
            int lost = 0;
            int inventoryLost = 0;
            int inventoryScrap = 0;

            foreach (var row in rows)
            {
                if (!string.Equals(
                        row.Fact.MediaKind,
                        mediaKind,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int rowInventoryLost = row.InventoryLostCopyCount > 0
                    ? row.InventoryLostCopyCount
                    : Math.Max(0, row.Fact.InventoryLostCopyCount);
                int rowInventoryScrap = row.InventoryScrapCopyCount > 0
                    ? row.InventoryScrapCopyCount
                    : Math.Max(0, row.Fact.InventoryScrapCopyCount);

                var breakdown = ArchiveBoxMediaItemCopyCountSupport.Resolve(
                    row.Fact,
                    row.PendingReturnCopyCount,
                    row.NoReturnCopyCount,
                    row.LostCopyCount,
                    rowInventoryLost,
                    rowInventoryScrap);

                currentInArchive += breakdown.CurrentInArchiveCopyCount;
                pendingReturn += breakdown.PendingReturnCopyCount;
                noReturn += breakdown.NoReturnCopyCount;
                lost += breakdown.LostCopyCount;
                inventoryLost += breakdown.InventoryLostCopyCount;
                inventoryScrap += breakdown.InventoryScrapCopyCount;
            }

            return new SimulatedBoxCopyCountTotals(
                currentInArchive, pendingReturn, noReturn, lost, inventoryLost, inventoryScrap);
        }
    }
}
