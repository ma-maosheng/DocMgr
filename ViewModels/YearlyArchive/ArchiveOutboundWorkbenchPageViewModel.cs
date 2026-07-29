using System.Collections.ObjectModel;

using System.ComponentModel;

using DocMgr.Models.SystemSettings;

using DocMgr.Models.Shared;

using DocMgr.Models.YearlyArchive;

using DocMgr.Services.Interfaces;

using DocMgr.ViewModels.Base;

using Microsoft.Extensions.DependencyInjection;



namespace DocMgr.ViewModels.YearlyArchive

{

    public sealed partial class ArchiveOutboundWorkbenchPageViewModel : ViewModelBase

    {

        private const string AllApplicantsText = "全部申请人";



        private readonly IArchiveOutboundService _outboundService;

        private readonly IDialogService _dialogService;

        private readonly IUserContextService _userContextService;

        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;

        private readonly ArchiveOutboundWorkspaceMode _workspaceMode;

        private readonly IServiceScopeFactory _scopeFactory;

        private readonly List<YearlyArchiveOutboundRecord> _allRecords = new();



        private bool _isInitialized;

        private bool _isUpdatingFilters;

        private bool _isApplicantPopupOpen;

        private int _selectedYear = DateTime.Today.Year;

        private YearlyArchiveOutboundRecord? _selectedRecord;

        private string _applicationOverdueSettingCode = ApplicationOverdueDomainValues.Default;

        private OutboundStatusFilterOption? _selectedStatusFilter;

        private int? _selectedProjectId;

        private string _selectedMediaKind = string.Empty;



        public ArchiveOutboundWorkbenchPageViewModel(

            IArchiveOutboundService outboundService,

            IDialogService dialogService,

            IUserContextService userContextService,

            IBusinessLogicSettingsService businessLogicSettingsService,

            IServiceScopeFactory scopeFactory,

            ArchiveOutboundWorkspaceMode workspaceMode,

            int initialRecordId = 0)

        {

            _outboundService = outboundService;

            _dialogService = dialogService;

            _userContextService = userContextService;

            _businessLogicSettingsService = businessLogicSettingsService;

            _scopeFactory = scopeFactory;

            _workspaceMode = workspaceMode;

            PendingSelectionRecordId = initialRecordId > 0 ? initialRecordId : null;



            StatusFilterOptions = CreateStatusFilterOptions(workspaceMode);

            _selectedStatusFilter = StatusFilterOptions.FirstOrDefault();



            AddCommand = new RelayCommand(async _ => await AddAsync(), _ => CanAdd());

            OpenCommand = new RelayCommand(async _ => await OpenAsync(), _ => SelectedRecord != null);

            ViewCommand = new RelayCommand(_ => ViewSelectedRecord(), _ => SelectedRecord != null);

            ApproveCommand = new RelayCommand(async _ => await OpenAsync(), _ => CanApprove());

            DestructiveCommand = new RelayCommand(async _ => await ExecuteDestructiveAsync(), _ => CanExecuteDestructive());

            RefreshCommand = new RelayCommand(async _ => await LoadRecordsAsync());

            InitializeInlineEditingCommands();

        }



        public int? PendingSelectionRecordId { get; private set; }



        public ObservableCollection<int> YearOptions { get; } = new();



        public ObservableCollection<YearlyArchiveOutboundRecord> Records { get; } = new();



        public ObservableCollection<ArchiveRegisterFilterOptionViewModel> ApplicantOptions { get; } = new();

        public ObservableCollection<ProjectFilterOption> ProjectOptions { get; } = new();

        public ObservableCollection<OutboundMediaKindFilterOption> MediaKindOptions { get; } =
        [
            new OutboundMediaKindFilterOption("全部介质", string.Empty),
            new OutboundMediaKindFilterOption(ArchiveRegisterDomainValues.MediaKindSimulated, ArchiveRegisterDomainValues.MediaKindSimulated),
            new OutboundMediaKindFilterOption(ArchiveRegisterDomainValues.MediaKindElectronic, ArchiveRegisterDomainValues.MediaKindElectronic)
        ];



