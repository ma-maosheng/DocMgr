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
    }
}
