namespace DocMgr.Models.YearlyArchive
{
    public static class FilingFactLifecycleStatus
    {
        public const string InArchive = "InArchive";
        public const string Borrowed = "Borrowed";
        public const string Transferred = "Transferred";
        public const string Destroyed = "Destroyed";
        public const string Disposed = "Disposed";
    }

    public static class FilingFactSourceLinkType
    {
        public const string BoxMediaItemLink = "BoxMediaItemLink";
        public const string ElectronicMediaItemLink = "ElectronicMediaItemLink";
    }

    public static class FilingFactBorrowHintLevel
    {
        public const string None = "None";
        public const string CopyBorrowed = "CopyBorrowed";
        public const string OriginalBorrowed = "OriginalBorrowed";
        public const string PartialAvailable = "PartialAvailable";
        public const string Unknown = "Unknown";
    }

    public static class FilingFactArchiveCopyRole
    {
        public const string Original = "Original";
        public const string Backup = "Backup";
    }

    public static class ArchiveSearchResultSetStatus
    {
        public const string Draft = "Draft";
        public const string Confirmed = "Confirmed";
        public const string Referenced = "Referenced";
    }

    public static class ArchiveFilingSearchDirection
    {
        public const string Register = "Register";
        public const string Container = "Container";
    }

    /// <summary>
    /// 检索筛选池条目范围：整资料子项，或子项内具体目录/文件。
    /// </summary>
    public static class ArchiveSearchSelectionScopeKind
    {
        public const string WholeMediaItem = "WholeMediaItem";
        public const string ContentEntry = "ContentEntry";
    }
}