        public IReadOnlyList<OutboundStatusFilterOption> StatusFilterOptions { get; }



        public bool ShowApplicantFilter => _workspaceMode == ArchiveOutboundWorkspaceMode.Approval
            || _workspaceMode == ArchiveOutboundWorkspaceMode.Handover;

        public bool ShowProjectFilter => _workspaceMode == ArchiveOutboundWorkspaceMode.Handover;

        public bool ShowMediumFilter => _workspaceMode == ArchiveOutboundWorkspaceMode.Handover;



        public bool ShowInteractiveStatusFilter => _workspaceMode == ArchiveOutboundWorkspaceMode.Application
            || _workspaceMode == ArchiveOutboundWorkspaceMode.Approval
            || _workspaceMode == ArchiveOutboundWorkspaceMode.Handover;



        public int SelectedYear

        {

            get => _selectedYear;

            set

            {

                if (SetProperty(ref _selectedYear, value) && _isInitialized)

                {

                    _ = LoadRecordsAsync();

                }

            }

        }



        public OutboundStatusFilterOption? SelectedStatusFilter

        {

            get => _selectedStatusFilter;

            set

            {

                if (SetProperty(ref _selectedStatusFilter, value) && _isInitialized)

                {

                    _ = LoadRecordsAsync();

                }

            }

        }

        public int? SelectedProjectId

        {

            get => _selectedProjectId;

            set

            {

                if (SetProperty(ref _selectedProjectId, value) && _isInitialized)

                {

                    ApplyRecordFilters();

                }

            }

        }

        public OutboundMediaKindFilterOption? SelectedMediaKindOption

        {

            get => MediaKindOptions.FirstOrDefault(option => option.Value == _selectedMediaKind)
                ?? MediaKindOptions.FirstOrDefault();

            set

            {

                string normalized = value?.Value ?? string.Empty;

                if (SetProperty(ref _selectedMediaKind, normalized) && _isInitialized)

                {

                    ApplyRecordFilters();

                }

            }

        }



        public YearlyArchiveOutboundRecord? SelectedRecord

        {

            get => _selectedRecord;

            set

            {

                if (SetProperty(ref _selectedRecord, value))

                {

                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();

                    if (HasEditingViewModel)

                    {

                        _ = SyncEditingPanelToSelectionAsync();

                    }

                }

            }

        }



        public bool IsApplicantPopupOpen

        {

            get => _isApplicantPopupOpen;

            set => SetProperty(ref _isApplicantPopupOpen, value);

        }



        public bool IsAllApplicantsSelected

        {

            get => ApplicantOptions.Count == 0 || ApplicantOptions.All(item => item.IsSelected);

            set

            {

                if (value)

                {

                    SetAllApplicantSelections(true);

                }

            }

        }



        public string SelectedApplicantSummary

        {

            get

            {

                var selectedApplicants = ApplicantOptions

                    .Where(item => item.IsSelected)

                    .Select(item => item.Label)

                    .ToList();



                return selectedApplicants.Count == 0 || selectedApplicants.Count == ApplicantOptions.Count

                    ? AllApplicantsText

                    : string.Join("、", selectedApplicants);

            }

        }



        public string PageTitle => _workspaceMode switch

        {

            ArchiveOutboundWorkspaceMode.Approval => "资料借出审批出库",

            ArchiveOutboundWorkspaceMode.Handover => "资料借出审批出库",

            _ => "资料借出申请"

        };



        public string PageSubtitle => _workspaceMode switch

        {

            ArchiveOutboundWorkspaceMode.Approval => "审批通过 → 实物交接 → 上传签批与照片 → 业务办结",

            ArchiveOutboundWorkspaceMode.Handover => "审批通过 → 实物交接 → 上传签批与照片 → 业务办结",

            _ => "Archive Outbound Application Ledger"

        };



