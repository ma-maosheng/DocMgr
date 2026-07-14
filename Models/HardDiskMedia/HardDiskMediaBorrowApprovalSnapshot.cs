namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 借出审批签字快照（资料出库单或硬盘借出申请）。
    /// </summary>
    public sealed class HardDiskMediaBorrowApprovalSnapshot
    {
        public string DeptAuditor { get; init; } = string.Empty;

        public DateTime? DeptAuditDate { get; init; }

        public string ArchiveRoomHead { get; init; } = string.Empty;

        public DateTime? ArchiveRoomHeadDate { get; init; }
    }
}
