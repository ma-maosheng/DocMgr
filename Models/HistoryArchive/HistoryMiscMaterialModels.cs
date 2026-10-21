namespace DocMgr.Models.HistoryArchive;

/// <summary>杂项资料已录入的大类、子类与容器类型。</summary>
public sealed class HistoryMiscVocabulary
{
    public IReadOnlyList<string> MajorCategories { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ContainerKinds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<HistoryMiscSubCategoryOption> SubCategories { get; init; } = Array.Empty<HistoryMiscSubCategoryOption>();
}

/// <summary>某个资料大类下已出现的子类。</summary>
public sealed class HistoryMiscSubCategoryOption
{
    public string MajorCategory { get; init; } = string.Empty;

    public string SubCategory { get; init; } = string.Empty;
}

/// <summary>立式/卧式文件柜格口推荐结果。不表示容量已满。</summary>
public sealed class HistoryMiscSlotOption
{
    public string CabinetName { get; init; } = string.Empty;

    public string CabinetTypeDisplay { get; init; } = string.Empty;

    /// <summary>档口键，如丙A-2-1。</summary>
    public string SlotKey { get; init; } = string.Empty;

    public int SuggestedSequence { get; init; }

    /// <summary>建议写入的四段位置。</summary>
    public string SuggestedLocation { get; init; } = string.Empty;

    /// <summary>该档口已被占用的序号个数，仅作提示。</summary>
    public int OccupiedCount { get; init; }

    public string OccupiedHint => OccupiedCount > 0 ? $"已有 {OccupiedCount} 个序号" : "尚无序号";
}

/// <summary>保存前暂存的实物照片。</summary>
public sealed class HistoryMiscPhotoDraft
{
    public string FileName { get; init; } = string.Empty;

    public byte[] Content { get; init; } = Array.Empty<byte>();
}

/// <summary>杂项资料保存请求。</summary>
public sealed class HistoryMiscMaterialSaveRequest
{
    public int Id { get; init; }

    public string MajorCategory { get; init; } = string.Empty;

    public string SubCategory { get; init; } = string.Empty;

    public string ContainerKind { get; init; } = string.Empty;

    public string ContainerCode { get; init; } = string.Empty;

    public string StorageLocation { get; init; } = string.Empty;

    public string Remark { get; init; } = string.Empty;

    public IReadOnlyList<string> DetailLines { get; init; } = Array.Empty<string>();

    public IReadOnlyList<int> RemovedAttachmentIds { get; init; } = Array.Empty<int>();

    public IReadOnlyList<HistoryMiscPhotoDraft> AddedPhotos { get; init; } = Array.Empty<HistoryMiscPhotoDraft>();
}

/// <summary>杂项实物照片目录项，不含文件内容。</summary>
public sealed class HistoryMiscPhotoCatalogItem
{
    public int AttachmentId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public DateTime UploadTime { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(FileName) ? "实物照片" : FileName.Trim();
}

/// <summary>杂项资料列表行。</summary>
public sealed class HistoryMiscMaterialListItem
{
    public int Id { get; init; }

    public string MajorCategory { get; init; } = string.Empty;

    public string SubCategory { get; init; } = string.Empty;

    public string ContainerKind { get; init; } = string.Empty;

    public string ContainerCode { get; init; } = string.Empty;

    public string StorageLocation { get; init; } = string.Empty;

    public string LastStorageLocation { get; init; } = string.Empty;

    public string LifecycleStatus { get; init; } = string.Empty;

    public string DetailSummary { get; init; } = string.Empty;

    public string Remark { get; init; } = string.Empty;

    public int PhotoCount { get; init; }

    public string LocationDisplay =>
        HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(LifecycleStatus)
            ? LastStorageLocation
            : StorageLocation;
}
