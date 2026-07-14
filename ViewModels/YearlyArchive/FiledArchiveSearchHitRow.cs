using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class FiledArchiveSearchHitRow : ViewModelBase
    {
        public FiledArchiveSearchHitRow(FiledArchiveSearchHit hit)
        {
            Hit = hit;
        }

        public FiledArchiveSearchHit Hit { get; }

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

        public string ItemName => Hit.ItemName;

        public string ContainerCode => Hit.ContainerCode;

        public string StorageLocation => Hit.StorageLocation;

        public string CurrentStorageLocation => Hit.CurrentStorageLocation;

        public string LifecycleStatusDisplay => Hit.LifecycleStatusDisplay;

        public string BorrowHintDisplay => Hit.BorrowHintDisplay;

        public string FiledAtDisplay => Hit.FiledAt.ToString("yyyy-MM-dd");

        public string MatchedContentEntrySummary => Hit.MatchedContentEntrySummary;

        public string MediumCode => Hit.MediumCode;

        public string StorageCarrierType => Hit.StorageCarrierType;

        public string LinkedMediumDisplay => Hit.LinkedMediumDisplay;

        public string RegisterMediaType => Hit.RegisterMediaType;

        public string MaterialCategory => Hit.MaterialCategory;

        public string SubCategory => Hit.SubCategory;

        public string DataOrganizationForm => Hit.DataOrganizationForm;

        public string ArchivePurpose => Hit.ArchivePurpose;

        public string StockCopyCountDisplay => Hit.StockCopyCountDisplay;

        public string ContentCountDisplay => Hit.ContentCountDisplay;

        public string FilingCopyCountDisplay => Hit.FilingCopyCountDisplay;
    }
}
