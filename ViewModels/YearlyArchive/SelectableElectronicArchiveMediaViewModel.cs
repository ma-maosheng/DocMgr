using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子介质待立档选择项。
    /// </summary>
    public sealed class SelectableElectronicArchiveMediaViewModel : ViewModelBase
    {
        private bool _isSelected;

        public int MediaEntryId { get; init; }

        public int MediaItemId { get; init; }

        public int RecordId { get; init; }

        public string FormNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string MediaType { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = string.Empty;

        public string SubCategory { get; init; } = string.Empty;

        public string DataOrganizationForm { get; init; } = string.Empty;

        public int MediaCount { get; init; }

        public decimal DataSizeMb { get; init; }

        public string StoragePath { get; init; } = string.Empty;

        public string Disposition { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public bool CanSelect { get; init; } = true;

        public string ArchiveStatusText { get; init; } = string.Empty;

        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string LinkedMediumCodes { get; init; } = string.Empty;

        /// <summary>
        /// 是否为资料室借出硬盘。
        /// </summary>
        public bool IsBorrowedHardDisk { get; init; }

        /// <summary>
        /// 借出硬盘介质编号（非借出硬盘时通常为空）。
        /// </summary>
        public string BorrowedHardDiskCode { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
