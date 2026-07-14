namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料出库（借出）申请审批单打印数据。
    /// </summary>
    public sealed class ArchiveOutboundPrintData
    {
        public string OutboundNo { get; init; } = string.Empty;

        public string ApplyDateText { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public string ArchiveYearText { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public string DestinationText { get; init; } = string.Empty;

        public string ConfidentialMaterialDispositionText { get; init; } = string.Empty;

        /// <summary>长期存档模拟介质提档后库内归零的重点提示（无则空）。</summary>
        public string LongTermSimulatedStockDepletionNoticeText { get; init; } = string.Empty;

        public string ProofMaterialNote { get; init; } = string.Empty;

        public string MaterialSummary { get; init; } = string.Empty;

        public string ExpectedReturnDateText { get; init; } = string.Empty;

        public List<string> ItemLines { get; init; } = new();

        public string DeptAuditBlock { get; init; } = string.Empty;

        public string ArchiveRoomHeadBlock { get; init; } = string.Empty;

        public string ProductionHeadBlock { get; init; } = string.Empty;

        public string VicePresidentBlock { get; init; } = string.Empty;

        public int PrintCount { get; init; }
    }
}
