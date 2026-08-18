namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 在网数据处置签批单打印数据。
    /// </summary>
    public sealed class NetworkOnNetDisposalPrintData
    {
        public string DisposalNo { get; init; } = string.Empty;

        public string ApplyDateText { get; init; } = string.Empty;

        public string DisposalReason { get; init; } = string.Empty;

        public string DispositionMethod { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public string ArchiveRoomHead { get; init; } = string.Empty;

        public string ArchiveRoomHeadDateText { get; init; } = string.Empty;

        public string ArchiveDeputyPresident { get; init; } = string.Empty;

        public string ArchiveDeputyPresidentDateText { get; init; } = string.Empty;

        public string CompletedBy { get; init; } = string.Empty;

        public string CompletedDateText { get; init; } = string.Empty;

        public bool IsCompleted { get; init; }

        /// <summary>已累计打印次数（不含本次）。</summary>
        public int PrintCount { get; init; }

        public IReadOnlyList<NetworkOnNetDisposalPrintItemData> Items { get; init; } =
            Array.Empty<NetworkOnNetDisposalPrintItemData>();
    }

    /// <summary>
    /// 在网数据处置签批单明细行。
    /// </summary>
    public sealed class NetworkOnNetDisposalPrintItemData
    {
        public int SortOrder { get; init; }

        public string AssetNo { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string AssetKind { get; init; } = string.Empty;

        public string ServerPath { get; init; } = string.Empty;

        public string BeforeLifecycleStatus { get; init; } = string.Empty;

        public string DisposalReason { get; init; } = string.Empty;

        public string DispositionMethod { get; init; } = string.Empty;
    }
}
