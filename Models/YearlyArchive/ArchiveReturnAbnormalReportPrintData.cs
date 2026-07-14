namespace DocMgr.Models.YearlyArchive

{

    /// <summary>

    /// 资料灭失情况表打印数据。

    /// </summary>

    public sealed class ArchiveReturnAbnormalReportPrintData

    {

        public string ReturnNo { get; init; } = string.Empty;



        public string SourceOutboundNo { get; init; } = string.Empty;



        public string ReturnDateText { get; init; } = string.Empty;



        public string BorrowerDept { get; init; } = string.Empty;



        public string BorrowerName { get; init; } = string.Empty;



        public string MaterialSummary { get; init; } = string.Empty;



        public List<string> BorrowItemLines { get; init; } = new();



        public List<string> IntactReturnItemLines { get; init; } = new();



        public List<string> LossItemLines { get; init; } = new();



        /// <summary>借出人签字栏日期（留白时为占位文本）。</summary>

        public string ReturnerSignatureDateText { get; init; } = string.Empty;



        /// <summary>借出人签字栏是否留白。</summary>

        public bool BlankReturnerSignature { get; init; } = true;



        public List<ArchiveReturnApprovalSignatureLine> OutboundApprovalLines { get; init; } = new();

    }



    /// <summary>

    /// 灭失情况表出库审核审批签字行。

    /// </summary>

    public sealed class ArchiveReturnApprovalSignatureLine

    {

        public string RoleLabel { get; init; } = string.Empty;



        public string SignerSlot { get; init; } = string.Empty;



        public string DateText { get; init; } = string.Empty;

    }

}

