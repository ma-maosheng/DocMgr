namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还工作台「查看借出详情」详单打印数据。
    /// </summary>
    public sealed class ArchiveReturnOutboundDetailPrintData
    {
        public string OutboundNo { get; init; } = string.Empty;

        public string PrintDateText { get; init; } = string.Empty;

        public string BorrowerDept { get; init; } = string.Empty;

        public string BorrowerName { get; init; } = string.Empty;

        public string ArchiveYearText { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string MaterialSummary { get; init; } = string.Empty;

        public string ExpectedReturnDateText { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public List<string> ItemLines { get; init; } = new();
    }
}
