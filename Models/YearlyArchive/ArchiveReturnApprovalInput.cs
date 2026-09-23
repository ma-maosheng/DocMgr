namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还审批/交接信息录入模型（对齐硬盘归还）。
    /// </summary>
    public sealed class ArchiveReturnApprovalInput
    {
        /// <summary>部门审核签字。</summary>
        public string DeptHead { get; set; } = string.Empty;

        /// <summary>部门审核日期。</summary>
        public DateTime? DeptHeadDate { get; set; }

        /// <summary>资料室签字。</summary>
        public string ArchiveRoomHead { get; set; } = string.Empty;

        /// <summary>资料室签字日期。</summary>
        public DateTime? ArchiveRoomHeadDate { get; set; }

        /// <summary>生产科签字。</summary>
        public string ProductionHeadName { get; set; } = string.Empty;

        /// <summary>生产科签字日期。</summary>
        public DateTime? ProductionHeadDate { get; set; }

        /// <summary>分管资料院长签字。</summary>
        public string ArchiveDeputyPresidentName { get; set; } = string.Empty;

        /// <summary>分管资料院长签字日期。</summary>
        public DateTime? ArchiveDeputyPresidentDate { get; set; }

        /// <summary>分管生产院长签字。</summary>
        public string ProductionVicePresidentName { get; set; } = string.Empty;

        /// <summary>分管生产院长签字日期。</summary>
        public DateTime? ProductionVicePresidentDate { get; set; }

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
