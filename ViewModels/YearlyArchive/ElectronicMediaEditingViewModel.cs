using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.Shared;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;
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
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子介质登记编辑 ViewModel，供资料登记与入网申请等场景复用。
    /// </summary>
    public partial class ElectronicMediaEditingViewModel : ViewModelBase
    {
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IDialogService _dialogService;
        private readonly IElectronicMediaContentScanService _electronicMediaContentScanService;
        private readonly IUserContextService _userContextService;
        private readonly IUserService _userService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PageDbOperationQueue _pageDb = new();

        private readonly List<string> _allElectronicDispositionOptions = new();
        private readonly List<string> _electronicDocumentSubCategoryOptions = new();
        private readonly List<string> _electronicDataSubCategoryOptions = new();
        private readonly List<string> _electronicSoftwareSubCategoryOptions = new();
        private readonly Dictionary<MediaItemViewModel, NotifyCollectionChangedEventHandler> _contentEntryQuantityHandlers = new();

        private bool _isInitialized;
        private int _electronicMediaSettingsSyncDepth;
        private bool _borrowedHardDiskCodesRefreshInProgress;

        private bool IsSyncingElectronicMediaSettings => _electronicMediaSettingsSyncDepth > 0;

        private void BeginElectronicMediaSettingsSync() => _electronicMediaSettingsSyncDepth++;

        private void EndElectronicMediaSettingsSync()
        {
            if (_electronicMediaSettingsSyncDepth > 0)
            {
                _electronicMediaSettingsSyncDepth--;
            }
        }

        private bool _canEditForm;
        private bool _canEditItemConfidentialLevel;
        private bool _lockElectronicMediaTypeAndDisposition;
        private bool _restrictRetainedHardDiskToBorrowedOnly;
        private bool _enableRetainedHardDiskBorrowedRegistration = true;
        private bool _showOutboundExternalHardDiskRequisitionFields;
        private bool _showArchiveFilingHardDiskFields;
        private bool _showElectronicDispositionInHeader = true;
        private bool _allowElectronicContentScan = true;
        private string _sectionHeader = "资料介质（电子）";

        private string _selectedElectronicMediaType = string.Empty;
        private string _selectedElectronicDisposition = string.Empty;

        public ElectronicMediaEditingViewModel(
            IArchiveRegisterService archiveRegisterService,
            IDialogService dialogService,
            IElectronicMediaContentScanService electronicMediaContentScanService,
            IUserContextService userContextService,
            IUserService userService,
            IServiceScopeFactory scopeFactory)
        {
            _archiveRegisterService = archiveRegisterService ?? throw new ArgumentNullException(nameof(archiveRegisterService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _electronicMediaContentScanService = electronicMediaContentScanService ?? throw new ArgumentNullException(nameof(electronicMediaContentScanService));
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

            DataElectronicMediaView = new ListCollectionView(MediaEntries)
            {
                Filter = m => RegisterMediaTreeSupport.IsDataElectronic(m as MediaEntryViewModel)
            };
            MediaEntries.CollectionChanged += MediaEntries_CollectionChanged;

            AddDataElectronicMediaEntryCommand = new RelayCommand(_ => AddDataElectronicMediaEntry(), _ => CanEditForm);
            AddMediaItemCommand = new RelayCommand<MediaEntryViewModel>(AddMediaItem, _ => CanEditForm);
            BatchAddElectronicMediaItemsCommand = new RelayCommand<MediaEntryViewModel>(
                async media => await BatchAddElectronicMediaItemsAsync(media),
                _ => CanEditForm && AllowElectronicContentScan);
            RemoveMediaEntryCommand = new RelayCommand<MediaEntryViewModel>(RemoveMediaEntry, _ => CanEditForm);
            RemoveMediaItemCommand = new RelayCommand<MediaItemViewModel>(RemoveMediaItem, _ => CanEditForm);
            FillDefaultOutboundStoragePathCommand = new RelayCommand<MediaItemViewModel>(
                FillDefaultOutboundStoragePath,
                item => item != null && item.IsStoragePathEditable);
            PickFolderAndScanElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                async item => await PickFolderAndScanElectronicContentAsync(item),
                item => AllowElectronicContentScan && item != null && item.IsDirectoryOrganizationForm);
            RescanElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                async item => await RescanElectronicContentAsync(item),
                item => AllowElectronicContentScan && item != null && CanRescanElectronicContent(item));
            ClearElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                item => ClearElectronicContent(item),
                item => AllowElectronicContentScan && item != null && item.HasScannedEntries);
            ViewElectronicContentEntriesCommand = new RelayCommand<MediaItemViewModel>(
                item => ViewElectronicContentEntries(item),
                item => item != null && item.HasScannedEntries && CanViewElectronicContentEntries());
            PickOutboundBlankHardDiskCommand = new RelayCommand<MediaEntryViewModel>(
                media => PickOutboundBlankHardDisk(media),
                media => CanPickOutboundBlankHardDisk(media));
        }

        public ObservableCollection<MediaEntryViewModel> MediaEntries { get; } = new();

        public ListCollectionView DataElectronicMediaView { get; }

        public ObservableCollection<string> DataElectronicMediaTypeOptions { get; } = new();

        public ObservableCollection<string> DataElectronicDispositionOptions { get; } = new();

        public ObservableCollection<string> ElectronicMaterialCategoryOptions { get; } = new();

        public ObservableCollection<string> ElectronicDataOrganizationFormOptions { get; } = new();

        public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();

        /// <summary>资料来源（内部/外来等），供子项下拉绑定。</summary>
        public ObservableCollection<string> SourceTypeOptions { get; } = new();

        /// <summary>部门列表，供子项「提供单位」内部下拉绑定。</summary>
        public ObservableCollection<Department> Departments { get; } = new();

        public ObservableCollection<string> UserBorrowedHardDiskCodes { get; } = new();

        public string SelectedElectronicMediaType
        {
            get => _selectedElectronicMediaType;
            set
            {
                if (!IsSyncingElectronicMediaSettings
                    && LockElectronicMediaTypeAndDisposition
                    && !string.IsNullOrWhiteSpace(_selectedElectronicMediaType)
                    && !string.Equals(value, _selectedElectronicMediaType, StringComparison.Ordinal))
                {
                    return;
                }

                if (SetProperty(ref _selectedElectronicMediaType, value)
                    && !IsSyncingElectronicMediaSettings)
                {
                    // 同步重建选项时跳过副作用，避免 Clear 触发的 null 回写冲掉明细 MediaType。
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
                if (!IsSyncingElectronicMediaSettings
                    && LockElectronicMediaTypeAndDisposition
                    && !string.IsNullOrWhiteSpace(_selectedElectronicDisposition)
                    && !string.Equals(value, _selectedElectronicDisposition, StringComparison.Ordinal))
                {
                    return;
                }

                if (SetProperty(ref _selectedElectronicDisposition, value)
                    && !IsSyncingElectronicMediaSettings)
                {
                    ApplySelectedElectronicMediaSettingsToEntries();
                }
            }
        }

        public int DataElectronicMediaCount => MediaEntries.Count(RegisterMediaTreeSupport.IsDataElectronic);

        public bool IsBorrowedHardDiskRegistrationVisible =>
            EnableRetainedHardDiskBorrowedRegistration
            && string.Equals(SelectedElectronicMediaType?.Trim(), ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
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
                    RefreshOutboundItemStoragePaths();
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
        /// 可选：覆盖默认处置方式解析（如入网档外「光盘→介质带回」、出网按目的地约束）。
        /// </summary>
        public Func<string?, IReadOnlyCollection<string>, IReadOnlyList<string>>? AllowedDispositionOptionsResolver { get; set; }

        /// <summary>
        /// 可选：覆盖默认可选电子介质类型（如出网按目的地限制光盘/硬盘/U盘）。
        /// </summary>
        public Func<IReadOnlyCollection<string>, IReadOnlyList<string>>? AllowedMediaTypeOptionsResolver { get; set; }

        /// <summary>
        /// 是否启用「硬盘·介质留存」借出硬盘登记区（出网场景应关闭）。
        /// </summary>
        public bool EnableRetainedHardDiskBorrowedRegistration
        {
            get => _enableRetainedHardDiskBorrowedRegistration;
            set
            {
                if (SetProperty(ref _enableRetainedHardDiskBorrowedRegistration, value))
                {
                    ApplyBorrowedHardDiskRegistrationVisibilityToEntries();
                    OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
                    OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
                }
            }
        }

        /// <summary>
        /// 是否在「出网资料/资料介质（电子）」卡片顶栏展示处置方式（出网申请页隐藏，后台仍按目的地约束）。
        /// </summary>
        public bool ShowElectronicDispositionInHeader
        {
            get => _showElectronicDispositionInHeader;
            set => SetProperty(ref _showElectronicDispositionInHeader, value);
        }

        /// <summary>
        /// 出网外部离线场景：展示库内空盘征用与归还设置。
        /// </summary>
        public bool ShowOutboundExternalHardDiskRequisitionFields
        {
            get => _showOutboundExternalHardDiskRequisitionFields;
            set
            {
                if (SetProperty(ref _showOutboundExternalHardDiskRequisitionFields, value))
                {
                    foreach (MediaEntryViewModel media in MediaEntries)
                    {
                        media.NotifyOutboundHardDiskRequisitionPropertiesChanged();
                    }
                }
            }
        }

        /// <summary>
        /// 资料室存档：硬盘可选库内空盘或并档目标，不展示归还。
        /// </summary>
        public bool ShowArchiveFilingHardDiskFields
        {
            get => _showArchiveFilingHardDiskFields;
            set
            {
                if (SetProperty(ref _showArchiveFilingHardDiskFields, value))
                {
                    foreach (MediaEntryViewModel media in MediaEntries)
                    {
                        media.NotifyArchiveFilingHardDiskPropertiesChanged();
                    }

                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<ArchiveFilingAppendTargetOption> ArchiveFilingAppendTargets { get; } = new();

        /// <summary>
        /// 替换并档候选，并按已保存的电子袋 Id 回选。
        /// </summary>
        /// <param name="retainUnlistedSavedTargets">
        /// 为 true 时，已保存但不在本次候选中的并入硬盘仍保留并显示，避免刷新下拉后丢失。
        /// 离开资料室存档场景时应传 false，以便清空不再适用的并档选择。
        /// </param>
        public void ReplaceArchiveFilingAppendTargets(
            IReadOnlyList<ArchiveFilingAppendTargetOption> options,
            bool retainUnlistedSavedTargets = false)
        {
            List<MediaEntryViewModel> medias = MediaEntries
                .Where(RegisterMediaTreeSupport.IsDataElectronic)
                .ToList();
            var snapshots = medias
                .Select(media => (
                    media,
                    media.AppendTargetElectronicUnitId,
                    media.AppendTargetHardDiskCode))
                .ToList();

            var merged = new List<ArchiveFilingAppendTargetOption>(options);
            if (retainUnlistedSavedTargets)
            {
                foreach (var (_, unitId, diskCode) in snapshots)
                {
                    if (unitId is not > 0 || merged.Any(option => option.UnitId == unitId.Value))
                    {
                        continue;
                    }

                    string code = diskCode?.Trim() ?? string.Empty;
                    merged.Insert(0, new ArchiveFilingAppendTargetOption
                    {
                        UnitId = unitId.Value,
                        DiskCode = code,
                        DisplayText = string.IsNullOrWhiteSpace(code)
                            ? $"已选并档硬盘（袋 {unitId.Value}）"
                            : code
                    });
                }
            }

            foreach (MediaEntryViewModel media in medias)
            {
                media.BeginAppendTargetSelectionWritebackSuppression();
            }

            try
            {
                // 必须先抑制：Clear 会让 ComboBox 把 SelectedItem 写成 null，并回写清空并入硬盘。
                ArchiveFilingAppendTargets.Clear();
                foreach (ArchiveFilingAppendTargetOption option in merged)
                {
                    ArchiveFilingAppendTargets.Add(option);
                }

                foreach (var (media, unitId, diskCode) in snapshots)
                {
                    if (media.AppendTargetElectronicUnitId is not > 0 && unitId is > 0)
                    {
                        media.AppendTargetElectronicUnitId = unitId;
                        if (string.IsNullOrWhiteSpace(media.AppendTargetHardDiskCode))
                        {
                            media.AppendTargetHardDiskCode = diskCode ?? string.Empty;
                        }
                    }

                    media.SyncAppendTargetSelection(ArchiveFilingAppendTargets, clearIfMissing: !retainUnlistedSavedTargets);
                }
            }
            finally
            {
                foreach (MediaEntryViewModel media in medias)
                {
                    media.EndAppendTargetSelectionWritebackSuppression();
                }
            }

            OnPropertyChanged(nameof(HasArchiveFilingAppendTargets));
        }

        public bool HasArchiveFilingAppendTargets => ArchiveFilingAppendTargets.Count > 0;

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

        /// <summary>
        /// 是否允许从本地选择目录/文件并扫描明细。出网申请阶段应关闭。
        /// </summary>
        public bool AllowElectronicContentScan
        {
            get => _allowElectronicContentScan;
            set
            {
                if (SetProperty(ref _allowElectronicContentScan, value))
                {
                    ApplyElectronicContentScanAvailabilityToItems();
                    CommandManager.InvalidateRequerySuggested();
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

        public RelayCommand<MediaEntryViewModel> BatchAddElectronicMediaItemsCommand { get; }

        public RelayCommand<MediaEntryViewModel> RemoveMediaEntryCommand { get; }

        public RelayCommand<MediaItemViewModel> RemoveMediaItemCommand { get; }

        public RelayCommand<MediaItemViewModel> FillDefaultOutboundStoragePathCommand { get; }

        public RelayCommand<MediaItemViewModel> PickFolderAndScanElectronicContentCommand { get; }

        public RelayCommand<MediaItemViewModel> RescanElectronicContentCommand { get; }

        public RelayCommand<MediaItemViewModel> ClearElectronicContentCommand { get; }

        public RelayCommand<MediaItemViewModel> ViewElectronicContentEntriesCommand { get; }

        public RelayCommand<MediaEntryViewModel> PickOutboundBlankHardDiskCommand { get; }

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
            // 独立 scope：本方法常在宿主 Initialize 期间执行，且容量/借出列表会 fire-and-forget，
            // 不可与宿主页面级 DbContext 叠查。
            ArchiveRegisterPageDomainOptions domainOptions;
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                var register = scope.ServiceProvider.GetRequiredService<IArchiveRegisterService>();
                domainOptions = await register.GetPageDomainOptionsAsync().ConfigureAwait(true);
            }

            string preferredMediaType = SelectedElectronicMediaType;
            string preferredDisposition = SelectedElectronicDisposition;

            BeginElectronicMediaSettingsSync();
            try
            {
                ApplyOptions(DataElectronicMediaTypeOptions,
                    GetAllowedElectronicMediaTypeOptions(domainOptions.DataElectronicMediaTypes));
                _allElectronicMediaTypeOptions.Clear();
                _allElectronicMediaTypeOptions.AddRange(
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
                ApplyOptions(SourceTypeOptions, domainOptions.SourceTypes);
                LoadDepartments();

                SelectedElectronicMediaType = ComboBoxSelectionPreserveSupport.ResolvePreferredOrFirst(
                    DataElectronicMediaTypeOptions,
                    preferredMediaType);
            }
            finally
            {
                EndElectronicMediaSettingsSync();
            }

            SelectedElectronicDisposition = preferredDisposition;
            RefreshElectronicDispositionOptions();
            EnsureElectronicMediaSelections();
            foreach (var item in MediaEntries.SelectMany(entry => entry.Items))
            {
                item.SourceType = ResolveSourceTypeFromRecord(item.SourceType);
                ApplyDefaultProvideUnitForInternalItem(item, onlyWhenEmpty: true);
                RefreshElectronicSubCategoryOptions(item);
            }

            SyncElectronicMediaSettingsFromEntries();
        }

        private void LoadDepartments()
        {
            try
            {
                Departments.Clear();
                foreach (Department department in _userService.GetAllDepartments())
                {
                    Departments.Add(department);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("加载部门列表失败: " + ex.Message);
            }
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
            RefreshOutboundItemStoragePaths();
            // 并档候选在回填介质之前加载。这里按已保存的电子袋 Id 补进下拉并回选，否则容量能显示、选中文本为空。
            EnsureSavedAppendTargetsListed();
            foreach (MediaEntryViewModel media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
            {
                media.SyncAppendTargetSelection(ArchiveFilingAppendTargets, clearIfMissing: false);
                RequestHardDiskCapacityRefresh(media);
            }
        }

        /// <summary>
        /// 已保存的并入硬盘若不在当前候选中，补一条只读选项，保证下拉能显示编号。
        /// </summary>
        private void EnsureSavedAppendTargetsListed()
        {
            foreach (MediaEntryViewModel media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
            {
                if (media.AppendTargetElectronicUnitId is not > 0
                    || ArchiveFilingAppendTargets.Any(option => option.UnitId == media.AppendTargetElectronicUnitId.Value))
                {
                    continue;
                }

                string code = media.AppendTargetHardDiskCode?.Trim() ?? string.Empty;
                int unitId = media.AppendTargetElectronicUnitId.Value;
                ArchiveFilingAppendTargets.Insert(0, new ArchiveFilingAppendTargetOption
                {
                    UnitId = unitId,
                    DiskCode = code,
                    DisplayText = string.IsNullOrWhiteSpace(code)
                        ? $"已选并档硬盘（袋 {unitId}）"
                        : code
                });
            }
        }

        /// <summary>
        /// 将界面电子介质明细构建为登记介质实体列表。
        /// </summary>
        public List<YearlyArchiveRegisterMedia> BuildEntities()
        {
            EnsureElectronicMediaSelections();
            RecalculateAllQuantities();
            if (!EnableRetainedHardDiskBorrowedRegistration)
            {
                ApplyBorrowedHardDiskRegistrationVisibilityToEntries();
            }

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

        /// <summary>
        /// 出网目的地变更后刷新类型/处置选项并同步到已有介质组。
        /// </summary>
        public void RefreshOutboundDestinationDependentSettings()
        {
            RefreshAllowedElectronicMediaTypeOptions();
            RefreshElectronicDispositionOptions();
            ApplySelectedElectronicMediaSettingsToEntries();
            ApplyBorrowedHardDiskRegistrationVisibilityToEntries();
            foreach (MediaEntryViewModel media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
            {
                if (!ShowOutboundExternalHardDiskRequisitionFields && !ShowArchiveFilingHardDiskFields)
                {
                    media.UseInStockBlankHardDisk = false;
                    media.ClearOutboundRequisitionedDiskState();
                    media.ClearAppendTarget();
                }
                else if (ShowArchiveFilingHardDiskFields)
                {
                    media.RequisitionedDiskNeedReturn = false;
                    media.ExpectedReturnDate = null;
                    if (!media.IsArchiveFilingHardDiskScenario)
                    {
                        media.ClearAppendTarget();
                    }
                }
                else
                {
                    media.ClearAppendTarget();
                }

                media.NotifyOutboundHardDiskRequisitionPropertiesChanged();
                media.NotifyArchiveFilingHardDiskPropertiesChanged();
                RequestHardDiskCapacityRefresh(media);
            }

            RefreshOutboundItemStoragePaths();
        }

        /// <summary>
        /// 按当前解析器刷新可选电子介质类型。
        /// </summary>
        /// <remarks>
        /// 必须先记住当前/明细上的类型，再 Clear 选项集合：ComboBox 默认 TwoWay，
        /// Clear 时会把 SelectedItem 写成 null 并回写到本属性，进而经
        /// <see cref="ApplySelectedElectronicMediaSettingsToEntries"/> 把明细 MediaType
        /// 冲成空，随后又落到允许列表首项（出网外部离线为「光盘」）。
        /// </remarks>
        public void RefreshAllowedElectronicMediaTypeOptions()
        {
            var allowedTypes = GetAllowedElectronicMediaTypeOptions(_allElectronicMediaTypeOptions);

            // Clear 选项前先固化偏好，避免 WPF ComboBox 绑定在集合重建时冲掉已选类型。
            string preferredMediaType = MediaEntries
                .FirstOrDefault(RegisterMediaTreeSupport.IsDataElectronic)
                ?.MediaType?.Trim()
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(preferredMediaType))
            {
                preferredMediaType = SelectedElectronicMediaType?.Trim() ?? string.Empty;
            }

            BeginElectronicMediaSettingsSync();
            try
            {
                ApplyOptions(DataElectronicMediaTypeOptions, allowedTypes);

                string? matched = allowedTypes.FirstOrDefault(option =>
                    string.Equals(option, preferredMediaType, StringComparison.OrdinalIgnoreCase));
                SelectedElectronicMediaType = matched ?? (allowedTypes.FirstOrDefault() ?? string.Empty);
            }
            finally
            {
                EndElectronicMediaSettingsSync();
            }
        }

        private readonly List<string> _allElectronicMediaTypeOptions = new();

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
                // 增删介质行时 ListCollectionView 已随集合更新；勿 Refresh，以免重建卡片打乱 Radio。
                NotifyMediaViewSummaries();
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
            media.SetShowBorrowedHardDiskRegistrationUi(EnableRetainedHardDiskBorrowedRegistration);
            media.PropertyChanged += MediaEntry_PropertyChanged;
            media.Items.CollectionChanged += MediaItems_CollectionChanged;
            foreach (var item in media.Items)
            {
                ConfigureElectronicMediaItem(item);
            }

            RecalculateQuantities(media);
        }

        private void ApplyBorrowedHardDiskRegistrationVisibilityToEntries()
        {
            foreach (MediaEntryViewModel media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
            {
                media.SetShowBorrowedHardDiskRegistrationUi(EnableRetainedHardDiskBorrowedRegistration);
            }
        }

        private void DetachMediaEntry(MediaEntryViewModel media)
        {
            media.PropertyChanged -= MediaEntry_PropertyChanged;
            media.Items.CollectionChanged -= MediaItems_CollectionChanged;
            foreach (var item in media.Items)
            {
                DetachContentEntryQuantityHandler(item);
            }
        }

        private void MediaEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is MediaEntryViewModel capacityMedia
                && e.PropertyName is nameof(MediaEntryViewModel.RequisitionedHardDiskCode)
                    or nameof(MediaEntryViewModel.AppendTargetHardDiskCode)
                    or nameof(MediaEntryViewModel.UseInStockBlankHardDisk))
            {
                RequestHardDiskCapacityRefresh(capacityMedia);
            }

            // 介质卡片内 Bool Radio / 关联字段：禁止 CollectionView.Refresh，避免 GroupName 互斥把选中值打回。
            if (e.PropertyName == nameof(MediaEntryViewModel.IsBorrowedHardDisk)
                || e.PropertyName == nameof(MediaEntryViewModel.BorrowedHardDiskCode)
                || e.PropertyName == nameof(MediaEntryViewModel.UseInStockBlankHardDisk)
                || e.PropertyName == nameof(MediaEntryViewModel.RequisitionedDiskNeedReturn)
                || e.PropertyName == nameof(MediaEntryViewModel.RequisitionedHardDiskCode)
                || e.PropertyName == nameof(MediaEntryViewModel.AppendTargetHardDiskCode)
                || e.PropertyName == nameof(MediaEntryViewModel.ExpectedReturnDate))
            {
                if (e.PropertyName == nameof(MediaEntryViewModel.IsBorrowedHardDisk)
                    || e.PropertyName == nameof(MediaEntryViewModel.BorrowedHardDiskCode))
                {
                    OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
                    SyncBorrowedHardDiskSettingsFromSelections();
                }

                return;
            }

            if (e.PropertyName == nameof(MediaEntryViewModel.MediaKind))
            {
                ScheduleRefreshMediaViews();
                SyncElectronicMediaSettingsFromEntries();
                OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
                return;
            }

            if (e.PropertyName == nameof(MediaEntryViewModel.MediaType)
                || e.PropertyName == nameof(MediaEntryViewModel.Disposition)
                || e.PropertyName == nameof(MediaEntryViewModel.MediaCount))
            {
                if (RestrictRetainedHardDiskToBorrowedOnly
                    && e.PropertyName is nameof(MediaEntryViewModel.MediaType) or nameof(MediaEntryViewModel.Disposition)
                    && sender is MediaEntryViewModel retainedMedia
                    && retainedMedia.IsRetainedHardDiskScenario
                    && !retainedMedia.IsBorrowedHardDisk)
                {
                    retainedMedia.IsBorrowedHardDisk = true;
                }

                NotifyMediaViewSummaries();
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
                }
            }

            var media = FindMediaEntryByItemsCollection(sender);
            if (media != null)
            {
                RecalculateQuantities(media);
            }

            // 子项增删不影响 Filter，禁止 Refresh 以免重建介质卡片打乱 Radio。
            NotifyMediaViewSummaries();
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

        /// <summary>
        /// 仅更新数量汇总，不调用 CollectionView.Refresh。
        /// </summary>
        private void NotifyMediaViewSummaries()
        {
            OnPropertyChanged(nameof(DataElectronicMediaCount));
        }

        private void RefreshMediaViews()
        {
            DataElectronicMediaView.Refresh();
            NotifyMediaViewSummaries();
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
            entry.ResolveAppendTargetOption = ResolveAppendTargetOption;
            entry.SetShowBorrowedHardDiskRegistrationUi(EnableRetainedHardDiskBorrowedRegistration);
            entry.Items.Add(CreateDefaultElectronicMediaItem());
            MediaEntries.Add(entry);
            entry.SyncAppendTargetSelection(ArchiveFilingAppendTargets);
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

            media.Items.Add(CreateDefaultElectronicMediaItem());
            RefreshOutboundItemStoragePath(media.Items[^1]);
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
            vm.ResolveAppendTargetOption = ResolveAppendTargetOption;
            RecalculateQuantities(vm);
            return vm;
        }

        private ArchiveFilingAppendTargetOption? ResolveAppendTargetOption(int unitId) =>
            ArchiveFilingAppendTargets.FirstOrDefault(option => option.UnitId == unitId);

        private void EnsureElectronicMediaSelections()
        {
            BeginElectronicMediaSettingsSync();
            try
            {
                if (string.IsNullOrWhiteSpace(SelectedElectronicMediaType))
                {
                    ApplyDefaultSelectedElectronicMediaType();
                }

                if (string.IsNullOrWhiteSpace(SelectedElectronicDisposition))
                {
                    SelectedElectronicDisposition = GetAllowedElectronicDispositionOptions().FirstOrDefault() ?? string.Empty;
                }
            }
            finally
            {
                EndElectronicMediaSettingsSync();
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        /// <summary>
        /// 优先沿用已有介质组上的类型，避免域值排序首项（U盘）覆盖用户已选光盘/硬盘。
        /// </summary>
        private void ApplyDefaultSelectedElectronicMediaType()
        {
            IReadOnlyList<string> allowedTypes = DataElectronicMediaTypeOptions.Count > 0
                ? DataElectronicMediaTypeOptions.ToList()
                : GetAllowedElectronicMediaTypeOptions(_allElectronicMediaTypeOptions);

            string? entryMediaType = MediaEntries
                .FirstOrDefault(RegisterMediaTreeSupport.IsDataElectronic)
                ?.MediaType?.Trim();
            if (!string.IsNullOrWhiteSpace(entryMediaType))
            {
                string? entryMatched = allowedTypes.FirstOrDefault(option =>
                    string.Equals(option, entryMediaType, StringComparison.OrdinalIgnoreCase));
                SelectedElectronicMediaType = entryMatched ?? entryMediaType;
                return;
            }

            SelectedElectronicMediaType = allowedTypes.FirstOrDefault() ?? string.Empty;
        }

        private void SyncElectronicMediaSettingsFromEntries()
        {
            var firstElectronicMedia = MediaEntries.FirstOrDefault(RegisterMediaTreeSupport.IsDataElectronic);

            BeginElectronicMediaSettingsSync();
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
                EndElectronicMediaSettingsSync();
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        private void ApplySelectedElectronicMediaSettingsToEntries()
        {
            if (IsSyncingElectronicMediaSettings)
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
            RefreshOutboundItemStoragePaths();
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

            string preferredDisposition = MediaEntries
                .FirstOrDefault(RegisterMediaTreeSupport.IsDataElectronic)
                ?.Disposition?.Trim()
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(preferredDisposition))
            {
                preferredDisposition = SelectedElectronicDisposition?.Trim() ?? string.Empty;
            }

            BeginElectronicMediaSettingsSync();
            try
            {
                ApplyOptions(DataElectronicDispositionOptions, allowedOptions);

                string? canon = allowedOptions.FirstOrDefault(o =>
                    string.Equals(o, preferredDisposition, StringComparison.OrdinalIgnoreCase));
                SelectedElectronicDisposition = canon ?? (allowedOptions.FirstOrDefault() ?? string.Empty);
            }
            finally
            {
                EndElectronicMediaSettingsSync();
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        private IReadOnlyList<string> GetAllowedElectronicMediaTypeOptions(IReadOnlyCollection<string> allMediaTypeOptions)
        {
            if (AllowedMediaTypeOptionsResolver != null)
            {
                return AllowedMediaTypeOptionsResolver(allMediaTypeOptions);
            }

            return ArchiveRegisterBusinessRules.FilterManualSelectableElectronicMediaTypes(allMediaTypeOptions);
        }

        private void RequestHardDiskCapacityRefresh(MediaEntryViewModel media)
        {
            _pageDb.RunInBackground(() => RefreshHardDiskCapacityDescriptionAsync(media));
        }

        private async Task RefreshHardDiskCapacityDescriptionAsync(MediaEntryViewModel media)
        {
            string diskCode = ResolveHardDiskCapacityDiskCode(media);
            if (string.IsNullOrWhiteSpace(diskCode))
            {
                media.HardDiskCapacityDescription = string.Empty;
                return;
            }

            string description;
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                var filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                description = await HardDiskCapacityDescriptionLoader.LoadAsync(filing, diskCode)
                    .ConfigureAwait(true);
            }

            if (string.Equals(ResolveHardDiskCapacityDiskCode(media), diskCode, StringComparison.Ordinal))
            {
                media.HardDiskCapacityDescription = description;
            }
        }

        private string ResolveHardDiskCapacityDiskCode(MediaEntryViewModel media)
        {
            if (!ShowOutboundExternalHardDiskRequisitionFields && !ShowArchiveFilingHardDiskFields)
            {
                return string.Empty;
            }

            if (media.OutboundBlankHardDiskFieldsVisibility || media.ArchiveFilingBlankDiskFieldsVisibility)
            {
                return media.RequisitionedHardDiskCode?.Trim() ?? string.Empty;
            }

            if (media.ArchiveFilingAppendTargetVisibility)
            {
                return media.AppendTargetHardDiskCode?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        private bool CanPickOutboundBlankHardDisk(MediaEntryViewModel? media) =>
            CanEditForm
            && media != null
            && (media.OutboundBlankHardDiskFieldsVisibility || media.ArchiveFilingBlankDiskFieldsVisibility);

        private void PickOutboundBlankHardDisk(MediaEntryViewModel? media)
        {
            if (!CanPickOutboundBlankHardDisk(media) || media == null)
            {
                return;
            }

            var selectedMedia = _dialogService.ShowHardDiskMediumSelectionDialog(
                string.IsNullOrWhiteSpace(media.RequisitionedHardDiskCode)
                    ? null
                    : new[] { media.RequisitionedHardDiskCode },
                currentElectronicArchiveUnitId: null,
                selectionMode: "BlankTarget");

            if (selectedMedia == null || selectedMedia.Count == 0)
            {
                return;
            }

            if (selectedMedia.Count > 1)
            {
                _dialogService.ShowMessage("请只选择一块库内空盘。", "提示");
                return;
            }

            media.ApplyOutboundRequisitionedDisk(
                selectedMedia[0].Id,
                selectedMedia[0].DiskCode,
                needReturn: media.IsExternalOfflineReturnedHardDiskScenario);
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
                _pageDb.RunInBackground(RefreshUserBorrowedHardDiskCodesAsync);
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
                    IReadOnlyList<string> codes;
                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        var hardDiskMedia = scope.ServiceProvider.GetRequiredService<IHardDiskMediaService>();
                        codes = await hardDiskMedia.GetCurrentUserBorrowedHardDiskCodesAsync(user)
                            .ConfigureAwait(true);
                    }

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
                    _dialogService.ShowError("加载借出硬盘列表失败: " + ex.GetBaseException().Message);
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

        private MediaItemViewModel CreateDefaultElectronicMediaItem()
        {
            var item = new MediaItemViewModel
            {
                MaterialCategory = ElectronicMaterialCategoryOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument,
                DataOrganizationForm = ElectronicDataOrganizationFormOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                ConfidentialLevel = ConfidentialLevelOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ConfidentialLevelNone,
                SourceType = GetDefaultSourceType()
            };
            ApplyDefaultProvideUnitForInternalItem(item, onlyWhenEmpty: true);

            ConfigureElectronicMediaItem(item);
            RefreshOutboundItemStoragePath(item);
            return item;
        }

        private string GetDefaultSourceType() =>
            SourceTypeOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.SourceTypeInternal;

        private string ResolveSourceTypeFromRecord(string? storedSourceType)
        {
            string normalized = storedSourceType?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return GetDefaultSourceType();
            }

            string? matched = SourceTypeOptions.FirstOrDefault(option =>
                string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase));
            return matched ?? GetDefaultSourceType();
        }

        /// <summary>
        /// 资料来源非「外来」时，为空白的提供单位填入默认值（当前用户部门，缺省为资料室）。
        /// </summary>
        private void ApplyDefaultProvideUnitForInternalItem(MediaItemViewModel item, bool onlyWhenEmpty)
        {
            if (item == null || item.IsExternalSource)
            {
                return;
            }

            if (onlyWhenEmpty && !string.IsNullOrWhiteSpace(item.ProvideUnit))
            {
                return;
            }

            string applicantDept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(applicantDept))
            {
                applicantDept = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom;
            }

            item.ProvideUnit = applicantDept;
        }

        private void ConfigureElectronicMediaItem(MediaItemViewModel item)
        {
            item.SubCategoryOptionsRefreshHandler = RefreshElectronicSubCategoryOptions;
            item.StoragePathRefreshHandler = RefreshOutboundItemStoragePath;
            item.TreatContentMetricsAsUnknown = !AllowElectronicContentScan;
            item.SourceType = ResolveSourceTypeFromRecord(item.SourceType);
            ApplyDefaultProvideUnitForInternalItem(item, onlyWhenEmpty: true);
            RefreshElectronicSubCategoryOptions(item);
            AttachContentEntryQuantityHandler(item);
        }

        private void ApplyElectronicContentScanAvailabilityToItems()
        {
            foreach (MediaItemViewModel item in MediaEntries.SelectMany(entry => entry.Items))
            {
                item.TreatContentMetricsAsUnknown = !AllowElectronicContentScan;
            }
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

            ComboBoxSelectionPreserveSupport.RefreshBoundStringOptions(
                item.AvailableSubCategories,
                options,
                () => item.SubCategory,
                value => item.SubCategory = value,
                fallbackToFirst: false,
                clearWhenInvalid: true);
        }

        private static ElectronicMediaItemEntryViewModel CreateContentEntryViewModel(ElectronicMediaContentScanEntry entry)
        {
            return new ElectronicMediaItemEntryViewModel
            {
                EntryKind = entry.EntryKind,
                EntryName = entry.EntryName,
                SizeMb = entry.SizeMb,
                CreatedAt = entry.CreatedAt,
                ModifiedAt = entry.ModifiedAt
            };
        }

        private void ApplyScanResult(
            MediaItemViewModel item,
            ElectronicMediaContentScanResult result,
            IReadOnlyList<string>? scannedDirectoryPaths = null)
        {
            item.StorageRootFullPath = result.RootPath;
            ApplyScannedStoragePath(item, result.RootPath);
            item.ContentEntries.Clear();
            foreach (var entry in result.Entries)
            {
                item.ContentEntries.Add(CreateContentEntryViewModel(entry));
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
                    || (!string.IsNullOrWhiteSpace(item.StorageRootFullPath)
                        && Directory.Exists(item.StorageRootFullPath));
            }

            return false;
        }

        private async Task PickFolderAndScanElectronicContentAsync(MediaItemViewModel? item)
        {
            if (item == null || !item.IsDirectoryOrganizationForm)
            {
                return;
            }

            string? folder = _dialogService.PickFolder("选择子项父目录（扫描该目录下的文件与一级子目录，支持选择盘符）");
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            if (ElectronicMediaItemSupport.IsDriveRoot(folder)
                && !_dialogService.ShowConfirm(
                    $"所选父目录为盘符根目录 {folder}，扫描将递归统计整盘数据量，可能耗时较长。是否继续？"))
            {
                return;
            }

            if (item.HasScannedEntries
                && !_dialogService.ShowConfirm("重新选择子项父目录将覆盖当前已扫描的目录/文件明细，是否继续？"))
            {
                return;
            }

            if (!TryEnsureArchiveFilingScannedPathAllowed(item, folder, out string pathError))
            {
                _dialogService.ShowError(pathError);
                return;
            }

            await ScanDirectoriesContentAsync(item, [folder]);
        }

        private async Task RescanElectronicContentAsync(MediaItemViewModel? item)
        {
            if (item == null || !CanRescanElectronicContent(item))
            {
                return;
            }

            var directories = ResolveScannedDirectoryPaths(item);
            if (directories.Count == 0)
            {
                _dialogService.ShowMessage("原子项父目录已不存在，请重新选择目录。");
                return;
            }

            string storageRoot = string.IsNullOrWhiteSpace(item.StorageRootFullPath)
                ? directories[0]
                : item.StorageRootFullPath;
            if (!TryEnsureArchiveFilingScannedPathAllowed(item, storageRoot, out string pathError))
            {
                _dialogService.ShowError(pathError);
                return;
            }

            await ScanDirectoriesContentAsync(
                item,
                directories,
                string.IsNullOrWhiteSpace(item.StorageRootFullPath) ? null : item.StorageRootFullPath);
        }

        private async Task ScanDirectoriesContentAsync(MediaItemViewModel item, IReadOnlyList<string> folders, string? storageRootDirectory = null)
        {
            _dialogService.SetBusyState(true);
            try
            {
                var result = await Task.Run(() => _electronicMediaContentScanService.ScanDirectories(folders, storageRootDirectory));
                if (!TryEnsureArchiveFilingScannedPathAllowed(item, result.RootPath, out string pathError))
                {
                    _dialogService.ShowError(pathError);
                    return;
                }

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

            if (!string.IsNullOrWhiteSpace(item.StorageRootFullPath) && Directory.Exists(item.StorageRootFullPath))
            {
                return [item.StorageRootFullPath];
            }

            return new List<string>();
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

            if (string.IsNullOrWhiteSpace(item.StorageRootFullPath) || string.IsNullOrWhiteSpace(entry.EntryName))
            {
                return "-";
            }

            string fullPath = Path.GetFullPath(Path.Combine(item.StorageRootFullPath, entry.EntryName));
            return ElectronicMediaItemSupport.FormatModifiedDate(
                ElectronicMediaItemSupport.ResolveEntryCreatedAt(fullPath, entry.EntryKind));
        }

        private static string FormatEntryModifiedDate(MediaItemViewModel item, ElectronicMediaItemEntryViewModel entry)
        {
            if (entry.ModifiedAt.HasValue)
            {
                return ElectronicMediaItemSupport.FormatModifiedDate(entry.ModifiedAt);
            }

            if (string.IsNullOrWhiteSpace(item.StorageRootFullPath) || string.IsNullOrWhiteSpace(entry.EntryName))
            {
                return "-";
            }

            string fullPath = Path.GetFullPath(Path.Combine(item.StorageRootFullPath, entry.EntryName));
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

        private static void ApplyOptions(ObservableCollection<string> target, IReadOnlyCollection<string> values) =>
            ComboBoxSelectionPreserveSupport.ReplaceStringOptions(target, values);
    }
}
