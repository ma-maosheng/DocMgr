namespace DocMgr.Models.YearlyArchive
{
    public sealed class ElectronicMediaContentScanEntry
    {
        public string EntryKind { get; init; } = string.Empty;

        public string EntryName { get; init; } = string.Empty;

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

    /// <summary>
    /// 资料名称文件夹的一级子目录清单。每个子目录对应一条待添加的资料子项。
    /// </summary>
    public sealed class ElectronicMediaMaterialFolderListing
    {
        public IReadOnlyList<string> SubItemDirectories { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 资料名称文件夹内直接散落的文件数。这些文件不生成资料子项。
        /// </summary>
        public int LooseFileCount { get; init; }
    }
}
