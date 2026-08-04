namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 模拟介质资料子项份数快照：与出库/柜体一致，按
    /// 「立档 = 库内 + 待还 + 不还 + 灭失」分解。
    /// </summary>
    public sealed class SimulatedInArchiveCopyCountInfo
    {
        public int FiledCopyCount { get; init; }

        /// <summary>已办结未归还提档份数（待还）。</summary>
        public int PendingReturnCopyCount { get; init; }

        /// <summary>兼容旧名：等同 <see cref="PendingReturnCopyCount"/>。</summary>
        public int WithdrawnCopyCount => PendingReturnCopyCount;

        public int NoReturnCopyCount { get; init; }

        public int LostCopyCount { get; init; }

        /// <summary>盘库登记累计丢失份数。</summary>
        public int InventoryLostCopyCount { get; init; }

        /// <summary>盘库登记累计拟销份数。</summary>
        public int InventoryScrapCopyCount { get; init; }

        public int CurrentInArchiveCopyCount { get; init; }

        /// <summary>展示如「2/5」：当前库内/立档。</summary>
        public string Display { get; init; } = string.Empty;
    }
}
