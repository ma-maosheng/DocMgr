namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料子项份数分解：立档份数 = 当前库内 + 出库待还 + 出库不还 + 出库灭失 + 盘库丢失。
    /// 盘库拟销不进恒等式（拟销仍计在当前库内）。
    /// </summary>
    public sealed class MediaItemCopyCountBreakdown
    {
        public int FiledCopyCount { get; init; }

        public int CurrentInArchiveCopyCount { get; init; }

        public int PendingReturnCopyCount { get; init; }

        public int NoReturnCopyCount { get; init; }

        /// <summary>出库灭失份数（归还灭失累计）。</summary>
        public int LostCopyCount { get; init; }

        /// <summary>盘库登记丢失份数。</summary>
        public int InventoryLostCopyCount { get; init; }

        /// <summary>盘库登记拟销份数（仍计入当前库内）。</summary>
        public int InventoryScrapCopyCount { get; init; }

        /// <summary>兼容字段；CB-BOX-CNT「介质盘库状态」由台账映射覆盖。</summary>
        public string ElectronicStockStatusText { get; init; } = string.Empty;

        public bool IsBalanced =>
            FiledCopyCount == CurrentInArchiveCopyCount
                + PendingReturnCopyCount
                + NoReturnCopyCount
                + LostCopyCount
                + InventoryLostCopyCount;
    }
}