        public string StatusFilterDisplayText => ShowInteractiveStatusFilter

            ? SelectedStatusFilter?.Label ?? "全部"

            : _workspaceMode switch

            {

                ArchiveOutboundWorkspaceMode.Application => "全部",

                _ => "待出库/已办结"

            };



        public string StatusFilterTooltipTitle => _workspaceMode switch

        {

            ArchiveOutboundWorkspaceMode.Approval => "可按状态筛选；选【全部】列出各状态申请（含未提交）",

            ArchiveOutboundWorkspaceMode.Handover => "可按状态筛选；选【全部】列出待出库与已办结记录",

            ArchiveOutboundWorkspaceMode.Application => "可按状态筛选；选【全部】列出各状态申请（含未提交）",

            _ => "办理状态已锁定为【待出库/已办结】"

        };



        public string AddActionText => "新增申请";



        public string OpenActionText => _workspaceMode switch

        {

            ArchiveOutboundWorkspaceMode.Approval => "打开办理",

            ArchiveOutboundWorkspaceMode.Handover => "打开办理",

            _ => "打开申请"

        };



        public string DestructiveActionText => _workspaceMode == ArchiveOutboundWorkspaceMode.Approval ? "强制作废" : "撤回申请";



        public bool ShowAddAction => _workspaceMode == ArchiveOutboundWorkspaceMode.Application;



        public bool ShowDestructiveAction => _workspaceMode != ArchiveOutboundWorkspaceMode.Handover;



        public RelayCommand AddCommand { get; }



        public RelayCommand OpenCommand { get; }



        public RelayCommand ViewCommand { get; }



        public RelayCommand ApproveCommand { get; }



        public RelayCommand DestructiveCommand { get; }



        public RelayCommand RefreshCommand { get; }



        public async Task InitializeAsync()

        {

            if (_isInitialized)

            {

                return;

            }



            bool openPendingRecord = PendingSelectionRecordId.HasValue;



            await LoadYearOptionsAsync();

            await LoadRecordsAsync();

            _isInitialized = true;



            if (openPendingRecord

                && SelectedRecord != null

                && (_workspaceMode == ArchiveOutboundWorkspaceMode.Approval

                    || _workspaceMode == ArchiveOutboundWorkspaceMode.Handover))

            {

                await OpenForInlineEditingAsync();

            }

        }



        public async Task OpenRecordByIdAsync(int recordId)

        {

            PendingSelectionRecordId = recordId;

            await LoadRecordsAsync();

            await OpenAsync();

        }



        private static IReadOnlyList<OutboundStatusFilterOption> CreateStatusFilterOptions(

            ArchiveOutboundWorkspaceMode workspaceMode)

        {

            if (workspaceMode == ArchiveOutboundWorkspaceMode.Handover)

            {

                return new List<OutboundStatusFilterOption>

                {

                    new("全部", null),

                    new(ApplicationWorkflowStatus.TextSignedUploaded, YearlyArchiveOutboundRecord.SignedUploaded),

                    new(ApplicationWorkflowStatus.TextCompleted, YearlyArchiveOutboundRecord.Completed),

                };

            }



            // Application / Approval：完整状态列表，默认【全部】
            return new List<OutboundStatusFilterOption>

            {

                new("全部", null),

                new(ApplicationWorkflowStatus.TextDraft, YearlyArchiveOutboundRecord.Unsubmitted),

                new(ApplicationWorkflowStatus.TextSubmitted, YearlyArchiveOutboundRecord.Submitted),

                new(ApplicationWorkflowStatus.TextApproved, YearlyArchiveOutboundRecord.Approved),

                new(ApplicationWorkflowStatus.TextSignedUploaded, YearlyArchiveOutboundRecord.SignedUploaded),

                new(ApplicationWorkflowStatus.TextCompleted, YearlyArchiveOutboundRecord.Completed),

                new(ApplicationWorkflowStatus.TextWithdrawn, YearlyArchiveOutboundRecord.WithdrawnVoid),

                new(ApplicationWorkflowStatus.TextForceWithdrawn, YearlyArchiveOutboundRecord.ForceVoided),

            };

        }



