using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed partial class ArchiveElectronicRelocationViewModel : ViewModelBase
    {
        private readonly IArchiveRelocationService _relocationService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IProjectService _projectService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly ICabinetService _cabinetService;
        private readonly IArchiveFilingService _filingService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly List<Cabinet> _magneticCabinets = new();

        private ArchiveRelocationContainerSummary? _sourceSummary;
        private string _selectedRelocationMode = ArchiveRelocationMode.PhysicalMove;
        private string _selectedYear = string.Empty;
        private int? _selectedProjectId;
        private ArchiveRelocationSourceOption? _selectedSourceOption;
        private string _remarks = string.Empty;
        private string _previewText = string.Empty;
        private bool _hasExecutablePreview;
        private ArchiveRelocationTargetOption? _selectedTarget;
        private ArchiveRelocationContainerSummary? _targetSummary;
        private HardDiskMediaReturnTargetLocationOption? _selectedSourceHardDiskReturnLocationOption;
        private string _selectedBlankHardDiskCode = string.Empty;
        private int? _selectedBlankHardDiskMediumId;
        private bool _confirmHardDiskFormatted;
        private bool _confirmOpticalDiscDestroyed;
        private bool _executeBackupMechanism;
        private bool _isBusy;
        private bool _isInitialized;
        private string _relocationNo = "待编单";
        private string _statusDisplay = "待办理";

        public ArchiveElectronicRelocationViewModel(
            IArchiveRelocationService relocationService,
            IArchiveRegisterService archiveRegisterService,
            IProjectService projectService,
            IUserContextService userContextService,
            IDialogService dialogService,
            ICabinetService cabinetService,
            IArchiveFilingService filingService,
            IHardDiskMediaService hardDiskMediaService)
        {
            _relocationService = relocationService;
            _archiveRegisterService = archiveRegisterService;
            _projectService = projectService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            _cabinetService = cabinetService;
            _filingService = filingService;
            _hardDiskMediaService = hardDiskMediaService;

            RelocationModes =
            [
                new RelocationModeOption("迁入其他档口", ArchiveRelocationMode.PhysicalMove),
                new RelocationModeOption("迁入空白硬盘", ArchiveRelocationMode.MoveToBlankHardDisk),
                new RelocationModeOption("迁入空白光盘", ArchiveRelocationMode.MoveToBlankOpticalDisc),
                new RelocationModeOption("并入同项目硬盘", ArchiveRelocationMode.MergeToExisting)
            ];

            Items = new ObservableCollection<ArchiveRelocationItemSummary>();
            ItemDetailsPanel = new ItemDetailsListPresenter<ArchiveRelocationItemSummary>(
                "迁档资料明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.ItemName,
                    "暂无迁档资料"));
            TargetOptions = new ObservableCollection<ArchiveRelocationTargetOption>();
            SourceOptions = new ObservableCollection<ArchiveRelocationSourceOption>();
            SourceHardDiskReturnLocationOptions = new ObservableCollection<HardDiskMediaReturnTargetLocationOption>();

            RefreshTargetsCommand = new RelayCommand(
                async _ => await RefreshTargetsAsync(),
                _ => !IsBusy && SourceSummary != null && IsMergeMode);
            SelectBlankHardDiskCommand = new RelayCommand(_ => SelectBlankHardDisk(), _ => !IsBusy && IsMoveToBlankHardDiskMode);
            RecommendTargetSlotCommand = new RelayCommand(
                async _ => await RecommendTargetSlotAsync(),
                _ => CanRecommendTargetSlot);
            ShowTargetSlotSnapshotCommand = new RelayCommand(
                _ => ShowTargetSlotSnapshot(),
                _ => CanShowTargetSlotSnapshot);
            ShowSourceHardDiskReturnSlotSnapshotCommand = new RelayCommand(
                _ => ShowSourceHardDiskReturnSlotSnapshot(),
                _ => CanShowSourceHardDiskReturnSlotSnapshot);
            PreviewCommand = new RelayCommand(async _ => await PreviewAsync(), _ => !IsBusy && IsArchiveAdmin && SourceSummary != null);
            ExecuteCommand = new RelayCommand(
                async _ => await ExecuteAsync(),
                _ => !IsBusy && IsArchiveAdmin && SourceSummary != null && HasExecutablePreview);
        }

        /// <summary>页眉标题（流程名 · 迁档编号 · 状态），对齐资料立档编辑窗。</summary>
        public string PageTitle => $"电子介质资料迁档 · {RelocationNo} · {StatusDisplay}";

        /// <summary>页眉说明文案。</summary>
        public string PageSubtitle =>
            "请选择迁档对象与目标，预览确认后执行。迁档无需审批，提交后立即生效；单号在执行时正式落库。";

        /// <summary>当前展示的迁档编号（待办理为预览下一号；已完成为实发单号）。</summary>
        public string RelocationNo
        {
            get => _relocationNo;
            private set
            {
                if (SetProperty(ref _relocationNo, value))
                {
                    OnPropertyChanged(nameof(PageTitle));
                }
            }
        }

        /// <summary>迁档办理状态展示。</summary>
        public string StatusDisplay
        {
            get => _statusDisplay;
            private set
            {
                if (SetProperty(ref _statusDisplay, value))
                {
                    OnPropertyChanged(nameof(PageTitle));
                }
            }
        }

        public ObservableCollection<string> Years { get; } = new();

        public ObservableCollection<ProjectFilterOption> ProjectOptions { get; } = new();

        public ObservableCollection<RelocationModeOption> RelocationModes { get; }

        public ObservableCollection<ArchiveRelocationItemSummary> Items { get; }

        public ItemDetailsListPresenter<ArchiveRelocationItemSummary> ItemDetailsPanel { get; }

        public ObservableCollection<ArchiveRelocationTargetOption> TargetOptions { get; }

        public ObservableCollection<ArchiveRelocationSourceOption> SourceOptions { get; }

        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> SourceHardDiskReturnLocationOptions { get; }

        public RelayCommand RefreshTargetsCommand { get; }

        public RelayCommand SelectBlankHardDiskCommand { get; }

        public RelayCommand RecommendTargetSlotCommand { get; }

        public RelayCommand ShowTargetSlotSnapshotCommand { get; }

        public RelayCommand ShowSourceHardDiskReturnSlotSnapshotCommand { get; }

        public RelayCommand PreviewCommand { get; }

        public RelayCommand ExecuteCommand { get; }

        public bool IsArchiveAdmin => _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool IsPhysicalMode => SelectedRelocationMode == ArchiveRelocationMode.PhysicalMove;

        public bool IsMoveToBlankHardDiskMode => ArchiveRelocationMode.IsMoveToBlankHardDisk(SelectedRelocationMode);

        public bool IsMoveToBlankOpticalDiscMode => ArchiveRelocationMode.IsMoveToBlankOpticalDisc(SelectedRelocationMode);

        public bool IsMoveToBlankCarrierMode => ArchiveRelocationMode.IsMoveToBlankCarrier(SelectedRelocationMode);

        public bool IsMergeMode => SelectedRelocationMode == ArchiveRelocationMode.MergeToExisting;

        public bool ShowTargetSlotSelector => IsPhysicalMode || IsMoveToBlankCarrierMode;

        public bool SupportsBackupMechanism => IsMoveToBlankCarrierMode || IsMergeMode;

        /// <summary>迁档并处置原介质（与备份互斥，默认选中）。</summary>
        public bool IsRelocateWithDispositionSelected
        {
            get => !ExecuteBackupMechanism;
            set
            {
                if (value)
                {
                    ExecuteBackupMechanism = false;
                }
            }
        }

        /// <summary>保留原件，仅登记备份副本（与处置迁档互斥）。</summary>
        public bool IsBackupMechanismSelected
        {
            get => ExecuteBackupMechanism;
            set
            {
                if (value)
                {
                    ExecuteBackupMechanism = true;
                }
            }
        }

        public bool IsContainerMode => IsMergeMode;

        public bool IsPhysicalMoveModeSelected
        {
            get => SelectedRelocationMode == ArchiveRelocationMode.PhysicalMove;
            set
            {
                if (value)
                {
                    SelectedRelocationMode = ArchiveRelocationMode.PhysicalMove;
                }
            }
        }

        public bool IsMoveToBlankHardDiskModeSelected
        {
            get => SelectedRelocationMode == ArchiveRelocationMode.MoveToBlankHardDisk;
            set
            {
                if (value)
                {
                    SelectedRelocationMode = ArchiveRelocationMode.MoveToBlankHardDisk;
                }
            }
        }

        public bool IsMoveToBlankOpticalDiscModeSelected
        {
            get => SelectedRelocationMode == ArchiveRelocationMode.MoveToBlankOpticalDisc;
            set
            {
                if (value)
                {
                    SelectedRelocationMode = ArchiveRelocationMode.MoveToBlankOpticalDisc;
                }
            }
        }

        public bool IsMergeToExistingModeSelected
        {
            get => SelectedRelocationMode == ArchiveRelocationMode.MergeToExisting;
            set
            {
                if (value)
                {
                    SelectedRelocationMode = ArchiveRelocationMode.MergeToExisting;
                }
            }
        }

        public bool RequiresHardDiskConfirmation =>
            !ExecuteBackupMechanism
            && (IsMoveToBlankCarrierMode || IsMergeMode)
            && SourceSummary != null
            && SourceSummary.StorageCarrierType.Contains("硬盘", StringComparison.Ordinal);

        public bool RequiresOpticalDiscConfirmation =>
            !ExecuteBackupMechanism
            && (IsMoveToBlankCarrierMode || IsMergeMode)
            && SourceSummary != null
            && SourceSummary.StorageCarrierType.Contains("光盘", StringComparison.Ordinal);

        public bool RequiresHardDiskReturnLocation =>
            !ExecuteBackupMechanism
            && (IsMoveToBlankCarrierMode || IsMergeMode)
            && RequiresHardDiskConfirmation;

        public bool ShowDispositionConfirmations =>
            !ExecuteBackupMechanism
            && (IsMoveToBlankCarrierMode || IsMergeMode)
            && SourceSummary != null
            && (RequiresHardDiskConfirmation || RequiresOpticalDiscConfirmation);

        public bool CanShowSourceHardDiskReturnSlotSnapshot =>
            RequiresHardDiskReturnLocation
            && SelectedSourceHardDiskReturnLocationOption != null
            && ArchiveSlotLocationSupport.TryParseSlotLocation(
                SelectedSourceHardDiskReturnLocationOption.Location,
                out _,
                out _,
                out _,
                out _);

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanRecommendTargetSlot));
                }
            }
        }

        public string SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (!SetProperty(ref _selectedYear, value))
                {
                    return;
                }

                LoadProjectOptions();
                _ = ReloadSourceOptionsAsync();
            }
        }

        public int? SelectedProjectId
        {
            get => _selectedProjectId;
            set
            {
                if (!SetProperty(ref _selectedProjectId, value))
                {
                    return;
                }

                _ = ReloadSourceOptionsAsync();
            }
        }

        public ArchiveRelocationSourceOption? SelectedSourceOption
        {
            get => _selectedSourceOption;
            set
            {
                if (!SetProperty(ref _selectedSourceOption, value))
                {
                    return;
                }

                _ = ApplySelectedSourceAsync();
            }
        }

        public string SelectedRelocationMode
        {
            get => _selectedRelocationMode;
            set
            {
                if (SetProperty(ref _selectedRelocationMode, value))
                {
                    OnPropertyChanged(nameof(IsPhysicalMode));
                    OnPropertyChanged(nameof(IsMoveToBlankHardDiskMode));
                    OnPropertyChanged(nameof(IsMoveToBlankOpticalDiscMode));
                    OnPropertyChanged(nameof(IsMoveToBlankCarrierMode));
                    OnPropertyChanged(nameof(IsMergeMode));
                    OnPropertyChanged(nameof(IsContainerMode));
                    OnPropertyChanged(nameof(ShowTargetSlotSelector));
                    OnPropertyChanged(nameof(SupportsBackupMechanism));
                    OnPropertyChanged(nameof(SourceSummaryText));
                    OnPropertyChanged(nameof(ShowSourceCurrentLocation));
                    OnPropertyChanged(nameof(RequiresHardDiskConfirmation));
                    OnPropertyChanged(nameof(RequiresOpticalDiscConfirmation));
                    OnPropertyChanged(nameof(RequiresHardDiskReturnLocation));
                    OnPropertyChanged(nameof(ShowDispositionConfirmations));
                    NotifyRelocationModeRadioProperties();
                    OnPropertyChanged(nameof(TargetLocationPanelTitle));
                    OnPropertyChanged(nameof(TargetSlotRecommendHintText));
                    OnPropertyChanged(nameof(CanRecommendTargetSlot));
                    OnPropertyChanged(nameof(TargetSlotValidationMessage));
                    PreviewText = string.Empty;
                    SelectedBlankHardDiskCode = string.Empty;
                    SelectedBlankHardDiskMediumId = null;
                    _ = RefreshTargetsAsync();
                    RefreshDisplayItems();
                    if (SourceSummary != null && (IsPhysicalMode || IsMoveToBlankCarrierMode))
                    {
                        _ = ResetTargetSlotSelectionAsync(preferSourceLocation: ShouldPreferSourceTargetSlot);
                    }
                    else
                    {
                        ClearTargetSlotSelection();
                    }

                    if (SourceSummary != null)
                    {
                        _ = RefreshSourceHardDiskReturnLocationOptionsAsync();
                    }
                }
            }
        }

        public ArchiveRelocationContainerSummary? SourceSummary
        {
            get => _sourceSummary;
            private set
            {
                if (SetProperty(ref _sourceSummary, value))
                {
                    OnPropertyChanged(nameof(HasSource));
                    OnPropertyChanged(nameof(SourceSummaryText));
                    OnPropertyChanged(nameof(SourceCurrentLocation));
                    OnPropertyChanged(nameof(ShowSourceCurrentLocation));
                    OnPropertyChanged(nameof(SourceLinkedMediumCodesDisplayText));
                    OnPropertyChanged(nameof(SourceItemsDescriptionText));
                    OnPropertyChanged(nameof(RequiresHardDiskConfirmation));
                    OnPropertyChanged(nameof(RequiresOpticalDiscConfirmation));
                    OnPropertyChanged(nameof(RequiresHardDiskReturnLocation));
                    OnPropertyChanged(nameof(ShowDispositionConfirmations));
                }
            }
        }

        public bool HasSource => SourceSummary != null;

        public string SourceSummaryText => SourceSummary == null
            ? "请选择年度、项目及源电子介质袋"
            : IsPhysicalMode
                ? $"{SourceSummary.ContainerCode} | {SourceSummary.ProjectName} | {SourceSummary.Year} | {SourceSummary.StorageCarrierType} | {SourceSummary.ItemCount} 项"
                : $"{SourceSummary.ContainerCode} | {SourceSummary.StorageLocation} | {SourceSummary.ProjectName} | {SourceSummary.Year} | {SourceSummary.StorageCarrierType} | {SourceSummary.ItemCount} 项";

        public string SourceCurrentLocation => SourceSummary?.StorageLocation?.Trim() ?? string.Empty;

        public bool ShowSourceCurrentLocation =>
            IsPhysicalMode && HasSource && !string.IsNullOrWhiteSpace(SourceCurrentLocation);

        public string SourceLinkedMediumCodesDisplayText => SourceSummary == null
            ? string.Empty
            : string.IsNullOrWhiteSpace(SourceSummary.ActiveLinkedMediumCode)
                ? "无关联硬盘"
                : SourceSummary.ActiveLinkedMediumCode;

        public string SourceItemsDescriptionText => SourceSummary == null
            ? string.Empty
            : ArchiveRelocationSourceDescriptionBuilder.BuildItemsDescription(SourceSummary.Items);

        public ArchiveRelocationTargetOption? SelectedTarget
        {
            get => _selectedTarget;
            set
            {
                if (!SetProperty(ref _selectedTarget, value))
                {
                    return;
                }

                _ = ApplySelectedTargetAsync();
            }
        }

        public ArchiveRelocationContainerSummary? TargetSummary
        {
            get => _targetSummary;
            private set
            {
                if (SetProperty(ref _targetSummary, value))
                {
                    OnPropertyChanged(nameof(HasSelectedTarget));
                    OnPropertyChanged(nameof(TargetSummaryText));
                    OnPropertyChanged(nameof(TargetLinkedMediumCodesDisplayText));
                    OnPropertyChanged(nameof(TargetItemsDescriptionText));
                    OnPropertyChanged(nameof(ShowTargetDescription));
                    OnPropertyChanged(nameof(ItemsSectionHeader));
                    OnPropertyChanged(nameof(ShowItemsEmptyHint));
                }
            }
        }

        public bool HasSelectedTarget => TargetSummary != null;

        public bool ShowTargetDescription => IsMergeMode && HasSelectedTarget;

        public string TargetSummaryText => TargetSummary == null
            ? string.Empty
            : $"{TargetSummary.ContainerCode} | {TargetSummary.StorageLocation} | {TargetSummary.ProjectName} | {TargetSummary.Year} | {TargetSummary.StorageCarrierType} | {TargetSummary.ItemCount} 项";

        public string TargetLinkedMediumCodesDisplayText => TargetSummary == null
            ? string.Empty
            : string.IsNullOrWhiteSpace(TargetSummary.ActiveLinkedMediumCode)
                ? "无关联硬盘"
                : TargetSummary.ActiveLinkedMediumCode;

        public string TargetItemsDescriptionText => TargetSummary == null
            ? string.Empty
            : ArchiveRelocationSourceDescriptionBuilder.BuildItemsDescription(TargetSummary.Items);

        public string ItemsSectionHeader =>
            ExecuteBackupMechanism && (IsMoveToBlankCarrierMode || IsMergeMode)
                ? "4. 资料清单（本次备份副本）"
                : IsMergeMode && HasSelectedTarget
                    ? "4. 资料清单（并档后）"
                    : "4. 资料清单";

        public bool ShowItemsEmptyHint =>
            IsMergeMode && SourceSummary != null && !HasSelectedTarget;

        public string ItemsEmptyHintText => ExecuteBackupMechanism
            ? "请选择目标硬盘袋后，此处将显示本次备份迁入的资料子项。"
            : "请选择目标硬盘袋后，此处将显示并档后的资料子项清单。";

        public string SelectedBlankHardDiskCode
        {
            get => _selectedBlankHardDiskCode;
            private set => SetProperty(ref _selectedBlankHardDiskCode, value);
        }

        public int? SelectedBlankHardDiskMediumId
        {
            get => _selectedBlankHardDiskMediumId;
            private set => SetProperty(ref _selectedBlankHardDiskMediumId, value);
        }

        public HardDiskMediaReturnTargetLocationOption? SelectedSourceHardDiskReturnLocationOption
        {
            get => _selectedSourceHardDiskReturnLocationOption;
            set
            {
                if (SetProperty(ref _selectedSourceHardDiskReturnLocationOption, value))
                {
                    OnPropertyChanged(nameof(CanShowSourceHardDiskReturnSlotSnapshot));
                }
            }
        }

        public bool ConfirmHardDiskFormatted
        {
            get => _confirmHardDiskFormatted;
            set
            {
                if (SetProperty(ref _confirmHardDiskFormatted, value))
                {
                    PreviewText = string.Empty;
                }
            }
        }

        public bool ConfirmOpticalDiscDestroyed
        {
            get => _confirmOpticalDiscDestroyed;
            set
            {
                if (SetProperty(ref _confirmOpticalDiscDestroyed, value))
                {
                    PreviewText = string.Empty;
                }
            }
        }

        public bool ExecuteBackupMechanism
        {
            get => _executeBackupMechanism;
            set
            {
                if (!SetProperty(ref _executeBackupMechanism, value))
                {
                    return;
                }

                if (value)
                {
                    ConfirmHardDiskFormatted = false;
                    ConfirmOpticalDiscDestroyed = false;
                    SelectedSourceHardDiskReturnLocationOption = null;
                }

                OnPropertyChanged(nameof(RequiresHardDiskConfirmation));
                OnPropertyChanged(nameof(RequiresOpticalDiscConfirmation));
                OnPropertyChanged(nameof(RequiresHardDiskReturnLocation));
                OnPropertyChanged(nameof(ShowDispositionConfirmations));
                OnPropertyChanged(nameof(IsRelocateWithDispositionSelected));
                OnPropertyChanged(nameof(IsBackupMechanismSelected));
                PreviewText = string.Empty;
                RefreshDisplayItems();
            }
        }

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        public string PreviewText
        {
            get => _previewText;
            private set
            {
                if (!SetProperty(ref _previewText, value))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    HasExecutablePreview = false;
                }
            }
        }

        /// <summary>最近一次预览是否可执行（失败/未预览时「确认迁档」不可用）。</summary>
        public bool HasExecutablePreview
        {
            get => _hasExecutablePreview;
            private set => SetProperty(ref _hasExecutablePreview, value);
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            if (!IsArchiveAdmin)
            {
                _dialogService.ShowMessage("仅资料管理员可执行资料迁档。", "权限不足");
            }

            await LoadYearsAsync();
            await LoadMagneticCabinetsAsync();
            await RefreshPendingRelocationNoAsync();
        }

        private async Task RefreshPendingRelocationNoAsync()
        {
            try
            {
                RelocationNo = await _relocationService.PeekNextRelocationNoAsync(
                    ArchiveRegisterDomainValues.MediaKindElectronic);
                StatusDisplay = "待办理";
            }
            catch
            {
                RelocationNo = "待编单";
                StatusDisplay = "待办理";
            }
        }

        private async Task LoadMagneticCabinetsAsync()
        {
            try
            {
                var allCabinets = await _cabinetService.GetAllCabinetsAsync();
                _magneticCabinets.Clear();
                _magneticCabinets.AddRange(CabinetSelectionSupport.BuildElectronicMagneticCabinetItems(allCabinets));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载防磁磁盘柜失败：{ex.Message}", "错误");
            }
        }

        private async Task LoadYearsAsync()
        {
            try
            {
                var yearsList = await _archiveRegisterService.GetExistingYearsAsync();
                Years.Clear();
                foreach (int year in yearsList)
                {
                    Years.Add(year.ToString());
                }

                if (Years.Count == 0)
                {
                    Years.Add(DateTime.Now.Year.ToString());
                }

                SelectedYear = Years.Contains(DateTime.Now.Year.ToString())
                    ? DateTime.Now.Year.ToString()
                    : Years[0];
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载年份失败：{ex.Message}", "错误");
            }
        }

        private void LoadProjectOptions()
        {
            try
            {
                ProjectOptions.Clear();
                foreach (var project in _projectService.SearchProjects(SelectedYear, keyword: null)
                             .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.ProjectName))
                             .OrderBy(item => item.ProjectName))
                {
                    ProjectOptions.Add(new ProjectFilterOption
                    {
                        Id = project.Id,
                        Name = project.ProjectName.Trim()
                    });
                }

                SelectedProjectId = ProjectOptions.FirstOrDefault()?.Id;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载项目列表失败：{ex.Message}", "错误");
            }
        }

        private async Task ReloadSourceOptionsAsync()
        {
            SelectedSourceOption = null;

            string? projectName = ResolveSelectedProjectName();
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(SelectedYear))
            {
                return;
            }

            try
            {
                IsBusy = true;
                var options = await _relocationService.GetElectronicSourceOptionsAsync(projectName, SelectedYear);
                SourceOptions.Clear();
                foreach (var option in options)
                {
                    SourceOptions.Add(option);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "加载源电子介质袋失败");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApplySelectedSourceAsync()
        {
            if (SelectedSourceOption == null)
            {
                ClearSourceState();
                return;
            }

            try
            {
                IsBusy = true;
                var summary = await _relocationService.LoadElectronicSourceByIdAsync(SelectedSourceOption.ContainerId);
                if (summary == null)
                {
                    ClearSourceState();
                    _dialogService.ShowMessage("未找到对应电子介质袋，请重新选择。", "提示");
                    return;
                }

                SourceSummary = summary;
                TargetSummary = null;
                SelectedTarget = null;
                ExecuteBackupMechanism = false;
                ConfirmHardDiskFormatted = false;
                ConfirmOpticalDiscDestroyed = false;
                SelectedBlankHardDiskCode = string.Empty;
                SelectedBlankHardDiskMediumId = null;
                PreviewText = string.Empty;
                await RefreshTargetsAsync();
                if (IsPhysicalMode || IsMoveToBlankCarrierMode)
                {
                    await ResetTargetSlotSelectionAsync(preferSourceLocation: ShouldPreferSourceTargetSlot);
                }
                else
                {
                    ClearTargetSlotSelection();
                }

                await RefreshSourceHardDiskReturnLocationOptionsAsync();
                RefreshDisplayItems();
                OnPropertyChanged(nameof(TargetSlotRecommendHintText));
                OnPropertyChanged(nameof(CanRecommendTargetSlot));
                OnPropertyChanged(nameof(IsTargetSameSlotAsSource));
                OnPropertyChanged(nameof(TargetSlotValidationMessage));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "加载失败");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApplySelectedTargetAsync()
        {
            if (!IsMergeMode || SelectedTarget == null)
            {
                TargetSummary = null;
                RefreshDisplayItems();
                return;
            }

            try
            {
                var summary = await _relocationService.LoadElectronicSourceByIdAsync(SelectedTarget.ContainerId);
                if (summary == null)
                {
                    TargetSummary = null;
                    _dialogService.ShowMessage("未找到对应目标电子介质袋，请重新选择。", "提示");
                    RefreshDisplayItems();
                    return;
                }

                TargetSummary = summary;
                RefreshDisplayItems();
                PreviewText = string.Empty;
            }
            catch (Exception ex)
            {
                TargetSummary = null;
                _dialogService.ShowError(ex.Message, "加载目标失败");
                RefreshDisplayItems();
            }
        }

        private void ClearSourceState()
        {
            SourceSummary = null;
            TargetSummary = null;
            ClearRelocationItems();
            PreviewText = string.Empty;
            TargetOptions.Clear();
            _selectedTarget = null;
            OnPropertyChanged(nameof(SelectedTarget));
            SelectedBlankHardDiskCode = string.Empty;
            SelectedBlankHardDiskMediumId = null;
            ClearTargetSlotSelection();
            SourceHardDiskReturnLocationOptions.Clear();
            SelectedSourceHardDiskReturnLocationOption = null;
        }

        private void SelectBlankHardDisk()
        {
            if (!IsMoveToBlankHardDiskMode)
            {
                return;
            }

            IEnumerable<string>? initialCodes = string.IsNullOrWhiteSpace(SelectedBlankHardDiskCode)
                ? null
                : [SelectedBlankHardDiskCode];

            var selectedMedia = _dialogService.ShowHardDiskMediumSelectionDialog(
                initialCodes,
                currentElectronicArchiveUnitId: null,
                ArchiveFilingBusinessRules.HardDiskSelectionModeBlankTarget);

            if (selectedMedia == null || selectedMedia.Count == 0)
            {
                return;
            }

            if (selectedMedia.Count > 1)
            {
                _dialogService.ShowMessage("一次只能选择一块空白硬盘作为迁入目标。", "提示");
                return;
            }

            var targetMedium = selectedMedia[0];
            SelectedBlankHardDiskCode = targetMedium.DiskCode;
            SelectedBlankHardDiskMediumId = targetMedium.Id;
            PreviewText = string.Empty;
        }

        private void ShowSourceHardDiskReturnSlotSnapshot()
        {
            string? location = SelectedSourceHardDiskReturnLocationOption?.Location?.Trim();
            if (string.IsNullOrWhiteSpace(location))
            {
                _dialogService.ShowMessage("当前原硬盘放回位置无法解析为有效档口。", "提示");
                return;
            }

            if (!TryShowSlotSnapshotByLocation(location))
            {
                _dialogService.ShowMessage($"未找到柜子或档口无法解析，无法打开档口快照。", "提示");
            }
        }

        private string? ResolveSelectedProjectName()
        {
            return ProjectOptions.FirstOrDefault(option => option.Id == SelectedProjectId)?.Name;
        }

        private async Task RefreshTargetsAsync()
        {
            TargetOptions.Clear();
            TargetSummary = null;
            _selectedTarget = null;
            OnPropertyChanged(nameof(SelectedTarget));
            RefreshDisplayItems();
            if (SourceSummary == null || !IsMergeMode)
            {
                return;
            }

            try
            {
                var options = await _relocationService.GetElectronicTargetOptionsAsync(
                    SourceSummary.ContainerId,
                    hardDiskMergeTargetsOnly: true);
                foreach (var option in options)
                {
                    TargetOptions.Add(option);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "加载目标电子介质袋失败");
            }
        }

        private void RefreshDisplayItems()
        {
            if (SourceSummary == null)
            {
                ClearRelocationItems();
                OnPropertyChanged(nameof(ItemsSectionHeader));
                OnPropertyChanged(nameof(ShowItemsEmptyHint));
                OnPropertyChanged(nameof(ItemsEmptyHintText));
                return;
            }

            string sourceLocation = ArchiveRelocationItemContainerCodeSupport.NormalizeStorageLocation(
                SourceSummary.StorageLocation);

            // 备份/拷贝：清单只列「将生成的备份副本」迁入目标位置，不列未移动的原件、也不混入目标袋既有资料。
            if (ExecuteBackupMechanism && (IsMoveToBlankCarrierMode || IsMergeMode))
            {
                if (IsMergeMode && TargetSummary == null)
                {
                    ClearRelocationItems();
                    OnPropertyChanged(nameof(ItemsSectionHeader));
                    OnPropertyChanged(nameof(ShowItemsEmptyHint));
                    OnPropertyChanged(nameof(ItemsEmptyHintText));
                    return;
                }

                string afterLocation = ResolveBackupCopyAfterStorageLocation(sourceLocation);
                ReplaceItems(
                    Items,
                    SourceSummary.Items
                        .Select(item => ArchiveRelocationItemContainerCodeSupport.WithStorageLocations(
                            item, sourceLocation, afterLocation))
                        .ToList());
                OnPropertyChanged(nameof(ItemsSectionHeader));
                OnPropertyChanged(nameof(ShowItemsEmptyHint));
                OnPropertyChanged(nameof(ItemsEmptyHintText));
                return;
            }

            if (!IsMergeMode || TargetSummary == null)
            {
                if (IsMergeMode)
                {
                    ClearRelocationItems();
                }
                else
                {
                    string afterLocation = ResolveElectronicAfterStorageLocation(sourceLocation);
                    ReplaceItems(
                        Items,
                        SourceSummary.Items
                            .Select(item => ArchiveRelocationItemContainerCodeSupport.WithStorageLocations(
                                item, sourceLocation, afterLocation))
                            .ToList());
                }

                OnPropertyChanged(nameof(ItemsSectionHeader));
                OnPropertyChanged(nameof(ShowItemsEmptyHint));
                OnPropertyChanged(nameof(ItemsEmptyHintText));
                return;
            }

            string targetLocation = ArchiveRelocationItemContainerCodeSupport.NormalizeStorageLocation(
                TargetSummary.StorageLocation);
            var mergedItems = new Dictionary<int, ArchiveRelocationItemSummary>();
            foreach (var item in TargetSummary.Items)
            {
                mergedItems[item.MediaItemId] = ArchiveRelocationItemContainerCodeSupport.WithStorageLocations(
                    item, targetLocation, targetLocation);
            }

            foreach (var item in SourceSummary.Items)
            {
                mergedItems[item.MediaItemId] = ArchiveRelocationItemContainerCodeSupport.WithStorageLocations(
                    item, sourceLocation, targetLocation);
            }

            ReplaceItems(
                Items,
                mergedItems.Values
                    .OrderBy(item => item.FormNo, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
                    .ToList());
            OnPropertyChanged(nameof(ItemsSectionHeader));
            OnPropertyChanged(nameof(ShowItemsEmptyHint));
            OnPropertyChanged(nameof(ItemsEmptyHintText));
        }

        private string ResolveBackupCopyAfterStorageLocation(string sourceLocation)
        {
            if (IsMergeMode && TargetSummary != null)
            {
                return ArchiveRelocationItemContainerCodeSupport.NormalizeStorageLocation(
                    TargetSummary.StorageLocation);
            }

            if (IsMoveToBlankCarrierMode)
            {
                return ArchiveRelocationItemContainerCodeSupport.NormalizeStorageLocation(TargetFullLocation);
            }

            return sourceLocation;
        }

        private string ResolveElectronicAfterStorageLocation(string sourceLocation)
        {
            if (IsPhysicalMode || IsMoveToBlankCarrierMode)
            {
                return ArchiveRelocationItemContainerCodeSupport.NormalizeStorageLocation(TargetFullLocation);
            }

            return ArchiveRelocationItemContainerCodeSupport.UnspecifiedStorageLocation;
        }

        private async Task RefreshSourceHardDiskReturnLocationOptionsAsync()
        {
            SourceHardDiskReturnLocationOptions.Clear();
            SelectedSourceHardDiskReturnLocationOption = null;

            if (!RequiresHardDiskReturnLocation || SourceSummary == null)
            {
                return;
            }

            try
            {
                var options = await _hardDiskMediaService.GetOrderedBlankDedicatedSlotLocationOptionsAsync();
                foreach (var option in options)
                {
                    SourceHardDiskReturnLocationOptions.Add(option);
                }

                string recommendedLocation = await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync() ?? string.Empty;
                SelectedSourceHardDiskReturnLocationOption =
                    SourceHardDiskReturnLocationOptions.FirstOrDefault(item =>
                        string.Equals(item.Location, recommendedLocation, StringComparison.OrdinalIgnoreCase))
                    ?? SourceHardDiskReturnLocationOptions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "加载原硬盘放回位置失败");
            }
        }

        private async Task<string> ResolveDefaultHardDiskReturnLocationAsync(string diskCode)
        {
            _ = diskCode;
            return await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync() ?? string.Empty;
        }

        private async Task PreviewAsync()
        {
            var request = BuildRequest();
            if (request == null)
            {
                return;
            }

            try
            {
                IsBusy = true;
                var preview = await _relocationService.PreviewElectronicRelocationAsync(request);
                ApplyPreviewResult(preview);
            }
            catch (Exception ex)
            {
                PreviewText = $"【预览失败】{ex.Message}";
                HasExecutablePreview = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteAsync()
        {
            var request = BuildRequest();
            if (request == null)
            {
                return;
            }

            ArchiveRelocationPreview preview;
            try
            {
                IsBusy = true;
                preview = await _relocationService.PreviewElectronicRelocationAsync(request);
                ApplyPreviewResult(preview);
            }
            catch (Exception ex)
            {
                PreviewText = $"【预览失败】{ex.Message}";
                HasExecutablePreview = false;
                _dialogService.ShowError(ex.Message, "迁档核验失败");
                return;
            }
            finally
            {
                IsBusy = false;
            }

            if (!preview.CanExecute)
            {
                _dialogService.ShowMessage(
                    string.IsNullOrWhiteSpace(preview.BlockReason)
                        ? "当前迁档条件不可执行，请先修正后再确认。"
                        : preview.BlockReason,
                    "不可执行");
                return;
            }

            string confirmTitle = request.ExecuteBackupMechanism ? "确认备份" : "确认迁档";
            string confirmLead = request.ExecuteBackupMechanism
                ? "确认执行资料备份？原件将保留在原档口，仅在目标介质生成可检索的备份副本。"
                : "确认执行迁档？此操作无需审批，提交后立即生效。";
            if (!_dialogService.ShowConfirm($"{PreviewText}\n\n{confirmLead}", confirmTitle))
            {
                return;
            }

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);
                var result = await _relocationService.ExecuteElectronicRelocationAsync(request);
                if (result.Success)
                {
                    string completedNo = string.IsNullOrWhiteSpace(result.RelocationNo)
                        ? string.Empty
                        : result.RelocationNo.Trim();
                    _dialogService.ShowMessage(
                        string.IsNullOrWhiteSpace(completedNo)
                            ? result.Message
                            : $"{result.Message}\n迁档单号：{completedNo}",
                        "迁档完成");
                    await ResetWorkbenchForNewBusinessAsync();
                }
                else
                {
                    _dialogService.ShowError(result.Message, "迁档失败");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "迁档失败");
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
            }
        }

        /// <summary>
        /// 迁档办结后恢复为待办理新单：清空源/目标与预览，页眉回到待编单。
        /// </summary>
        private async Task ResetWorkbenchForNewBusinessAsync()
        {
            Remarks = string.Empty;
            ExecuteBackupMechanism = false;
            ConfirmHardDiskFormatted = false;
            ConfirmOpticalDiscDestroyed = false;

            if (SelectedRelocationMode != ArchiveRelocationMode.PhysicalMove)
            {
                SelectedRelocationMode = ArchiveRelocationMode.PhysicalMove;
            }

            if (SelectedSourceOption != null)
            {
                SelectedSourceOption = null;
            }
            else
            {
                ClearSourceState();
            }

            await ReloadSourceOptionsKeepingSelectionClearedAsync();
            await RefreshPendingRelocationNoAsync();
        }

        private async Task ReloadSourceOptionsKeepingSelectionClearedAsync()
        {
            string? projectName = ResolveSelectedProjectName();
            SourceOptions.Clear();
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(SelectedYear))
            {
                return;
            }

            try
            {
                var options = await _relocationService.GetElectronicSourceOptionsAsync(projectName, SelectedYear);
                foreach (var option in options)
                {
                    SourceOptions.Add(option);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "加载源电子介质袋失败");
            }
        }

        private ElectronicRelocationRequest? BuildRequest()
        {
            if (SourceSummary == null)
            {
                _dialogService.ShowMessage("请先选择源电子介质袋。", "提示");
                return null;
            }

            if (RequiresHardDiskConfirmation && !ConfirmHardDiskFormatted)
            {
                _dialogService.ShowMessage("请确认原硬盘已格式化并将按空盘管理。", "提示");
                return null;
            }

            if (RequiresOpticalDiscConfirmation && !ConfirmOpticalDiscDestroyed)
            {
                _dialogService.ShowMessage("请确认原光盘已物理销毁。", "提示");
                return null;
            }

            var request = new ElectronicRelocationRequest
            {
                RelocationMode = SelectedRelocationMode,
                SourceUnitId = SourceSummary.ContainerId,
                Remarks = Remarks,
                ConfirmHardDiskFormatted = ConfirmHardDiskFormatted,
                ConfirmOpticalDiscDestroyed = ConfirmOpticalDiscDestroyed,
                ExecuteBackupMechanism = ExecuteBackupMechanism
            };

            if (IsPhysicalMode)
            {
                if (!TryApplySelectedTargetLocation(request, out string message))
                {
                    _dialogService.ShowMessage(message, "提示");
                    return null;
                }

                return request;
            }

            if (IsMoveToBlankHardDiskMode)
            {
                if (!TryApplySelectedTargetLocation(request, out string message))
                {
                    _dialogService.ShowMessage(message, "提示");
                    return null;
                }

                if (SelectedBlankHardDiskMediumId is not > 0 || string.IsNullOrWhiteSpace(SelectedBlankHardDiskCode))
                {
                    _dialogService.ShowMessage("请选择拟迁入的空白硬盘。", "提示");
                    return null;
                }

                if (RequiresHardDiskReturnLocation && SelectedSourceHardDiskReturnLocationOption == null)
                {
                    _dialogService.ShowMessage("请选择原硬盘放回位置。", "提示");
                    return null;
                }

                request.TargetBlankHardDiskMediumId = SelectedBlankHardDiskMediumId;
                request.TargetBlankHardDiskCode = SelectedBlankHardDiskCode.Trim();
                request.SourceHardDiskReturnLocation = SelectedSourceHardDiskReturnLocationOption?.Location?.Trim() ?? string.Empty;
                return request;
            }

            if (IsMoveToBlankOpticalDiscMode)
            {
                if (!TryApplySelectedTargetLocation(request, out string message))
                {
                    _dialogService.ShowMessage(message, "提示");
                    return null;
                }

                if (RequiresHardDiskReturnLocation && SelectedSourceHardDiskReturnLocationOption == null)
                {
                    _dialogService.ShowMessage("请选择原硬盘放回位置。", "提示");
                    return null;
                }

                request.SourceHardDiskReturnLocation = SelectedSourceHardDiskReturnLocationOption?.Location?.Trim() ?? string.Empty;
                return request;
            }

            if (SelectedTarget == null)
            {
                _dialogService.ShowMessage("请选择目标硬盘袋。", "提示");
                return null;
            }

            if (RequiresHardDiskReturnLocation && SelectedSourceHardDiskReturnLocationOption == null)
            {
                _dialogService.ShowMessage("请选择原硬盘放回位置。", "提示");
                return null;
            }

            request.TargetUnitId = SelectedTarget.ContainerId;
            request.SourceHardDiskReturnLocation = SelectedSourceHardDiskReturnLocationOption?.Location?.Trim() ?? string.Empty;
            return request;
        }

        private void ApplyPreviewResult(ArchiveRelocationPreview preview)
        {
            if (preview.CanExecute)
            {
                PreviewText = preview.SummaryText;
                HasExecutablePreview = true;
                return;
            }

            PreviewText = $"【不可执行】{preview.BlockReason}";
            HasExecutablePreview = false;
        }

        private void ClearRelocationItems()
        {
            Items.Clear();
            ItemDetailsPanel.RefreshItems(Items, preserveExpanded: ItemDetailsPanel.IsExpanded);
        }

        private void ReplaceItems(
            ObservableCollection<ArchiveRelocationItemSummary> target,
            IReadOnlyList<ArchiveRelocationItemSummary> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }

            ItemDetailsPanel.RefreshItems(target, preserveExpanded: ItemDetailsPanel.IsExpanded);
        }

        private void NotifyRelocationModeRadioProperties()
        {
            OnPropertyChanged(nameof(IsPhysicalMoveModeSelected));
            OnPropertyChanged(nameof(IsMoveToBlankHardDiskModeSelected));
            OnPropertyChanged(nameof(IsMoveToBlankOpticalDiscModeSelected));
            OnPropertyChanged(nameof(IsMergeToExistingModeSelected));
        }
    }
}
