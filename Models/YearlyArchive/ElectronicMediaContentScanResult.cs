namespace DocMgr.Models.YearlyArchive
{
    public sealed class ElectronicMediaContentScanEntry
    {
        public string EntryKind { get; init; } = string.Empty;

        public string EntryName { get; init; } = string.Empty;

        public string RelativePath { get; init; } = string.Empty;

        public decimal? SizeMb { get; init; }

        public DateTime? CreatedAt { get; init; }

        public DateTime? ModifiedAt { get; init; }
    }

    public sealed class ElectronicMediaContentScanResult
    {
        public string RootPath { get; init; } = string.Empty;

        public IReadOnlyList<ElectronicMediaContentScanEntry> Entries { get; init; } = Array.Empty<ElectronicMediaContentScanEntry>();

        public int FileCount { get; init; }

        public decimal TotalSizeMb { get; init; }
    }
}
