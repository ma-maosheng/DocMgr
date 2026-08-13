using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveSearchPoolItemRow : ViewModelBase
    {
        public ArchiveSearchPoolItemRow(
            FiledArchiveSearchHit hit,
            ArchiveSearchPoolSelection selection,
            MatchedContentEntryInfo? contentEntry = null)
        {
            Hit = hit;
            Selection = selection;
            ContentEntry = contentEntry;

            if (Selection.RequestedCopyCount < 1)
            {
                Selection.RequestedCopyCount = ArchiveSearchPoolCopyCountSupport.DefaultRequestedCopyCount;
            }
        }

        public FiledArchiveSearchHit Hit { get; }

        public ArchiveSearchPoolSelection Selection { get; }

        public MatchedContentEntryInfo? ContentEntry { get; }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int FilingFactId => Hit.FilingFactId;

        public string FormNo => Hit.FormNo;

        public string MaterialName => Hit.MaterialName;

        public string ProjectName => Hit.ProjectName;

        public string ProjectYear => Hit.ProjectYear;

        public string ItemName => Hit.ItemName;

        public string ConfidentialLevel => Hit.ConfidentialLevel;

        public string ContainerCode => Hit.ContainerCode;

        public string StorageLocation => Hit.StorageLocation;

        public string CurrentStorageLocation => Hit.CurrentStorageLocation;

        public string LifecycleStatusDisplay => Hit.LifecycleStatusDisplay;

        public string BorrowHintDisplay => Hit.BorrowHintDisplay;

        public string StockCopyCountDisplay => Hit.StockCopyCountDisplay;

        public string FilingCopyCountDisplay => Hit.FilingCopyCountDisplay;

        public string RegisterMediaType => Hit.RegisterMediaType;

        public string StorageCarrierTypeDisplay => Hit.StorageCarrierTypeDisplay;

        public string FilingDirectoryDisplay => Hit.FilingStoragePath?.Trim() ?? string.Empty;

        public string DataSizeDisplay => Hit.DataSizeDisplay;

        public string MaterialCategory => Hit.MaterialCategory;

        public string SubCategory => Hit.SubCategory;

        public string DataOrganizationForm => Hit.DataOrganizationForm;

        public string ArchivePurpose => Hit.ArchivePurpose;

        public int MaxCopyCount => ArchiveSearchPoolCopyCountSupport.ResolveMaxCopyCount(
            string.Equals(Hit.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal)
                ? Hit.CurrentInArchiveCopyCount
                : Hit.ContentCount);

        public bool IsCopyCountEditable => ArchiveSearchPoolCopyCountSupport.IsEditableSimulatedWholeItem(
            Hit.MediaKind,
            Selection);

        public int RequestedCopyCount
        {
            get => Selection.RequestedCopyCount;
            set
            {
                if (Selection.RequestedCopyCount == value)
                {
                    return;
                }

                Selection.RequestedCopyCount = value;
                OnPropertyChanged();
            }
        }

        public string SelectionScopeDisplay => ArchiveSearchPoolSupport.ResolveSelectionScopeDisplay(
            Selection.SelectionScopeKind,
            ContentEntry?.EntryKind ?? string.Empty,
            ContentEntry?.EntryName ?? string.Empty,
            ContentEntry?.RelativePath ?? string.Empty);

        public string MatchedContentEntrySummary => ArchiveSearchPoolSupport.ResolveMatchedContentEntrySummary(
            Selection.SelectionScopeKind,
            ContentEntry?.EntryKind ?? string.Empty,
            ContentEntry?.EntryName ?? string.Empty,
            ContentEntry?.RelativePath ?? string.Empty,
            Hit.MatchedContentEntrySummary);
    }
}
