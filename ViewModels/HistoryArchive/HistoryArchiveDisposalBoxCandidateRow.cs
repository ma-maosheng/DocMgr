using System.Windows.Input;
using DocMgr.Models.HistoryArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 历史存档离库处置候选盒勾选项。
/// </summary>
public sealed class HistoryArchiveDisposalBoxCandidateRow : ViewModelBase
{
    private bool _isSelected;

    public HistoryArchiveDisposalBoxCandidateRow(HistoryArchiveDisposalBoxCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        Candidate = candidate;
    }

    public HistoryArchiveDisposalBoxCandidate Candidate { get; }

    public event Action? SelectionChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            SelectionChanged?.Invoke();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string BoxCode => Candidate.BoxCode;
    public string BoxSpecification => Candidate.BoxSpecification;
    public string CabinetName => Candidate.CabinetName;
    public string StorageLocation => Candidate.StorageLocation;
    public string ContentSummary => Candidate.ContentSummary;
    public int LedgerRecordCount => Candidate.LedgerRecordCount;
    public int RelatedBoxCount => Candidate.RelatedBoxCount;
    public string RelatedBoxCodesText => Candidate.RelatedBoxCodesText;
    public bool IsMixedPlacement => Candidate.IsMixedPlacement;
    public string MixedPlacementText => Candidate.IsMixedPlacement ? "混放" : string.Empty;
    public bool IsSelectable => Candidate.IsSelectable;
    public string UnavailableReason =>
        Candidate.IsCrossTypeMixed
            ? "跨类同盒，请先拆盒"
            : Candidate.IsLockedByOther
                ? "已被其他未办结单占用"
                : string.Empty;
}
