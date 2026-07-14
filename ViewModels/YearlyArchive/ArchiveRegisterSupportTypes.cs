using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    public class MediaEntryViewModel : ViewModelBase
    {
        public MediaEntryViewModel()
        {
            ItemDetailsPanel = new ItemDetailsListPresenter<MediaItemViewModel>(
                "资料子项",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.ContentDesc,
                    "暂无资料子项"));

            Items.CollectionChanged += (_, _) => ItemDetailsPanel.RefreshItems(Items, preserveExpanded: ItemDetailsPanel.IsExpanded);
            ItemDetailsPanel.RefreshItems(Items);
        }

        /// <summary>同一介质组内「借出硬盘是/否」单选互斥；每行唯一，避免跨多组介质串组。</summary>
        private readonly string _borrowedHardDiskRadioGroup = Guid.NewGuid().ToString("N");
        public string BorrowedHardDiskRadioGroup => _borrowedHardDiskRadioGroup;

        private string _mediaKind = string.Empty;
        public string MediaKind
        {
            get => _mediaKind;
            set
            {
                if (SetProperty(ref _mediaKind, value))
                {
                    ResetBorrowedHardDiskRegistrationIfNotApplicable();
                    OnPropertyChanged(nameof(IsElectronicMedia));
                    OnPropertyChanged(nameof(IsSimulatedMedia));
                    OnPropertyChanged(nameof(IsRetainedHardDiskScenario));
                    OnPropertyChanged(nameof(BorrowedHardDiskDetailsVisibility));
                }
            }
        }

        private string _mediaType = string.Empty;
        public string MediaType
        {
            get => _mediaType;
            set
            {
                if (SetProperty(ref _mediaType, value))
                {
                    ResetBorrowedHardDiskRegistrationIfNotApplicable();
                    OnPropertyChanged(nameof(IsRetainedHardDiskScenario));
                    OnPropertyChanged(nameof(BorrowedHardDiskDetailsVisibility));
                }
            }
        }

        private int _mediaCount = 1;
        public int MediaCount { get => _mediaCount; private set => SetProperty(ref _mediaCount, value); }

        internal void SetAutoMediaCount(int count)
        {
            if (_mediaCount != count)
            {
                _mediaCount = count;
                OnPropertyChanged(nameof(MediaCount));
            }
        }

        private string _disposition = string.Empty;
        public string Disposition
        {
            get => _disposition;
            set
            {
                if (SetProperty(ref _disposition, value))
                {
                    ResetBorrowedHardDiskRegistrationIfNotApplicable();
                    OnPropertyChanged(nameof(IsRetainedHardDiskScenario));
                    OnPropertyChanged(nameof(BorrowedHardDiskDetailsVisibility));
                }
            }
        }

        private string _otherDesc = string.Empty;
        public string OtherDesc { get => _otherDesc; set => SetProperty(ref _otherDesc, value); }

        public bool IsElectronicMedia => string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal);

        public bool IsSimulatedMedia => string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);

        private bool _isBorrowedHardDisk;
        public bool IsBorrowedHardDisk
        {
            get => _isBorrowedHardDisk;
            set
            {
                if (SetProperty(ref _isBorrowedHardDisk, value))
                {
                    if (!value)
                    {
                        BorrowedHardDiskCode = string.Empty;
                    }

                    OnPropertyChanged(nameof(BorrowedHardDiskDetailsVisibility));
                }
            }
        }

        private string _borrowedHardDiskCode = string.Empty;
        public string BorrowedHardDiskCode
        {
            get => _borrowedHardDiskCode;
            set => SetProperty(ref _borrowedHardDiskCode, value);
        }

        public bool IsRetainedHardDiskScenario =>
            string.Equals(MediaKind?.Trim(), ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)
            && string.Equals(MediaType?.Trim(), ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Disposition?.Trim(), ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase);

        public bool BorrowedHardDiskDetailsVisibility => IsRetainedHardDiskScenario && IsBorrowedHardDisk;

        private void ResetBorrowedHardDiskRegistrationIfNotApplicable()
        {
            if (IsRetainedHardDiskScenario)
            {
                return;
            }

            if (_isBorrowedHardDisk)
            {
                _isBorrowedHardDisk = false;
                OnPropertyChanged(nameof(IsBorrowedHardDisk));
            }

            if (!string.IsNullOrWhiteSpace(_borrowedHardDiskCode))
            {
                _borrowedHardDiskCode = string.Empty;
                OnPropertyChanged(nameof(BorrowedHardDiskCode));
            }
        }

        public ObservableCollection<MediaItemViewModel> Items { get; } = new();

        public ItemDetailsListPresenter<MediaItemViewModel> ItemDetailsPanel { get; }
    }

    public class ElectronicMediaItemEntryViewModel : ViewModelBase
    {
        private string _entryKind = string.Empty;
        public string EntryKind
        {
            get => _entryKind;
            set => SetProperty(ref _entryKind, value);
        }

        private string _entryName = string.Empty;
        public string EntryName
        {
            get => _entryName;
            set => SetProperty(ref _entryName, value);
        }

        private string _relativePath = string.Empty;
        public string RelativePath
        {
            get => _relativePath;
            set => SetProperty(ref _relativePath, value);
        }

        private decimal? _sizeMb;
        public decimal? SizeMb
        {
            get => _sizeMb;
            set => SetProperty(ref _sizeMb, value);
        }

        private DateTime? _createdAt;
        public DateTime? CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        private DateTime? _modifiedAt;
        public DateTime? ModifiedAt
        {
            get => _modifiedAt;
            set => SetProperty(ref _modifiedAt, value);
        }
    }

    public class MediaItemViewModel : ViewModelBase
    {
        public Action<MediaItemViewModel>? SubCategoryOptionsRefreshHandler { get; set; }

        private string _contentScanSummaryText = "尚未扫描目录/文件明细";
        private int _contentFileCount;

        public MediaItemViewModel()
        {
            ContentEntries.CollectionChanged += ContentEntries_CollectionChanged;
        }

        private void ContentEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshContentScanSummary();
        }

        public List<string> LastScannedFilePaths { get; } = new();

        public List<string> LastScannedDirectoryPaths { get; } = new();

        /// <summary>
        /// 扫描时的完整根目录（仅当前编辑会话内用于重新扫描，不入库）。
        /// </summary>
        internal string StorageRootFullPath { get; set; } = string.Empty;

        private string _itemType = string.Empty;
        public string ItemType { get => _itemType; set => SetProperty(ref _itemType, value); }

        private string _contentDesc = string.Empty;
        public string ContentDesc { get => _contentDesc; set => SetProperty(ref _contentDesc, value); }

        private int _contentCount = 1;
        public int ContentCount
        {
            get => _contentCount;
            set => SetProperty(ref _contentCount, Math.Max(1, value));
        }

        internal void SetAutoContentCount(int count) => ContentCount = count;

        private string _storagePath = string.Empty;
        public string StoragePath { get => _storagePath; set => SetProperty(ref _storagePath, value); }

        private string _note = string.Empty;
        public string Note { get => _note; set => SetProperty(ref _note, value); }

        private string _confidentialLevel = ArchiveRegisterDomainValues.ConfidentialLevelNone;
        public string ConfidentialLevel
        {
            get => _confidentialLevel;
            set => SetProperty(ref _confidentialLevel, value);
        }

        private string _materialCategory = string.Empty;
        public string MaterialCategory
        {
            get => _materialCategory;
            set
            {
                if (SetProperty(ref _materialCategory, value))
                {
                    SubCategory = string.Empty;
                    SubCategoryOptionsRefreshHandler?.Invoke(this);
                }
            }
        }

        private string _subCategory = string.Empty;
        public string SubCategory
        {
            get => _subCategory;
            set => SetProperty(ref _subCategory, value);
        }

        private string _dataOrganizationForm = string.Empty;
        public string DataOrganizationForm
        {
            get => _dataOrganizationForm;
            set
            {
                if (SetProperty(ref _dataOrganizationForm, value))
                {
                    ClearScannedContent();
                    OnPropertyChanged(nameof(ContentEntryKindLabel));
                    OnPropertyChanged(nameof(IsDirectoryOrganizationForm));
                    OnPropertyChanged(nameof(IsFileOrganizationForm));
                    OnPropertyChanged(nameof(ScannedEntryCountDisplay));
                }
            }
        }

        private decimal _dataSizeMb;
        public decimal DataSizeMb
        {
            get => _dataSizeMb;
            set => SetProperty(ref _dataSizeMb, value);
        }

        public ObservableCollection<string> AvailableSubCategories { get; } = new();

        public ObservableCollection<ElectronicMediaItemEntryViewModel> ContentEntries { get; } = new();

        public bool IsDirectoryOrganizationForm =>
            string.Equals(DataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory, StringComparison.Ordinal);

        public bool IsFileOrganizationForm =>
            string.Equals(DataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormFile, StringComparison.Ordinal);

        public bool HasScannedEntries => ContentEntries.Count > 0;

        public int ContentEntryCount => ContentEntries.Count;

        public int ContentFileCount
        {
            get => _contentFileCount;
            private set => SetProperty(ref _contentFileCount, value);
        }

        public string ContentScanSummaryText
        {
            get => _contentScanSummaryText;
            private set => SetProperty(ref _contentScanSummaryText, value);
        }

        public string ContentEntryKindLabel =>
            string.Equals(DataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory, StringComparison.Ordinal)
                ? ArchiveRegisterDomainValues.ElectronicEntryKindDirectory
                : string.Equals(DataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormFile, StringComparison.Ordinal)
                    ? ArchiveRegisterDomainValues.ElectronicEntryKindFile
                    : "目录/文件";

        /// <summary>
        /// 扫描得到的目录或文件数量展示（非资料子项份数）。
        /// </summary>
        public string ScannedEntryCountDisplay
        {
            get
            {
                int count = ContentEntries.Count;
                if (IsDirectoryOrganizationForm)
                {
                    return $"目录个数：{count}";
                }

                if (IsFileOrganizationForm)
                {
                    return $"文件个数：{count}";
                }

                return $"目录个数：{count}";
            }
        }

        public void SyncContentEntryKinds()
        {
            string entryKind = ElectronicMediaItemSupport.ResolveEntryKind(DataOrganizationForm);
            if (string.IsNullOrWhiteSpace(entryKind))
            {
                return;
            }

            foreach (var entry in ContentEntries)
            {
                entry.EntryKind = entryKind;
            }
        }

        public void RefreshContentScanSummary(int? fileCount = null)
        {
            ContentFileCount = fileCount ?? (IsFileOrganizationForm ? ContentEntries.Count : ContentFileCount);
            decimal totalSizeMb = DataSizeMb > 0
                ? DataSizeMb
                : ContentEntries.Sum(entry => entry.SizeMb ?? 0);
            ContentScanSummaryText = ElectronicMediaItemSupport.BuildContentScanSummary(
                DataOrganizationForm,
                ContentEntries.Count,
                ContentFileCount,
                totalSizeMb);
            OnPropertyChanged(nameof(HasScannedEntries));
            OnPropertyChanged(nameof(ContentEntryCount));
            OnPropertyChanged(nameof(ScannedEntryCountDisplay));
        }

        public void ClearScannedContent()
        {
            ContentEntries.Clear();
            LastScannedFilePaths.Clear();
            LastScannedDirectoryPaths.Clear();
            StorageRootFullPath = string.Empty;
            ContentFileCount = 0;
            DataSizeMb = 0;
            RefreshContentScanSummary();
        }
    }
}
