namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质申请审批单打印数据。
    /// </summary>
    public sealed record HardDiskMediaPrintData
    {
        /// <summary>
        /// 申请单编号。
        /// </summary>
        public string ApplicationNo { get; init; } = string.Empty;

        /// <summary>
        /// 来源申请单编号。
        /// </summary>
        public string SourceApplicationNo { get; init; } = string.Empty;

        /// <summary>
        /// 申请类型。
        /// </summary>
        public string ApplicationType { get; init; } = string.Empty;

        /// <summary>
        /// 申请状态。
        /// </summary>
        public string ApplicationStatus { get; init; } = string.Empty;

        /// <summary>
        /// 硬盘编号。
        /// </summary>
        public string DiskCode { get; init; } = string.Empty;

        /// <summary>
        /// 序列号。
        /// </summary>
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>
        /// 硬盘类型。
        /// </summary>
        public string DiskType { get; init; } = string.Empty;

        /// <summary>
        /// 品牌容量摘要。
        /// </summary>
        public string DeviceSummary { get; init; } = string.Empty;

        /// <summary>
        /// 当前状态。
        /// </summary>
        public string CurrentStatus { get; init; } = string.Empty;

        /// <summary>
        /// 当前介质属性。
        /// </summary>
        public string MediaNature { get; init; } = string.Empty;

        /// <summary>
        /// 登记方式。
        /// </summary>
        public string RegistrationMethod { get; init; } = string.Empty;

        /// <summary>
        /// 申请人。
        /// </summary>
        public string ApplicantName { get; init; } = string.Empty;

        /// <summary>
        /// 申请部门。
        /// </summary>
        public string ApplicantDept { get; init; } = string.Empty;

        /// <summary>
        /// 申请日期文本。
        /// </summary>
        public string ApplyDateText { get; init; } = string.Empty;

        /// <summary>
        /// 当前存放位置。
        /// </summary>
        public string CurrentLocation { get; init; } = string.Empty;

        /// <summary>
        /// 目标位置。
        /// </summary>
        public string TargetLocation { get; init; } = string.Empty;

        /// <summary>
        /// 对方人员或单位。
        /// </summary>
        public string TargetPersonOrUnit { get; init; } = string.Empty;

        /// <summary>
        /// 预计归还日期文本。
        /// </summary>
        public string ExpectedReturnDateText { get; init; } = string.Empty;

        /// <summary>
        /// 相关批次。
        /// </summary>
        public string RelatedBatch { get; init; } = string.Empty;

        /// <summary>
        /// 相关资料标题。
        /// </summary>
        public string RelatedArchiveTitle { get; init; } = string.Empty;

        /// <summary>
        /// 申请原因。
        /// </summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remark { get; init; } = string.Empty;

        /// <summary>
        /// 审核人。
        /// </summary>
        public string ReviewerName { get; init; } = string.Empty;

        /// <summary>
        /// 审核日期文本。
        /// </summary>
        public string ReviewerDateText { get; init; } = string.Empty;

        /// <summary>
        /// 审批人。
        /// </summary>
        public string ApproverName { get; init; } = string.Empty;

        /// <summary>
        /// 审批日期文本。
        /// </summary>
        public string ApproverDateText { get; init; } = string.Empty;

        /// <summary>
        /// 经办（申请侧）人员。
        /// </summary>
        public string HandoverApplicant { get; init; } = string.Empty;

        /// <summary>
        /// 经办（资料室侧）人员。
        /// </summary>
        public string HandoverAdmin { get; init; } = string.Empty;

        /// <summary>
        /// 经办日期文本。
        /// </summary>
        public string HandoverDateText { get; init; } = string.Empty;

        /// <summary>
        /// 查验结果。
        /// </summary>
        public string InspectionResultText { get; init; } = string.Empty;

        /// <summary>
        /// 格式化确认文本。
        /// </summary>
        public string FormatConfirmationText { get; init; } = string.Empty;

        /// <summary>
        /// 审批意见。
        /// </summary>
        public string ApprovalOpinion { get; init; } = string.Empty;

        /// <summary>
        /// 审批签字文本。
        /// </summary>
        public string ApprovalSignatureText { get; init; } = string.Empty;

        /// <summary>
        /// 打印次数。
        /// </summary>
        public int PrintCount { get; init; }
    }
}