        private async Task LoadYearOptionsAsync()

        {

            List<int> years = await _outboundService.GetExistingApplyYearsAsync();

            int currentYear = DateTime.Today.Year;

            if (!years.Contains(currentYear))

            {

                years.Add(currentYear);

            }



            years = years

                .Distinct()

                .OrderByDescending(year => year)

                .ToList();



            YearOptions.Clear();

            foreach (int year in years)

            {

                YearOptions.Add(year);

            }



            if (YearOptions.Count == 0)

            {

                YearOptions.Add(currentYear);

            }



            if (!YearOptions.Contains(_selectedYear))

            {

                _selectedYear = YearOptions[0];

                OnPropertyChanged(nameof(SelectedYear));

            }

        }



        private bool CanAdd()
        {
            return _workspaceMode == ArchiveOutboundWorkspaceMode.Application
                   && _outboundService.CanSubmitApplication(_userContextService.CurrentUser);
        }



        private bool CanApprove()
        {
            if (SelectedRecord == null)
            {
                return false;
            }

            return _outboundService.IsArchiveAdminUser(_userContextService.CurrentUser)
                && IsApprovalProcessingStatus(SelectedRecord.Status);
        }

        private static bool IsApprovalProcessingStatus(int status)
        {
            return status == YearlyArchiveOutboundRecord.Submitted
                || status == YearlyArchiveOutboundRecord.Approved
                || status == YearlyArchiveOutboundRecord.SignedUploaded;
        }

        private bool CanExecuteDestructive()

        {

            if (SelectedRecord == null)

            {

                return false;

            }



            return _workspaceMode == ArchiveOutboundWorkspaceMode.Approval

                ? SelectedRecord.CanForceVoid

                  && _outboundService.IsArchiveAdminUser(_userContextService.CurrentUser)

                  && _businessLogicSettingsService.IsEligibleForAdminForceVoid(

                      ApplicationOverdueSettingSupport.ResolveOutboundApplyDate(SelectedRecord),

                      _applicationOverdueSettingCode)

                : SelectedRecord.CanApplicantWithdraw;

        }



        private async Task LoadRecordsAsync()

        {

            _applicationOverdueSettingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();



            var user = _userContextService.CurrentUser;

            if (user == null)

            {

                Records.Clear();

                SelectedRecord = null;

                _allRecords.Clear();

                UpdateApplicantOptions();

                return;

            }



            var records = await _outboundService.ListRecordsAsync(new OutboundListCriteria

            {

                Year = SelectedYear,

                WorkspaceMode = _workspaceMode,

                StatusFilter = ShowInteractiveStatusFilter ? SelectedStatusFilter?.Value : null,

                OnlyMine = _workspaceMode == ArchiveOutboundWorkspaceMode.Application

                    && !_outboundService.IsArchiveAdminUser(user)

            }, user);



            _allRecords.Clear();

            _allRecords.AddRange(records);



            UpdateApplicantOptions();

            UpdateProjectOptions();

            ApplyRecordFilters();

        }



        private void ApplyRecordFilters()

