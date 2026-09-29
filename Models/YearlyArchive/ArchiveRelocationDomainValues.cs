namespace DocMgr.Models.YearlyArchive
{
    public static class ArchiveRelocationMode
    {
        public const string PhysicalMove = "PhysicalMove";

        /// <summary>迁入空白硬盘（电子介质）。</summary>
        public const string MoveToBlankHardDisk = "MoveToBlankHardDisk";

        /// <summary>迁入空白光盘（电子介质；系统不管理空白光盘库存，确认后按立档规则登记数据光盘）。</summary>
        public const string MoveToBlankOpticalDisc = "MoveToBlankOpticalDisc";

        /// <summary>历史兼容：旧「迁入空盘/空袋」，语义等同 <see cref="MoveToBlankHardDisk"/>。</summary>
        public const string MoveToEmpty = "MoveToEmpty";

        public const string MergeToExisting = "MergeToExisting";
        public const string BatchPhysicalMove = "BatchPhysicalMove";

        /// <summary>是否为迁入空白载体类模式（空白硬盘 / 空白光盘，含历史 MoveToEmpty）。</summary>
        public static bool IsMoveToBlankCarrier(string? relocationMode)
        {
            return string.Equals(relocationMode, MoveToBlankHardDisk, StringComparison.Ordinal)
                || string.Equals(relocationMode, MoveToBlankOpticalDisc, StringComparison.Ordinal)
                || string.Equals(relocationMode, MoveToEmpty, StringComparison.Ordinal);
        }

        /// <summary>是否为迁入空白硬盘（含历史 MoveToEmpty）。</summary>
        public static bool IsMoveToBlankHardDisk(string? relocationMode)
        {
            return string.Equals(relocationMode, MoveToBlankHardDisk, StringComparison.Ordinal)
                || string.Equals(relocationMode, MoveToEmpty, StringComparison.Ordinal);
        }

        /// <summary>是否为迁入空白光盘。</summary>
        public static bool IsMoveToBlankOpticalDisc(string? relocationMode)
            => string.Equals(relocationMode, MoveToBlankOpticalDisc, StringComparison.Ordinal);
    }

    public static class ArchiveContainerLifecycleStatus
    {
        public const string InUse = "InUse";
        public const string Emptied = "Emptied";
        public const string Retired = "Retired";
        public const string Relocated = "Relocated";
        public const string Disposed = "Disposed";

        public static bool OccupiesCabinet(string? status)
        {
            return string.Equals(status, InUse, StringComparison.Ordinal);
        }
    }

    public static class ArchiveRelocationSourceDisposition
    {
        public const string None = "None";
        public const string BoxEmptied = "BoxEmptied";
        public const string BoxRetired = "BoxRetired";
        public const string HardDiskFormattedBlank = "HardDiskFormattedBlank";
        public const string OpticalDiscDestroyed = "OpticalDiscDestroyed";
        public const string UnitRelocated = "UnitRelocated";
        public const string OriginalRetained = "OriginalRetained";
    }
}
