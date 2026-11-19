namespace DocMgr.Models.HardDiskMedia;

/// <summary>
/// 硬盘总览用的数据库聚合结果。只含计数和分组，不含介质、申请、流水的全表行。
/// </summary>
public sealed class HardDiskOverviewAggregate
{
    public int MissingLedgerCount { get; init; }

    public IReadOnlyDictionary<string, int> StatusCounts { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NatureCounts { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal);

    public int NeedReturnCount { get; init; }

    public int TemporaryNeedReturnCount { get; init; }

    public int LongTermNeedReturnCount { get; init; }

    public int MissingLocationCount { get; init; }

    public int OutboundWithoutKeeperCount { get; init; }

    public int LockedCount { get; init; }

    /// <summary>按原始存放位置分组，展示前再按档口归并。</summary>
    public IReadOnlyList<HardDiskOverviewLabelCount> AllLocations { get; init; } = Array.Empty<HardDiskOverviewLabelCount>();

    public IReadOnlyList<HardDiskOverviewLabelCount> InStockLocations { get; init; } = Array.Empty<HardDiskOverviewLabelCount>();

    /// <summary>借出硬盘的保管人与位置原文，由总览按保管规则归并。</summary>
    public IReadOnlyList<HardDiskOverviewKeeperSource> BorrowedKeepers { get; init; } = Array.Empty<HardDiskOverviewKeeperSource>();

    public IReadOnlyList<HardDiskOverviewLabelCount> LockTypes { get; init; } = Array.Empty<HardDiskOverviewLabelCount>();

    public IReadOnlyList<HardDiskOverviewCapacityRow> OutboundCapacities { get; init; } = Array.Empty<HardDiskOverviewCapacityRow>();

    public IReadOnlyList<HardDiskOverviewApplicationGroup> Applications { get; init; } = Array.Empty<HardDiskOverviewApplicationGroup>();

    public IReadOnlyList<HardDiskOverviewDisposalGroup> Disposals { get; init; } = Array.Empty<HardDiskOverviewDisposalGroup>();

    public IReadOnlyList<HardDiskOverviewInventoryGroup> Inventories { get; init; } = Array.Empty<HardDiskOverviewInventoryGroup>();

    public IReadOnlyList<HardDiskOverviewLabelCount> RecentTransactionTypes { get; init; } = Array.Empty<HardDiskOverviewLabelCount>();
}

public sealed record HardDiskOverviewLabelCount(string Label, int Count);

public sealed record HardDiskOverviewKeeperSource(string? Holder, string? Location);

public sealed record HardDiskOverviewCapacityRow(string Status, string? Capacity);

public sealed record HardDiskOverviewApplicationGroup(
    string? ApplicationType,
    int ApplicationStatus,
    bool SignedAttachmentUploaded,
    int Count);

public sealed record HardDiskOverviewDisposalGroup(int Status, bool SignedAttachmentUploaded, int Count);

public sealed record HardDiskOverviewInventoryGroup(int Status, string? RegisterKind, int Count);
