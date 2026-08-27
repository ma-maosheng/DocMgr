using DocMgr.Models.HistoryArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 历史存档离库处置已选明细行。
/// </summary>
public sealed class HistoryArchiveDisposalItemRow : ViewModelBase
{
    public HistoryArchiveDisposalItemRow(HistoryArchiveDisposalBoxCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        BoxCode = candidate.BoxCode;
        BoxSpecification = candidate.BoxSpecification;
        CabinetName = candidate.CabinetName;
        FaceCode = candidate.FaceCode;
        SlotCode = candidate.SlotCode;
        BeforeStorageLocation = candidate.StorageLocation;
        ContentSummary = candidate.ContentSummary;
        LedgerRecordCount = candidate.LedgerRecordCount;
        SourceRecordKeys = candidate.SourceRecordKeys;
        IsMixedPlacement = candidate.IsMixedPlacement;
        RelatedBoxCodes = candidate.RelatedBoxCodesText;
    }

    public HistoryArchiveDisposalItemRow(HistoryArchiveDisposalItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        BoxCode = item.BoxCode;
        BoxSpecification = item.BoxSpecification;
        CabinetName = item.CabinetName;
        FaceCode = item.FaceCode;
        SlotCode = item.SlotCode;
        BeforeStorageLocation = item.BeforeStorageLocation;
        ContentSummary = item.ContentSummary;
        LedgerRecordCount = item.LedgerRecordCount;
        SourceRecordKeys = item.SourceRecordKeys;
        IsMixedPlacement = item.IsMixedPlacement;
        RelatedBoxCodes = item.RelatedBoxCodes;
    }

    public string BoxCode { get; }
    public string BoxSpecification { get; }
    public string CabinetName { get; }
    public string FaceCode { get; }
    public string SlotCode { get; }
    public string BeforeStorageLocation { get; }
    public string ContentSummary { get; }
    public int LedgerRecordCount { get; }
    public string SourceRecordKeys { get; }
    public bool IsMixedPlacement { get; }
    public string RelatedBoxCodes { get; }
    public string MixedPlacementText => IsMixedPlacement ? "混放" : string.Empty;

    public HistoryArchiveDisposalItem ToItem(int sortOrder) =>
        new()
        {
            SortOrder = sortOrder,
            BoxCode = BoxCode,
            BoxSpecification = BoxSpecification,
            CabinetName = CabinetName,
            FaceCode = FaceCode,
            SlotCode = SlotCode,
            BeforeStorageLocation = BeforeStorageLocation,
            ContentSummary = ContentSummary,
            LedgerRecordCount = LedgerRecordCount,
            SourceRecordKeys = SourceRecordKeys,
            IsMixedPlacement = IsMixedPlacement,
            RelatedBoxCodes = RelatedBoxCodes,
            CreatedAt = DateTime.Now
        };
}
