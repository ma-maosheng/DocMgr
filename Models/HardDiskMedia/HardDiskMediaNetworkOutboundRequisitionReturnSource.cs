namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 出网办结后待归还硬盘来源快照（库内空盘征用）。
    /// </summary>
    public sealed record HardDiskMediaNetworkOutboundRequisitionReturnSource
    {
        public int OutboundRecordId { get; init; }

        public string OutboundNo { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public int MediumId { get; init; }

        public string DiskCode { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public string Capacity { get; init; } = string.Empty;

        public string InterfaceType { get; init; } = string.Empty;

        public string BorrowedLocation { get; init; } = string.Empty;

        public string OriginalLocation { get; init; } = string.Empty;

        public string CurrentStatus { get; init; } = string.Empty;

        public DateTime? ExpectedReturnDate { get; init; }
    }
}
