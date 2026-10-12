using DocMgr.Models.HistoryArchive;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 历史存档盒台账列表行。
/// </summary>
public sealed class HistoryArchiveBoxLedgerRow
{
    public HistoryArchiveBoxLedgerRow(HistoryArchiveBoxLedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
    }

    public HistoryArchiveBoxLedgerEntry Entry { get; }

    public string MaterialKindDisplay => Entry.MaterialKindDisplay;

    public string BoxCode => Entry.BoxCode;

    public string BoxSpecification => Entry.BoxSpecification;

    public string CabinetName => Entry.CabinetName;

    public string ContentSummary => Entry.ContentSummary;

    public int LedgerRecordCount => Entry.LedgerRecordCount;

    public string MixedPlacementText => Entry.IsMixedPlacement ? "混放" : string.Empty;

    public int RelatedBoxCount => Entry.RelatedBoxCount;

    public string RelatedBoxCodesText => Entry.RelatedBoxCodesText;

    public string LifecycleStatus => Entry.LifecycleStatus;

    /// <summary>已离库行：盒号等追溯字段用独立文本色。</summary>
    public bool IsDisposed => Entry.IsDisposed;

    public bool IsSelectable => Entry.IsSelectable;

    public string AvailabilityText
    {
        get
        {
            if (Entry.IsDisposed)
            {
                return "已离库";
            }

            if (HistoryArchiveDisposalDomainValues.IsLockedLifecycle(Entry.LifecycleStatus)
                || Entry.IsLockedByOther)
            {
                return "处置占用中";
            }

            if (Entry.IsCrossTypeMixed)
            {
                return "跨类同盒";
            }

            return Entry.IsSelectable ? "可处置" : "不可选";
        }
    }
}
