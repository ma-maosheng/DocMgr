namespace DocMgr.Models.YearlyArchive
{
    public static class ArchiveRelocationMode
    {
        public const string PhysicalMove = "PhysicalMove";
        public const string MoveToEmpty = "MoveToEmpty";
        public const string MergeToExisting = "MergeToExisting";
        public const string BatchPhysicalMove = "BatchPhysicalMove";
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
