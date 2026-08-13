using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子介质登记编辑 ViewModel，供资料登记与入网申请等场景复用。
    /// </summary>
    public class ElectronicMediaEditingViewModel : ViewModelBase
    {
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IDialogService _dialogService;
        private readonly IElectronicMediaContentScanService _electronicMediaContentScanService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IUserContextService _userContextService;

        private readonly List<string> _allElectronicDispositionOptions = new();
        private readonly List<string> _electronicDocumentSubCategoryOptions = new();
        private readonly List<string> _electronicDataSubCategoryOptions = new();
        private readonly List<string> _electronicSoftwareSubCategoryOptions = new();
        private readonly Dictionary<MediaItemViewModel, NotifyCollectionChangedEventHandler> _contentEntryQuantityHandlers = new();

        private bool _isInitialized;
        private bool _isSyncingElectronicMediaSettings;
        private bool _borrowedHardDiskCodesRefreshInProgress;

        private bool _canEditForm;
        private bool _canEditItemConfidentialLevel;
        private bool _lockElectronicMediaTypeAndDisposition;
        private bool _restrictRetainedHardDiskToBorrowedOnly;
        private string _sectionHeader = "资料介质（电子）";

        private string _selectedElectronicMediaType = string.Empty;
        private string _selectedElectronicDisposition = string.Empty;

        public ElectronicMediaEditingViewModel(
            IArchiveRegisterService archiveRegisterService,
            IDialogService dialogService,
            IElectronicMediaContentScanService electronicMediaContentScanService,
            IHardDiskMediaService hardDiskMediaService,
            IUserContextService userContextService)
        {
            _archiveRegisterService = archiveRegisterService ?? throw new ArgumentNullException(nameof(archiveRegisterService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _electronicMediaContentScanService = electronicMediaContentScanService ?? throw new ArgumentNullException(nameof(electronicMediaContentScanService));
            _hardDiskMediaService = hardDiskMediaService ?? throw new ArgumentNullException(nameof(hardDiskMediaService));
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));

            DataElectronicMediaView = new ListCollectionView(MediaEntries)
            {
                Filter = m => RegisterMediaTreeSupport.IsDataElectronic(m as MediaEntryViewModel)
            };
            MediaEntries.CollectionChanged += MediaEntries_CollectionChanged;

            AddDataElectronicMediaEntryCommand = new RelayCommand(_ => AddDataElectronicMediaEntry(), _ => CanEditForm);
            AddMediaItemCommand = new RelayCommand<MediaEntryViewModel>(AddMediaItem, _ => CanEditForm);
            RemoveMediaEntryCommand = new RelayCommand<MediaEntryViewModel>(RemoveMediaEntry, _ => CanEditForm);
            RemoveMediaItemCommand = new RelayCommand<MediaItemViewModel>(RemoveMediaItem, _ => CanEditForm);
            PickFolderAndScanElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                async item => await PickFolderAndScanElectronicContentAsync(item),
                item => CanEditForm && item != null && item.IsDirectoryOrganizationForm);
            PickFilesAndScanElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                async item => await PickFilesAndScanElectronicContentAsync(item),
                item => CanEditForm && item != null && item.IsFileOrganizationForm);
            RescanElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                async item => await RescanElectronicContentAsync(item),
                item => CanEditForm && item != null && CanRescanElectronicContent(item));
            ClearElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                item => ClearElectronicContent(item),
                item => CanEditForm && item != null && item.HasScannedEntries);
            ViewElectronicContentEntriesCommand = new RelayCommand<MediaItemViewModel>(
                item => ViewElectronicContentEntries(item),
                item => item != null && item.HasScannedEntries && CanViewElectronicContentEntries());
        }

        public ObservableCollection<MediaEntryViewModel> MediaEntries { get; } = new();

        public ListCollectionView DataElectronicMediaView { get; }

        public ObservableCollection<string> DataElectronicMediaTypeOptions { get; } = new();

        public ObservableCollection<string> DataElectronicDispositionOptions { get; } = new();

        public ObservableCollection<string> ElectronicMaterialCategoryOptions { get; } = new();

        public ObservableCollection<string> ElectronicDataOrganizationFormOptions { get; } = new();

        public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();

        public ObservableCollection<string> UserBorrowedHardDiskCodes { get; } = new();

        public string SelectedElectronicMediaType
        {
            get => _selectedElectronicMediaType;
            set
            {
                if (!_isSyncingElectronicMediaSettings
                    && LockElectronicMediaTypeAndDisposition
                    && !string.IsNullOrWhiteSpace(_selectedElectronicMediaType)
                    && !string.Equals(value, _selectedElectronicMediaType, StringComparison.Ordinal))
                {
                    return;
                }

                if (SetProperty(ref _selectedElectronicMediaType, value))
                {
                    RefreshElectronicDispositionOptions();
                    ApplySelectedElectronicMediaSettingsToEntries();
                }
            }
        }

        public string SelectedElectronicDisposition
        {
            get => _selectedElectronicDisposition;
            set
            {
                if (!_isSyncingElectronicMediaSettings
                    && LockElectronicMediaTypeAndDisposition
                    && !string.IsNullOrWhiteSpace(_selectedElectronicDisposition)
                    && !string.Equals(value, _selectedElectronicDisposition, StringComparison.Ordinal))
                {
                    return;
                }

                if (SetProperty(ref _selectedElectronicDisposition, value))
                {
                    ApplySelectedElectronicMediaSettingsToEntries();
                }
            }
        }

        public int DataElectronicMediaCount => MediaEntries.Count(RegisterMediaTreeSupport.IsDataElectronic);

        public bool IsBorrowedHardDiskRegistrationVisible =>
            string.Equals(SelectedElectronicMediaType?.Trim(), ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SelectedElectronicDisposition?.Trim(), ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase);

        public bool IsBorrowedHardDiskCodeRequired =>
            IsBorrowedHardDiskRegistrationVisible
            && (RestrictRetainedHardDiskToBorrowedOnly
                || MediaEntries.Any(m =>
                    RegisterMediaTreeSupport.IsDataElectronic(m) && m.IsRetainedHardDiskScenario && m.IsBorrowedHardDisk));

        public bool CanEditForm
        {
            get => _canEditForm;
            set
            {
                if (SetProperty(ref _canEditForm, value))
                {
                    OnPropertyChanged(nameof(IsElectronicMediaTypeEditable));
                    OnPropertyChanged(nameof(IsElectronicDispositionEditable));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>审批态允许资料室仅补录资料子项密级（对齐 YA-REG-Ed）。</summary>
        public bool CanEditItemConfidentialLevel
        {
            get => _canEditItemConfidentialLevel;
            set
            {
                if (SetProperty(ref _canEditItemConfidentialLevel, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// 锁定电子介质类型与处置（如出网转入建档），与资料登记出网场景一致。
        /// </summary>
        public bool LockElectronicMediaTypeAndDisposition
        {
            get => _lockElectronicMediaTypeAndDisposition;
            set
            {
                if (SetProperty(ref _lockElectronicMediaTypeAndDisposition, value))
                {
                    OnPropertyChanged(nameof(IsElectronicMediaTypeEditable));
                    OnPropertyChanged(nameof(IsElectronicDispositionEditable));
                }
            }
        }

        /// <summary>
        /// 可选：覆盖默认处置方式解析（如入网档外「光盘→介质带回」）。
        /// </summary>
        public Func<string?, IReadOnlyCollection<string>, IReadOnlyList<string>>? AllowedDispositionOptionsResolver { get; set; }

        /// <summary>
        /// 硬盘·介质留存时仅允许资料室借出硬盘，且必须填写借出硬盘编号。
        /// </summary>
        public bool RestrictRetainedHardDiskToBorrowedOnly
        {
            get => _restrictRetainedHardDiskToBorrowedOnly;
            set
            {
                if (SetProperty(ref _restrictRetainedHardDiskToBorrowedOnly, value))
                {
                    EnforceRetainedHardDiskBorrowedRegistration();
                    OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
                }
            }
        }

        /// <summary>卡片标题（入网场景为「数据来源（档外/档内）」）。</summary>
        public string SectionHeader
        {
            get => _sectionHeader;
            set => SetProperty(ref _sectionHeader, value);
        }

        public bool IsElectronicMediaTypeEditable => CanEditForm && !LockElectronicMediaTypeAndDisposition;

        public bool IsElectronicDispositionEditable => CanEditForm && !LockElectronicMediaTypeAndDisposition;

        public RelayCommand AddDataElectronicMediaEntryCommand { get; }

        public RelayCommand<MediaEntryViewModel> AddMediaItemCommand { get; }

        public RelayCommand<MediaEntryViewModel> RemoveMediaEntryCommand { get; }

        public RelayCommand<MediaItemViewModel> RemoveMediaItemCommand { get; }

        public RelayCommand<MediaItemViewModel> PickFolderAndScanElectronicContentCommand { get; }

        public RelayCommand<MediaItemViewModel> PickFilesAndScanElectronicContentCommand { get; }

        public RelayCommand<MediaItemViewModel> RescanElectronicContentCommand { get; }

        public RelayCommand<MediaItemViewModel> ClearElectronicContentCommand { get; }

        public RelayCommand<MediaItemViewModel> ViewElectronicContentEntriesCommand { get; }

        /// <summary>
        /// 加载域值选项并刷新借出硬盘列表。
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadDomainOptionsAsync();
            await RefreshUserBorrowedHardDiskCodesAsync();
            _isInitialized = true;
        }

        /// <summary>
        /// 仅加载电子介质相关域值选项。
        /// </summary>
        public async Task LoadDomainOptionsAsync()
        {
            var domainOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();

            ApplyOptions(DataElectronicMediaTypeOptions,
                ArchiveRegisterBusinessRules.FilterManualSelectableElectronicMediaTypes(domainOptions.DataElectronicMediaTypes));
            ApplyOptions(DataElectronicDispositionOptions, domainOptions.DataElectronicDispositions);
            _allElectronicDispositionOptions.Clear();
            _allElectronicDispositionOptions.AddRange(domainOptions.DataElectronicDispositions);
            ApplyOptions(ElectronicMaterialCategoryOptions, domainOptions.ElectronicMaterialCategories);
            ApplyOptions(ElectronicDataOrganizationFormOptions, domainOptions.ElectronicDataOrganizationForms);
            _electronicDocumentSubCategoryOptions.Clear();
            _electronicDocumentSubCategoryOptions.AddRange(domainOptions.ElectronicDocumentSubCategories);
            _electronicDataSubCategoryOptions.Clear();
            _electronicDataSubCategoryOptions.AddRange(domainOptions.ElectronicDataSubCategories);
            _electronicSoftwareSubCategoryOptions.Clear();
            _electronicSoftwareSubCategoryOptions.AddRange(domainOptions.ElectronicSoftwareSubCategories);
            ApplyOptions(ConfidentialLevelOptions, domainOptions.ConfidentialLevels);

            RefreshElectronicDispositionOptions();
            EnsureElectronicMediaSelections();
            foreach (var item in MediaEntries.SelectMany(entry => entry.Items))
            {
                RefreshElectronicSubCategoryOptions(item);
            }

            SyncElectronicMediaSettingsFromEntries();
        }

        /// <summary>
        /// 从登记介质实体同步电子介质明细到界面。
        /// </summary>
        public void SyncFromEntities(IReadOnlyList<YearlyArchiveRegisterMedia> mediaEntries)
        {
            MediaEntries.Clear();
            if (mediaEntries != null)
            {
                foreach (var media in mediaEntries.Where(RegisterMediaTreeSupport.IsElectronicMediaEntity))
                {
                    MediaEntries.Add(CreateMediaEntryViewModel(media));
                }
            }

            SyncElectronicMediaSettingsFromEntries();
            RecalculateAllQuantities();
            EnsureUserBorrowedHardDiskListIncludesSelected();
            EnforceRetainedHardDiskBorrowedRegistration();
        }

        /// <summary>
        /// 将界面电子介质明细构建为登记介质实体列表。
        /// </summary>
        public List<YearlyArchiveRegisterMedia> BuildEntities()
        {
            EnsureElectronicMediaSelections();
            RecalculateAllQuantities();
            return RegisterMediaTreeSupport.BuildElectronicMediaEntities(
                MediaEntries,
                ResolveSelectedElectronicMediaType,
                ResolveSelectedElectronicDisposition);
        }

        /// <summary>
        /// 重新计算全部介质组与子项份数。
        /// </summary>
        public void RecalculateAllQuantities()
        {
            RecalculateQuantities();
        }

        private void MediaEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (MediaEntryViewModel media in e.NewItems)
                {
                    AttachMediaEntry(media);
                }
            }

            if (e.OldItems != null)
            {
                foreach (MediaEntryViewModel media in e.OldItems)
                {
                    DetachMediaEntry(media);
                }
            }

            ScheduleAfterMediaEntriesChanged();
        }

        private void ScheduleAfterMediaEntriesChanged()
        {
            void Work()
            {
                RefreshMediaViews();
                SyncElectronicMediaSettingsFromEntries();
                OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
                OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
            }

            if (Application.Current?.Dispatcher is { } dispatcher
                && !dispatcher.HasShutdownStarted
                && !dispatcher.HasShutdownFinished)
            {
                dispatcher.BeginInvoke(Work, DispatcherPriority.Background);
            }
            else
            {
                Work();
            }
        }

        private void AttachMediaEntry(MediaEntryViewModel media)
        {
            media.PropertyChanged += MediaEntry_PropertyChanged;
            media.Items.CollectionChanged += MediaItems_CollectionChanged;
            foreach (var item in media.Items)
            {
                ConfigureElectronicMediaItem(item);
                item.PropertyChanged += MediaItem_PropertyChanged;
            }

            RecalculateQuantities(media);
        }

        private void DetachMediaEntry(MediaEntryViewModel media)
        {
            media.PropertyChanged -= MediaEntry_PropertyChanged;
            media.Items.CollectionChanged -= MediaItems_CollectionChanged;
            foreach (var item in media.Items)
            {
                item.PropertyChanged -= MediaItem_PropertyChanged;
                DetachContentEntryQuantityHandler(item);
            }
        }

        private void MediaEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MediaEntryViewModel.MediaKind)
                || e.PropertyName == nameof(MediaEntryViewModel.MediaType)
                || e.PropertyName == nameof(MediaEntryViewModel.Disposition)
                || e.PropertyName == nameof(MediaEntryViewModel.MediaCount)
                || e.PropertyName == nameof(MediaEntryViewModel.IsBorrowedHardDisk)
                || e.PropertyName == nameof(MediaEntryViewModel.BorrowedHardDiskCode))
            {
                if (RestrictRetainedHardDiskToBorrowedOnly
                    && e.PropertyName is nameof(MediaEntryViewModel.MediaType) or nameof(MediaEntryViewModel.Disposition)
                    && sender is MediaEntryViewModel retainedMedia
                    && retainedMedia.IsRetainedHardDiskScenario
                    && !retainedMedia.IsBorrowedHardDisk)
                {
                    retainedMedia.IsBorrowedHardDisk = true;
                }

                RefreshMediaViews();
                SyncElectronicMediaSettingsFromEntries();
                OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
            }
        }

        private void MediaItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (MediaItemViewModel item in e.NewItems)
                {
                    ConfigureElectronicMediaItem(item);
                    item.PropertyChanged += MediaItem_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (MediaItemViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= MediaItem_PropertyChanged;
                }
            }

            var media = FindMediaEntryByItemsCollection(sender);
            if (media != null)
            {
                RecalculateQuantities(media);
            }

            ScheduleRefreshMediaViews();
        }

        private void MediaItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MediaItemViewModel.ItemType))
            {
                ScheduleRefreshMediaViews();
            }
        }

        private void ScheduleRefreshMediaViews()
        {
            if (Application.Current?.Dispatcher is { } dispatcher
                && !dispatcher.HasShutdownStarted
                && !dispatcher.HasShutdownFinished)
            {
                dispatcher.BeginInvoke(RefreshMediaViews, DispatcherPriority.Background);
            }
            else
            {
                RefreshMediaViews();
            }
        }

        private void RefreshMediaViews()
        {
            DataElectronicMediaView.Refresh();
            OnPropertyChanged(nameof(DataElectronicMediaCount));
        }

        private MediaEntryViewModel? FindMediaEntryByItemsCollection(object? sender)
        {
            return MediaEntries.FirstOrDefault(m => ReferenceEquals(m.Items, sender));
        }

        private void RecalculateQuantities(MediaEntryViewModel? media = null)
        {
            IEnumerable<MediaEntryViewModel> entries = media == null
                ? MediaEntries
                : new[] { media };

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (RegisterMediaTreeSupport.IsDataElectronic(entry))
                {
                    entry.SetAutoMediaCount(1);
                    foreach (var item in entry.Items)
                    {
                        RecalculateElectronicItemContentCount(item);
                    }
                }
            }
        }

        private static void RecalculateElectronicItemContentCount(MediaItemViewModel item)
        {
            item.SetAutoContentCount(1);
        }

        private void AddDataElectronicMediaEntry()
        {
            EnsureElectronicMediaSelections();
            var entry = new MediaEntryViewModel
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                MediaType = ResolveSelectedElectronicMediaType(),
                Disposition = ResolveSelectedElectronicDisposition(),
                IsBorrowedHardDisk = false,
                BorrowedHardDiskCode = string.Empty
            };
            entry.Items.Add(CreateDefaultElectronicMediaItem(ArchiveRegisterDomainValues.ItemTypeData));
            MediaEntries.Add(entry);
            RecalculateQuantities(entry);
            ApplySelectedElectronicMediaSettingsToEntries();
        }

        private void RemoveMediaEntry(MediaEntryViewModel? media)
        {
            if (media != null)
            {
                MediaEntries.Remove(media);
            }
        }

        private void AddMediaItem(MediaEntryViewModel? media)
        {
            if (media == null)
            {
                return;
            }

            media.Items.Add(CreateDefaultElectronicMediaItem(ArchiveRegisterDomainValues.ItemTypeData));
        }

        private void RemoveMediaItem(MediaItemViewModel? item)
        {
            if (item == null)
            {
                return;
            }

            var parent = MediaEntries.FirstOrDefault(m => m.Items.Contains(item));
            parent?.Items.Remove(item);
        }

        private MediaEntryViewModel CreateMediaEntryViewModel(YearlyArchiveRegisterMedia media)
        {
            var vm = RegisterMediaTreeSupport.CreateMediaEntryViewModel(
                media,
                ResolveConfidentialLevelFromRecord,
                ConfigureElectronicMediaItem);
            RecalculateQuantities(vm);
            return vm;
        }

        private void EnsureElectronicMediaSelections()
        {
            _isSyncingElectronicMediaSettings = true;
            try
            {
                if (string.IsNullOrWhiteSpace(SelectedElectronicMediaType))
                {
                    SelectedElectronicMediaType = DataElectronicMediaTypeOptions.FirstOrDefault() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(SelectedElectronicDisposition))
                {
                    SelectedElectronicDisposition = GetAllowedElectronicDispositionOptions().FirstOrDefault() ?? string.Empty;
                }
            }
            finally
            {
                _isSyncingElectronicMediaSettings = false;
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        private void SyncElectronicMediaSettingsFromEntries()
        {
            var firstElectronicMedia = MediaEntries.FirstOrDefault(RegisterMediaTreeSupport.IsDataElectronic);

            _isSyncingElectronicMediaSettings = true;
            try
            {
                string rawMediaType = string.IsNullOrWhiteSpace(firstElectronicMedia?.MediaType)
                    ? string.Empty
                    : firstElectronicMedia.MediaType.Trim();
                SelectedElectronicMediaType = string.IsNullOrEmpty(rawMediaType)
                    ? (DataElectronicMediaTypeOptions.FirstOrDefault() ?? string.Empty)
                    : (DataElectronicMediaTypeOptions.FirstOrDefault(o => string.Equals(o, rawMediaType, StringComparison.OrdinalIgnoreCase))
                       ?? rawMediaType);

                RefreshElectronicDispositionOptions();

                string preferredDisposition = string.IsNullOrWhiteSpace(firstElectronicMedia?.Disposition)
                    ? string.Empty
                    : firstElectronicMedia.Disposition.Trim();
                var allowedDispositions = GetAllowedElectronicDispositionOptions();

                string? matchedDisposition = allowedDispositions
                    .FirstOrDefault(o => string.Equals(o, preferredDisposition, StringComparison.OrdinalIgnoreCase));
                SelectedElectronicDisposition = matchedDisposition
                    ?? (allowedDispositions.FirstOrDefault() ?? string.Empty);
            }
            finally
            {
                _isSyncingElectronicMediaSettings = false;
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        private void ApplySelectedElectronicMediaSettingsToEntries()
        {
            if (_isSyncingElectronicMediaSettings)
            {
                return;
            }

            foreach (var media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
            {
                media.MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic;
                if (!LockElectronicMediaTypeAndDisposition)
                {
                    media.MediaType = ResolveSelectedElectronicMediaType();
                    media.Disposition = ResolveSelectedElectronicDisposition();
                }

                media.SetAutoMediaCount(1);
            }

            EnforceRetainedHardDiskBorrowedRegistration();
            OnPropertyChanged(nameof(DataElectronicMediaCount));
            OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
            OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
        }

        private string ResolveSelectedElectronicMediaType()
        {
            return string.IsNullOrWhiteSpace(SelectedElectronicMediaType)
                ? (DataElectronicMediaTypeOptions.FirstOrDefault() ?? string.Empty)
                : SelectedElectronicMediaType;
        }

        private string ResolveSelectedElectronicDisposition()
        {
            return string.IsNullOrWhiteSpace(SelectedElectronicDisposition)
                ? (GetAllowedElectronicDispositionOptions().FirstOrDefault() ?? string.Empty)
                : SelectedElectronicDisposition;
        }

        private void RefreshElectronicDispositionOptions()
        {
            var allowedOptions = GetAllowedElectronicDispositionOptions();
            ApplyOptions(DataElectronicDispositionOptions, allowedOptions);

            string current = SelectedElectronicDisposition?.Trim() ?? string.Empty;
            string? canon = allowedOptions.FirstOrDefault(o => string.Equals(o, current, StringComparison.OrdinalIgnoreCase));
            if (canon != null)
            {
                if (!string.Equals(SelectedElectronicDisposition, canon, StringComparison.Ordinal))
                {
                    SelectedElectronicDisposition = canon;
                }
            }
            else
            {
                SelectedElectronicDisposition = allowedOptions.FirstOrDefault() ?? string.Empty;
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        private IReadOnlyList<string> GetAllowedElectronicDispositionOptions()
        {
            if (AllowedDispositionOptionsResolver != null)
            {
                return AllowedDispositionOptionsResolver(
                    ResolveSelectedElectronicMediaType(),
                    _allElectronicDispositionOptions);
            }

            return _archiveRegisterService.GetAllowedElectronicDispositions(
                ResolveSelectedElectronicMediaType(),
                _allElectronicDispositionOptions);
        }

        private void EnforceRetainedHardDiskBorrowedRegistration()
        {
            if (!RestrictRetainedHardDiskToBorrowedOnly)
            {
                return;
            }

            foreach (MediaEntryViewModel media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
            {
                if (media.IsRetainedHardDiskScenario && !media.IsBorrowedHardDisk)
                {
                    media.IsBorrowedHardDisk = true;
                }
            }

            OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
        }

        private void SyncBorrowedHardDiskSettingsFromSelections()
        {
            bool isVisible = IsBorrowedHardDiskRegistrationVisible;
            if (isVisible)
            {
                OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
                OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
                _ = RefreshUserBorrowedHardDiskCodesAsync();
                return;
            }

            OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
            OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
        }

        private void EnsureUserBorrowedHardDiskListIncludesSelected()
        {
            if (!IsBorrowedHardDiskRegistrationVisible)
            {
                return;
            }

            foreach (var media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
            {
                if (!media.IsRetainedHardDiskScenario || !media.IsBorrowedHardDisk)
                {
                    continue;
                }

                string code = media.BorrowedHardDiskCode?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                if (!UserBorrowedHardDiskCodes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
                {
                    UserBorrowedHardDiskCodes.Insert(0, code);
                }
            }
        }

        private async Task RefreshUserBorrowedHardDiskCodesAsync()
        {
            if (_borrowedHardDiskCodesRefreshInProgress)
            {
                return;
            }

            _borrowedHardDiskCodesRefreshInProgress = true;
            try
            {
                var preservedSnapshots = new Dictionary<MediaEntryViewModel, string>();
                foreach (var media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
                {
                    if (!media.IsRetainedHardDiskScenario || !media.IsBorrowedHardDisk)
                    {
                        continue;
                    }

                    string code = media.BorrowedHardDiskCode?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(code))
                    {
                        preservedSnapshots[media] = code;
                    }
                }

                UserBorrowedHardDiskCodes.Clear();
                var user = _userContextService.CurrentUser;
                if (user == null)
                {
                    RestoreBorrowedHardDiskUiAfterCodesReload(preservedSnapshots);
                    return;
                }

                try
                {
                    var codes = await _hardDiskMediaService.GetCurrentUserBorrowedHardDiskCodesAsync(user);
                    foreach (var code in codes)
                    {
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            UserBorrowedHardDiskCodes.Add(code);
                        }
                    }

                    RestoreBorrowedHardDiskUiAfterCodesReload(preservedSnapshots);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("加载借出硬盘列表失败: " + ex.Message);
                    RestoreBorrowedHardDiskUiAfterCodesReload(preservedSnapshots);
                }
            }
            finally
            {
                _borrowedHardDiskCodesRefreshInProgress = false;
            }
        }

        private void RestoreBorrowedHardDiskUiAfterCodesReload(Dictionary<MediaEntryViewModel, string> preservedSnapshots)
        {
            foreach (var code in preservedSnapshots.Values.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!UserBorrowedHardDiskCodes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
                {
                    UserBorrowedHardDiskCodes.Insert(0, code);
                }
            }

            foreach (var kv in preservedSnapshots)
            {
                MediaEntryViewModel vm = kv.Key;
                string preservedCode = kv.Value;
                string current = vm.BorrowedHardDiskCode?.Trim() ?? string.Empty;
                if (!string.Equals(current, preservedCode, StringComparison.OrdinalIgnoreCase))
                {
                    vm.BorrowedHardDiskCode = preservedCode;
                }
            }

            EnsureUserBorrowedHardDiskListIncludesSelected();
        }

        private MediaItemViewModel CreateDefaultElectronicMediaItem(string itemType)
        {
            var item = new MediaItemViewModel
            {
                ItemType = itemType,
                MaterialCategory = ElectronicMaterialCategoryOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument,
                DataOrganizationForm = ElectronicDataOrganizationFormOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                ConfidentialLevel = ConfidentialLevelOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ConfidentialLevelNone
            };

            ConfigureElectronicMediaItem(item);
            return item;
        }

        private void ConfigureElectronicMediaItem(MediaItemViewModel item)
        {
            item.SubCategoryOptionsRefreshHandler = RefreshElectronicSubCategoryOptions;
            RefreshElectronicSubCategoryOptions(item);
            AttachContentEntryQuantityHandler(item);
        }

        private void AttachContentEntryQuantityHandler(MediaItemViewModel item)
        {
            if (_contentEntryQuantityHandlers.ContainsKey(item))
            {
                return;
            }

            NotifyCollectionChangedEventHandler handler = (_, _) =>
            {
                RecalculateElectronicItemContentCount(item);
            };
            _contentEntryQuantityHandlers[item] = handler;
            item.ContentEntries.CollectionChanged += handler;
        }

        private void DetachContentEntryQuantityHandler(MediaItemViewModel item)
        {
            if (_contentEntryQuantityHandlers.TryGetValue(item, out var handler))
            {
                item.ContentEntries.CollectionChanged -= handler;
                _contentEntryQuantityHandlers.Remove(item);
            }
        }

        private void RefreshElectronicSubCategoryOptions(MediaItemViewModel item)
        {
            IReadOnlyList<string> options = string.Equals(item.MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal)
                ? _electronicDocumentSubCategoryOptions
                : string.Equals(item.MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal)
                    ? _electronicDataSubCategoryOptions
                    : string.Equals(item.MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftware, StringComparison.Ordinal)
                        ? _electronicSoftwareSubCategoryOptions
                        : Array.Empty<string>();

            item.AvailableSubCategories.Clear();
            foreach (var option in options)
            {
                item.AvailableSubCategories.Add(option);
            }

            if (!string.IsNullOrWhiteSpace(item.SubCategory)
                && !options.Any(option => string.Equals(option, item.SubCategory, StringComparison.OrdinalIgnoreCase)))
            {
                item.SubCategory = string.Empty;
            }
        }

        private static ElectronicMediaItemEntryViewModel CreateContentEntryViewModel(ElectronicMediaContentScanEntry entry)
        {
            return new ElectronicMediaItemEntryViewModel
            {
                EntryKind = entry.EntryKind,
                EntryName = entry.EntryName,
                RelativePath = entry.RelativePath,
                SizeMb = entry.SizeMb,
                CreatedAt = entry.CreatedAt,
                ModifiedAt = entry.ModifiedAt
            };
        }

        private void ApplyScanResult(
            MediaItemViewModel item,
            ElectronicMediaContentScanResult result,
            IReadOnlyList<string>? scannedFilePaths = null,
            IReadOnlyList<string>? scannedDirectoryPaths = null)
        {
            item.StorageRootFullPath = result.RootPath;
            item.StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(result.RootPath);
            item.ContentEntries.Clear();
            foreach (var entry in result.Entries)
            {
                item.ContentEntries.Add(CreateContentEntryViewModel(entry));
            }

            item.LastScannedFilePaths.Clear();
            if (scannedFilePaths != null)
            {
                item.LastScannedFilePaths.AddRange(scannedFilePaths);
            }

            item.LastScannedDirectoryPaths.Clear();
            if (scannedDirectoryPaths != null)
            {
                item.LastScannedDirectoryPaths.AddRange(scannedDirectoryPaths);
            }

            item.DataSizeMb = result.TotalSizeMb;
            item.RefreshContentScanSummary(result.FileCount);
            RecalculateElectronicItemContentCount(item);
        }

        private static bool CanRescanElectronicContent(MediaItemViewModel item)
        {
            if (item.IsDirectoryOrganizationForm)
            {
                return item.LastScannedDirectoryPaths.Count > 0
                    || (item.HasScannedEntries && !string.IsNullOrWhiteSpace(item.StoragePath));
            }

            if (item.IsFileOrganizationForm)
            {
                return item.LastScannedFilePaths.Count > 0;
            }

            return false;
        }

        private async Task PickFolderAndScanElectronicContentAsync(MediaItemViewModel? item)
        {
            if (item == null || !item.IsDirectoryOrganizationForm)
            {
                return;
            }

            var folders = _dialogService.PickFolders("选择电子资料目录", multiselect: true);
            if (folders == null || folders.Count == 0)
            {
                return;
            }

            if (item.HasScannedEntries
                && !_dialogService.ShowConfirm("重新选择目录将覆盖当前已扫描的目录/文件明细，是否继续？"))
            {
                return;
            }

            await ScanDirectoriesContentAsync(item, folders);
        }

        private async Task PickFilesAndScanElectronicContentAsync(MediaItemViewModel? item)
        {
            if (item == null || !item.IsFileOrganizationForm)
            {
                return;
            }

            var files = _dialogService.PickFiles("选择电子资料文件", multiselect: true);
            if (files == null || files.Count == 0)
            {
                return;
            }

            if (item.HasScannedEntries
                && !_dialogService.ShowConfirm("重新选择文件将覆盖当前已扫描的目录/文件明细，是否继续？"))
            {
                return;
            }

            await ScanFileContentAsync(item, files);
        }

        private async Task RescanElectronicContentAsync(MediaItemViewModel? item)
        {
            if (item == null || !CanRescanElectronicContent(item))
            {
                return;
            }

            if (item.IsDirectoryOrganizationForm)
            {
                var directories = ResolveScannedDirectoryPaths(item);
                if (directories.Count == 0)
                {
                    _dialogService.ShowMessage("原扫描目录已不存在，请重新选择目录。");
                    return;
                }

                await ScanDirectoriesContentAsync(
                    item,
                    directories,
                    string.IsNullOrWhiteSpace(item.StorageRootFullPath) ? null : item.StorageRootFullPath);
                return;
            }

            if (item.LastScannedFilePaths.Count > 0)
            {
                var existingFiles = item.LastScannedFilePaths
                    .Where(File.Exists)
                    .ToList();

                if (existingFiles.Count == 0)
                {
                    _dialogService.ShowMessage("原扫描文件已不存在，请重新选择文件。");
                    return;
                }

                await ScanFileContentAsync(
                    item,
                    existingFiles,
                    string.IsNullOrWhiteSpace(item.StorageRootFullPath) ? null : item.StorageRootFullPath);
                return;
            }

            _dialogService.ShowMessage("当前没有可重新扫描的文件来源，请重新选择文件。");
        }

        private async Task ScanDirectoriesContentAsync(MediaItemViewModel item, IReadOnlyList<string> folders, string? storageRootDirectory = null)
        {
            _dialogService.SetBusyState(true);
            try
            {
                var result = await Task.Run(() => _electronicMediaContentScanService.ScanDirectories(folders, storageRootDirectory));
                ApplyScanResult(item, result, scannedDirectoryPaths: folders.ToList());
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"扫描失败：{ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private static List<string> ResolveScannedDirectoryPaths(MediaItemViewModel item)
        {
            if (item.LastScannedDirectoryPaths.Count > 0)
            {
                return item.LastScannedDirectoryPaths
                    .Where(Directory.Exists)
                    .ToList();
            }

            if (string.IsNullOrWhiteSpace(item.StoragePath) || item.ContentEntries.Count == 0)
            {
                return new List<string>();
            }

            return item.ContentEntries
                .Select(entry => Path.GetFullPath(Path.Combine(item.StoragePath, entry.RelativePath)))
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task ScanFileContentAsync(MediaItemViewModel item, IReadOnlyList<string> files, string? storageRootDirectory = null)
        {
            _dialogService.SetBusyState(true);
            try
            {
                var result = await Task.Run(() => _electronicMediaContentScanService.ScanFiles(files, storageRootDirectory));
                ApplyScanResult(item, result, files.ToList());
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"扫描失败：{ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void ClearElectronicContent(MediaItemViewModel? item)
        {
            if (item == null || !item.HasScannedEntries)
            {
                return;
            }

            if (!_dialogService.ShowConfirm("确定清空当前目录/文件明细吗？"))
            {
                return;
            }

            item.ClearScannedContent();
            RecalculateElectronicItemContentCount(item);
        }

        private void ViewElectronicContentEntries(MediaItemViewModel? item)
        {
            if (item == null || !item.HasScannedEntries)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(item.ContentDesc)
                ? "目录/文件明细"
                : $"{item.ContentDesc} - 目录/文件明细";

            var entries = item.ContentEntries
                .Select(entry => new ElectronicMediaItemEntryDisplayItem(
                    entry.EntryKind,
                    entry.EntryName,
                    FormatEntryCreatedDate(item, entry),
                    FormatEntryModifiedDate(item, entry),
                    entry.SizeMb))
                .ToList();

            _dialogService.ShowElectronicMediaItemEntriesDialog(title, entries, item.ContentScanSummaryText);
        }

        private static string FormatEntryCreatedDate(MediaItemViewModel item, ElectronicMediaItemEntryViewModel entry)
        {
            if (entry.CreatedAt.HasValue)
            {
                return ElectronicMediaItemSupport.FormatModifiedDate(entry.CreatedAt);
            }

            if (string.IsNullOrWhiteSpace(item.StorageRootFullPath) || string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                return "-";
            }

            string fullPath = Path.GetFullPath(Path.Combine(item.StorageRootFullPath, entry.RelativePath));
            return ElectronicMediaItemSupport.FormatModifiedDate(
                ElectronicMediaItemSupport.ResolveEntryCreatedAt(fullPath, entry.EntryKind));
        }

        private static string FormatEntryModifiedDate(MediaItemViewModel item, ElectronicMediaItemEntryViewModel entry)
        {
            if (entry.ModifiedAt.HasValue)
            {
                return ElectronicMediaItemSupport.FormatModifiedDate(entry.ModifiedAt);
            }

            if (string.IsNullOrWhiteSpace(item.StorageRootFullPath) || string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                return "-";
            }

            string fullPath = Path.GetFullPath(Path.Combine(item.StorageRootFullPath, entry.RelativePath));
            return ElectronicMediaItemSupport.FormatModifiedDate(
                ElectronicMediaItemSupport.ResolveEntryModifiedAt(fullPath, entry.EntryKind));
        }

        private bool CanViewElectronicContentEntries()
        {
            return CanEditForm
                || _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);
        }

        private string ResolveConfidentialLevelFromRecord(string? storedLevel)
        {
            string normalized = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(storedLevel);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return ConfidentialLevelOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ConfidentialLevelNone;
            }

            return ConfidentialLevelOptions.FirstOrDefault(option =>
                       string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase))
                   ?? normalized;
        }

        private static void ApplyOptions(ObservableCollection<string> target, IReadOnlyCollection<string> values)
        {
            target.Clear();
            foreach (var item in values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                target.Add(item);
            }
        }
    }
}
