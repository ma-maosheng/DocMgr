namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还回执（交接单）打印数据。
    /// </summary>
    public sealed class ArchiveReturnReceiptPrintData
    {
        /// <summary>打印单据标题（签批交接单 / 交接单）。</summary>
        public string DocumentTitle { get; init; } = "河北省第三测绘院资料室年度资料归还交接单";

        public string ReturnNo { get; init; } = string.Empty;

        public string SourceOutboundNo { get; init; } = string.Empty;

        public string ReturnDateText { get; init; } = string.Empty;

        public string BorrowerDept { get; init; } = string.Empty;

        public string BorrowerName { get; init; } = string.Empty;

        public string RegisteredByName { get; init; } = string.Empty;

        public string ExpectedReturnDateText { get; init; } = string.Empty;

        public string MaterialSummary { get; init; } = string.Empty;

        public List<string> ItemLines { get; init; } = new();

        /// <summary>交接签字栏（归还人与资料室资料管理员，按行结构化以便对齐排版）。</summary>
        public List<ArchiveReturnApprovalSignatureLine> HandoverSignatureLines { get; init; } = new();

        /// <summary>
        /// 审核/审批人签字栏：正常归还仅部门负责人；灭失时含全部审核审批人。
        /// 表单标签不含「借出时」前缀。
        /// </summary>
        public List<ArchiveReturnApprovalSignatureLine> ApprovalSignatureLines { get; init; } = new();

        public string Remark { get; init; } = string.Empty;

        /// <summary>灭失情况描述（存在灭失份数时输出）。</summary>
        public string LossDescription { get; init; } = string.Empty;

        public bool HasLossReturn { get; init; }

        public int PrintCount { get; init; }
    }
}
