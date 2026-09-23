namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质审批信息录入模型。
    /// </summary>
    public sealed class HardDiskMediaApprovalInput
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
        public string ProductionHead { get; set; } = string.Empty;

        /// <summary>生产科签字日期。</summary>
        public DateTime? ProductionHeadDate { get; set; }

        /// <summary>分管资料院长签字。</summary>
        public string ArchiveDeputyPresident { get; set; } = string.Empty;

        /// <summary>分管资料院长签字日期。</summary>
        public DateTime? ArchiveDeputyPresidentDate { get; set; }

        /// <summary>分管生产院长签字。</summary>
        public string ProductionVicePresident { get; set; } = string.Empty;

        /// <summary>分管生产院长签字日期。</summary>
        public DateTime? ProductionVicePresidentDate { get; set; }

        /// <summary>办理交接人：申请人。</summary>
        public string HandoverApplicant { get; set; } = string.Empty;

        /// <summary>办理交接人：资料管理员。</summary>
        public string HandoverAdmin { get; set; } = string.Empty;

        /// <summary>通用办理交接人（兼容旧字段名 <c>HandoverName</c>）。</summary>
        public string HandoverName { get; set; } = string.Empty;

        /// <summary>办理交接日期。</summary>
        public DateTime? HandoverDate { get; set; }

        /// <summary>审批意见。</summary>
        public string ApprovalOpinion { get; set; } = string.Empty;

        /// <summary>审批后是否触发签字件上传。</summary>
        public bool TriggerUploadSignedAttachment { get; set; }

        /// <summary>审批后是否触发交接单打印。</summary>
        public bool TriggerPrintHandoverSheet { get; set; }

        /// <summary>审批后是否触发办理完成。</summary>
        public bool TriggerCompleteApplication { get; set; }

        /// <summary>归还位置（由资料管理员在审批办理时指定）。</summary>
        public string TargetLocation { get; set; } = string.Empty;
    }
}
