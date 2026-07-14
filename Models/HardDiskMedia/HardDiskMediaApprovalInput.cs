namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质审批信息录入模型。
    /// </summary>
    public sealed class HardDiskMediaApprovalInput
    {
        /// <summary>
        /// 审核人。
        /// </summary>
        public string ReviewerName { get; set; } = string.Empty;

        /// <summary>
        /// 审核日期。
        /// </summary>
        public DateTime? ReviewerDate { get; set; }

        /// <summary>
        /// 审批人。
        /// </summary>
        public string ApproverName { get; set; } = string.Empty;

        /// <summary>
        /// 审批日期。
        /// </summary>
        public DateTime? ApproverDate { get; set; }

        /// <summary>
        /// 办理交接人：申请人
        /// </summary>
        public string HandoverApplicant { get; set; } = string.Empty;

        /// <summary>
        /// 办理交接人：资料室资料管理员
        /// </summary>
        public string HandoverAdmin { get; set; } = string.Empty;

        /// <summary>
        /// 通用办理交接人（兼容旧字段名 `HandoverName`）
        /// </summary>
        public string HandoverName { get; set; } = string.Empty;

        /// <summary>
        /// 办理交接日期。
        /// </summary>
        public DateTime? HandoverDate { get; set; }

        /// <summary>
        /// 审批意见。
        /// </summary>
        public string ApprovalOpinion { get; set; } = string.Empty;

        /// <summary>
        /// 审批后是否触发签字件上传。
        /// </summary>
        public bool TriggerUploadSignedAttachment { get; set; }

        /// <summary>
        /// 审批后是否触发交接单打印。
        /// </summary>
        public bool TriggerPrintHandoverSheet { get; set; }

        /// <summary>
        /// 审批后是否触发办理完成。
        /// </summary>
        public bool TriggerCompleteApplication { get; set; }
    }
}
