using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟介质资料子项份数：立档份数取 <see cref="YearlyArchiveFilingFact.ContentCount"/>；
    /// 当前库内份数按「立档 = 库内 + 待还 + 不还 + 归还灭失 + 盘库丢失」公式实时计算。
    /// </summary>
    public static class SimulatedInArchiveCopyCountSupport
    {
        public static int ResolveFiledCopyCount(int contentCount) => Math.Max(1, contentCount);

        /// <summary>
        /// 按份数分解公式计算资料子项当前库内份数。
        /// </summary>
        public static int ResolveCurrentInArchiveCopyCount(
            int filedCopyCount,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount,
            int inventoryLostCopyCount = 0)
        {
            int filed = ResolveFiledCopyCount(filedCopyCount);
            int current = filed
                - Math.Max(0, pendingReturnCopyCount)
                - Math.Max(0, noReturnCopyCount)
                - Math.Max(0, lostCopyCount)
                - Math.Max(0, inventoryLostCopyCount);
            return Math.Max(0, current);
        }

        public static int ResolveCurrentInArchiveCopyCount(
            int filedCopyCount,
            SimulatedFilingFactCopyCountSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return ResolveCurrentInArchiveCopyCount(
                filedCopyCount,
                snapshot.PendingReturnCopyCount,
                snapshot.NoReturnCopyCount,
                snapshot.LostCopyCount,
                snapshot.InventoryLostCopyCount);
        }

        /// <summary>展示当前库内/立档，如「2/5」。</summary>
        public static string FormatCurrentVsFiled(int currentInArchiveCopyCount, int filedCopyCount)
        {
            int filed = ResolveFiledCopyCount(filedCopyCount);
            int current = Math.Max(0, currentInArchiveCopyCount);
            return $"{current}/{filed}";
        }

        /// <summary>
        /// 兼容旧调用：仅扣减待还份数（不含不还/灭失/盘库丢失）。新逻辑请用完整公式或 <see cref="FormatCurrentVsFiled"/>。
        /// </summary>
        public static string FormatDisplay(int filedCopyCount, int withdrawnCopyCount) =>
            FormatCurrentVsFiled(
                ResolveCurrentInArchiveCopyCount(
                    filedCopyCount,
                    pendingReturnCopyCount: withdrawnCopyCount,
                    noReturnCopyCount: 0,
                    lostCopyCount: 0),
                filedCopyCount);

        public static SimulatedInArchiveCopyCountInfo BuildInfo(
            int contentCount,
            SimulatedFilingFactCopyCountSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            int filedCopyCount = ResolveFiledCopyCount(contentCount);
            int currentInArchiveCopyCount = ResolveCurrentInArchiveCopyCount(filedCopyCount, snapshot);
            return new SimulatedInArchiveCopyCountInfo
            {
                FiledCopyCount = filedCopyCount,
                PendingReturnCopyCount = Math.Max(0, snapshot.PendingReturnCopyCount),
                NoReturnCopyCount = Math.Max(0, snapshot.NoReturnCopyCount),
                LostCopyCount = Math.Max(0, snapshot.LostCopyCount),
                InventoryLostCopyCount = Math.Max(0, snapshot.InventoryLostCopyCount),
                CurrentInArchiveCopyCount = currentInArchiveCopyCount,
                Display = FormatCurrentVsFiled(currentInArchiveCopyCount, filedCopyCount)
            };
        }
    }
}
