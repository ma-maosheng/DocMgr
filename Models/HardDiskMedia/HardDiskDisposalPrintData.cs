namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘离库处置签批单打印数据。
    /// </summary>
    public sealed class HardDiskDisposalPrintData
    {
        public string DisposalNo { get; init; } = string.Empty;

        public string ApplyDateText { get; init; } = string.Empty;

        public string DisposalReason { get; init; } = string.Empty;

        public string DispositionMethod { get; init; } = string.Empty;

        public string OtherRemark { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public string ApprovedBy { get; init; } = string.Empty;

        public string ApprovedDateText { get; init; } = string.Empty;

        public string ApprovalOpinion { get; init; } = string.Empty;

        public string CompletedBy { get; init; } = string.Empty;

        public string CompletedDateText { get; init; } = string.Empty;

        public bool IsCompleted { get; init; }

        /// <summary>已累计打印次数（不含本次）。</summary>
        public int PrintCount { get; init; }

        public IReadOnlyList<HardDiskDisposalPrintItemData> Items { get; init; } = Array.Empty<HardDiskDisposalPrintItemData>();
    }

    /// <summary>
    /// 硬盘离库处置签批单明细行。
    /// </summary>
    public sealed class HardDiskDisposalPrintItemData
    {
        public int SortOrder { get; init; }

        public string DiskCode { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public string BeforeMediaStatus { get; init; } = string.Empty;

        public string BeforeMediaNature { get; init; } = string.Empty;

        public string BeforeStorageLocation { get; init; } = string.Empty;
    }
}
