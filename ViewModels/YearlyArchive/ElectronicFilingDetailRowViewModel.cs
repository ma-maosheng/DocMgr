using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 第四步「资料立档明细」行。
    /// </summary>
    public sealed class ElectronicFilingDetailRowViewModel : ViewModelBase
    {
        private string _filingStoragePath = string.Empty;

        public int MediaItemId { get; init; }

        public int MediaEntryId { get; init; }

        public string FormNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string MediaType { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = string.Empty;

        public string SubCategory { get; init; } = string.Empty;

        public string DataOrganizationForm { get; init; } = string.Empty;

        public int ContentCount { get; init; }

        public decimal DataSizeMb { get; init; }

        public string SourceStoragePath { get; init; } = string.Empty;

        public string FilingStoragePath
        {
            get => _filingStoragePath;
            set => SetProperty(ref _filingStoragePath, value);
        }

        public string ItemName { get; init; } = string.Empty;

        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string MediumCode { get; init; } = string.Empty;

        public bool IsStoragePathEditable { get; init; }

        public DateTime? ArchivedAt { get; init; }
    }
}