        {

            int? selectedId = PendingSelectionRecordId ?? SelectedRecord?.Id;



            IEnumerable<YearlyArchiveOutboundRecord> filtered = _allRecords;

            if (ShowApplicantFilter)

            {

                var selectedApplicants = ApplicantOptions

                    .Where(item => item.IsSelected)

                    .Select(item => item.Label)

                    .ToHashSet(StringComparer.OrdinalIgnoreCase);



                filtered = filtered.Where(item =>

                    selectedApplicants.Count == 0

                    || selectedApplicants.Contains(item.ApplicantName?.Trim() ?? string.Empty));

            }

            if (SelectedProjectId is int projectId)

            {

                filtered = filtered.Where(record => record.ProjectId == projectId);

            }

            if (!string.IsNullOrWhiteSpace(_selectedMediaKind))

            {

                filtered = filtered.Where(record => record.Items.Any(item =>

                    string.Equals(item.MediaKind?.Trim(), _selectedMediaKind, StringComparison.Ordinal)));

            }



            Records.Clear();

            foreach (var record in filtered.OrderByDescending(record => record.OutboundNo))

            {

                Records.Add(record);

            }



            SelectedRecord = selectedId.HasValue

                ? Records.FirstOrDefault(record => record.Id == selectedId.Value)

                : Records.FirstOrDefault();



            if (PendingSelectionRecordId.HasValue && SelectedRecord?.Id == PendingSelectionRecordId.Value)

            {

                PendingSelectionRecordId = null;

            }

        }



        private void UpdateApplicantOptions()

        {

            if (!ShowApplicantFilter)

            {

                foreach (var option in ApplicantOptions)

                {

                    option.PropertyChanged -= OnApplicantOptionPropertyChanged;

                }



                ApplicantOptions.Clear();

                return;

            }



            var selectedApplicants = ApplicantOptions

                .Where(item => item.IsSelected)

                .Select(item => item.Label)

                .ToHashSet(StringComparer.OrdinalIgnoreCase);



            foreach (var option in ApplicantOptions)

            {

                option.PropertyChanged -= OnApplicantOptionPropertyChanged;

            }



            var applicants = _allRecords

                .Where(item => !string.IsNullOrWhiteSpace(item.ApplicantName))

                .Select(item => item.ApplicantName.Trim())

                .Distinct(StringComparer.OrdinalIgnoreCase)

                .OrderBy(item => item, StringComparer.CurrentCulture)

                .ToList();



            bool selectAll = selectedApplicants.Count == 0 || selectedApplicants.Count == ApplicantOptions.Count;



            ApplicantOptions.Clear();

            foreach (var applicant in applicants)

            {

                var option = new ArchiveRegisterFilterOptionViewModel(applicant, selectAll || selectedApplicants.Contains(applicant));

                option.PropertyChanged += OnApplicantOptionPropertyChanged;

                ApplicantOptions.Add(option);

            }



            OnPropertyChanged(nameof(IsAllApplicantsSelected));

            OnPropertyChanged(nameof(SelectedApplicantSummary));

        }

        private void UpdateProjectOptions()

        {

            if (!ShowProjectFilter)

            {

                ProjectOptions.Clear();

                return;

            }



            int? previousSelection = SelectedProjectId;

            var projects = _allRecords

                .Where(record => record.ProjectId is > 0 && !string.IsNullOrWhiteSpace(record.ProjectName))

                .GroupBy(record => record.ProjectId!.Value)

                .Select(group => new ProjectFilterOption

                {

                    Id = group.Key,

                    Name = group.First().ProjectName.Trim()

                })

                .OrderBy(option => option.Name, StringComparer.CurrentCulture)

                .ToList();



            ProjectOptions.Clear();

            ProjectOptions.Add(new ProjectFilterOption { Id = null, Name = "全部项目" });

            foreach (ProjectFilterOption project in projects)

            {

                ProjectOptions.Add(project);

            }



            SelectedProjectId = previousSelection.HasValue

                && ProjectOptions.Any(option => option.Id == previousSelection)

                ? previousSelection

                : null;

        }



        private void OnApplicantOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)

        {

            if (e.PropertyName != nameof(ArchiveRegisterFilterOptionViewModel.IsSelected))

            {

                return;

            }



            OnPropertyChanged(nameof(IsAllApplicantsSelected));

            OnPropertyChanged(nameof(SelectedApplicantSummary));



            if (_isInitialized && !_isUpdatingFilters)

            {

                ApplyRecordFilters();

            }

        }



        private void SetAllApplicantSelections(bool isSelected)

