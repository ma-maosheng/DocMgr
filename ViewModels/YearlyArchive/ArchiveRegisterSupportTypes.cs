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
                    OnPropertyChanged(nameof(BorrowedHardDiskRegistrationRowVisibility));
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
                    OnPropertyChanged(nameof(IsExternalOfflineReturnedHardDiskScenario));
                    OnPropertyChanged(nameof(BorrowedHardDiskRegistrationRowVisibility));
                    OnPropertyChanged(nameof(BorrowedHardDiskDetailsVisibility));
                    NotifyOutboundHardDiskRequisitionPropertiesChanged();
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
                    OnPropertyChanged(nameof(IsExternalOfflineReturnedHardDiskScenario));
                    OnPropertyChanged(nameof(BorrowedHardDiskRegistrationRowVisibility));
                    OnPropertyChanged(nameof(BorrowedHardDiskDetailsVisibility));
                    NotifyOutboundHardDiskRequisitionPropertiesChanged();
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

        public bool IsExternalOfflineReturnedHardDiskScenario =>
            string.Equals(MediaKind?.Trim(), ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)
            && string.Equals(MediaType?.Trim(), ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Disposition?.Trim(), ArchiveRegisterDomainValues.ElectronicDispositionReturn, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 是否允许展示「借出硬盘」交互（出网资料室立档场景关闭，由后续立档负责）。
        /// </summary>
        private bool _showBorrowedHardDiskRegistrationUi = true;

        public bool ShowBorrowedHardDiskRegistrationUi
        {
            get => _showBorrowedHardDiskRegistrationUi;
            private set
            {
                if (SetProperty(ref _showBorrowedHardDiskRegistrationUi, value))
                {
                    OnPropertyChanged(nameof(BorrowedHardDiskRegistrationRowVisibility));
                    if (!value && IsBorrowedHardDisk)
                    {
                        IsBorrowedHardDisk = false;
                    }
                }
            }
        }

        /// <summary>硬盘·介质留存且允许录入借出硬盘时显示「借出硬盘是/否」行。</summary>
        public bool BorrowedHardDiskRegistrationRowVisibility =>
            IsRetainedHardDiskScenario && ShowBorrowedHardDiskRegistrationUi;

        public bool BorrowedHardDiskDetailsVisibility =>
            BorrowedHardDiskRegistrationRowVisibility && IsBorrowedHardDisk;

        internal void SetShowBorrowedHardDiskRegistrationUi(bool show) =>
            ShowBorrowedHardDiskRegistrationUi = show;

        public bool OutboundHardDiskRequisitionVisibility => IsExternalOfflineReturnedHardDiskScenario;

        public bool OutboundBlankHardDiskFieldsVisibility =>
            IsExternalOfflineReturnedHardDiskScenario && UseInStockBlankHardDisk;

        public bool OutboundRequisitionedDiskNeedReturnVisibility =>
            OutboundBlankHardDiskFieldsVisibility && RequisitionedMediumId is > 0;

        public bool RequiresExpectedReturnDate =>
            OutboundRequisitionedDiskNeedReturnVisibility && RequisitionedDiskNeedReturn;

        private readonly string _useInStockBlankHardDiskRadioGroup = Guid.NewGuid().ToString("N");
        public string UseInStockBlankHardDiskRadioGroup => _useInStockBlankHardDiskRadioGroup;

        private readonly string _requisitionedDiskNeedReturnRadioGroup = Guid.NewGuid().ToString("N");
        public string RequisitionedDiskNeedReturnRadioGroup => _requisitionedDiskNeedReturnRadioGroup;

        private bool _useInStockBlankHardDisk;
        public bool UseInStockBlankHardDisk
        {
            get => _useInStockBlankHardDisk;
            set
            {
                if (SetProperty(ref _useInStockBlankHardDisk, value))
                {
                    if (!value)
                    {
                        ClearOutboundRequisitionedDiskState();
                    }
                    else
                    {
                        RequisitionedDiskNeedReturn = true;
                    }

                    NotifyOutboundHardDiskRequisitionPropertiesChanged();
                }
            }
        }

        private int? _requisitionedMediumId;
        public int? RequisitionedMediumId
        {
            get => _requisitionedMediumId;
            set
            {
                if (SetProperty(ref _requisitionedMediumId, value))
                {
                    NotifyOutboundHardDiskRequisitionPropertiesChanged();
                }
            }
        }

        private string _requisitionedHardDiskCode = string.Empty;
        public string RequisitionedHardDiskCode
        {
            get => _requisitionedHardDiskCode;
            set => SetProperty(ref _requisitionedHardDiskCode, value);
        }

        private bool _requisitionedDiskNeedReturn = true;
        public bool RequisitionedDiskNeedReturn
        {
            get => _requisitionedDiskNeedReturn;
            set
            {
                if (SetProperty(ref _requisitionedDiskNeedReturn, value))
                {
                    if (!value)
                    {
                        ExpectedReturnDate = null;
                    }

                    NotifyOutboundHardDiskRequisitionPropertiesChanged();
                }
            }
        }

        private DateTime? _expectedReturnDate;
        public DateTime? ExpectedReturnDate
        {
            get => _expectedReturnDate;
            set => SetProperty(ref _expectedReturnDate, value?.Date);
        }

        internal void ApplyOutboundRequisitionedDisk(int mediumId, string diskCode)
        {
            RequisitionedMediumId = mediumId;
            RequisitionedHardDiskCode = diskCode?.Trim() ?? string.Empty;
            RequisitionedDiskNeedReturn = true;
            NotifyOutboundHardDiskRequisitionPropertiesChanged();
        }

        internal void ClearOutboundRequisitionedDiskState()
        {
            RequisitionedMediumId = null;
            RequisitionedHardDiskCode = string.Empty;
            RequisitionedDiskNeedReturn = false;
            ExpectedReturnDate = null;
            NotifyOutboundHardDiskRequisitionPropertiesChanged();
        }

        internal void NotifyOutboundHardDiskRequisitionPropertiesChanged()
        {
            OnPropertyChanged(nameof(OutboundHardDiskRequisitionVisibility));
            OnPropertyChanged(nameof(OutboundBlankHardDiskFieldsVisibility));
            OnPropertyChanged(nameof(OutboundRequisitionedDiskNeedReturnVisibility));
            OnPropertyChanged(nameof(RequiresExpectedReturnDate));
        }

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

            if (!IsExternalOfflineReturnedHardDiskScenario)
            {
                if (_useInStockBlankHardDisk)
                {
                    _useInStockBlankHardDisk = false;
                    OnPropertyChanged(nameof(UseInStockBlankHardDisk));
                }

                ClearOutboundRequisitionedDiskState();
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

        public Action<MediaItemViewModel>? StoragePathRefreshHandler { get; set; }

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
        public string ContentDesc
        {
            get => _contentDesc;
            set
            {
                if (SetProperty(ref _contentDesc, value))
                {
                    StoragePathRefreshHandler?.Invoke(this);
                }
            }
        }

        private int _contentCount = 1;
        public int ContentCount
        {
            get => _contentCount;
            set => SetProperty(ref _contentCount, Math.Max(1, value));
        }

        internal void SetAutoContentCount(int count) => ContentCount = count;

        private string _storagePath = string.Empty;
        private bool _suppressOutboundStoragePathUserEdit;
        private string _storagePathLabel = "存储目录：";
        private bool _isStoragePathEditable = true;
        private bool _showOutboundStoragePathHint;
        private string _outboundServerFullPathHint = string.Empty;

        public string StoragePath
        {
            get => _storagePath;
            set
            {
                if (SetProperty(ref _storagePath, value) && !_suppressOutboundStoragePathUserEdit)
                {
                    HasCustomizedOutboundStoragePath = true;
                }
            }
        }

        public string StoragePathLabel
        {
            get => _storagePathLabel;
            set => SetProperty(ref _storagePathLabel, value);
        }

        public bool IsStoragePathEditable
        {
            get => _isStoragePathEditable;
            set => SetProperty(ref _isStoragePathEditable, value);
        }

        public bool ShowOutboundStoragePathHint
        {
            get => _showOutboundStoragePathHint;
            set => SetProperty(ref _showOutboundStoragePathHint, value);
        }

        public string OutboundServerFullPathHint
        {
            get => _outboundServerFullPathHint;
            set => SetProperty(ref _outboundServerFullPathHint, value);
        }

        internal bool HasCustomizedOutboundStoragePath { get; set; }

        internal void SetStoragePathFromSystem(string path)
        {
            _suppressOutboundStoragePathUserEdit = true;
            try
            {
                StoragePath = path ?? string.Empty;
            }
            finally
            {
                _suppressOutboundStoragePathUserEdit = false;
            }
        }

        private string _note = string.Empty;
        public string Note { get => _note; set => SetProperty(ref _note, value); }

        private string _confidentialLevel = ArchiveRegisterDomainValues.ConfidentialLevelNone;
        public string ConfidentialLevel
        {
            get => _confidentialLevel;
            set => SetProperty(ref _confidentialLevel, value);
        }

        private bool _suppressElectronicDetailSideEffects;
        private bool _treatContentMetricsAsUnknown;

        private string _materialCategory = string.Empty;
        public string MaterialCategory
        {
            get => _materialCategory;
            set
            {
                if (SetProperty(ref _materialCategory, value) && !_suppressElectronicDetailSideEffects)
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
                if (SetProperty(ref _dataOrganizationForm, value) && !_suppressElectronicDetailSideEffects)
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
            set
            {
                if (SetProperty(ref _dataSizeMb, value))
                {
                    OnPropertyChanged(nameof(DataSizeMbDisplay));
                }
            }
        }

        /// <summary>
        /// 从已持久化的电子扩展信息回填，避免资料类型/组织形式 setter 清空子类与扫描明细。
        /// </summary>
        internal void LoadElectronicDetail(
            string? materialCategory,
            string? subCategory,
            string? dataOrganizationForm,
            decimal dataSizeMb)
        {
            _suppressElectronicDetailSideEffects = true;
            try
            {
                MaterialCategory = materialCategory?.Trim() ?? string.Empty;
                SubCategory = subCategory?.Trim() ?? string.Empty;
                DataOrganizationForm = dataOrganizationForm?.Trim() ?? string.Empty;
                DataSizeMb = dataSizeMb;
            }
            finally
            {
                _suppressElectronicDetailSideEffects = false;
            }

            OnPropertyChanged(nameof(ContentEntryKindLabel));
            OnPropertyChanged(nameof(IsDirectoryOrganizationForm));
            OnPropertyChanged(nameof(IsFileOrganizationForm));
            OnPropertyChanged(nameof(ScannedEntryCountDisplay));
            SubCategoryOptionsRefreshHandler?.Invoke(this);
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
        /// <summary>
        /// 申请阶段无法扫描时，数据量与目录/文件个数按未知展示。
        /// </summary>
        public bool TreatContentMetricsAsUnknown
        {
            get => _treatContentMetricsAsUnknown;
            set
            {
                if (SetProperty(ref _treatContentMetricsAsUnknown, value))
                {
                    OnPropertyChanged(nameof(ScannedEntryCountDisplay));
                    OnPropertyChanged(nameof(DataSizeMbDisplay));
                    RefreshContentScanSummary();
                }
            }
        }

        /// <summary>数据量展示：未知或具体 MB。</summary>
        public string DataSizeMbDisplay =>
            TreatContentMetricsAsUnknown && DataSizeMb <= 0
                ? "未知"
                : DataSizeMb.ToString("0.##");

        public string ScannedEntryCountDisplay
        {
            get
            {
                if (TreatContentMetricsAsUnknown && ContentEntries.Count == 0)
                {
                    return IsFileOrganizationForm ? "文件个数：未知" : "目录个数：未知";
                }

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
            if (TreatContentMetricsAsUnknown && ContentEntries.Count == 0)
            {
                ContentScanSummaryText = "申请阶段尚不能读取具体目录或文件，数据量与文件个数均为未知。";
                OnPropertyChanged(nameof(HasScannedEntries));
                OnPropertyChanged(nameof(ContentEntryCount));
                OnPropertyChanged(nameof(ScannedEntryCountDisplay));
                OnPropertyChanged(nameof(DataSizeMbDisplay));
                return;
            }

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
