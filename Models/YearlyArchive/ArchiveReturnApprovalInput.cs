namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还审批/交接信息录入模型（对齐硬盘归还）。
    /// </summary>
    public sealed class ArchiveReturnApprovalInput
    {
        /// <summary>审核人。</summary>
        public string ReviewerName { get; set; } = string.Empty;

        /// <summary>审核日期。</summary>
        public DateTime? ReviewerDate { get; set; }

        /// <summary>审批人。</summary>
        public string ApproverName { get; set; } = string.Empty;

        /// <summary>审批日期。</summary>
        public DateTime? ApproverDate { get; set; }

        /// <summary>生产科负责人。</summary>
        public string ProductionHeadName { get; set; } = string.Empty;

        /// <summary>生产科负责人签字日期。</summary>
        public DateTime? ProductionHeadDate { get; set; }

        /// <summary>生产副院长。</summary>
        public string VicePresidentName { get; set; } = string.Empty;

        /// <summary>生产副院长签字日期。</summary>
        public DateTime? VicePresidentDate { get; set; }

        /// <summary>审批意见。</summary>
        public string ApprovalOpinion { get; set; } = string.Empty;

        /// <summary>办理交接人（归还人）。</summary>
        public string HandoverApplicant { get; set; } = string.Empty;

        /// <summary>办理交接人（资料管理员）。</summary>
        public string HandoverAdmin { get; set; } = string.Empty;

        /// <summary>办理交接日期。</summary>
        public DateTime? HandoverDate { get; set; }
    }
}
