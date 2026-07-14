using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveDetailMediaItem : ViewModelBase
    {
        private bool _isDetailsExpanded;
        private bool _isSearchHighlighted;
        private bool _isFilterSelected;
        private int _filingFactId;

        public ArchiveDetailMediaItem(
            int mediaItemId,
            string itemType,
            string contentDesc,
            string contentCountText,
            string storagePath,
            string confidentialLevel,
            string mediaType,
            string materialCategory,
            string subCategory,
            string dataOrganizationForm,
            string dataSizeText,
            string note,
            IEnumerable<ArchiveDetailElectronicContentEntryItem> contentEntries)
        {
            MediaItemId = mediaItemId;
            ItemType = itemType;
            ContentDesc = contentDesc;
            ContentCountText = contentCountText;
            StoragePath = storagePath;
            ConfidentialLevel = confidentialLevel;
            MediaType = mediaType;
            MaterialCategory = materialCategory;
            SubCategory = subCategory;
            DataOrganizationForm = dataOrganizationForm;
            DataSizeText = dataSizeText;
            Note = note;
            ContentEntries = new ObservableCollection<ArchiveDetailElectronicContentEntryItem>(contentEntries);
        }

        public int MediaItemId { get; }

        public string ItemType { get; }

        public string ContentDesc { get; }

        public string ContentCountText { get; }

        public string StoragePath { get; }

        public string ConfidentialLevel { get; }

        public string MediaType { get; }

        public string MaterialCategory { get; }

        public string SubCategory { get; }

        public string DataOrganizationForm { get; }

        public bool HasElectronicDetail =>
            !string.IsNullOrWhiteSpace(MaterialCategory)
            || !string.IsNullOrWhiteSpace(SubCategory)
            || !string.IsNullOrWhiteSpace(DataOrganizationForm);

        public string DataSizeText { get; }

        public string Note { get; }

        public ObservableCollection<ArchiveDetailElectronicContentEntryItem> ContentEntries { get; }

        public bool HasContentEntries => ContentEntries.Count > 0;

        public int DirectoryCount => ElectronicContentEntryStatsSupport.CountEntryKinds(
            ContentEntries.Select(entry => entry.EntryKind)).DirectoryCount;

        public int FileCount => ElectronicContentEntryStatsSupport.CountEntryKinds(
            ContentEntries.Select(entry => entry.EntryKind)).FileCount;

        public string ContentEntryBreakdownText => ElectronicContentEntryStatsSupport.FormatBreakdown(
            DirectoryCount,
            FileCount,
            ContentEntries.Count);

        public string ContentEntryCountText => ContentEntryBreakdownText;

        public int FilingFactId
        {
            get => _filingFactId;
            set
            {
                if (SetProperty(ref _filingFactId, value))
                {
                    OnPropertyChanged(nameof(CanFilterSelect));
                }
            }
        }

        public bool CanFilterSelect => FilingFactId > 0;

        public bool IsFilterSelected
        {
            get => _isFilterSelected;
            set => SetProperty(ref _isFilterSelected, value);
        }

        public bool IsDetailsExpanded
        {
            get => _isDetailsExpanded;
            set => SetProperty(ref _isDetailsExpanded, value);
        }

        public bool IsSearchHighlighted
        {
            get => _isSearchHighlighted;
            set => SetProperty(ref _isSearchHighlighted, value);
        }
    }

    public sealed class ArchiveDetailElectronicContentEntryItem : ViewModelBase
    {
        private bool _isSearchHighlighted;
        private bool _isFilterSelected;

        public ArchiveDetailElectronicContentEntryItem(
            int entryId,
            string entryKind,
            string entryName,
            string relativePath,
            string createdDateText,
            string modifiedDateText,
            string sizeText)
        {
            EntryId = entryId;
            EntryKind = entryKind;
            EntryName = entryName;
            RelativePath = relativePath;
            CreatedDateText = createdDateText;
            ModifiedDateText = modifiedDateText;
            SizeText = sizeText;
        }

        public int EntryId { get; }

        public string EntryKind { get; }

        public string EntryName { get; }

        public string RelativePath { get; }

        public string CreatedDateText { get; }

        public string ModifiedDateText { get; }

        public string SizeText { get; }

        public string EntryDisplayName =>
            ElectronicContentEntryDisplaySupport.FormatEntryDisplayName(EntryName, RelativePath);

        public bool IsFilterSelected
        {
            get => _isFilterSelected;
            set => SetProperty(ref _isFilterSelected, value);
        }

        public bool IsSearchHighlighted
        {
            get => _isSearchHighlighted;
            set => SetProperty(ref _isSearchHighlighted, value);
        }
    }

    public sealed class ArchiveDetailMediaEntryItem : ViewModelBase
    {
        private bool _isSearchHighlighted;

        public ArchiveDetailMediaEntryItem(
            int registerMediaId,
            string mediaKind,
            string mediaType,
            string mediaCountText,
            string storagePath,
            string disposition,
            IReadOnlyList<ArchiveDetailMediaItem> items)
        {
            RegisterMediaId = registerMediaId;
            MediaKind = mediaKind;
            MediaType = mediaType;
            MediaCountText = mediaCountText;
            StoragePath = storagePath;
            Disposition = disposition;
            Items = items;
            ItemDetailsPanel = new ItemDetailsListPresenter<ArchiveDetailMediaItem>(
                "资料子项",
                summaryBuilder: mediaItems => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    mediaItems,
                    item => item.ContentDesc,
                    "暂无资料子项"));
            ItemDetailsPanel.RefreshItems(Items);
        }

        public int RegisterMediaId { get; }

        public string MediaKind { get; }

        public string MediaType { get; }

        public string MediaCountText { get; }

        public string StoragePath { get; }

        public string Disposition { get; }

        public IReadOnlyList<ArchiveDetailMediaItem> Items { get; }

        public ItemDetailsListPresenter<ArchiveDetailMediaItem> ItemDetailsPanel { get; }

        public bool HasItems => Items.Count > 0;

        public bool IsSearchHighlighted
        {
            get => _isSearchHighlighted;
            set => SetProperty(ref _isSearchHighlighted, value);
        }
    }

    public sealed class ArchiveDetailArchiveBoxResult : ViewModelBase
    {
        private bool _isSearchHighlighted;

        public ArchiveDetailArchiveBoxResult(
            string archiveSequenceNo,
            string boxLocationCode,
            string specifications,
            string placementMode,
            string archivedBy,
            string archivedDateText,
            string remarks,
            IReadOnlyList<ArchiveDetailMediaItem> items)
        {
            ArchiveSequenceNo = archiveSequenceNo;
            BoxLocationCode = boxLocationCode;
            Specifications = specifications;
            PlacementMode = placementMode;
            ArchivedBy = archivedBy;
            ArchivedDateText = archivedDateText;
            Remarks = remarks;
            Items = items;
            ItemDetailsPanel = new ItemDetailsListPresenter<ArchiveDetailMediaItem>(
                "关联资料子项",
                summaryBuilder: mediaItems => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    mediaItems,
                    item => item.ContentDesc,
                    "暂无关联资料子项"));
            ItemDetailsPanel.RefreshItems(Items);
        }

        public string ArchiveSequenceNo { get; }

        public string BoxLocationCode { get; }

        public string Specifications { get; }

        public string PlacementMode { get; }

        public string ArchivedBy { get; }

        public string ArchivedDateText { get; }

        public string Remarks { get; }

        public IReadOnlyList<ArchiveDetailMediaItem> Items { get; }

        public ItemDetailsListPresenter<ArchiveDetailMediaItem> ItemDetailsPanel { get; }

        public bool HasItems => Items.Count > 0;

        public bool IsSearchHighlighted
        {
            get => _isSearchHighlighted;
            set => SetProperty(ref _isSearchHighlighted, value);
        }
    }

    public sealed class ArchiveDetailElectronicUnitFilingItem : ViewModelBase
    {
        private bool _isSearchHighlighted;
        private bool _isFilterSelected;
        private bool _isDetailsExpanded;
        private int _filingFactId;

        public ArchiveDetailElectronicUnitFilingItem(
            int mediaItemId,
            string formNo,
            string yearText,
            string projectName,
            string materialName,
            string itemName,
            string itemType,
            string confidentialLevel,
            string materialCategory,
            string subCategory,
            string dataOrganizationForm,
            string registerMediaType,
            string dataSizeText,
            string filingStoragePath,
            IEnumerable<ArchiveDetailElectronicContentEntryItem> contentEntries)
        {
            MediaItemId = mediaItemId;
            FormNo = formNo;
            YearText = yearText;
            ProjectName = projectName;
            MaterialName = materialName;
            ItemName = itemName;
            ItemType = itemType;
            ConfidentialLevel = confidentialLevel;
            MaterialCategory = materialCategory;
            SubCategory = subCategory;
            DataOrganizationForm = dataOrganizationForm;
            RegisterMediaType = registerMediaType;
            DataSizeText = dataSizeText;
            FilingStoragePath = filingStoragePath;
            ContentEntries = new ObservableCollection<ArchiveDetailElectronicContentEntryItem>(contentEntries);
        }

        public int MediaItemId { get; }

        public string FormNo { get; }

        public string YearText { get; }

        public string ProjectName { get; }

        public string MaterialName { get; }

        public string ItemName { get; }

        public string ItemType { get; }

        public string ConfidentialLevel { get; }

        public string MaterialCategory { get; }

        public string SubCategory { get; }

        public string DataOrganizationForm { get; }

        public string RegisterMediaType { get; }

        public bool HasElectronicDetail =>
            !string.IsNullOrWhiteSpace(MaterialCategory)
            || !string.IsNullOrWhiteSpace(SubCategory)
            || !string.IsNullOrWhiteSpace(DataOrganizationForm);

        public string DataSizeText { get; }

        public string FilingStoragePath { get; }

        public ObservableCollection<ArchiveDetailElectronicContentEntryItem> ContentEntries { get; }

        public bool HasContentEntries => ContentEntries.Count > 0;

        public int DirectoryCount => ElectronicContentEntryStatsSupport.CountEntryKinds(
            ContentEntries.Select(entry => entry.EntryKind)).DirectoryCount;

        public int FileCount => ElectronicContentEntryStatsSupport.CountEntryKinds(
            ContentEntries.Select(entry => entry.EntryKind)).FileCount;

        public string ContentEntryBreakdownText => ElectronicContentEntryStatsSupport.FormatBreakdown(
            DirectoryCount,
            FileCount,
            ContentEntries.Count);

        public string SummaryText =>
            $"年度 {YearText}；项目 {ProjectName}；资料名称 {MaterialName}；子项名称 {ItemName}；密级 {ConfidentialLevel}；来源介质 {RegisterMediaType}；资料类型 {MaterialCategory}；所属子类 {SubCategory}；组织形式 {DataOrganizationForm}；{ContentEntryBreakdownText}；数据量 {DataSizeText}；立档路径： {FilingStoragePath}";

        public int FilingFactId
        {
            get => _filingFactId;
            set
            {
                if (SetProperty(ref _filingFactId, value))
                {
                    OnPropertyChanged(nameof(CanFilterSelect));
                }
            }
        }

        public bool CanFilterSelect => FilingFactId > 0;

        public bool IsFilterSelected
        {
            get => _isFilterSelected;
            set => SetProperty(ref _isFilterSelected, value);
        }

        public bool IsDetailsExpanded
        {
            get => _isDetailsExpanded;
            set => SetProperty(ref _isDetailsExpanded, value);
        }

        public bool IsSearchHighlighted
        {
            get => _isSearchHighlighted;
            set => SetProperty(ref _isSearchHighlighted, value);
        }
    }

    public sealed class ArchiveDetailElectronicUnitResult : ViewModelBase
    {
        private bool _isSearchHighlighted;

        public ArchiveDetailElectronicUnitResult(
            string electronicArchiveNo,
            string storageLocation,
            string carrierType,
            string linkedMediumCodes,
            string disposition,
            string mediaCountText,
            string contentSummary,
            string archivedBy,
            string archivedDateText,
            string remarks,
            IReadOnlyList<ArchiveDetailElectronicUnitFilingItem> items)
        {
            ElectronicArchiveNo = electronicArchiveNo;
            StorageLocation = storageLocation;
            CarrierType = carrierType;
            LinkedMediumCodes = linkedMediumCodes;
            Disposition = disposition;
            MediaCountText = mediaCountText;
            ContentSummary = contentSummary;
            ArchivedBy = archivedBy;
            ArchivedDateText = archivedDateText;
            Remarks = remarks;
            Items = items;
            ItemDetailsPanel = new ItemDetailsListPresenter<ArchiveDetailElectronicUnitFilingItem>(
                "关联立档明细",
                summaryBuilder: filingItems => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    filingItems,
                    item => item.ItemName,
                    "暂无关联立档明细"));
            ItemDetailsPanel.RefreshItems(Items);
        }

        public string ElectronicArchiveNo { get; }

        public string StorageLocation { get; }

        public string CarrierType { get; }

        public string LinkedMediumCodes { get; }

        public string Disposition { get; }

        public string MediaCountText { get; }

        public string ContentSummary { get; }

        public string ArchivedBy { get; }

        public string ArchivedDateText { get; }

        public string Remarks { get; }

        public IReadOnlyList<ArchiveDetailElectronicUnitFilingItem> Items { get; }

        public ItemDetailsListPresenter<ArchiveDetailElectronicUnitFilingItem> ItemDetailsPanel { get; }

        public bool HasItems => Items.Count > 0;

        public bool IsSearchHighlighted
        {
            get => _isSearchHighlighted;
            set => SetProperty(ref _isSearchHighlighted, value);
        }
    }
}
