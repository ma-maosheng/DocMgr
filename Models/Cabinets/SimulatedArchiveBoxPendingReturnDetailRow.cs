namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 模拟档案盒内单条待还资料追溯明细（已办结出库、尚未归还的提档记录）。
    /// </summary>
    public sealed class SimulatedArchiveBoxPendingReturnDetailRow
    {
        public int OutboundRecordId { get; init; }

        public string OutboundNo { get; init; } = string.Empty;

        public string OutboundStatusDisplay { get; init; } = string.Empty;

        public DateTime? OutboundCompletedAt { get; init; }

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public DateTime ApplyDate { get; init; }

        public string Reason { get; init; } = string.Empty;

        public int FilingFactId { get; init; }

        public string FilingFactNo { get; init; } = string.Empty;

        public string FormNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string MediaType { get; init; } = string.Empty;

        public int PendingReturnCopyCount { get; init; }

        public DateTime? ExpectedReturnDate { get; init; }

        public string ArchivePurpose { get; init; } = string.Empty;

        public string OutboundCompletedAtDisplay =>
            OutboundCompletedAt?.ToString("yyyy-MM-dd") ?? "—";

        public string ApplyDateDisplay => ApplyDate.ToString("yyyy-MM-dd");

        public string ExpectedReturnDateDisplay =>
            ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "—";
    }
}
