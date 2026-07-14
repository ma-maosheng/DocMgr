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
    public sealed class ArchiveElectronicRelocationViewModel : ViewModelBase
    {
        private readonly IArchiveRelocationService _relocationService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IProjectService _projectService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly IHardDiskMediaService _hardDiskMediaService;

        private ArchiveRelocationContainerSummary? _sourceSummary;
        private string _selectedRelocationMode = ArchiveRelocationMode.PhysicalMove;
        private string _selectedYear = string.Empty;
        private int? _selectedProjectId;
        private ArchiveRelocationSourceOption? _selectedSourceOption;
        private string _remarks = string.Empty;
        private string _previewText = string.Empty;
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
            _hardDiskMediaService = hardDiskMediaService;
            TargetLocation = new RelocationPhysicalLocationSelectionModel(
                RelocationPhysicalLocationKind.ElectronicArchiveUnit,
                cabinetService,
                filingService,
                dialogService);

            RelocationModes =
            [
                new RelocationModeOption("物理位置迁移", ArchiveRelocationMode.PhysicalMove),
                new RelocationModeOption("迁入空盘/空袋", ArchiveRelocationMode.MoveToEmpty),
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
            SelectBlankHardDiskCommand = new RelayCommand(_ => SelectBlankHardDisk(), _ => !IsBusy && IsMoveToEmptyMode);
            ShowSourceHardDiskReturnSlotSnapshotCommand = new RelayCommand(
                _ => ShowSourceHardDiskReturnSlotSnapshot(),
                _ => CanShowSourceHardDiskReturnSlotSnapshot);
            PreviewCommand = new RelayCommand(async _ => await PreviewAsync(), _ => !IsBusy && SourceSummary != null);
            ExecuteCommand = new RelayCommand(async _ => await ExecuteAsync(), _ => !IsBusy && SourceSummary != null && !string.IsNullOrWhiteSpace(PreviewText));
        }

        public string PageTitle => "电子介质资料迁档";

        public RelocationPhysicalLocationSelectionModel TargetLocation { get; }

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

        public RelayCommand ShowSourceHardDiskReturnSlotSnapshotCommand { get; }

        public RelayCommand PreviewCommand { get; }

        public RelayCommand ExecuteCommand { get; }

        public bool IsArchiveAdmin => _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool IsPhysicalMode => SelectedRelocationMode == ArchiveRelocationMode.PhysicalMove;

        public bool IsMoveToEmptyMode => SelectedRelocationMode == ArchiveRelocationMode.MoveToEmpty;

        public bool IsMergeMode => SelectedRelocationMode == ArchiveRelocationMode.MergeToExisting;

        public bool SupportsBackupMechanism => IsMoveToEmptyMode || IsMergeMode;

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

        public bool IsMoveToEmptyModeSelected
        {
            get => SelectedRelocationMode == ArchiveRelocationMode.MoveToEmpty;
            set
            {
                if (value)
                {
                    SelectedRelocationMode = ArchiveRelocationMode.MoveToEmpty;
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
            && (IsMoveToEmptyMode || IsMergeMode)
            && SourceSummary != null
            && SourceSummary.StorageCarrierType.Contains("硬盘", StringComparison.Ordinal);

        public bool RequiresOpticalDiscConfirmation =>
            !ExecuteBackupMechanism
            && (IsMoveToEmptyMode || IsMergeMode)
            && SourceSummary != null
            && SourceSummary.StorageCarrierType.Contains("光盘", StringComparison.Ordinal);

        public bool RequiresHardDiskReturnLocation =>
            !ExecuteBackupMechanism
            && (IsMoveToEmptyMode || IsMergeMode)
            && RequiresHardDiskConfirmation;

        public bool ShowDispositionConfirmations =>
            !ExecuteBackupMechanism
            && (IsMoveToEmptyMode || IsMergeMode)
            && SourceSummary != null
            && (RequiresHardDiskConfirmation || RequiresOpticalDiscConfirmation);

        public bool CanShowSourceHardDiskReturnSlotSnapshot =>
            RequiresHardDiskReturnLocation
            && SelectedSourceHardDiskReturnLocationOption != null
            && TryParseHardDiskSlotLocation(
                SelectedSourceHardDiskReturnLocationOption.Location,
                out _,
                out _,
                out _,
                out _);

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
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
                    OnPropertyChanged(nameof(IsMoveToEmptyMode));
                    OnPropertyChanged(nameof(IsMergeMode));
                    OnPropertyChanged(nameof(IsContainerMode));
                    OnPropertyChanged(nameof(SupportsBackupMechanism));
                    OnPropertyChanged(nameof(SourceSummaryText));
                    OnPropertyChanged(nameof(ShowSourceCurrentLocation));
                    OnPropertyChanged(nameof(RequiresHardDiskConfirmation));
                    OnPropertyChanged(nameof(RequiresOpticalDiscConfirmation));
                    OnPropertyChanged(nameof(RequiresHardDiskReturnLocation));
                    OnPropertyChanged(nameof(ShowDispositionConfirmations));
                    NotifyRelocationModeRadioProperties();
                    TargetLocation.ConfigureForMoveToEmpty(IsMoveToEmptyMode, SourceSummary?.ContainerId);
                    PreviewText = string.Empty;
                    _ = RefreshTargetsAsync();
                    RefreshDisplayItems();
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
            IsMergeMode && HasSelectedTarget ? "资料清单（并档后）" : "资料清单";

        public bool ShowItemsEmptyHint =>
            IsMergeMode && SourceSummary != null && !HasSelectedTarget;

        public string ItemsEmptyHintText => "请选择目标硬盘袋后，此处将显示并档后的资料子项清单。";

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
            set => SetProperty(ref _confirmHardDiskFormatted, value);
        }

        public bool ConfirmOpticalDiscDestroyed
        {
            get => _confirmOpticalDiscDestroyed;
            set => SetProperty(ref _confirmOpticalDiscDestroyed, value);
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
                PreviewText = string.Empty;
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
            private set => SetProperty(ref _previewText, value);
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
                _dialogService.ShowMessage("仅资料室管理员可执行资料迁档。", "权限不足");
            }

            await LoadYearsAsync();
            await TargetLocation.LoadCabinetsAsync();
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
                TargetLocation.ConfigureForMoveToEmpty(IsMoveToEmptyMode, summary.ContainerId);
                if (IsMoveToEmptyMode)
                {
                    TargetLocation.InitializeFromSourceLocation(summary.StorageLocation, summary.ContainerId);
                }
                else
                {
                    TargetLocation.CurrentSourceLocation = summary.StorageLocation;
                    TargetLocation.ResetTargetSelection();
                }
                ExecuteBackupMechanism = false;
                ConfirmHardDiskFormatted = false;
                ConfirmOpticalDiscDestroyed = false;
                SelectedBlankHardDiskCode = string.Empty;
                SelectedBlankHardDiskMediumId = null;
                PreviewText = string.Empty;
                await RefreshTargetsAsync();
                await RefreshSourceHardDiskReturnLocationOptionsAsync();
                RefreshDisplayItems();
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
            TargetLocation.CurrentSourceLocation = string.Empty;
            TargetLocation.ResetTargetSelection();
            SourceHardDiskReturnLocationOptions.Clear();
            SelectedSourceHardDiskReturnLocationOption = null;
        }

        private void SelectBlankHardDisk()
        {
            if (!IsMoveToEmptyMode)
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
            if (string.IsNullOrWhiteSpace(location)
                || !TryParseHardDiskSlotLocation(location, out string cabinetName, out string side, out string row, out string column))
            {
                _dialogService.ShowMessage("当前原硬盘放回位置无法解析为有效档口。", "提示");
                return;
            }

            var cabinet = TargetLocation.Cabinets.FirstOrDefault(item =>
                string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                _dialogService.ShowMessage($"未找到柜子 [{cabinetName}]，无法打开档口快照。", "提示");
                return;
            }

            CabinetFace face = string.Equals(side, "B", StringComparison.OrdinalIgnoreCase)
                ? CabinetFace.B
                : CabinetFace.A;

            _dialogService.ShowCabinetOpenDialog(new CabinetOpenRequest
            {
                CabinetId = cabinet.Id,
                CabinetName = cabinet.Name,
                CabinetType = cabinet.Type,
                Face = face,
                LayerCount = cabinet.LayerCount,
                ColumnCount = cabinet.ColumnCount,
                TargetSlotCode = $"{row}-{column}",
                WidthCm = cabinet.Width,
                HeightCm = cabinet.Height,
                DepthCm = cabinet.Depth
            });
        }

        private static bool TryParseHardDiskSlotLocation(
            string location,
            out string cabinetName,
            out string side,
            out string row,
            out string column)
        {
            cabinetName = string.Empty;
            side = string.Empty;
            row = string.Empty;
            column = string.Empty;

            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            var parts = location.Trim().Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return false;
            }

            string cabinetAndSide = parts[0];
            if (cabinetAndSide.Length < 2)
            {
                return false;
            }

            side = cabinetAndSide[^1].ToString();
            cabinetName = cabinetAndSide[..^1];
            row = parts[1];
            column = parts[2];
            return true;
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
                    ReplaceItems(Items, SourceSummary.Items);
                }

                OnPropertyChanged(nameof(ItemsSectionHeader));
                OnPropertyChanged(nameof(ShowItemsEmptyHint));
                return;
            }

            var mergedItems = new Dictionary<int, ArchiveRelocationItemSummary>();
            foreach (var item in TargetSummary.Items)
            {
                mergedItems[item.MediaItemId] = item;
            }

            foreach (var item in SourceSummary.Items)
            {
                mergedItems[item.MediaItemId] = item;
            }

            ReplaceItems(
                Items,
                mergedItems.Values
                    .OrderBy(item => item.FormNo, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
                    .ToList());
            OnPropertyChanged(nameof(ItemsSectionHeader));
            OnPropertyChanged(nameof(ShowItemsEmptyHint));
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
                PreviewText = preview.CanExecute
                    ? preview.SummaryText
                    : $"【不可执行】{preview.BlockReason}";
            }
            catch (Exception ex)
            {
                PreviewText = $"【预览失败】{ex.Message}";
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

            string confirmTitle = request.ExecuteBackupMechanism ? "确认备份" : "确认迁档";
            string confirmLead = request.ExecuteBackupMechanism
                ? "确认执行资料备份？原件将保留在原档口，仅在目标介质生成可检索的备份副本。"
                : "确认执行迁档？此操作无需审批，提交后立即生效。";
            if (!_dialogService.ShowConfirm($"{PreviewText}\n\n{confirmLead}", confirmTitle))
            {
                return;
            }

            int? previousSourceId = SelectedSourceOption?.ContainerId;

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);
                var result = await _relocationService.ExecuteElectronicRelocationAsync(request);
                if (result.Success)
                {
                    _dialogService.ShowMessage($"{result.Message}\n迁档单号：{result.RelocationNo}", "迁档完成");
                    await ReloadSourceOptionsAsync();
                    var restored = SourceOptions.FirstOrDefault(option => option.ContainerId == previousSourceId);
                    if (restored != null)
                    {
                        SelectedSourceOption = restored;
                    }
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

        private ElectronicRelocationRequest? BuildRequest()
        {
            if (SourceSummary == null)
            {
                _dialogService.ShowMessage("请先选择源电子介质袋。", "提示");
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
                if (!TargetLocation.TryApplyToElectronicRequest(request, out string message))
                {
                    _dialogService.ShowMessage(message, "提示");
                    return null;
                }

                return request;
            }

            if (IsMoveToEmptyMode)
            {
                if (!TargetLocation.TryApplyToMoveToEmptyRequest(request, out string message))
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
            OnPropertyChanged(nameof(IsMoveToEmptyModeSelected));
            OnPropertyChanged(nameof(IsMergeToExistingModeSelected));
        }
    }
}
