using System;
using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    public sealed class FiledArchiveSearchGroupHit
    {
        public FiledArchiveSearchHit PrimaryHit { get; init; } = null!;

        public IReadOnlyList<FiledArchiveSearchHit> BackupHits { get; init; } = Array.Empty<FiledArchiveSearchHit>();

        public int BackupCount => BackupHits.Count;

        public bool HasMatchingBackup { get; init; }

        public bool ExpandByDefault { get; init; }
    }

    /// <summary>
    /// 模拟介质检索结果：按档案盒归组后的盒级摘要与盒内资料子项分组。
    /// </summary>
    public sealed class FiledArchiveSearchBoxGroupHit
    {
        public string ArchiveSequenceNo { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string CurrentStorageLocation { get; init; } = string.Empty;

        public string Specifications { get; init; } = string.Empty;

        public string PlacementMode { get; init; } = string.Empty;

        public string ArchivedBy { get; init; } = string.Empty;

        public DateTime? ArchivedDate { get; init; }

        public string Remarks { get; init; } = string.Empty;

        /// <summary>档案盒容器生命周期状态（InUse/Emptied/Retired 等）。</summary>
        public string ContainerLifecycleStatus { get; init; } = string.Empty;

        public IReadOnlyList<FiledArchiveSearchGroupHit> ItemGroups { get; init; } =
            Array.Empty<FiledArchiveSearchGroupHit>();

        public int MatchedItemCount => ItemGroups.Count;
    }
}
