using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 介质出库申请列表 ViewModel。
    /// </summary>
    public class HardDiskMediaOutboundApplicationPageViewModel : ViewModelBase
    {
        private const string AllApplicantsText = "全部申请人";

        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly List<HardDiskMediaApplication> _allApplications = new();

        private bool _isInitialized;
        private bool _isUpdatingFilters;
        private bool _isApplicantPopupOpen;
        private int _applicationYear = DateTime.Today.Year;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private string _selectedApplicationType = "全部";
        private HardDiskMediaApplication? _selectedApplication;

        public HardDiskMediaOutboundApplicationPageViewModel(
            IHardDiskMediaService hardDiskMediaService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            AddCommand = new RelayCommand(
                async _ => await AddApplicationAsync(),
                _ => HardDiskMediaApplicationViewModelHelper.CanSubmitApplication(_userContextService.CurrentUser));
            EditCommand = new RelayCommand(async _ => await OpenApplicationAsync(), _ => CanOpenSelectedApplication());
            SubmitCommand = new RelayCommand(async _ => await SubmitApplicationAsync(), _ => CanSubmitSelectedApplication());
            WithdrawCommand = new RelayCommand(async _ => await WithdrawApplicationAsync(), _ => CanWithdrawSelectedApplication());
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => SelectedApplication != null);
        }

        public ObservableCollection<HardDiskMediaApplication> Applications { get; } = new();

        public ObservableCollection<HardDiskMediaStatusOptionViewModel> ApplicantOptions { get; } = new();

        public ObservableCollection<int> ApplicationYears { get; } = new();

        public ObservableCollection<string> StatusOptions { get; } = new();

        public ObservableCollection<string> ApplicationTypeOptions { get; } = new();

        public string PageTitle => "介质出库申请";

        public string AddActionText => "新增申请";

        public string EditActionText => "打开申请";

        public string SubmitActionText => "提交申请";

        public string PrintActionText => "打印申请审批单";

        public int ApplicationYear
        {
            get => _applicationYear;
            set
            {
                if (SetProperty(ref _applicationYear, value))
                {
                    if (_isInitialized && !_isUpdatingFilters)
                    {
                        ApplyApplicationFilters();
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

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set => SetProperty(ref _selectedStatus, value);
        }

        public string SelectedApplicationType
        {
            get => _selectedApplicationType;
            set => SetProperty(ref _selectedApplicationType, value);
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

        public RelayCommand SearchCommand { get; }

        public RelayCommand RefreshCommand { get; }

        public RelayCommand AddCommand { get; }

        public RelayCommand EditCommand { get; }

        public RelayCommand SubmitCommand { get; }

        public RelayCommand WithdrawCommand { get; }

        public RelayCommand PrintCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadOptionsAsync();
            await LoadApplicationsAsync();
            _isInitialized = true;
        }

        private async Task LoadOptionsAsync()
        {
            var applicationTypes = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMediaApplication), nameof(HardDiskMediaApplication.ApplicationType));

            var statuses = ApplicationWorkflowStatus.AllOptions.Select(item => item.Label).ToList();

            HardDiskMediaApplicationViewModelHelper.ResetOptions(StatusOptions, statuses);
            HardDiskMediaApplicationViewModelHelper.ResetOptions(
                ApplicationTypeOptions,
                applicationTypes.Where(HardDiskMediaApplicationViewModelHelper.IsSelectableOutboundApplicationType).ToList());
            SelectedApplicationType = ApplicationTypeOptions.FirstOrDefault() ?? "全部";
        }

        private async Task SearchAsync()
        {
            await LoadApplicationsAsync();
        }

        private async Task LoadApplicationsAsync()
        {
            try
            {
                int? selectedId = SelectedApplication?.Id;
                string? keyword = string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword;
                var items = await _hardDiskMediaService.SearchApplicationsAsync(keyword, null, null);

                _allApplications.Clear();
                _allApplications.AddRange(items.Where(item => HardDiskMediaApplicationViewModelHelper.IsOutboundApplicationType(item.ApplicationType)));

                UpdateYearOptions();
                UpdateApplicantOptions();
                ApplyApplicationFilters(selectedId);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task SubmitApplicationAsync()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定要提交申请单据 [{SelectedApplication.ApplicationNo}] 吗？", "提示"))
            {
                return;
            }

            try
            {
                await _hardDiskMediaService.SubmitApplicationAsync(SelectedApplication.Id, _userContextService.CurrentUser);
                await SearchAsync();
                _dialogService.ShowMessage("提交申请成功。");
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PrintAsync()
        {
            try
            {
                var data = await _hardDiskMediaService.BuildPrintDataAsync(SelectedApplication);
                var document = HardDiskMediaPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };

                await _hardDiskMediaService.MarkApplicationPrintedAsync(SelectedApplication);
                await SearchAsync();
                previewWindow.ShowDialog();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task RefreshAsync()
        {
            await LoadApplicationsAsync();
        }

        private async Task AddApplicationAsync()
        {
            var draft = new HardDiskMediaApplication
            {
                ApplicationType = HardDiskMediaApplication.TypeOutboundTemporary
            };

            await OpenAndReopenApplicationDialogAsync(draft);
        }

        private async Task OpenApplicationAsync()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            var application = HardDiskMediaApplicationViewModelHelper.CloneApplication(SelectedApplication);
            if (IsEditableDraftApplication(SelectedApplication))
            {
                await OpenAndReopenApplicationDialogAsync(application);
                return;
            }

            _dialogService.ShowHardDiskMediaApplicationViewDialog(application);
        }

        private async Task OpenAndReopenApplicationDialogAsync(HardDiskMediaApplication application)
        {
            while (_dialogService.ShowHardDiskMediaOutboundApplicationEditDialog(application))
            {
                await LoadApplicationsAsync();

                if (application.Id <= 0)
                {
                    continue;
                }

                var latest = Applications.FirstOrDefault(item => item.Id == application.Id);
                if (latest == null)
                {
                    continue;
                }

                SelectedApplication = latest;
                application = HardDiskMediaApplicationViewModelHelper.CloneApplication(latest);
            }
        }

        private async Task WithdrawApplicationAsync()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定要撤回作废申请单 [{SelectedApplication.ApplicationNo}] 吗？", "提示"))
            {
                return;
            }

            try
            {
                var result = await _hardDiskMediaService.WithdrawApplicationAsync(SelectedApplication, _userContextService.CurrentUser, null);
                _dialogService.ShowMessage(result.Message);
                if (!result.Success)
                {
                    return;
                }

                await LoadApplicationsAsync();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void OnApplicantOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HardDiskMediaStatusOptionViewModel.IsSelected))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAllApplicantsSelected));
            OnPropertyChanged(nameof(SelectedApplicantSummary));

            if (_isInitialized && !_isUpdatingFilters)
            {
                ApplyApplicationFilters();
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
                ApplyApplicationFilters();
            }
        }

        private void UpdateApplicantOptions()
        {
            var selectedApplicants = ApplicantOptions
                .Where(item => item.IsSelected)
                .Select(item => item.Label)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var option in ApplicantOptions)
            {
                option.PropertyChanged -= OnApplicantOptionPropertyChanged;
            }

            var applicants = _allApplications
                .Where(item => !string.IsNullOrWhiteSpace(item.ApplicantName))
                .Select(item => item.ApplicantName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.CurrentCulture)
                .ToList();

            bool selectAll = selectedApplicants.Count == 0 || selectedApplicants.Count == ApplicantOptions.Count;

            ApplicantOptions.Clear();
            foreach (var applicant in applicants)
            {
                var option = new HardDiskMediaStatusOptionViewModel(applicant, selectAll || selectedApplicants.Contains(applicant));
                option.PropertyChanged += OnApplicantOptionPropertyChanged;
                ApplicantOptions.Add(option);
            }

            OnPropertyChanged(nameof(IsAllApplicantsSelected));
            OnPropertyChanged(nameof(SelectedApplicantSummary));
        }

        private void UpdateYearOptions()
        {
            var years = _allApplications
                .Where(item => item.ApplyTime.Year > 2000)
                .Select(item => item.ApplyTime.Year)
                .Distinct()
                .OrderBy(item => item)
                .ToList();

            int currentYear = DateTime.Today.Year;
            if (years.Count == 0)
            {
                years.Add(currentYear);
            }
            else if (!years.Contains(currentYear))
            {
                years.Add(currentYear);
                years.Sort();
            }

            ApplicationYears.Clear();
            foreach (int year in years)
            {
                ApplicationYears.Add(year);
            }

            if (_applicationYear < 2000 || !ApplicationYears.Contains(_applicationYear))
            {
                _applicationYear = ApplicationYears[^1];
                OnPropertyChanged(nameof(ApplicationYear));
            }
        }

        private void ApplyApplicationFilters(int? selectedId = null)
        {
            selectedId ??= SelectedApplication?.Id;

            var selectedApplicants = ApplicantOptions
                .Where(item => item.IsSelected)
                .Select(item => item.Label)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var filteredItems = _allApplications
                .Where(item => (selectedApplicants.Count == 0 || selectedApplicants.Contains(item.ApplicantName?.Trim() ?? string.Empty)) &&
                               item.ApplyTime.Year == _applicationYear)
                .ToList();

            Applications.Clear();
            foreach (var item in filteredItems)
            {
                Applications.Add(item);
            }

            SelectedApplication = selectedId.HasValue
                ? Applications.FirstOrDefault(item => item.Id == selectedId.Value)
                : Applications.FirstOrDefault();
        }

        private bool CanOpenSelectedApplication()
        {
            if (SelectedApplication == null)
            {
                return false;
            }

            if (IsEditableDraftApplication(SelectedApplication))
            {
                return true;
            }

            return IsCurrentUserApplicant(SelectedApplication);
        }

        private static bool IsEditableDraftApplication(HardDiskMediaApplication application)
        {
            return application.ApplicationStatus == HardDiskMediaApplication.StatusDraft;
        }

        private bool IsCurrentUserApplicant(HardDiskMediaApplication application)
        {
            return string.Equals(
                application.ApplicantName?.Trim(),
                _userContextService.CurrentUser?.RealName?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool CanWithdrawSelectedApplication()
        {
            if (SelectedApplication == null)
            {
                return false;
            }

            if (!IsCurrentUserApplicant(SelectedApplication))
            {
                return false;
            }

            return SelectedApplication.ApplicationStatus == HardDiskMediaApplication.StatusDraft ||
                   SelectedApplication.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted;
        }

        private bool CanSubmitSelectedApplication()
        {
            return SelectedApplication?.ApplicationStatus == HardDiskMediaApplication.StatusDraft
                   && HardDiskMediaApplicationViewModelHelper.CanSubmitApplication(_userContextService.CurrentUser)
                   && IsCurrentUserApplicant(SelectedApplication);
        }

    }
}
