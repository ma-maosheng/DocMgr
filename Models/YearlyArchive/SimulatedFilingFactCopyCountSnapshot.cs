namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 模拟介质立档事实的出库相关份数快照（待还、不还、灭失）。
    /// </summary>
    public sealed class SimulatedFilingFactCopyCountSnapshot
    {
        public int PendingReturnCopyCount { get; init; }

        public int NoReturnCopyCount { get; init; }

        public int LostCopyCount { get; init; }

        /// <summary>盘库登记累计丢失份数。</summary>
        public int InventoryLostCopyCount { get; init; }
    }
}
