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
    /// 在网数据处置页：台账浏览 + 加工产出登记 + 处置单。
    /// </summary>
    public sealed class NetworkOnNetDisposalPageViewModel : ViewModelBase
    {
        public const string PendingInProgressStatus = "进行中（待办结前）";

        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly List<NetworkOnNetDisposalRecord> _allRecords = new();
        private bool _isInitialized;
        private int _applyYear = DateTime.Today.Year;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private string _assetKeyword = string.Empty;
        private string _assetOriginKind = "全部";
        private string _assetLifecycle = "全部";
        private NetworkOnNetDisposalRecord? _selectedRecord;

        public NetworkOnNetDisposalPageViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _service = service;
            _dialogService = dialogService;
            _userContextService = userContextService;

            RefreshCommand = new RelayCommand(async _ => await RefreshAllAsync());
            SearchCommand = new RelayCommand(async _ => await RefreshDisposalsAsync());
            SearchAssetsCommand = new RelayCommand(async _ => await RefreshAssetsAsync());
            RegisterOutputCommand = new RelayCommand(async _ => await RegisterOutputAsync(), _ => CanOperate);
            AddDisposalCommand = new RelayCommand(async _ => await AddDisposalAsync(), _ => CanOperate);
            OpenDisposalCommand = new RelayCommand(async _ => await OpenDisposalAsync(), _ => SelectedRecord != null && CanOperate);
            WithdrawDisposalCommand = new RelayCommand(async _ => await WithdrawDisposalAsync(), _ => CanWithdrawSelected);
        }

        public ObservableCollection<NetworkOnNetAsset> Assets { get; } = new();
        public ObservableCollection<NetworkOnNetDisposalRecord> Records { get; } = new();
        public ObservableCollection<int> ApplyYears { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new();
        public ObservableCollection<string> OriginKindOptions { get; } = new() { "全部", NetworkTransferDomainValues.OriginKindInbound, NetworkTransferDomainValues.OriginKindProcessedOutput };
        public ObservableCollection<string> LifecycleOptions { get; } = new()
        {
            "全部",
            NetworkTransferDomainValues.LifecycleOnNet,
            NetworkTransferDomainValues.LifecycleOutboundLocked,
            NetworkTransferDomainValues.LifecycleOutbounded,
            NetworkTransferDomainValues.LifecycleDisposalLocked,
            NetworkTransferDomainValues.LifecycleDisposed
        };

        public string SearchKeyword { get => _searchKeyword; set => SetProperty(ref _searchKeyword, value); }
        public string AssetKeyword { get => _assetKeyword; set => SetProperty(ref _assetKeyword, value); }
        public string AssetOriginKind
        {
            get => _assetOriginKind;
            set { if (SetProperty(ref _assetOriginKind, value) && _isInitialized) _ = RefreshAssetsAsync(); }
        }
        public string AssetLifecycle
        {
            get => _assetLifecycle;
            set { if (SetProperty(ref _assetLifecycle, value) && _isInitialized) _ = RefreshAssetsAsync(); }
        }
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
        public NetworkOnNetDisposalRecord? SelectedRecord
        {
            get => _selectedRecord;
            set { if (SetProperty(ref _selectedRecord, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        private bool CanOperate => ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        private bool CanWithdrawSelected =>
            CanOperate
            && SelectedRecord != null
            && SelectedRecord.Status is NetworkOnNetDisposalRecord.StatusDraft or NetworkOnNetDisposalRecord.StatusSubmitted;

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand SearchAssetsCommand { get; }
        public RelayCommand RegisterOutputCommand { get; }
        public RelayCommand AddDisposalCommand { get; }
        public RelayCommand OpenDisposalCommand { get; }
        public RelayCommand WithdrawDisposalCommand { get; }

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
            await RefreshAllAsync();
            _isInitialized = true;
        }

        private async Task RefreshAllAsync()
        {
            await RefreshAssetsAsync();
            await RefreshDisposalsAsync();
        }

        private async Task RefreshAssetsAsync()
        {
            try
            {
                string? origin = string.Equals(AssetOriginKind, "全部", StringComparison.Ordinal) ? null : AssetOriginKind;
                string? life = string.Equals(AssetLifecycle, "全部", StringComparison.Ordinal) ? null : AssetLifecycle;
                string? keyword = string.IsNullOrWhiteSpace(AssetKeyword) ? null : AssetKeyword.Trim();
                var list = await _service.SearchOnNetAssetsAsync(keyword, origin, life);
                Assets.Clear();
                foreach (var item in list) Assets.Add(item);
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task RefreshDisposalsAsync()
        {
            try
            {
                int? selectedId = SelectedRecord?.Id;
                string? keyword = string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword.Trim();
                var list = await _service.SearchDisposalRecordsAsync(keyword, null, null);
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
            IEnumerable<NetworkOnNetDisposalRecord> query = _allRecords.Where(item => item.ApplyTime.Year == ApplyYear);
            if (string.Equals(SelectedStatus, PendingInProgressStatus, StringComparison.Ordinal))
            {
                query = query.Where(item =>
                    item.Status is NetworkOnNetDisposalRecord.StatusSubmitted
                        or NetworkOnNetDisposalRecord.StatusApproved
                        or NetworkOnNetDisposalRecord.StatusSignedUploaded);
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

        private async Task RegisterOutputAsync()
        {
            if (_dialogService.ShowNetworkProcessedOutputEditDialog())
                await RefreshAssetsAsync();
        }

        private async Task AddDisposalAsync()
        {
            var draft = new NetworkOnNetDisposalRecord
            {
                ApplyTime = DateTime.Now,
                ApplicantName = _userContextService.CurrentUser?.RealName?.Trim() ?? string.Empty,
                ApplicantDept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty,
                Status = NetworkOnNetDisposalRecord.StatusDraft
            };
            if (_dialogService.ShowNetworkOnNetDisposalEditDialog(draft))
            {
                await RefreshDisposalsAsync();
                await RefreshAssetsAsync();
            }
        }

        private async Task OpenDisposalAsync()
        {
            if (SelectedRecord == null) return;
            var latest = await _service.GetDisposalByIdAsync(SelectedRecord.Id);
            if (latest == null)
            {
                _dialogService.ShowError("未找到处置单。");
                await RefreshDisposalsAsync();
                return;
            }

            if (_dialogService.ShowNetworkOnNetDisposalEditDialog(latest))
            {
                await RefreshDisposalsAsync();
                await RefreshAssetsAsync();
            }
        }

        private async Task WithdrawDisposalAsync()
        {
            if (SelectedRecord == null) return;
            try
            {
                if (!_dialogService.ShowConfirm($"确认撤回作废处置单【{SelectedRecord.DisposalNo}】？"))
                    return;
                await _service.WithdrawDisposalAsync(SelectedRecord.Id, null,
                    _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。"));
                await RefreshDisposalsAsync();
                await RefreshAssetsAsync();
                _dialogService.ShowMessage("已撤回作废。");
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }
    }
}