        {

            _isUpdatingFilters = true;

            foreach (var option in ApplicantOptions)

            {

                option.IsSelected = isSelected;

            }



            _isUpdatingFilters = false;



            OnPropertyChanged(nameof(IsAllApplicantsSelected));

            OnPropertyChanged(nameof(SelectedApplicantSummary));



            if (_isInitialized)

            {

                ApplyRecordFilters();

            }

        }



        private async Task AddAsync()

        {

            var user = _userContextService.CurrentUser;

            if (user == null)

            {

                _dialogService.ShowError("请先登录。");

                return;

            }



            var draft = await _outboundService.CreateDraftRecordAsync(user);

            await OpenAndReopenDialogAsync(initialDraft: draft);

            await LoadRecordsAsync();

        }



        private async Task OpenAsync()

        {

            if (SelectedRecord == null)

            {

                return;

            }



            if (_workspaceMode == ArchiveOutboundWorkspaceMode.Approval

                || _workspaceMode == ArchiveOutboundWorkspaceMode.Handover)

            {

                await OpenForInlineEditingAsync();

                return;

            }



            await OpenAndReopenDialogAsync(recordId: SelectedRecord.Id);

            await LoadRecordsAsync();

        }



        private void ViewSelectedRecord()
        {
            if (SelectedRecord == null)
            {
                return;
            }

            _dialogService.ShowArchiveOutboundApplicationViewDialog(SelectedRecord);
        }

        private Task OpenAndReopenDialogAsync(int? recordId = null, YearlyArchiveOutboundRecord? initialDraft = null)

        {

            YearlyArchiveOutboundRecord? draft = initialDraft;



            do

            {

                bool committed = _dialogService.ShowArchiveOutboundEditDialog(

                    _workspaceMode,

                    out int? committedRecordId,

                    recordId,

                    draft);



                if (!committed || committedRecordId is not int nextId || nextId <= 0)

                {

                    break;

                }



                recordId = nextId;

                draft = null;

            }

            while (true);

            return Task.CompletedTask;

        }



        private async Task ExecuteDestructiveAsync()

        {

            if (SelectedRecord == null)

            {

                return;

            }



            var user = _userContextService.CurrentUser;

            if (user == null)

            {

                return;

            }



            if (_workspaceMode == ArchiveOutboundWorkspaceMode.Approval)

            {

                if (!_dialogService.ShowConfirm("确定要强制作废该申请单吗？仅未审批的申请可强制作废。", "强制作废确认"))

                {

                    return;

                }



                var result = await _outboundService.ForceVoidByAdminFlowAsync(SelectedRecord.Id, "资料室管理员强制作废", user);

                if (result.Success)

                {

                    _dialogService.ShowMessage(result.Message, "操作成功");

                }

                else

                {

                    _dialogService.ShowError(result.Message);

                }

            }

            else

            {

                if (!_dialogService.ShowConfirm("确定要撤回该申请单吗？撤回后将注销预订记录。", "撤回确认"))

                {

                    return;

                }



                var result = await _outboundService.WithdrawApplicationFlowAsync(SelectedRecord.Id, "申请人撤回", user);

                if (result.Success)

                {

                    _dialogService.ShowMessage(result.Message, "操作成功");

                }

                else

                {

                    _dialogService.ShowError(result.Message);

                }

            }



            await LoadRecordsAsync();

        }

    }



    /// <summary>

    /// 出库申请列表状态筛选项。

    /// </summary>

    public sealed class OutboundStatusFilterOption

    {

        public OutboundStatusFilterOption(string label, int? value)

        {

            Label = label;

            Value = value;

        }



        public string Label { get; }



        public int? Value { get; }

    }

    /// <summary>
    /// 出库列表介质类别筛选项。
    /// </summary>
    public sealed class OutboundMediaKindFilterOption

    {

        public OutboundMediaKindFilterOption(string label, string value)

        {

            Label = label;

            Value = value;

        }



        public string Label { get; }



        public string Value { get; }

    }

}

