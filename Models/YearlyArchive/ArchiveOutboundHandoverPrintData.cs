namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料出库交接单打印数据。
    /// </summary>
    public sealed class ArchiveOutboundHandoverPrintData
    {
        public string OutboundNo { get; init; } = string.Empty;

        public string PrintDateText { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string MaterialSummary { get; init; } = string.Empty;

        public List<string> ItemLines { get; init; } = new();

        public string HandoverSignatureBlock { get; init; } = string.Empty;

        public string HandoverRemark { get; init; } = string.Empty;

        public int PrintCount { get; init; }
    }
}
