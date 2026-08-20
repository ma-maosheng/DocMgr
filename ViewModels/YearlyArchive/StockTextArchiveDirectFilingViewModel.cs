using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.Projects;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 存档文本资料直办立档向导（一盒一提交）。
    /// </summary>
    public sealed class StockTextArchiveDirectFilingViewModel : ViewModelBase
    {
        private readonly IStockTextArchiveDirectFilingService _filingService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

        private string _year = string.Empty;
        private string _projectName = string.Empty;
        private string _projectCode = string.Empty;
        private string _materialName = string.Empty;
        private string _previousProjectNameForMaterialDefault = string.Empty;
        private string _projectHint = "请填写实施年度与项目名称。";
        private string _yearProjectHint = "填写年度后，可查看该年已有项目。";
        private string _businessNumberHint = "确认立档后：生成一条建档单号，并新建一个模拟档案盒。";
        private string _previewArchiveSequenceNo = string.Empty;
        private int _archiveNoPreviewToken;
        private readonly string _sourceType = ArchiveRegisterDomainValues.SourceTypeStockDirect;
        private string _archivePurpose = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;
        private string _confidentialLevel = "秘密";
        private readonly string _provideUnit = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom;
        private string _selectedSpec = "标准(5cm)";
        private ArchiveBoxTargetLocationOption? _selectedSlotOption;
        private string _boxLocationPreview = string.Empty;
        private string _remarks = string.Empty;
        private string _selectedSimulatedMediaType = ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper;
        private bool _isBusy;
        private bool _suppressSlotApply;

        private readonly List<string> _textSubCategories = new();
        private readonly List<string> _mapSubCategories = new();

        public StockTextArchiveDirectFilingViewModel(
            IStockTextArchiveDirectFilingService filingService,
            IArchiveRegisterService archiveRegisterService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _filingService = filingService;
            _archiveRegisterService = archiveRegisterService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            MediaGroups = new ObservableCollection<StockTextArchiveMediaGroupViewModel>();
            MediaGroups.CollectionChanged += OnMediaGroupsCollectionChanged;

            ViewYearProjectsCommand = new RelayCommand(_ => ViewYearProjects(), _ => !IsBusy);
            AddMediaGroupCommand = new RelayCommand(_ => AddMediaGroup(), _ => !IsBusy);
            RemoveMediaGroupCommand = new RelayCommand(
                param => RemoveMediaGroup(param as StockTextArchiveMediaGroupViewModel),
                param => !IsBusy && param is StockTextArchiveMediaGroupViewModel);
            AddMediaItemCommand = new RelayCommand(
                param => AddMediaItem(param as StockTextArchiveMediaGroupViewModel),
                param => !IsBusy && param is StockTextArchiveMediaGroupViewModel);
            RemoveMediaItemCommand = new RelayCommand(
                param => RemoveMediaItem(param as StockTextArchiveMediaItemViewModel),
                param => !IsBusy && param is StockTextArchiveMediaItemViewModel);
            RecommendSlotCommand = new RelayCommand(async _ => await RecommendSlotAsync(), _ => !IsBusy);
            ShowSlotSnapshotCommand = new RelayCommand(async _ => await ShowSlotSnapshotAsync(), _ => !IsBusy && CanShowSlotSnapshot);
            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => !IsBusy);
            RefreshSlotOptionsCommand = new RelayCommand(async _ => await LoadSlotOptionsAsync(), _ => !IsBusy);
        }

        public ObservableCollection<string> ArchivePurposeOptions { get; } = new();
        public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();
        public ObservableCollection<string> YearOptions { get; } = new();
        public ObservableCollection<string> ProjectNameOptions { get; } = new();
        public ObservableCollection<string> MediaTypeOptions { get; } = new();
        public ObservableCollection<string> MaterialCategoryOptions { get; } = new();
        public ObservableCollection<string> OrganizationFormOptions { get; } = new();
        public ObservableCollection<string> Specs { get; } = new()
        {
            "标准(10cm)", "标准(5cm)", "标准(3cm)", "标准(2cm)", "非标(10cm)"
        };
        public ObservableCollection<ArchiveBoxTargetLocationOption> SlotOptions { get; } = new();
        public ObservableCollection<StockTextArchiveMediaGroupViewModel> MediaGroups { get; }

        public ICommand ViewYearProjectsCommand { get; }
        public ICommand AddMediaGroupCommand { get; }
        public ICommand RemoveMediaGroupCommand { get; }
        public ICommand AddMediaItemCommand { get; }
        public ICommand RemoveMediaItemCommand { get; }
        public ICommand RecommendSlotCommand { get; }
        public ICommand ShowSlotSnapshotCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand RefreshSlotOptionsCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string Year
        {
            get => _year;
            set
            {
                if (SetProperty(ref _year, value))
                {
                    EnsureOption(YearOptions, value);
                    RefreshProjectNameOptions();
                    RefreshProjectHint();
                    RefreshYearProjectHint();
                    _ = RefreshPreviewArchiveSequenceNoAsync();
                    _ = LoadSlotOptionsAsync();
                }
            }
        }

        public string ProjectName
        {
            get => _projectName;
            set
            {
                string previous = _projectName;
                if (SetProperty(ref _projectName, value))
                {
                    EnsureOption(ProjectNameOptions, value);
                    ApplyMaterialNameDefaultFromProject(previous, value);
                    TryFillProjectCodeFromRegistered(value);
                    RefreshProjectHint();
                    _ = LoadSlotOptionsAsync();
                }
            }
        }

        public string ProjectCode
        {
            get => _projectCode;
            set => SetProperty(ref _projectCode, value);
        }

        public string MaterialName
        {
            get => _materialName;
            set => SetProperty(ref _materialName, value);
        }

        public string ProjectHint
        {
            get => _projectHint;
            private set => SetProperty(ref _projectHint, value);
        }

        public string YearProjectHint
        {
            get => _yearProjectHint;
            private set => SetProperty(ref _yearProjectHint, value);
        }

        public string BusinessNumberHint
        {
            get => _businessNumberHint;
            private set => SetProperty(ref _businessNumberHint, value);
        }

        public string PreviewArchiveSequenceNo
        {
            get => _previewArchiveSequenceNo;
            private set
            {
                if (SetProperty(ref _previewArchiveSequenceNo, value))
                {
                    OnPropertyChanged(nameof(PageTitle));
                    RefreshBusinessNumberHint();
                }
            }
        }

        public string PageTitle
        {
            get
            {
                string archiveNo = string.IsNullOrWhiteSpace(PreviewArchiveSequenceNo)
                    ? "待填写年度"
                    : PreviewArchiveSequenceNo.Trim();
                return $"存档文本直办立档 · {archiveNo}";
            }
        }

        public string SourceType => _sourceType;

        public string ArchivePurpose
        {
            get => _archivePurpose;
            set => SetProperty(ref _archivePurpose, value);
        }

        public string ConfidentialLevel
        {
            get => _confidentialLevel;
            set => SetProperty(ref _confidentialLevel, value);
        }

        public string SelectedSimulatedMediaType
        {
            get => _selectedSimulatedMediaType;
            set
            {
                if (SetProperty(ref _selectedSimulatedMediaType, value))
                {
                    foreach (var group in MediaGroups)
                    {
                        group.MediaType = value ?? string.Empty;
                    }
                }
            }
        }

        public string ProvideUnit => _provideUnit;

        public string SelectedSpec
        {
            get => _selectedSpec;
            set
            {
                if (SetProperty(ref _selectedSpec, value))
                {
                    _ = LoadSlotOptionsAsync();
                }
            }
        }

        public ArchiveBoxTargetLocationOption? SelectedSlotOption
        {
            get => _selectedSlotOption;
            set
            {
                if (SetProperty(ref _selectedSlotOption, value) && !_suppressSlotApply)
                {
                    ApplySelectedSlot(value);
                }
            }
        }

        public string BoxLocationPreview
        {
            get => _boxLocationPreview;
            private set
            {
                if (SetProperty(ref _boxLocationPreview, value))
                {
                    OnPropertyChanged(nameof(CanShowSlotSnapshot));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        public bool CanShowSlotSnapshot => SelectedSlotOption != null
            && !string.IsNullOrWhiteSpace(SelectedSlotOption.CabinetName);

        public int DataSimulatedMediaCount => MediaGroups.Count;

        public string MediaSummary
        {
            get
            {
                int groupCount = MediaGroups.Count;
                int itemCount = MediaGroups.Sum(group => group.Items.Count);
                return $"共 {groupCount} 组介质、{itemCount} 条子项。";
            }
        }

        public async Task InitializeAsync()
        {
            var options = await _archiveRegisterService.GetPageDomainOptionsAsync();
            Replace(ArchivePurposeOptions, options.ArchivePurposes, ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage);
            Replace(ConfidentialLevelOptions, options.ConfidentialLevels, "秘密");
            Replace(MediaTypeOptions, options.DataSimulatedMediaTypes, ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper);
            Replace(MaterialCategoryOptions, options.SimulatedMaterialCategories, ArchiveRegisterDomainValues.SimulatedMaterialCategoryText);
            Replace(OrganizationFormOptions, options.SimulatedOrganizationForms, ArchiveRegisterDomainValues.SimulatedOrganizationFormBound);
            _textSubCategories.Clear();
            _textSubCategories.AddRange(
                options.SimulatedTextSubCategories.Count > 0
                    ? options.SimulatedTextSubCategories
                    : ArchiveRegisterDomainValues.SimulatedTextSubCategories);
            _mapSubCategories.Clear();
            _mapSubCategories.AddRange(
                options.SimulatedMapSubCategories.Count > 0
                    ? options.SimulatedMapSubCategories
                    : ArchiveRegisterDomainValues.SimulatedMapSubCategories);
            if (string.IsNullOrWhiteSpace(SelectedSimulatedMediaType) || !MediaTypeOptions.Contains(SelectedSimulatedMediaType))
            {
                SelectedSimulatedMediaType = MediaTypeOptions.FirstOrDefault()
                    ?? ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper;
            }
            RefreshYearOptions();
            RefreshProjectNameOptions();

            if (string.IsNullOrWhiteSpace(ArchivePurpose) || !ArchivePurposeOptions.Contains(ArchivePurpose))
            {
                ArchivePurpose = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;
            }

            if (string.IsNullOrWhiteSpace(ConfidentialLevel) || !ConfidentialLevelOptions.Contains(ConfidentialLevel))
            {
                ConfidentialLevel = ConfidentialLevelOptions.Contains("秘密")
                    ? "秘密"
                    : ConfidentialLevelOptions.FirstOrDefault() ?? "秘密";
            }

            if (MediaGroups.Count == 0)
            {
                AddMediaGroup();
            }

            await RefreshPreviewArchiveSequenceNoAsync();
            await LoadSlotOptionsAsync();
        }

        private void RefreshYearOptions()
        {
            string current = Year?.Trim() ?? string.Empty;
            YearOptions.Clear();
            foreach (string year in _filingService.ListRegisteredYears())
            {
                YearOptions.Add(year);
            }

            EnsureOption(YearOptions, current);
        }

        private void RefreshProjectNameOptions()
        {
            string current = ProjectName?.Trim() ?? string.Empty;
            ProjectNameOptions.Clear();
            foreach (string name in _filingService.ListRegisteredProjectNames(Year))
            {
                ProjectNameOptions.Add(name);
            }

            EnsureOption(ProjectNameOptions, current);
        }

        private void TryFillProjectCodeFromRegistered(string? projectName)
        {
            string normalizedName = projectName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(Year))
            {
                return;
            }

            var match = _filingService.ListProjectsByYear(Year)
                .FirstOrDefault(item =>
                    string.Equals(item.ProjectName?.Trim(), normalizedName, StringComparison.Ordinal)
                    && item.Id > 0
                    && !string.IsNullOrWhiteSpace(item.ProjectCode));
            if (match != null)
            {
                ProjectCode = match.ProjectCode.Trim();
            }
        }

        private void ApplyMaterialNameDefaultFromProject(string previousProjectName, string newProjectName)
        {
            string trimmedNew = newProjectName?.Trim() ?? string.Empty;
            string trimmedMaterial = MaterialName?.Trim() ?? string.Empty;
            string trimmedPrevious = previousProjectName?.Trim() ?? string.Empty;
            string lastDefault = _previousProjectNameForMaterialDefault?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(trimmedMaterial)
                || string.Equals(trimmedMaterial, trimmedPrevious, StringComparison.Ordinal)
                || string.Equals(trimmedMaterial, lastDefault, StringComparison.Ordinal))
            {
                MaterialName = trimmedNew;
                _previousProjectNameForMaterialDefault = trimmedNew;
            }
        }

        private void AddMediaGroup()
        {
            var group = new StockTextArchiveMediaGroupViewModel(
                SelectedSimulatedMediaType,
                ConfidentialLevelOptions,
                ConfidentialLevel,
                MaterialCategoryOptions,
                OrganizationFormOptions,
                _textSubCategories,
                _mapSubCategories);
            group.AddItem(ConfidentialLevel);
            AttachMediaGroup(group);
            MediaGroups.Add(group);
            RaiseMediaSummaryChanged();
        }

        private void RemoveMediaGroup(StockTextArchiveMediaGroupViewModel? group)
        {
            if (group == null)
            {
                return;
            }

            if (MediaGroups.Count <= 1)
            {
                _dialogService.ShowMessage("至少保留一组资料介质。");
                return;
            }

            DetachMediaGroup(group);
            MediaGroups.Remove(group);
            RaiseMediaSummaryChanged();
        }

        private void AddMediaItem(StockTextArchiveMediaGroupViewModel? group)
        {
            group?.AddItem(ConfidentialLevel);
            RaiseMediaSummaryChanged();
        }

        private void RemoveMediaItem(StockTextArchiveMediaItemViewModel? item)
        {
            if (item?.Owner == null)
            {
                return;
            }

            if (item.Owner.Items.Count <= 1)
            {
                _dialogService.ShowMessage("每组介质至少保留一条资料子项。");
                return;
            }

            item.Owner.Items.Remove(item);
            item.Owner.RefreshMediaCount();
            RaiseMediaSummaryChanged();
        }

        private void OnMediaGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaiseMediaSummaryChanged();
        }

        private void AttachMediaGroup(StockTextArchiveMediaGroupViewModel group)
        {
            group.PropertyChanged += OnMediaGroupPropertyChanged;
            group.Items.CollectionChanged += OnMediaItemsChanged;
        }

        private void DetachMediaGroup(StockTextArchiveMediaGroupViewModel group)
        {
            group.PropertyChanged -= OnMediaGroupPropertyChanged;
            group.Items.CollectionChanged -= OnMediaItemsChanged;
        }

        private void OnMediaGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
            => RaiseMediaSummaryChanged();

        private void OnMediaItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => RaiseMediaSummaryChanged();

        private void RaiseMediaSummaryChanged()
        {
            OnPropertyChanged(nameof(MediaSummary));
            OnPropertyChanged(nameof(DataSimulatedMediaCount));
            RefreshBusinessNumberHint();
        }

        private async Task LoadSlotOptionsAsync()
        {
            if (string.IsNullOrWhiteSpace(Year) || string.IsNullOrWhiteSpace(SelectedSpec))
            {
                SlotOptions.Clear();
                return;
            }

            try
            {
                var options = await _filingService.GetBoxSlotOptionsAsync(
                    ProjectName,
                    Year,
                    SelectedSpec);
                string? previousLocation = SelectedSlotOption?.Location;
                SlotOptions.Clear();
                foreach (var option in options)
                {
                    SlotOptions.Add(option);
                }

                _suppressSlotApply = true;
                try
                {
                    SelectedSlotOption = SlotOptions.FirstOrDefault(item =>
                            string.Equals(item.Location, previousLocation, StringComparison.OrdinalIgnoreCase))
                        ?? SlotOptions.FirstOrDefault();
                }
                finally
                {
                    _suppressSlotApply = false;
                }

                ApplySelectedSlot(SelectedSlotOption);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("加载可选档口失败：" + ex.Message);
            }
        }

        private async Task RecommendSlotAsync()
        {
            if (string.IsNullOrWhiteSpace(Year))
            {
                _dialogService.ShowMessage("请先填写实施年度，再推荐档口。");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedSpec))
            {
                _dialogService.ShowMessage("请先选择档案盒规格。");
                return;
            }

            await LoadSlotOptionsAsync();
            var suggestion = await _filingService.SuggestBoxSlotAsync(ProjectName, Year, SelectedSpec);
            if (suggestion == null || string.IsNullOrWhiteSpace(suggestion.CabinetName))
            {
                _dialogService.ShowMessage("未找到可用的年度资料专用档口，请先在标准档案柜开柜界面完成用途设置。");
                return;
            }

            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                suggestion.CabinetName,
                suggestion.Side,
                suggestion.Row,
                suggestion.Column);

            _suppressSlotApply = true;
            try
            {
                SelectedSlotOption = SlotOptions.FirstOrDefault(item =>
                    string.Equals(item.Location, slotKey, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _suppressSlotApply = false;
            }

            if (SelectedSlotOption == null)
            {
                var option = new ArchiveBoxTargetLocationOption
                {
                    Location = slotKey,
                    CabinetName = suggestion.CabinetName,
                    Side = suggestion.Side,
                    Row = suggestion.Row,
                    Column = suggestion.Column,
                    ExistingBoxCount = suggestion.ExistingBoxCount,
                    Priority = 0,
                    FitsCapacity = true
                };
                SlotOptions.Insert(0, option);
                _suppressSlotApply = true;
                try
                {
                    SelectedSlotOption = option;
                }
                finally
                {
                    _suppressSlotApply = false;
                }
            }

            ApplySelectedSlot(SelectedSlotOption);
            if (!string.IsNullOrWhiteSpace(suggestion.SuggestionSummary))
            {
                BoxLocationPreview = $"{slotKey}　{suggestion.SuggestionSummary}";
            }
        }

        private void ApplySelectedSlot(ArchiveBoxTargetLocationOption? option)
        {
            if (option == null)
            {
                BoxLocationPreview = string.Empty;
                return;
            }

            BoxLocationPreview =
                $"{option.Location}（当前 {option.ExistingBoxCount} 盒；确认写入时分配最小可用盒内序号）";
        }

        private async Task ShowSlotSnapshotAsync()
        {
            if (SelectedSlotOption == null)
            {
                _dialogService.ShowMessage("请先推荐空位或从列表选择档口后再查看快照。");
                return;
            }

            var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                .FirstOrDefault(item =>
                    string.Equals(item.Name, SelectedSlotOption.CabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                _dialogService.ShowMessage($"未找到柜号 [{SelectedSlotOption.CabinetName}] 对应的资料柜。");
                return;
            }

            CabinetFace face = string.Equals(SelectedSlotOption.Side, "B", StringComparison.OrdinalIgnoreCase)
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
                TargetSlotCode = $"{SelectedSlotOption.Row}-{SelectedSlotOption.Column}",
                WidthCm = cabinet.Width,
                HeightCm = cabinet.Height,
                DepthCm = cabinet.Depth
            });
        }

        private void ViewYearProjects()
        {
            if (string.IsNullOrWhiteSpace(Year))
            {
                _dialogService.ShowMessage("请先填写实施年度，再查看该年已有项目。", "年度已有项目");
                return;
            }

            var projects = _filingService.ListProjectsByYear(Year);
            RefreshYearProjectHint(projects);
            var selected = _dialogService.ShowYearProjectPickDialog(Year, projects);
            if (selected == null)
            {
                return;
            }

            ProjectName = selected.ProjectName?.Trim() ?? string.Empty;
            ProjectCode = selected.ProjectCode?.Trim() ?? string.Empty;
            EnsureOption(ProjectNameOptions, ProjectName);
            RefreshProjectHint();
        }

        private void RefreshProjectHint()
        {
            if (string.IsNullOrWhiteSpace(Year) || string.IsNullOrWhiteSpace(ProjectName))
            {
                ProjectHint = "请填写实施年度与项目名称。";
                return;
            }

            var project = _filingService.FindProject(Year, ProjectName);
            ProjectHint = project == null
                ? "库中无此年度/项目，提交时将自动新建（项目编号非必填）。若实为已有项目，请先查看年度已有项目并采用其名称。"
                : $"已匹配项目：{project.ProjectName}（{project.ImplementYear}）";
        }

        private void RefreshYearProjectHint(IReadOnlyList<ProjectInfo>? projects = null)
        {
            if (string.IsNullOrWhiteSpace(Year))
            {
                YearProjectHint = "填写年度后，可查看该年已有项目。";
                return;
            }

            IReadOnlyList<ProjectInfo> yearProjects = projects ?? _filingService.ListProjectsByYear(Year);
            YearProjectHint = yearProjects.Count == 0
                ? $"库中尚无 {Year.Trim()} 年度项目（含模拟盒/电子袋），提交时将按当前名称新建。"
                : $"{Year.Trim()} 年度已登记 {yearProjects.Count} 个项目（项目信息 ∪ 模拟盒 ∪ 电子袋）。同一项目请采用已有名称，勿另起别名。";
        }

        private async Task RefreshPreviewArchiveSequenceNoAsync()
        {
            int token = ++_archiveNoPreviewToken;
            string year = Year?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(year))
            {
                PreviewArchiveSequenceNo = string.Empty;
                return;
            }

            try
            {
                string preview = await _filingService.PeekNextArchiveSequenceNoAsync(year);
                if (token != _archiveNoPreviewToken)
                {
                    return;
                }

                PreviewArchiveSequenceNo = preview;
            }
            catch
            {
                if (token != _archiveNoPreviewToken)
                {
                    return;
                }

                PreviewArchiveSequenceNo = $"年度模拟-{year}-???";
            }
        }

        private void RefreshBusinessNumberHint()
        {
            int itemCount = MediaGroups.Sum(group => group.Items.Count);
            if (itemCount <= 0 || string.IsNullOrWhiteSpace(Year))
            {
                BusinessNumberHint = "确认立档后：生成一条建档单号，并新建一个模拟档案盒。";
                return;
            }

            string previewBoxNo = string.IsNullOrWhiteSpace(PreviewArchiveSequenceNo)
                ? "年度模拟-????-???"
                : PreviewArchiveSequenceNo;
            BusinessNumberHint =
                $"确认立档后将生成 1 条建档单号，档案盒号预计为 [{previewBoxNo}]（确认写入时按当时库内最大号顺延；建档单号与盒号年度均取自项目实施年度）。{MediaSummary}";
        }

        private async Task ConfirmAsync()
        {
            if (!_archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser))
            {
                _dialogService.ShowMessage("仅资料室资料管理员可执行存档文本直办立档。");
                return;
            }

            var request = BuildRequest();
            var errors = await _filingService.CollectCommitErrorsAsync(request, _userContextService.CurrentUser);
            if (errors.Count > 0)
            {
                _dialogService.ShowMessage(string.Join(Environment.NewLine, errors), "请先完善立档信息");
                return;
            }

            string confirmText =
                $"即将以档案盒为单位直接立档（跳过申请审批）。\n"
                + $"年度/项目：{Year} / {ProjectName}\n"
                + $"资料名称：{MaterialName}\n"
                + $"来源：{SourceType}　提供单位：{ProvideUnit}\n"
                + $"盒规格：{SelectedSpec}\n"
                + $"档口：{SelectedSlotOption?.Location}\n"
                + $"{MediaSummary}\n"
                + $"{BusinessNumberHint}\n\n是否继续？";
            if (!_dialogService.ShowConfirm(confirmText, "确认存档文本直办立档"))
            {
                return;
            }

            IsBusy = true;
            _dialogService.SetBusyState(true);
            try
            {
                var result = await _filingService.CommitAsync(request, _userContextService.CurrentUser);
                if (!result.Succeeded)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ResetForNextFilingAsync();
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
            }
        }

        private StockTextArchiveDirectFilingRequest BuildRequest()
        {
            var groups = MediaGroups.Select(group => new StockTextArchiveMediaGroupDraft
            {
                MediaType = group.MediaType?.Trim() ?? string.Empty,
                Items = group.Items.Select(item => new StockTextArchiveMediaItemDraft
                {
                    ContentDesc = item.ContentDesc?.Trim() ?? string.Empty,
                    ConfidentialLevel = item.ConfidentialLevel?.Trim() ?? string.Empty,
                    ContentCount = item.ContentCount,
                    Note = item.Note?.Trim() ?? string.Empty,
                    MaterialCategory = item.MaterialCategory?.Trim() ?? string.Empty,
                    SubCategory = item.SubCategory?.Trim() ?? string.Empty,
                    OrganizationForm = item.OrganizationForm?.Trim() ?? string.Empty
                }).ToList()
            }).ToList();

            return new StockTextArchiveDirectFilingRequest
            {
                Year = Year?.Trim() ?? string.Empty,
                ProjectName = ProjectName?.Trim() ?? string.Empty,
                ProjectCode = ProjectCode?.Trim() ?? string.Empty,
                MaterialName = MaterialName?.Trim() ?? string.Empty,
                SourceType = SourceType,
                ArchivePurpose = ArchivePurpose?.Trim() ?? string.Empty,
                ConfidentialLevel = ConfidentialLevel?.Trim() ?? string.Empty,
                ProvideUnit = ProvideUnit,
                BoxSpecification = SelectedSpec?.Trim() ?? string.Empty,
                CabinetName = SelectedSlotOption?.CabinetName?.Trim() ?? string.Empty,
                Side = SelectedSlotOption?.Side?.Trim() ?? string.Empty,
                Row = SelectedSlotOption?.Row ?? 0,
                Column = SelectedSlotOption?.Column ?? 0,
                Remarks = Remarks?.Trim() ?? string.Empty,
                MediaGroups = groups
            };
        }

        private async Task ResetForNextFilingAsync()
        {
            Year = string.Empty;
            ProjectName = string.Empty;
            ProjectCode = string.Empty;
            MaterialName = string.Empty;
            _previousProjectNameForMaterialDefault = string.Empty;
            Remarks = string.Empty;
            PreviewArchiveSequenceNo = string.Empty;
            ProjectHint = "请填写实施年度与项目名称。";
            YearProjectHint = "填写年度后，可查看该年已有项目。";
            BusinessNumberHint = "确认立档后：生成一条建档单号，并新建一个模拟档案盒。";
            SelectedSpec = "标准(5cm)";

            foreach (var group in MediaGroups.ToList())
            {
                DetachMediaGroup(group);
            }

            MediaGroups.Clear();
            AddMediaGroup();

            _suppressSlotApply = true;
            try
            {
                SelectedSlotOption = null;
            }
            finally
            {
                _suppressSlotApply = false;
            }

            BoxLocationPreview = string.Empty;
            SlotOptions.Clear();
            RefreshYearOptions();
            RefreshProjectNameOptions();
            await InitializeAsync();
        }

        private static void EnsureOption(ObservableCollection<string> target, string? value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return;
            }

            if (!target.Any(item => string.Equals(item, trimmed, StringComparison.Ordinal)))
            {
                target.Add(trimmed);
            }
        }

        private static void Replace(ObservableCollection<string> target, IReadOnlyList<string> values, string preferred)
        {
            target.Clear();
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Add(value);
                }
            }

            if (!string.IsNullOrWhiteSpace(preferred) && !target.Contains(preferred))
            {
                target.Insert(0, preferred);
            }
        }
    }

    /// <summary>
    /// 存档文本直办：介质组录入行。
    /// </summary>
    public sealed class StockTextArchiveMediaGroupViewModel : ViewModelBase
    {
        private string _mediaType;

        public StockTextArchiveMediaGroupViewModel(
            string mediaType,
            ObservableCollection<string> confidentialLevelOptions,
            string defaultConfidentialLevel,
            ObservableCollection<string> materialCategoryOptions,
            ObservableCollection<string> organizationFormOptions,
            IReadOnlyList<string> textSubCategories,
            IReadOnlyList<string> mapSubCategories)
        {
            _mediaType = mediaType;
            ConfidentialLevelOptions = confidentialLevelOptions;
            DefaultConfidentialLevel = defaultConfidentialLevel;
            MaterialCategoryOptions = materialCategoryOptions;
            OrganizationFormOptions = organizationFormOptions;
            TextSubCategories = textSubCategories;
            MapSubCategories = mapSubCategories;
            Items = new ObservableCollection<StockTextArchiveMediaItemViewModel>();
            Items.CollectionChanged += (_, _) => RefreshMediaCount();
        }

        public ObservableCollection<string> ConfidentialLevelOptions { get; }

        public ObservableCollection<string> MaterialCategoryOptions { get; }

        public ObservableCollection<string> OrganizationFormOptions { get; }

        public IReadOnlyList<string> TextSubCategories { get; }

        public IReadOnlyList<string> MapSubCategories { get; }

        public string DefaultConfidentialLevel { get; set; }

        public ObservableCollection<StockTextArchiveMediaItemViewModel> Items { get; }

        public string MediaKind => ArchiveRegisterDomainValues.MediaKindSimulated;

        public string MediaType
        {
            get => _mediaType;
            set => SetProperty(ref _mediaType, value);
        }

        public int MediaCount => Items.Count;

        public void AddItem(string? confidentialLevel)
        {
            string level = string.IsNullOrWhiteSpace(confidentialLevel)
                ? DefaultConfidentialLevel
                : confidentialLevel.Trim();
            Items.Add(new StockTextArchiveMediaItemViewModel(this)
            {
                ConfidentialLevel = level,
                ContentCount = 1,
                MaterialCategory = ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                OrganizationForm = ArchiveRegisterDomainValues.SimulatedOrganizationFormBound
            });
            RefreshMediaCount();
        }

        public void RefreshMediaCount() => OnPropertyChanged(nameof(MediaCount));
    }

    /// <summary>
    /// 存档文本直办：资料子项录入行。
    /// </summary>
    public sealed class StockTextArchiveMediaItemViewModel : ViewModelBase
    {
        private string _contentDesc = string.Empty;
        private string _confidentialLevel = "秘密";
        private int _contentCount = 1;
        private string _note = string.Empty;
        private string _materialCategory = ArchiveRegisterDomainValues.SimulatedMaterialCategoryText;
        private string _subCategory = string.Empty;
        private string _organizationForm = ArchiveRegisterDomainValues.SimulatedOrganizationFormBound;

        public StockTextArchiveMediaItemViewModel(StockTextArchiveMediaGroupViewModel owner)
        {
            Owner = owner;
            RefreshSubCategoryOptions();
        }

        public StockTextArchiveMediaGroupViewModel Owner { get; }

        public string ContentDesc
        {
            get => _contentDesc;
            set => SetProperty(ref _contentDesc, value);
        }

        public string ConfidentialLevel
        {
            get => _confidentialLevel;
            set => SetProperty(ref _confidentialLevel, value);
        }

        public int ContentCount
        {
            get => _contentCount;
            set => SetProperty(ref _contentCount, value);
        }

        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        public string MaterialCategory
        {
            get => _materialCategory;
            set
            {
                if (SetProperty(ref _materialCategory, value))
                {
                    RefreshSubCategoryOptions();
                }
            }
        }

        public string SubCategory
        {
            get => _subCategory;
            set => SetProperty(ref _subCategory, value);
        }

        public string OrganizationForm
        {
            get => _organizationForm;
            set => SetProperty(ref _organizationForm, value);
        }

        public ObservableCollection<string> AvailableSubCategories { get; } = new();

        private void RefreshSubCategoryOptions()
        {
            var domainOptions = string.Equals(_materialCategory, ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap, StringComparison.Ordinal)
                ? Owner.MapSubCategories
                : Owner.TextSubCategories;
            var options = domainOptions.Count > 0
                ? domainOptions
                : ArchiveRegisterDomainValues.GetSimulatedSubCategories(_materialCategory);
            AvailableSubCategories.Clear();
            foreach (var option in options)
            {
                AvailableSubCategories.Add(option);
            }

            if (string.IsNullOrWhiteSpace(SubCategory) || !AvailableSubCategories.Contains(SubCategory))
            {
                SubCategory = AvailableSubCategories.FirstOrDefault() ?? string.Empty;
            }
        }
    }
}
