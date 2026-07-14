using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质审批办理页 ViewModel。
    /// </summary>
    public class HardDiskMediaApprovalPageViewModel : ViewModelBase
    {
        private const string AllStatusesText = "全部";
        private const string AllApplicantsText = "全部申请人";

        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;
        private readonly List<HardDiskMediaApplication> _allApplications = new();

        private bool _isInitialized;
        private bool _isUpdatingFilters;
        private bool _isApplicantPopupOpen;
        private int _applicationYear = DateTime.Today.Year;
        private int? _pendingSelectionApplicationId;
        private HardDiskMediaApplication? _selectedApplication;
        private string _applicationOverdueSettingCode = ApplicationOverdueDomainValues.Default;

        public HardDiskMediaApprovalPageViewModel(
            IHardDiskMediaService hardDiskMediaService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IBusinessLogicSettingsService businessLogicSettingsService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _businessLogicSettingsService = businessLogicSettingsService;

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            ViewApplicationCommand = new RelayCommand(_ => ViewApplication(), _ => CanViewApplication());
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove());
            ForceWithdrawCommand = new RelayCommand(async _ => await ForceWithdrawAsync(), _ => CanForceWithdraw());
        }

        public ObservableCollection<HardDiskMediaApplication> Applications { get; } = new();
        public ObservableCollection<HardDiskMediaStatusOptionViewModel> StatusOptions { get; } = new();
        public ObservableCollection<HardDiskMediaStatusOptionViewModel> ApplicantOptions { get; } = new();
        public ObservableCollection<int> ApplicationYears { get; } = new();

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

        public bool IsAllStatusesSelected
        {
            get => StatusOptions.Count == 0 || StatusOptions.All(item => item.IsSelected);
            set
            {
                if (value)
                {
                    SetAllStatusSelections(true);
                }
            }
        }

        public string SelectedStatusSummary
        {
            get
            {
                var selectedStatuses = StatusOptions
                    .Where(item => item.IsSelected)
                    .Select(item => item.Label)
                    .ToList();

                return selectedStatuses.Count == 0 || selectedStatuses.Count == StatusOptions.Count
                    ? AllStatusesText
                    : string.Join("、", selectedStatuses);
            }
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

        public RelayCommand RefreshCommand { get; }

        public RelayCommand ViewApplicationCommand { get; }

        public RelayCommand ApproveCommand { get; }

        public RelayCommand ForceWithdrawCommand { get; }

        public async Task InitializeAsync(int? initialApplicationId = null)
        {
            if (initialApplicationId.HasValue)
            {
                _pendingSelectionApplicationId = initialApplicationId.Value;
            }

            if (_isInitialized)
            {
                if (initialApplicationId.HasValue)
                {
                    ApplyApplicationFilters(initialApplicationId);
                }

                return;
            }

            await LoadStatusOptionsAsync();
            await LoadApplicationsAsync();
            _isInitialized = true;
        }

        private async Task LoadStatusOptionsAsync()
        {
            var statuses = new List<string>
            {
                HardDiskMediaApplication.StatusDraft,
                HardDiskMediaApplication.StatusSubmitted,
                HardDiskMediaApplication.StatusApproved,
                HardDiskMediaApplication.StatusSignedUploaded,
                HardDiskMediaApplication.StatusCompleted,
                HardDiskMediaApplication.StatusWithdrawn,
                HardDiskMediaApplication.StatusForceWithdrawn
            };

            foreach (var option in StatusOptions)
            {
                option.PropertyChanged -= OnStatusOptionPropertyChanged;
            }

            StatusOptions.Clear();
            foreach (var status in statuses)
            {
                var option = new HardDiskMediaStatusOptionViewModel(status, true);
                option.PropertyChanged += OnStatusOptionPropertyChanged;
                StatusOptions.Add(option);
            }

            OnPropertyChanged(nameof(IsAllStatusesSelected));
            OnPropertyChanged(nameof(SelectedStatusSummary));
        }

        private async Task LoadApplicationsAsync()
        {
            try
            {
                _applicationOverdueSettingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();

                int? selectedId = SelectedApplication?.Id;
                var items = (await _hardDiskMediaService.SearchApplicationsAsync(null, null, null))
                    .Where(item => !IsRegistrationWithoutApprovalType(item.ApplicationType))
                    .ToList();

                _allApplications.Clear();
                _allApplications.AddRange(items);

                UpdateYearOptions();
                UpdateApplicantOptions();
                ApplyApplicationFilters(selectedId);
            }
            catch (System.InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task RefreshAsync()
        {
            await LoadApplicationsAsync();
        }

        private void ViewApplication()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            var viewModel = HardDiskMediaApplicationViewModelHelper.CloneApplication(SelectedApplication);
            _dialogService.ShowHardDiskMediaApplicationViewDialog(viewModel);
        }

        private async Task ApproveAsync()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            var editable = HardDiskMediaApplicationViewModelHelper.CloneApplication(SelectedApplication);
            await OpenAndReopenApprovalDialogAsync(editable);
        }

        private async Task OpenAndReopenApprovalDialogAsync(HardDiskMediaApplication application)
        {
            while (_dialogService.ShowHardDiskMediaApprovalEditDialog(application, _userContextService.CurrentUser, out _))
            {
                await RefreshAsync();

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

        private async Task ForceWithdrawAsync()
        {
            var result = await _hardDiskMediaService.ForceWithdrawApplicationAsync(SelectedApplication, _userContextService.CurrentUser, null);
            await HandleFlowResultAsync(result);
        }

        private async Task UploadSignedAttachmentAsync()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            var filePath = _dialogService.OpenFileDialog("所有文件|*.*", "选择签字件附件");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileContent = await File.ReadAllBytesAsync(filePath);
                var result = await _hardDiskMediaService.UploadSignedAttachmentAsync(SelectedApplication, _userContextService.CurrentUser, fileInfo.Name, fileInfo.Extension, fileInfo.Length, fileContent);
                if (!result.Success)
                {
                    _dialogService.ShowMessage(result.Message);
                    return;
                }

                await RefreshAsync();
                _dialogService.ShowMessage(result.Message);
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"读取附件失败：{ex.Message}");
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
                await RefreshAsync();
                previewWindow.ShowDialog();
            }
            catch (System.InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task HandleFlowResultAsync(HardDiskMediaFlowResult result)
        {
            _dialogService.ShowMessage(result.Message);
            if (!result.Success)
            {
                return;
            }

            await RefreshAsync();
        }

        private bool CanViewApplication()
        {
            return SelectedApplication != null &&
                   HardDiskMediaApplicationViewModelHelper.IsArchiveRoomMediaAdmin(_userContextService.CurrentUser);
        }

        private bool CanApprove()
        {
            return SelectedApplication != null &&
                   !IsRegistrationWithoutApprovalType(SelectedApplication.ApplicationType) &&
                   IsApprovalProcessingStatus(SelectedApplication.ApplicationStatus) &&
                   IsArchiveRoomAdminUser(_userContextService.CurrentUser);
        }

        private static bool IsApprovalProcessingStatus(string? applicationStatus)
        {
            return applicationStatus == HardDiskMediaApplication.StatusSubmitted ||
                   applicationStatus == HardDiskMediaApplication.StatusApproved ||
                   applicationStatus == HardDiskMediaApplication.StatusSignedUploaded;
        }

        private static bool IsRegistrationWithoutApprovalType(string? applicationType)
        {
            return applicationType == HardDiskMediaApplication.TypeReturnBlankRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDataRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                   applicationType == HardDiskMediaApplication.TypeLossRegistration;
        }

        private bool CanForceWithdraw()
        {
            if (SelectedApplication == null || !IsArchiveRoomAdminUser(_userContextService.CurrentUser))
            {
                return false;
            }

            if (SelectedApplication.ApplicationStatus != HardDiskMediaApplication.StatusDraft &&
                SelectedApplication.ApplicationStatus != HardDiskMediaApplication.StatusSubmitted)
            {
                return false;
            }

            return _businessLogicSettingsService.IsEligibleForAdminForceVoid(
                SelectedApplication.ApplyTime,
                _applicationOverdueSettingCode);
        }

        private void OnStatusOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HardDiskMediaStatusOptionViewModel.IsSelected))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAllStatusesSelected));
            OnPropertyChanged(nameof(SelectedStatusSummary));

            if (_isInitialized && !_isUpdatingFilters)
            {
                ApplyApplicationFilters();
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

        private void SetAllStatusSelections(bool isSelected)
        {
            _isUpdatingFilters = true;
            foreach (var option in StatusOptions)
            {
                option.IsSelected = isSelected;
            }

            _isUpdatingFilters = false;

            OnPropertyChanged(nameof(IsAllStatusesSelected));
            OnPropertyChanged(nameof(SelectedStatusSummary));

            if (_isInitialized)
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
            selectedId ??= _pendingSelectionApplicationId ?? SelectedApplication?.Id;

            var selectedStatuses = StatusOptions
                .Where(item => item.IsSelected)
                .Select(item => item.Label)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var selectedApplicants = ApplicantOptions
                .Where(item => item.IsSelected)
                .Select(item => item.Label)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var filteredItems = _allApplications
                .Where(item => (selectedStatuses.Count == 0 || selectedStatuses.Contains(item.ApplicationStatus)) &&
                               (selectedApplicants.Count == 0 || selectedApplicants.Contains(item.ApplicantName?.Trim() ?? string.Empty)) &&
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

            if (SelectedApplication?.Id == _pendingSelectionApplicationId)
            {
                _pendingSelectionApplicationId = null;
            }
        }

        private static bool IsArchiveRoomAdminUser(User? user)
        {
            string dept = user?.Department?.Trim() ?? string.Empty;
            string role = user?.Role?.Trim() ?? string.Empty;

            return (string.Equals(dept, "资料室", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(role, "部门资料管理员", StringComparison.OrdinalIgnoreCase)) ||
                   string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class HardDiskMediaStatusOptionViewModel : ViewModelBase
    {
        private bool _isSelected;

        public HardDiskMediaStatusOptionViewModel(string label, bool isSelected)
        {
            Label = label;
            _isSelected = isSelected;
        }

        public string Label { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
