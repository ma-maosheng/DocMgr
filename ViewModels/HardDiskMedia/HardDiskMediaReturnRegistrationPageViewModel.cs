using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘归还工作台：待归还介质发起归还 → 申请/审批分流办理。
    /// </summary>
    public partial class HardDiskMediaReturnRegistrationPageViewModel : ViewModelBase
    {
        private const string ReturnStageAll = "全部";
        private const string ReturnStageRegistered = "已登记归还信息";
        private const string ReturnStagePendingComplete = "待办结";
        private const string ReturnStageSignedUploaded = "已上传签字件";
        private const string ReturnStageCompleted = "已办结";

        private readonly HardDiskReturnWorkspaceMode _workspaceMode;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IUserService _userService;
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;
        private readonly List<HardDiskMediaApplication> _allApplications = new();
        private readonly List<HardDiskMediaReturnCandidate> _allReturnCandidates = new();
        private readonly Dictionary<int, string> _sourceBorrowApplicationNoByReturnId = new();
        private bool _isPageInitialized;
        private bool _overdueOnly;
        private bool _matchAllYears;
        private int _applicationYear = DateTime.Today.Year;
        private string _searchKeyword = string.Empty;
        private string _selectedApplicantFilter = ReturnStageAll;
        private string _selectedStatus = ReturnStageAll;
        private string _selectedApplicationType = ReturnStageAll;
        private string _applicationOverdueSettingCode = ApplicationOverdueDomainValues.Default;
        private HardDiskMediaApplication? _selectedApplication;
        private HardDiskMediaReturnCandidate? _selectedCandidate;
        private bool _isLeftPanelExpanded = true;

        public HardDiskMediaReturnRegistrationPageViewModel(
            HardDiskReturnWorkspaceMode workspaceMode,
            IHardDiskMediaService hardDiskMediaService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IUserService userService,
            IBusinessLogicSettingsService businessLogicSettingsService)
        {
            _workspaceMode = workspaceMode;
            _hardDiskMediaService = hardDiskMediaService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _userService = userService;
            _businessLogicSettingsService = businessLogicSettingsService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            StartReturnCommand = new RelayCommand(async _ => await StartReturnAsync(), _ => CanStartReturn());
            OpenReturnCommand = new RelayCommand(async _ => await OpenReturnAsync(), _ => CanOpenReturn());
            WithdrawCommand = new RelayCommand(async _ => await WithdrawApplicationAsync(), _ => CanWithdrawSelectedApplication());
            ForceWithdrawCommand = new RelayCommand(async _ => await ForceWithdrawApplicationAsync(), _ => CanForceWithdrawSelectedApplication());
            ToggleLeftPanelCommand = new RelayCommand(_ => IsLeftPanelExpanded = !IsLeftPanelExpanded);

            RecommendTargetLocationCommand = new RelayCommand(async _ => await RecommendTargetLocationAsync(), _ => CanRecommendTargetLocation);
            ShowTargetLocationSnapshotCommand = new RelayCommand(async _ => await ShowTargetLocationSnapshotAsync(), _ => CanShowTargetLocationSnapshot);
            SaveDraftCommand = new RelayCommand(async _ => await SaveAsync(HardDiskMediaApplication.StatusDraft), _ => IsRegistrationEditable);
            SubmitCommand = new RelayCommand(async _ => await SaveAsync(HardDiskMediaApplication.StatusSubmitted), _ => IsRegistrationEditable);
            PrintSignedHandoverCommand = new RelayCommand(async _ => await PrintHandoverSheetAsync(), _ => CanPrintSignedHandoverOnApplication);
            PrintHandoverSheetCommand = new RelayCommand(async _ => await PrintHandoverSheetAsync(), _ => CanPrintHandoverSheet);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            ViewAttachmentCommand = new RelayCommand(async attachment => await ViewAttachmentAsync(attachment as SystemAttachment), attachment => attachment is SystemAttachment);
            DeleteAttachmentCommand = new RelayCommand(async attachment => await DeleteAttachmentAsync(attachment as SystemAttachment), attachment => attachment is SystemAttachment && CanDeleteAttachment);
            CancelEditCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmHandoverCommand = new RelayCommand(async _ => await ConfirmHandoverAsync(), _ => CanConfirmHandover);
            UploadSignedAttachmentCommand = new RelayCommand(async _ => await UploadSignedAttachmentAsync(), _ => CanUploadSignedAttachment);
        }

        public HardDiskReturnWorkspaceMode WorkspaceMode => _workspaceMode;

        public string PageTitle => _workspaceMode == HardDiskReturnWorkspaceMode.Approval
            ? "硬盘审批入库"
            : "硬盘归还申请";

        public string PageSubtitle => _workspaceMode == HardDiskReturnWorkspaceMode.Approval
            ? "对已提交的归还申请进行审批、实物交接、上传签批交接单并办结。"
            : "选择待归还介质，填写归还申请、打印签批交接单并提交审批。";

        public ObservableCollection<HardDiskMediaApplication> Applications { get; } = new();

        public ObservableCollection<HardDiskMediaReturnCandidate> ReturnCandidates { get; } = new();

        public ObservableCollection<string> ApplicantFilterOptions { get; } = new();

        public ObservableCollection<string> StatusOptions { get; } = new();

        public ObservableCollection<string> ApplicationTypeOptions { get; } = new();

        public ObservableCollection<int> ApplicationYears { get; } = new();

        public int ApplicationYear
        {
            get => _applicationYear;
            set
            {
                if (SetProperty(ref _applicationYear, value))
                {
                    if (_isPageInitialized)
                    {
                        _matchAllYears = false;
                        ApplyListFilters();
                    }
                }
            }
        }

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        /// <summary>工具栏「借出/归还人」筛选；「全部」不过滤。</summary>
        public string SelectedApplicantFilter
        {
            get => _selectedApplicantFilter;
            set
            {
                if (SetProperty(ref _selectedApplicantFilter, value))
                {
                    if (_isPageInitialized)
                    {
                        ApplyListFilters();
                    }
                }
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                {
                    if (_isPageInitialized)
                    {
                        ApplyListFilters();
                    }
                }
            }
        }

        public string SelectedApplicationType
        {
            get => _selectedApplicationType;
            set
            {
                if (SetProperty(ref _selectedApplicationType, value))
                {
                    if (_isPageInitialized)
                    {
                        ApplyListFilters();
                    }
                }
            }
        }

        public HardDiskMediaReturnCandidate? SelectedCandidate
        {
            get => _selectedCandidate;
            set
            {
                if (SetProperty(ref _selectedCandidate, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public HardDiskMediaApplication? SelectedApplication
        {
            get => _selectedApplication;
            set
            {
                if (SetProperty(ref _selectedApplication, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsLeftPanelExpanded
        {
            get => _isLeftPanelExpanded;
            set
            {
                if (SetProperty(ref _isLeftPanelExpanded, value))
                {
                    OnPropertyChanged(nameof(LeftPanelToggleToolTip));
                    OnPropertyChanged(nameof(LeftPanelToggleGlyph));
                }
            }
        }

        public string LeftPanelToggleToolTip => IsLeftPanelExpanded ? "收起左侧列表" : "展开左侧列表";

        public string LeftPanelToggleGlyph => IsLeftPanelExpanded ? "◀" : "▶";

        public RelayCommand SearchCommand { get; }

        public RelayCommand RefreshCommand { get; }

        public RelayCommand StartReturnCommand { get; }

        public RelayCommand OpenReturnCommand { get; }

        public RelayCommand WithdrawCommand { get; }

        public RelayCommand ForceWithdrawCommand { get; }

        public RelayCommand ToggleLeftPanelCommand { get; }

        public RelayCommand ApproveCommand { get; }

        public RelayCommand ConfirmHandoverCommand { get; }

        public RelayCommand UploadSignedAttachmentCommand { get; }

        public async Task InitializeAsync(bool overdueOnly = false, bool matchAllYears = false)
        {
            _overdueOnly = overdueOnly;
            _matchAllYears = matchAllYears;

            if (_isPageInitialized)
            {
                await SearchAsync();
                return;
            }

            await LoadFilterOptionsAsync();
            await SearchAsync();
            _isPageInitialized = true;
        }

        private async Task LoadFilterOptionsAsync()
        {
            ApplicantFilterOptions.Clear();
            ApplicantFilterOptions.Add(ReturnStageAll);
            SelectedApplicantFilter = ReturnStageAll;

            StatusOptions.Clear();
            StatusOptions.Add(ReturnStageAll);
            foreach (var (_, label) in ApplicationWorkflowStatus.AllOptions)
            {
                StatusOptions.Add(label);
            }

            SelectedStatus = ReturnStageAll;

            HardDiskMediaApplicationViewModelHelper.ResetReturnRegistrationKindOptions(ApplicationTypeOptions);
            SelectedApplicationType = ApplicationTypeOptions.FirstOrDefault() ?? ReturnStageAll;

            await Task.CompletedTask;
        }

        private async Task SearchAsync()
        {
            try
            {
                _applicationOverdueSettingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();

                int? selectedApplicationId = SelectedApplication?.Id ?? _editingApplication?.Id;
                string? selectedCandidateKey = SelectedCandidate == null ? null : BuildCandidateKey(SelectedCandidate);
                string? keyword = string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword;
                var items = await _hardDiskMediaService.SearchApplicationsAsync(keyword, null, null);

                _allApplications.Clear();
                _allApplications.AddRange(items.Where(item => HardDiskMediaApplicationViewModelHelper.IsReturnRegistrationType(item.ApplicationType)));

                var candidates = await _hardDiskMediaService.GetReturnRegistrationCandidatesAsync();
                _allReturnCandidates.Clear();
                _allReturnCandidates.AddRange(candidates);

                await LoadSourceBorrowApplicationNosAsync();
                UpdateApplicantFilterOptions();
                UpdateYearOptions();
                ApplyListFilters(selectedApplicationId, selectedCandidateKey);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void ApplyListFilters(int? selectedApplicationId = null, string? selectedCandidateKey = null)
        {
            selectedApplicationId ??= SelectedApplication?.Id ?? _editingApplication?.Id;
            selectedCandidateKey ??= SelectedCandidate == null ? null : BuildCandidateKey(SelectedCandidate);

            var filteredApplications = _allApplications
                .Where(MatchesSelectedBorrowApplicationYear)
                .Where(MatchesSelectedApplicantFilter)
                .Where(MatchesSelectedRegistrationKind)
                .Where(MatchesSelectedReturnStage)
                .ToList();

            Applications.Clear();
            foreach (var item in filteredApplications)
            {
                Applications.Add(item);
            }

            var filteredCandidates = _allReturnCandidates
                .Where(MatchesSelectedBorrowApplicationYear)
                .Where(MatchesSelectedApplicantFilter)
                .Where(MatchesOverdueOnlyFilter)
                .OrderBy(item => item.ExpectedReturnDate ?? DateTime.MaxValue)
                .ThenBy(item => item.DiskCode, StringComparer.Ordinal)
                .ToList();

            ReturnCandidates.Clear();
            foreach (var candidate in filteredCandidates)
            {
                ReturnCandidates.Add(candidate);
            }

            SelectedApplication = selectedApplicationId.HasValue
                ? Applications.FirstOrDefault(item => item.Id == selectedApplicationId.Value)
                : Applications.FirstOrDefault();

            SelectedCandidate = selectedCandidateKey == null
                ? ReturnCandidates.FirstOrDefault()
                : ReturnCandidates.FirstOrDefault(item => string.Equals(BuildCandidateKey(item), selectedCandidateKey, StringComparison.Ordinal))
                  ?? ReturnCandidates.FirstOrDefault();
        }

        private async Task LoadSourceBorrowApplicationNosAsync()
        {
            _sourceBorrowApplicationNoByReturnId.Clear();

            foreach (var application in _allApplications)
            {
                string sourceBorrowApplicationNo = await _hardDiskMediaService.ResolveReturnSourceApplicationNoAsync(
                    application.SourceApplicationId,
                    application.SourceOutboundRecordId);

                if (!string.IsNullOrWhiteSpace(sourceBorrowApplicationNo))
                {
                    _sourceBorrowApplicationNoByReturnId[application.Id] = sourceBorrowApplicationNo.Trim();
                }
            }
        }

        private void UpdateApplicantFilterOptions()
        {
            string previous = SelectedApplicantFilter;
            var names = _allReturnCandidates
                .Select(item => item.ApplicantName?.Trim())
                .Concat(_allApplications.Select(item => item.ApplicantName?.Trim()))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ApplicantFilterOptions.Clear();
            ApplicantFilterOptions.Add(ReturnStageAll);
            foreach (string name in names)
            {
                ApplicantFilterOptions.Add(name);
            }

            _selectedApplicantFilter = ApplicantFilterOptions.Any(item =>
                    string.Equals(item, previous, StringComparison.OrdinalIgnoreCase))
                ? previous
                : ReturnStageAll;

            // ItemsSource 重建后须强制刷新 SelectedItem 绑定，否则 ComboBox 会短暂处于校验失败（红边、文本空白）。
            OnPropertyChanged(nameof(SelectedApplicantFilter));
        }

        private void UpdateYearOptions()
        {
            int currentYear = DateTime.Today.Year;
            var years = _allReturnCandidates
                .Select(item => HardDiskMediaApplicationNoSupport.TryParseBusinessNoYear(item.SourceApplicationNo, out int year)
                    ? year
                    : (int?)null)
                .Concat(_sourceBorrowApplicationNoByReturnId.Values.Select(sourceNo =>
                    HardDiskMediaApplicationNoSupport.TryParseBusinessNoYear(sourceNo, out int year)
                        ? year
                        : (int?)null))
                .Where(year => year.HasValue)
                .Select(year => year!.Value)
                .Distinct()
                .OrderBy(year => year)
                .ToList();

            if (!years.Contains(currentYear))
            {
                years.Add(currentYear);
                years.Sort();
            }

            ApplicationYears.Clear();
            foreach (int year in years)
            {
                ApplicationYears.Add(year);
            }

            if (!ApplicationYears.Contains(_applicationYear))
            {
                _applicationYear = ApplicationYears[^1];
            }

            // ItemsSource 重建后须强制刷新 SelectedItem 绑定，否则 ComboBox 会短暂处于校验失败（红边、文本空白）。
            OnPropertyChanged(nameof(ApplicationYear));
        }

        private bool MatchesSelectedApplicantFilter(HardDiskMediaApplication application)
        {
            if (string.Equals(SelectedApplicantFilter, ReturnStageAll, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(
                application.ApplicantName?.Trim(),
                SelectedApplicantFilter?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesSelectedApplicantFilter(HardDiskMediaReturnCandidate candidate)
        {
            if (string.Equals(SelectedApplicantFilter, ReturnStageAll, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(
                candidate.ApplicantName?.Trim(),
                SelectedApplicantFilter?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesSelectedBorrowApplicationYear(HardDiskMediaApplication application)
        {
            if (_matchAllYears)
            {
                return true;
            }

            return _sourceBorrowApplicationNoByReturnId.TryGetValue(application.Id, out string? sourceBorrowApplicationNo)
                   && HardDiskMediaApplicationNoSupport.TryParseBusinessNoYear(sourceBorrowApplicationNo, out int year)
                   && year == _applicationYear;
        }

        private bool MatchesSelectedBorrowApplicationYear(HardDiskMediaReturnCandidate candidate)
        {
            if (_matchAllYears)
            {
                return true;
            }

            return HardDiskMediaApplicationNoSupport.TryParseBusinessNoYear(candidate.SourceApplicationNo, out int year)
                   && year == _applicationYear;
        }

        private bool MatchesOverdueOnlyFilter(HardDiskMediaReturnCandidate candidate)
        {
            if (!_overdueOnly)
            {
                return true;
            }

            return candidate.ExpectedReturnDate.HasValue
                   && candidate.ExpectedReturnDate.Value.Date < DateTime.Today;
        }

        private async Task RefreshListsKeepingEditorAsync()
        {
            int? editingId = _editingApplication?.Id;
            await SearchAsync();
            if (editingId is > 0)
            {
                SelectedApplication = Applications.FirstOrDefault(item => item.Id == editingId.Value) ?? SelectedApplication;
                if (SelectedApplication != null)
                {
                    SynchronizeEditingApplication(SelectedApplication);
                }
            }
        }

        private bool MatchesSelectedRegistrationKind(HardDiskMediaApplication application)
        {
            return HardDiskMediaReturnDomainValues.MatchesRegistrationKindFilter(
                SelectedApplicationType,
                application.ApplicationType,
                application.InspectionResult);
        }

        private bool MatchesSelectedReturnStage(HardDiskMediaApplication application)
        {
            if (SelectedStatus == ReturnStageAll)
            {
                return true;
            }

            return string.Equals(
                application.StatusStr,
                SelectedStatus,
                StringComparison.Ordinal);
        }

        private static string ResolveReturnStageText(HardDiskMediaApplication? application)
        {
            if (application == null)
            {
                return "(无)";
            }

            return application.StatusStr;
        }

        private async Task RefreshAsync()
        {
            await SearchAsync();
        }

        private async Task StartReturnAsync()
        {
            if (SelectedCandidate == null)
            {
                return;
            }

            var existingReturn = await _hardDiskMediaService.GetActiveReturnRegistrationByMediumIdAsync(SelectedCandidate.MediumId);
            if (existingReturn != null)
            {
                var editable = HardDiskMediaApplicationViewModelHelper.CloneApplication(existingReturn);
                await LoadEditorSessionAsync(editable);
                SelectedApplication = Applications.FirstOrDefault(item => item.Id == existingReturn.Id) ?? SelectedApplication;
                _dialogService.ShowMessage(
                    $"该硬盘已有未办结归还登记单 [{existingReturn.ApplicationNo}]，已为您打开续办。",
                    "提示");
                return;
            }

            var draft = new HardDiskMediaApplication
            {
                ApplicationType = HardDiskMediaApplication.TypeReturnBlankRegistration,
                MediumId = SelectedCandidate.MediumId,
                SourceApplicationId = SelectedCandidate.SourceApplicationId,
                SourceOutboundRecordId = SelectedCandidate.SourceOutboundRecordId,
                ApplicantName = SelectedCandidate.ApplicantName,
                ApplicantDept = SelectedCandidate.ApplicantDept,
                CurrentLocation = SelectedCandidate.BorrowedLocation,
                ExpectedReturnDate = SelectedCandidate.ExpectedReturnDate,
                ApplyTime = DateTime.Today
            };

            await LoadEditorSessionAsync(draft);
        }

        private async Task OpenReturnAsync()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            var editable = HardDiskMediaApplicationViewModelHelper.CloneApplication(SelectedApplication);
            await LoadEditorSessionAsync(editable);
        }

        private async Task WithdrawApplicationAsync()
        {
            HardDiskMediaApplication? target = ResolveVoidTargetApplication();
            if (target == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定要撤回作废归还单 [{target.ApplicationNo}] 吗？", "提示"))
            {
                return;
            }

            try
            {
                var result = await _hardDiskMediaService.WithdrawApplicationAsync(
                    target,
                    _userContextService.CurrentUser,
                    null);
                _dialogService.ShowMessage(result.Message);
                if (!result.Success)
                {
                    return;
                }

                CancelEdit();
                await SearchAsync();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ForceWithdrawApplicationAsync()
        {
            HardDiskMediaApplication? target = ResolveVoidTargetApplication();
            if (target == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定要强制作废归还单 [{target.ApplicationNo}] 吗？", "提示"))
            {
                return;
            }

            try
            {
                var result = await _hardDiskMediaService.ForceWithdrawApplicationAsync(
                    target,
                    _userContextService.CurrentUser,
                    null);
                _dialogService.ShowMessage(result.Message);
                if (!result.Success)
                {
                    return;
                }

                CancelEdit();
                await SearchAsync();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private bool CanStartReturn()
        {
            return _workspaceMode == HardDiskReturnWorkspaceMode.Application
                   && SelectedCandidate != null
                   && HardDiskMediaApplicationViewModelHelper.CanSubmitApplication(_userContextService.CurrentUser);
        }

        private bool CanOpenReturn()
        {
            return SelectedApplication != null;
        }

        private bool CanWithdrawSelectedApplication()
        {
            if (_workspaceMode != HardDiskReturnWorkspaceMode.Application)
            {
                return false;
            }

            HardDiskMediaApplication? target = ResolveVoidTargetApplication();
            if (target == null || !IsCurrentUserApplicant(target))
            {
                return false;
            }

            return target.ApplicationStatus == HardDiskMediaApplication.StatusDraft
                   || target.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted;
        }

        private bool CanForceWithdrawSelectedApplication()
        {
            if (_workspaceMode != HardDiskReturnWorkspaceMode.Approval || !IsCurrentUserArchiveAdmin())
            {
                return false;
            }

            HardDiskMediaApplication? target = ResolveVoidTargetApplication();
            if (target == null)
            {
                return false;
            }

            if (target.ApplicationStatus != HardDiskMediaApplication.StatusDraft
                && target.ApplicationStatus != HardDiskMediaApplication.StatusSubmitted)
            {
                return false;
            }

            return _businessLogicSettingsService.IsEligibleForAdminForceVoid(
                target.ApplyTime,
                _applicationOverdueSettingCode);
        }

        private HardDiskMediaApplication? ResolveVoidTargetApplication()
        {
            HardDiskMediaApplication? target = _editingApplication?.Id > 0
                ? _editingApplication
                : SelectedApplication;
            return target is { Id: > 0 } ? target : null;
        }

        private bool IsCurrentUserApplicant(HardDiskMediaApplication application)
        {
            return string.Equals(
                application.ApplicantName?.Trim(),
                _userContextService.CurrentUser?.RealName?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsCurrentUserArchiveAdmin()
        {
            return HardDiskMediaApplicationViewModelHelper.IsArchiveRoomMediaAdmin(_userContextService.CurrentUser);
        }

        private static string BuildCandidateKey(HardDiskMediaReturnCandidate candidate)
        {
            return $"{candidate.MediumId}|{candidate.SourceApplicationId}|{candidate.SourceOutboundRecordId}";
        }
    }
}
