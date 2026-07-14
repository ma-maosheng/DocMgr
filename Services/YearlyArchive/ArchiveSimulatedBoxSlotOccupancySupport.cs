using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟介质档案盒占格规则：有待还（含部分提档待还）保留占格并标识；库内与待还均为 0 且存在不还/灭失份数时释放占格。
    /// 电子介质不存在部分提档，不参与本类份数汇总。
    /// </summary>
    internal static class ArchiveSimulatedBoxSlotOccupancySupport
    {
        public readonly record struct SimulatedBoxCopyCountTotals(
            int CurrentInArchive,
            int PendingReturn,
            int NoReturn,
            int Lost)
        {
            /// <summary>
            /// 库内与待还均为 0，且存在不还或灭失份数时释放占格（灭失与「提档不还」同等处理）。
            /// </summary>
            public bool ShouldReleaseSlot =>
                CurrentInArchive == 0
                && PendingReturn == 0
                && (NoReturn > 0 || Lost > 0);

            public bool HasPendingReturn => PendingReturn > 0;
        }

        public static SimulatedBoxCopyCountTotals AggregateRows(IEnumerable<YearlyArchiveBoxMediaItemRow> rows)
        {
            int currentInArchive = 0;
            int pendingReturn = 0;
            int noReturn = 0;
            int lost = 0;

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
                    row.LostCopyCount);

                currentInArchive += breakdown.CurrentInArchiveCopyCount;
                pendingReturn += breakdown.PendingReturnCopyCount;
                noReturn += breakdown.NoReturnCopyCount;
                lost += breakdown.LostCopyCount;
            }

            return new SimulatedBoxCopyCountTotals(currentInArchive, pendingReturn, noReturn, lost);
        }
    }
}
