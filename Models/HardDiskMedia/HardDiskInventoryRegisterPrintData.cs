namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘盘库登记签批单打印数据。
    /// </summary>
    public sealed class HardDiskInventoryRegisterPrintData
    {
        public string RegisterNo { get; init; } = string.Empty;

        public string ApplyDateText { get; init; } = string.Empty;

        public string RegisterKind { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public string ApprovedBy { get; init; } = string.Empty;

        public string ApprovedDateText { get; init; } = string.Empty;

        public string ApprovalOpinion { get; init; } = string.Empty;

        public string CompletedBy { get; init; } = string.Empty;

        public string CompletedDateText { get; init; } = string.Empty;

        public bool IsCompleted { get; init; }

        public string DeptHead { get; init; } = string.Empty;

        public string DeptHeadDateText { get; init; } = string.Empty;

        public string ArchiveRoomHead { get; init; } = string.Empty;

        public string ArchiveRoomHeadDateText { get; init; } = string.Empty;

        public string ProductionHead { get; init; } = string.Empty;

        public string ProductionHeadDateText { get; init; } = string.Empty;

        public string ArchiveDeputyPresident { get; init; } = string.Empty;

        public string ArchiveDeputyPresidentDateText { get; init; } = string.Empty;

        public string ProductionVicePresident { get; init; } = string.Empty;

        public string ProductionVicePresidentDateText { get; init; } = string.Empty;

        public bool EnableDeptHead { get; init; }

        public bool EnableArchiveRoomHead { get; init; } = true;

        public bool EnableProductionHead { get; init; }

        public bool EnableArchiveDeputyPresident { get; init; } = true;

        public bool EnableProductionVicePresident { get; init; }

        /// <summary>已累计打印次数（不含本次）。</summary>
        public int PrintCount { get; init; }

        public IReadOnlyList<HardDiskInventoryRegisterPrintItemData> Items { get; init; } =
            Array.Empty<HardDiskInventoryRegisterPrintItemData>();
    }

    /// <summary>
    /// 硬盘盘库登记签批单明细行。
    /// </summary>
    public sealed class HardDiskInventoryRegisterPrintItemData
    {
        public int SortOrder { get; init; }

        public string DiskCode { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public string BeforeMediaStatus { get; init; } = string.Empty;

        public string BeforeStorageLocation { get; init; } = string.Empty;

        public string TargetStorageLocation { get; init; } = string.Empty;
    }
}
