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

    public string ContainerKind => Entry.ContainerKind;

    public string ContainerCode => Entry.ContainerCode;

    public string MajorCategory => Entry.MajorCategory;

    public string SubCategory => Entry.SubCategory;

    /// <summary>杂项资料主键。档案盒行为 0。</summary>
    public int MiscMaterialId => Entry.MiscMaterialId;

    /// <summary>杂项实物照片张数。</summary>
    public int PhotoCount => Entry.PhotoCount;

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
