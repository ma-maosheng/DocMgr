using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using System.Collections.ObjectModel;
using System.Linq;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveSimulatedRelocationViewModel : ViewModelBase
    {
        private readonly IArchiveRelocationService _relocationService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IProjectService _projectService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;

        private ArchiveRelocationContainerSummary? _sourceSummary;
        private string _selectedRelocationMode = ArchiveRelocationMode.PhysicalMove;
        private string _selectedYear = string.Empty;
        private int? _selectedProjectId;
        private ArchiveRelocationSourceOption? _selectedSourceOption;
        private string _remarks = string.Empty;
        private string _previewText = string.Empty;
        private ArchiveRelocationTargetOption? _selectedTarget;
        private ArchiveRelocationContainerSummary? _targetSummary;
        private bool _moveContentsToNewEmptyBox;
        private string _selectedNewBoxSpecification = "标准(5cm)";
        private bool _isBusy;
        private bool _isInitialized;

        public ArchiveSimulatedRelocationViewModel(
            IArchiveRelocationService relocationService,
            IArchiveRegisterService archiveRegisterService,
            IProjectService projectService,
            IUserContextService userContextService,
            IDialogService dialogService,
            ICabinetService cabinetService,
            IArchiveFilingService filingService)
        {
            _relocationService = relocationService;
            _archiveRegisterService = archiveRegisterService;
            _projectService = projectService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            TargetLocation = new RelocationPhysicalLocationSelectionModel(
                RelocationPhysicalLocationKind.SimulatedArchiveBox,
                cabinetService,
                filingService,
                dialogService);

            RelocationModes =
            [
                new RelocationModeOption("物理位置迁移", ArchiveRelocationMode.PhysicalMove),
                new RelocationModeOption("并入已有档案盒", ArchiveRelocationMode.MergeToExisting)
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
            BoxSpecifications = new ObservableCollection<string> { "标准(10cm)", "标准(5cm)", "标准(3cm)", "标准(2cm)", "非标(10cm)" };

            RefreshTargetsCommand = new RelayCommand(async _ => await RefreshTargetsAsync(), _ => !IsBusy && SourceSummary != null && IsContainerMode);
            PreviewCommand = new RelayCommand(async _ => await PreviewAsync(), _ => !IsBusy && SourceSummary != null);
            ExecuteCommand = new RelayCommand(async _ => await ExecuteAsync(), _ => !IsBusy && SourceSummary != null && !string.IsNullOrWhiteSpace(PreviewText));
        }

        public string PageTitle => "模拟介质资料迁档";

        public RelocationPhysicalLocationSelectionModel TargetLocation { get; }

        public ObservableCollection<string> Years { get; } = new();

        public ObservableCollection<ProjectFilterOption> ProjectOptions { get; } = new();

        public ObservableCollection<RelocationModeOption> RelocationModes { get; }

        public ObservableCollection<ArchiveRelocationItemSummary> Items { get; }

        public ItemDetailsListPresenter<ArchiveRelocationItemSummary> ItemDetailsPanel { get; }

        public ObservableCollection<ArchiveRelocationTargetOption> TargetOptions { get; }

        public ObservableCollection<ArchiveRelocationSourceOption> SourceOptions { get; }

        public ObservableCollection<string> BoxSpecifications { get; }

        public RelayCommand RefreshTargetsCommand { get; }

        public RelayCommand PreviewCommand { get; }

        public RelayCommand ExecuteCommand { get; }

        public bool IsArchiveAdmin => _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool IsPhysicalMode => SelectedRelocationMode == ArchiveRelocationMode.PhysicalMove;

        public bool IsContainerMode => SelectedRelocationMode == ArchiveRelocationMode.MergeToExisting;

        public bool ShowNewBoxSpecificationSelector => IsPhysicalMode && MoveContentsToNewEmptyBox;

        public bool MoveContentsToNewEmptyBox
        {
            get => _moveContentsToNewEmptyBox;
            set
            {
                if (SetProperty(ref _moveContentsToNewEmptyBox, value))
                {
                    OnPropertyChanged(nameof(ShowNewBoxSpecificationSelector));
                    PreviewText = string.Empty;
                }
            }
        }

        public string SelectedNewBoxSpecification
        {
            get => _selectedNewBoxSpecification;
            set => SetProperty(ref _selectedNewBoxSpecification, value);
        }

        public bool IsMergeMode => IsContainerMode;

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
                    OnPropertyChanged(nameof(IsContainerMode));
                    OnPropertyChanged(nameof(SourceSummaryText));
                    OnPropertyChanged(nameof(ShowSourceCurrentLocation));
                    OnPropertyChanged(nameof(ItemsSectionHeader));
                    OnPropertyChanged(nameof(ShowItemsEmptyHint));
                    OnPropertyChanged(nameof(ShowNewBoxSpecificationSelector));
                    NotifyRelocationModeRadioProperties();
                    PreviewText = string.Empty;
                    _ = RefreshTargetsAsync();
                    RefreshDisplayItems();
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
                    OnPropertyChanged(nameof(SourceItemsDescriptionText));
                }
            }
        }

        public bool HasSource => SourceSummary != null;

        public string SourceSummaryText => SourceSummary == null
            ? "请选择年度、项目及源档案盒"
            : IsPhysicalMode
                ? $"{SourceSummary.ContainerCode} | {SourceSummary.ProjectName} | {SourceSummary.Year} | {SourceSummary.ItemCount} 项"
                : $"{SourceSummary.ContainerCode} | {SourceSummary.StorageLocation} | {SourceSummary.ProjectName} | {SourceSummary.Year} | {SourceSummary.ItemCount} 项";

        public string SourceCurrentLocation => SourceSummary?.StorageLocation?.Trim() ?? string.Empty;

        public bool ShowSourceCurrentLocation =>
            IsPhysicalMode && HasSource && !string.IsNullOrWhiteSpace(SourceCurrentLocation);

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
            : $"{TargetSummary.ContainerCode} | {TargetSummary.StorageLocation} | {TargetSummary.ProjectName} | {TargetSummary.Year} | {TargetSummary.ItemCount} 项";

        public string TargetItemsDescriptionText => TargetSummary == null
            ? string.Empty
            : ArchiveRelocationSourceDescriptionBuilder.BuildItemsDescription(TargetSummary.Items);

        public string ItemsSectionHeader =>
            IsMergeMode && HasSelectedTarget ? "资料清单（并档后）" : "资料清单";

        public bool ShowItemsEmptyHint =>
            IsMergeMode && SourceSummary != null && !HasSelectedTarget;

        public string ItemsEmptyHintText => "请选择目标档案盒后，此处将显示并档后的资料子项清单。";

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
                var options = await _relocationService.GetSimulatedSourceOptionsAsync(projectName, SelectedYear);
                SourceOptions.Clear();
                foreach (var option in options)
                {
                    SourceOptions.Add(option);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "加载源档案盒失败");
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
                var summary = await _relocationService.LoadSimulatedSourceByIdAsync(SelectedSourceOption.ContainerId);
                if (summary == null)
                {
                    ClearSourceState();
                    _dialogService.ShowMessage("未找到对应档案盒，请重新选择。", "提示");
                    return;
                }

                SourceSummary = summary;
                TargetSummary = null;
                _selectedTarget = null;
                OnPropertyChanged(nameof(SelectedTarget));
                TargetLocation.CurrentSourceLocation = summary.StorageLocation;
                TargetLocation.ResetTargetSelection();
                PreviewText = string.Empty;
                await RefreshTargetsAsync();
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
                var summary = await _relocationService.LoadSimulatedSourceByIdAsync(SelectedTarget.ContainerId);
                if (summary == null)
                {
                    TargetSummary = null;
                    _dialogService.ShowMessage("未找到对应目标档案盒，请重新选择。", "提示");
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
            TargetLocation.CurrentSourceLocation = string.Empty;
            TargetLocation.ResetTargetSelection();
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
            if (SourceSummary == null || !IsContainerMode)
            {
                return;
            }

            try
            {
                var options = await _relocationService.GetSimulatedTargetOptionsAsync(SourceSummary.ContainerId);
                IEnumerable<ArchiveRelocationTargetOption> filtered = SelectedRelocationMode switch
                {
                    ArchiveRelocationMode.MergeToExisting => options.Where(option => !option.IsEmpty),
                    _ => options
                };

                foreach (var option in filtered)
                {
                    TargetOptions.Add(option);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "加载目标档案盒失败");
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
                var preview = await _relocationService.PreviewSimulatedRelocationAsync(request);
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

            if (!_dialogService.ShowConfirm($"{PreviewText}\n\n确认执行迁档？此操作无需审批，提交后立即生效。", "确认迁档"))
            {
                return;
            }

            int sourceBoxId = request.SourceBoxId;
            string? pendingReturnWarning = await _relocationService.GetSimulatedPendingReturnConfirmMessageAsync(
                sourceBoxId,
                "实施迁档/销号");
            if (!string.IsNullOrWhiteSpace(pendingReturnWarning)
                && !_dialogService.ShowConfirm(pendingReturnWarning, "待归还提醒"))
            {
                return;
            }

            int? previousSourceId = SelectedSourceOption?.ContainerId;

            try
            {
                IsBusy = true;
                _dialogService.SetBusyState(true);
                var result = await _relocationService.ExecuteSimulatedRelocationAsync(request);
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

        private SimulatedRelocationRequest? BuildRequest()
        {
            if (SourceSummary == null)
            {
                _dialogService.ShowMessage("请先选择源档案盒。", "提示");
                return null;
            }

            var request = new SimulatedRelocationRequest
            {
                RelocationMode = SelectedRelocationMode,
                SourceBoxId = SourceSummary.ContainerId,
                Remarks = Remarks
            };

            if (IsPhysicalMode)
            {
                if (!TargetLocation.TryApplyToSimulatedRequest(request, out string message))
                {
                    _dialogService.ShowMessage(message, "提示");
                    return null;
                }

                request.MoveContentsToNewEmptyBox = MoveContentsToNewEmptyBox;
                request.NewBoxSpecification = MoveContentsToNewEmptyBox
                    ? SelectedNewBoxSpecification
                    : string.Empty;
                return request;
            }

            if (SelectedTarget == null)
            {
                _dialogService.ShowMessage("请选择目标档案盒。", "提示");
                return null;
            }

            request.TargetBoxId = SelectedTarget.ContainerId;
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
            OnPropertyChanged(nameof(IsMergeToExistingModeSelected));
        }
    }
}
