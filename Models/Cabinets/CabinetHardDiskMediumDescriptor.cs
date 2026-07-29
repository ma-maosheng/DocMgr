namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 防磁柜格口中的硬盘介质展示描述。
    /// </summary>
    public sealed class CabinetHardDiskMediumDescriptor
    {
        public string DiskCode { get; init; } = string.Empty;

        public string CapacityText { get; init; } = string.Empty;

        public string StatusText { get; init; } = string.Empty;

        public string CurrentLocationText { get; init; } = string.Empty;

        public string CurrentHolderText { get; init; } = string.Empty;

        public string ElectronicArchiveNoText { get; init; } = string.Empty;

        public string ElectronicArchiveLocationText { get; init; } = string.Empty;

        public string MediumInfoText { get; init; } = string.Empty;

        public string ArchiveInfoText { get; init; } = string.Empty;

        public bool HasArchiveInfo { get; init; }

        public bool IsPendingReturn { get; init; }

        public bool IsYearlyArchiveDisplay { get; init; }

        public bool IsOpticalDiscMedia { get; init; }

        public string YearText { get; init; } = string.Empty;

        public string ProjectText { get; init; } = string.Empty;

        public string UsedCapacityDisplayText { get; init; } = string.Empty;

        public string RemainingCapacityDisplayText { get; init; } = string.Empty;

        public int ArchiveSequenceNumber { get; init; }

        public string ArchiveSequenceText { get; init; } = string.Empty;

        public string ToolTipText { get; init; } = string.Empty;

        public int ElectronicArchiveUnitId { get; init; }

        public int MediumId { get; init; }

        public bool IsBlankInStock { get; init; }

        public bool HasOccupationLock { get; init; }

        public string OccupationLockToolTipText { get; init; } = string.Empty;

        public string OccupationLockBadgeText { get; init; } = string.Empty;

        /// <summary>盘库标识：空 / 失 / X（电子袋介质卡）。</summary>
        public string InventoryMarkBadgeText { get; init; } = string.Empty;
    }
}
