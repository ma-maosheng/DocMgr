namespace DocMgr.Models.HistoryArchive;

/// <summary>
/// 历史存档盒台账条目（在库 / 离库锁定 / 已离库）。
/// </summary>
public sealed class HistoryArchiveBoxLedgerEntry
{
    public string MaterialKind { get; init; } = string.Empty;

    public string MaterialKindDisplay { get; init; } = string.Empty;

    public string BoxCode { get; init; } = string.Empty;

    public string BoxSpecification { get; init; } = string.Empty;

    public string CabinetName { get; init; } = string.Empty;

    public string ContentSummary { get; init; } = string.Empty;

    public int LedgerRecordCount { get; init; }

    public bool IsMixedPlacement { get; init; }

    public IReadOnlyList<string> RelatedBoxCodes { get; init; } = Array.Empty<string>();

    public int RelatedBoxCount => RelatedBoxCodes.Count;

    public string RelatedBoxCodesText =>
        RelatedBoxCodes.Count == 0
            ? string.Empty
            : string.Join("；", RelatedBoxCodes);

    /// <summary>盒生命周期：在库 / 离库锁定 / 已离库。</summary>
    public string LifecycleStatus { get; init; } = HistoryArchiveDisposalDomainValues.LifecycleInStock;

    public bool IsCrossTypeMixed { get; init; }

    public bool IsLockedByOther { get; init; }

    public bool IsDisposed =>
        HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(LifecycleStatus);

    public bool IsSelectable =>
        !IsDisposed && !IsCrossTypeMixed && !IsLockedByOther && LedgerRecordCount > 0
        && HistoryArchiveDisposalDomainValues.IsInStockLifecycle(LifecycleStatus);
}
