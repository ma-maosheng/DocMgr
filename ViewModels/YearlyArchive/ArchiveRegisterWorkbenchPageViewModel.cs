using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料登记主页面（申请/审批）列表 VM，交互模式对齐“主页面 + 弹窗表单”。
    /// </summary>
    public class ArchiveRegisterWorkbenchPageViewModel : ViewModelBase
    {
        private const string AllApplicantsText = "全部申请人";

        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;
        private readonly ArchiveRegisterWorkspaceMode _workspaceMode;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly List<YearlyArchiveRegisterRecord> _allRecords = new();

        private int? _pendingSelectionRecordId;
        private bool _isInitialized;
        private bool _isUpdatingFilters;
        private bool _isApplicantPopupOpen;
        private int _selectedYear = DateTime.Today.Year;
        private YearlyArchiveRegisterRecord? _selectedRecord;
        private string _applicationOverdueSettingCode = ApplicationOverdueDomainValues.Default;

        public ArchiveRegisterWorkbenchPageViewModel(
            IArchiveRegisterService archiveRegisterService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IBusinessLogicSettingsService businessLogicSettingsService,
            IServiceScopeFactory scopeFactory,
            ArchiveRegisterWorkspaceMode workspaceMode,
            int initialRecordId = 0)
        {
            _archiveRegisterService = archiveRegisterService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _businessLogicSettingsService = businessLogicSettingsService;
            _scopeFactory = scopeFactory;
            _workspaceMode = workspaceMode;
            _pendingSelectionRecordId = initialRecordId > 0 ? initialRecordId : null;

            AddCommand = new RelayCommand(async _ => await AddAsync(), _ => CanAdd());
            OpenCommand = new RelayCommand(async _ => await OpenAsync(), _ => SelectedRecord != null);
            ViewCommand = new RelayCommand(_ => ViewSelectedRecord(), _ => SelectedRecord != null);
            ApproveCommand = new RelayCommand(async _ => await OpenAsync(), _ => CanApprove());
            DestructiveCommand = new RelayCommand(async _ => await ExecuteDestructiveAsync(), _ => CanExecuteDestructive());
            RefreshCommand = new RelayCommand(async _ => await LoadRecordsAsync());
        }

        public ObservableCollection<int> YearOptions { get; } = new();

        public ObservableCollection<YearlyArchiveRegisterRecord> Records { get; } = new();

        public ObservableCollection<ArchiveRegisterFilterOptionViewModel> ApplicantOptions { get; } = new();

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

        public YearlyArchiveRegisterRecord? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (SetProperty(ref _selectedRecord, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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

        public string PageTitle => _workspaceMode == ArchiveRegisterWorkspaceMode.Approval
            ? "资料登记审批办理表"
            : "资料登记申请办理表";

        public string PageSubtitle => _workspaceMode == ArchiveRegisterWorkspaceMode.Approval
            ? "Archive Register Approval Ledger"
            : "Archive Register Application Ledger";

        public string AddActionText => "新增申请";

        public string OpenActionText => _workspaceMode == ArchiveRegisterWorkspaceMode.Approval ? "打开审批" : "打开申请";

        public string DestructiveActionText => _workspaceMode == ArchiveRegisterWorkspaceMode.Approval ? "强制作废" : "撤回申请";

        public bool ShowAddAction => _workspaceMode != ArchiveRegisterWorkspaceMode.Approval;

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

            await LoadYearOptionsAsync();
            await LoadRecordsAsync();
            _isInitialized = true;
        }

        private async Task LoadYearOptionsAsync()
        {
            List<int> years = await ExecuteWithFreshArchiveServiceAsync(service => service.GetExistingYearsAsync());
            int currentYear = DateTime.Today.Year;
            if (!years.Contains(currentYear))
            {
                years.Add(currentYear);
            }

            years = years
                .Distinct()
                .OrderBy(y => y)
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
                _selectedYear = YearOptions[^1];
                OnPropertyChanged(nameof(SelectedYear));
            }
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

            List<YearlyArchiveRegisterRecord> sourceRecords = _archiveRegisterService.IsArchiveAdminUser(user)
                ? await ExecuteWithFreshArchiveServiceAsync(service => service.GetAllRecordsByYearAsync(SelectedYear))
                : (await ExecuteWithFreshArchiveServiceAsync(service => service.GetMyRecordsAsync(user.RealName)))
                    .Where(r => r.CreatedDate.Year == SelectedYear)
                    .ToList();

            IEnumerable<YearlyArchiveRegisterRecord> filtered = sourceRecords;

            _allRecords.Clear();
            _allRecords.AddRange(filtered.OrderByDescending(r => r.FormNo));

            UpdateApplicantOptions();
            ApplyRecordFilters();
        }

        private void ApplyRecordFilters()
        {
            int? selectedId = _pendingSelectionRecordId ?? SelectedRecord?.Id;

            var selectedApplicants = ApplicantOptions
                .Where(item => item.IsSelected)
                .Select(item => item.Label)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var filteredItems = _allRecords
                .Where(item => selectedApplicants.Count == 0 || selectedApplicants.Contains(item.ApplicantName?.Trim() ?? string.Empty))
                .OrderByDescending(item => item.FormNo)
                .ToList();

            Records.Clear();
            foreach (var record in filteredItems)
            {
                Records.Add(record);
            }

            SelectedRecord = selectedId.HasValue
                ? Records.FirstOrDefault(r => r.Id == selectedId.Value)
                : Records.FirstOrDefault();

            if (_pendingSelectionRecordId.HasValue && SelectedRecord?.Id == _pendingSelectionRecordId.Value)
            {
                _pendingSelectionRecordId = null;
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

        private bool CanAdd()
        {
            if (_workspaceMode == ArchiveRegisterWorkspaceMode.Approval)
            {
                return false;
            }

            return _archiveRegisterService.CanSubmitApplication(_userContextService.CurrentUser);
        }

        private async Task AddAsync()
        {
            await OpenAndReopenDialogAsync(null);
        }

        private async Task OpenAsync()
        {
            if (SelectedRecord == null)
            {
                return;
            }

            await OpenAndReopenDialogAsync(SelectedRecord.Id);
        }

        private void ViewSelectedRecord()
        {
            if (SelectedRecord == null)
            {
                return;
            }

            _dialogService.ShowArchiveRegisterApplicationViewDialog(SelectedRecord);
        }

        private async Task OpenAndReopenDialogAsync(int? initialRecordId)
        {
            int? dialogRecordId = initialRecordId;

            while (true)
            {
                bool changed = _dialogService.ShowArchiveRegisterEditDialog(_workspaceMode, out int? committedRecordId, dialogRecordId);
                if (!changed)
                {
                    return;
                }

                dialogRecordId = committedRecordId ?? dialogRecordId;
                if (dialogRecordId.HasValue)
                {
                    _pendingSelectionRecordId = dialogRecordId.Value;
                }

                await LoadRecordsAsync();

                if (_workspaceMode != ArchiveRegisterWorkspaceMode.Application)
                {
                    return;
                }

                dialogRecordId ??= SelectedRecord?.Id;
                if (!dialogRecordId.HasValue)
                {
                    return;
                }
            }
        }

        private bool CanApprove()
        {
            return SelectedRecord != null
                && _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser)
                && IsApprovalProcessingStatus(SelectedRecord.Status);
        }

        private static bool IsApprovalProcessingStatus(int status)
        {
            return status == YearlyArchiveRegisterRecord.Submitted
                || status == YearlyArchiveRegisterRecord.Approved
                || status == YearlyArchiveRegisterRecord.SignedUploaded;
        }

        private async Task<T> ExecuteWithFreshArchiveServiceAsync<T>(Func<IArchiveRegisterService, Task<T>> action)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IArchiveRegisterService>();
            return await action(service);
        }

        private bool CanExecuteDestructive()
        {
            if (SelectedRecord == null)
            {
                return false;
            }

            return _workspaceMode == ArchiveRegisterWorkspaceMode.Approval
                ? SelectedRecord.CanForceCleanupRegister
                  && _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser)
                  && _businessLogicSettingsService.IsEligibleForAdminForceVoid(
                      ApplicationOverdueSettingSupport.ResolveRegisterApplyDate(SelectedRecord),
                      _applicationOverdueSettingCode)
                : SelectedRecord.CanCancelRegister && _archiveRegisterService.IsApplicantUser(_userContextService.CurrentUser);
        }

        private async Task ExecuteDestructiveAsync()
        {
            if (SelectedRecord == null)
            {
                return;
            }

            if (_workspaceMode == ArchiveRegisterWorkspaceMode.Approval)
            {
                if (!_dialogService.ShowConfirm($"确定要强制作废登记单 [{SelectedRecord.FormNo}] 吗？", "提示"))
                {
                    return;
                }

                var result = await _archiveRegisterService.ForceCleanupRegisterFlowAsync(SelectedRecord, _userContextService.CurrentUser);
                _dialogService.ShowMessage(result.Message);
                if (result.Success)
                {
                    await LoadRecordsAsync();
                }

                return;
            }

            if (!_dialogService.ShowConfirm($"确定要撤回作废登记单 [{SelectedRecord.FormNo}] 吗？", "提示"))
            {
                return;
            }

            var cancelResult = await _archiveRegisterService.CancelRegisterFlowAsync(SelectedRecord, _userContextService.CurrentUser);
            _dialogService.ShowMessage(cancelResult.Message);
            if (cancelResult.Success)
            {
                await LoadRecordsAsync();
            }
        }
    }

    public sealed class ArchiveRegisterFilterOptionViewModel : ViewModelBase
    {
        private bool _isSelected;

        public ArchiveRegisterFilterOptionViewModel(string label, bool isSelected)
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
