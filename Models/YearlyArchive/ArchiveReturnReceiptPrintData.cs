using DocMgr.Models.YearlyArchive;



namespace DocMgr.Models.YearlyArchive

{

    /// <summary>

    /// 资料归还回执（交接单）打印数据。

    /// </summary>

    public sealed class ArchiveReturnReceiptPrintData

    {

        public string ReturnNo { get; init; } = string.Empty;



        public string SourceOutboundNo { get; init; } = string.Empty;



        public string ReturnDateText { get; init; } = string.Empty;



        public string BorrowerDept { get; init; } = string.Empty;



        public string BorrowerName { get; init; } = string.Empty;



        public string RegisteredByName { get; init; } = string.Empty;



        public string ExpectedReturnDateText { get; init; } = string.Empty;



        public string MaterialSummary { get; init; } = string.Empty;



        public List<string> ItemLines { get; init; } = new();



        /// <summary>交接签字栏（归还人与资料室资料员）。</summary>

        public string HandoverSignatureBlock { get; init; } = string.Empty;



        public string Remark { get; init; } = string.Empty;



        /// <summary>灭失情况描述（存在灭失份数时输出）。</summary>

        public string LossDescription { get; init; } = string.Empty;



        public bool HasLossReturn { get; init; }



        public int PrintCount { get; init; }

    }

}

