using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟介质资料子项份数：立档份数取 <see cref="YearlyArchiveFilingFact.ContentCount"/>；
    /// 当前库内份数按「立档 = 库内 + 待还 + 不还 + 出库灭失 + 盘库丢失」计算（不扣盘库拟销）；
    /// 拟销仍占库内，可借/可盘库登记量另按「库内 − 拟销」计算。
    /// </summary>
    public static class SimulatedInArchiveCopyCountSupport
    {
        public static int ResolveFiledCopyCount(int contentCount) => Math.Max(1, contentCount);

        /// <summary>
        /// 当前库内份数：扣待还/不还/出库灭失/盘库丢失，不扣盘库拟销。
        /// </summary>
        public static int ResolveCurrentInArchiveCopyCount(
            int filedCopyCount,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount,
            int inventoryLostCopyCount = 0,
            int inventoryScrapCopyCount = 0)
        {
            // inventoryScrapCopyCount 保留参数以兼容旧调用签名，故意不参与库内扣减。
            _ = inventoryScrapCopyCount;
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
                snapshot.InventoryLostCopyCount,
                snapshot.InventoryScrapCopyCount);
        }

        /// <summary>
        /// 可借/可盘库登记份数：当前库内减去已拟销（拟销仍计在库内，但不可再借出或再次盘库占用）。
        /// </summary>
        public static int ResolveAvailableCopyCount(
            int currentInArchiveCopyCount,
            int inventoryScrapCopyCount) =>
            Math.Max(0, Math.Max(0, currentInArchiveCopyCount) - Math.Max(0, inventoryScrapCopyCount));

        public static int ResolveAvailableCopyCount(
            int filedCopyCount,
            SimulatedFilingFactCopyCountSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            int current = ResolveCurrentInArchiveCopyCount(filedCopyCount, snapshot);
            return ResolveAvailableCopyCount(current, snapshot.InventoryScrapCopyCount);
        }

        /// <summary>展示当前库内/立档，如「2/5」。</summary>
        public static string FormatCurrentVsFiled(int currentInArchiveCopyCount, int filedCopyCount)
        {
            int filed = ResolveFiledCopyCount(filedCopyCount);
            int current = Math.Max(0, currentInArchiveCopyCount);
            return $"{current}/{filed}";
        }

        /// <summary>
        /// 兼容旧调用：仅扣减待还份数（不含不还/灭失/盘库丢失/拟销）。新逻辑请用完整公式或 <see cref="FormatCurrentVsFiled"/>。
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
                InventoryScrapCopyCount = Math.Max(0, snapshot.InventoryScrapCopyCount),
                CurrentInArchiveCopyCount = currentInArchiveCopyCount,
                Display = FormatCurrentVsFiled(currentInArchiveCopyCount, filedCopyCount)
            };
        }
    }
}
