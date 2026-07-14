namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 资料出库库内空盘征用后，待归还登记的来源快照。
    /// </summary>
    public sealed record HardDiskMediaArchiveOutboundRequisitionReturnSource
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
