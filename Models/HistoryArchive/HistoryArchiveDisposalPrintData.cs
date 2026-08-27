namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档离库处置签批单打印数据。
    /// </summary>
    public sealed class HistoryArchiveDisposalPrintData
    {
        public string DisposalNo { get; init; } = string.Empty;

        public string ApplyDateText { get; init; } = string.Empty;

        public string MaterialKindDisplay { get; init; } = string.Empty;

        public string DispositionMethod { get; init; } = string.Empty;

        public string TransferTarget { get; init; } = string.Empty;

        public string OtherRemark { get; init; } = string.Empty;

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

        public int PrintCount { get; init; }

        public IReadOnlyList<HistoryArchiveDisposalPrintItemData> Items { get; init; } =
            Array.Empty<HistoryArchiveDisposalPrintItemData>();
    }

    /// <summary>
    /// 历史存档离库处置签批单明细行。
    /// </summary>
    public sealed class HistoryArchiveDisposalPrintItemData
    {
        public int SortOrder { get; init; }

        public string BoxCode { get; init; } = string.Empty;

        public string BoxSpecification { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string ContentSummary { get; init; } = string.Empty;

        public string MixedPlacementText { get; init; } = string.Empty;

        public string DispositionMethod { get; init; } = string.Empty;
    }
}
