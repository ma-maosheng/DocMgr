using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 出网申请/审批列表工作台。
    /// </summary>
    public sealed class NetworkOutboundWorkbenchPageViewModel : ViewModelBase
    {
        public const string PendingInProgressStatus = "进行中（待办结前）";

        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly NetworkTransferWorkspaceMode _mode;
        private readonly List<NetworkOutboundRecord> _allRecords = new();
        private readonly int _initialRecordId;
        private bool _isInitialized;
        private int _applyYear = DateTime.Today.Year;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private NetworkOutboundRecord? _selectedRecord;

        public NetworkOutboundWorkbenchPageViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService,
            NetworkTransferWorkspaceMode mode,
            int initialRecordId = 0)
        {
            _service = service;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _mode = mode;
            _initialRecordId = initialRecordId;

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            SearchCommand = new RelayCommand(async _ => await RefreshAsync());
            AddCommand = new RelayCommand(async _ => await AddAsync(), _ => IsApplicationMode && CanApplicantOperate);
            OpenCommand = new RelayCommand(async _ => await OpenAsync(), _ => SelectedRecord != null);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdrawSelected);
        }

        public bool IsApplicationMode => _mode == NetworkTransferWorkspaceMode.Application;

        public string PageTitle => IsApplicationMode ? "出网申请" : "出网审批";

        public ObservableCollection<NetworkOutboundRecord> Records { get; } = new();
        public ObservableCollection<int> ApplyYears { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new();

        public string SearchKeyword { get => _searchKeyword; set => SetProperty(ref _searchKeyword, value); }

        public int ApplyYear
        {
            get => _applyYear;
            set { if (SetProperty(ref _applyYear, value) && _isInitialized) ApplyFilters(); }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set { if (SetProperty(ref _selectedStatus, value) && _isInitialized) ApplyFilters(); }
        }

        public NetworkOutboundRecord? SelectedRecord
        {
            get => _selectedRecord;
            set { if (SetProperty(ref _selectedRecord, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        private bool CanApplicantOperate =>
            ArchiveRegisterBusinessRules.CanSubmitApplication(_userContextService.CurrentUser)
            || ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        private bool CanWithdrawSelected =>
            IsApplicationMode
            && SelectedRecord != null
            && SelectedRecord.Status is NetworkOutboundRecord.StatusDraft or NetworkOutboundRecord.StatusSubmitted;

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand WithdrawCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized) { ApplyFilters(); return; }

            StatusOptions.Clear();
            StatusOptions.Add("全部");
            StatusOptions.Add(PendingInProgressStatus);
            foreach (var option in ApplicationWorkflowStatus.AllOptions)
                StatusOptions.Add(option.Label);

            ApplyYears.Clear();
            int currentYear = DateTime.Today.Year;
            for (int year = currentYear; year >= currentYear - 5; year--)
                ApplyYears.Add(year);

            if (_mode == NetworkTransferWorkspaceMode.Approval)
            {
                _selectedStatus = PendingInProgressStatus;
                OnPropertyChanged(nameof(SelectedStatus));
            }

            await RefreshAsync();
            _isInitialized = true;

            if (_initialRecordId > 0)
            {
                SelectedRecord = Records.FirstOrDefault(item => item.Id == _initialRecordId)
                    ?? _allRecords.FirstOrDefault(item => item.Id == _initialRecordId);
                if (SelectedRecord != null) await OpenAsync();
            }
        }

        private async Task RefreshAsync()
        {
            try
            {
                int? selectedId = SelectedRecord?.Id;
                string? keyword = string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword.Trim();
                var list = await _service.SearchOutboundRecordsAsync(keyword, null, null);
                _allRecords.Clear();
                _allRecords.AddRange(list);
                ApplyFilters();
                if (selectedId.HasValue)
                    SelectedRecord = Records.FirstOrDefault(item => item.Id == selectedId.Value);
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private void ApplyFilters()
        {
            IEnumerable<NetworkOutboundRecord> query = _allRecords.Where(item => item.ApplyTime.Year == ApplyYear);
            if (IsApplicationMode
                && !ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
                && _userContextService.CurrentUser != null)
            {
                int userId = _userContextService.CurrentUser.Id;
                query = query.Where(item => item.ApplicantUserId == userId);
            }

            if (string.Equals(SelectedStatus, PendingInProgressStatus, StringComparison.Ordinal))
            {
                query = query.Where(item =>
                    item.Status is NetworkOutboundRecord.StatusSubmitted
                        or NetworkOutboundRecord.StatusApproved
                        or NetworkOutboundRecord.StatusSignedUploaded);
            }
            else if (!string.Equals(SelectedStatus, "全部", StringComparison.Ordinal)
                     && !string.IsNullOrWhiteSpace(SelectedStatus))
            {
                var matched = ApplicationWorkflowStatus.AllOptions
                    .FirstOrDefault(item => string.Equals(item.Label, SelectedStatus, StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(matched.Label))
                    query = query.Where(item => item.Status == matched.Value);
            }

            Records.Clear();
            foreach (var item in query.OrderByDescending(r => r.ApplyTime).ThenByDescending(r => r.Id))
                Records.Add(item);
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task AddAsync()
        {
            var draft = new NetworkOutboundRecord
            {
                ApplyTime = DateTime.Now,
                ApplicantName = _userContextService.CurrentUser?.RealName?.Trim() ?? string.Empty,
                ApplicantDept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty,
                DestinationKind = NetworkTransferDomainValues.DestinationKindExternalOffline,
                Status = NetworkOutboundRecord.StatusDraft
            };
            if (_dialogService.ShowNetworkOutboundEditDialog(draft, _mode))
                await RefreshAsync();
        }

        private async Task OpenAsync()
        {
            if (SelectedRecord == null) return;
            var latest = await _service.GetOutboundByIdAsync(SelectedRecord.Id);
            if (latest == null)
            {
                _dialogService.ShowError("未找到出网申请单。");
                await RefreshAsync();
                return;
            }

            if (_dialogService.ShowNetworkOutboundEditDialog(latest, _mode))
                await RefreshAsync();
        }

        private async Task WithdrawAsync()
        {
            if (SelectedRecord == null) return;
            try
            {
                if (!_dialogService.ShowConfirm($"确认撤回作废出网单【{SelectedRecord.OutboundNo}】？"))
                    return;
                await _service.WithdrawOutboundAsync(SelectedRecord.Id, null,
                    _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。"));
                await RefreshAsync();
                _dialogService.ShowMessage("已撤回作废。");
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }
    }
}
