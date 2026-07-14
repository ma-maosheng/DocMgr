namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质非正常归还情况表打印数据。
    /// </summary>
    public sealed class HardDiskMediaAbnormalReturnReportPrintData
    {
        public string ApplicationNo { get; init; } = string.Empty;

        public string SourceApplicationNo { get; init; } = string.Empty;

        public string ReturnDateText { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicationType { get; init; } = string.Empty;

        public string DiskCode { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public string CurrentLocation { get; init; } = string.Empty;

        public string InspectionResult { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        /// <summary>申请人所属部门负责人签字栏姓名（留白时为空）。</summary>
        public string ApplicantDeptHeadSignerSlot { get; init; } = string.Empty;

        /// <summary>申请人所属部门负责人签字栏日期文本。</summary>
        public string ApplicantDeptHeadSignatureDateText { get; init; } = string.Empty;

        /// <summary>资料室负责人签字栏姓名（留白时为空）。</summary>
        public string ArchiveRoomHeadSignerSlot { get; init; } = string.Empty;

        /// <summary>资料室负责人签字栏日期文本。</summary>
        public string ArchiveRoomHeadSignatureDateText { get; init; } = string.Empty;

        /// <summary>归还人签字栏是否留白。</summary>
        public bool BlankReturnerSignature { get; init; } = true;

        /// <summary>借出审批签字栏是否留白（部门负责人、资料室负责人）。</summary>
        public bool BlankBorrowApprovalSignatures { get; init; } = true;
    }
}
