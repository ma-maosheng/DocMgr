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

    /// <summary>
    /// 立档台账「立档方式」：按建档申请是否来源于出网申请区分网内/网外资料立档。
    /// </summary>
    public static class FilingLedgerFilingMethod
    {
        /// <summary>未关联出网申请的常规建档立档。</summary>
        public const string OffNetwork = "OffNetwork";

        /// <summary>出网办结转建档后立档。</summary>
        public const string OnNetwork = "OnNetwork";

        public const string OffNetworkLabel = "网外资料立档";

        public const string OnNetworkLabel = "网内资料立档";

        public static string ToDisplay(string? value)
        {
            if (string.Equals(value, OnNetwork, StringComparison.Ordinal))
            {
                return OnNetworkLabel;
            }

            if (string.Equals(value, OffNetwork, StringComparison.Ordinal))
            {
                return OffNetworkLabel;
            }

            return string.Empty;
        }

        /// <summary>
        /// 由建档申请是否关联出网申请解析立档方式编码。
        /// </summary>
        public static string ResolveCode(bool hasSourceNetworkOutbound)
            => hasSourceNetworkOutbound ? OnNetwork : OffNetwork;

        /// <summary>
        /// 由建档申请是否关联出网申请解析立档方式显示名。
        /// </summary>
        public static string ResolveDisplay(bool hasSourceNetworkOutbound)
            => hasSourceNetworkOutbound ? OnNetworkLabel : OffNetworkLabel;
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
