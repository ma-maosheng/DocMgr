using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘离库处置列表页 ViewModel。
    /// </summary>
    public sealed class HardDiskDisposalPageViewModel : ViewModelBase
    {
        private readonly IHardDiskDisposalService _disposalService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly List<HardDiskDisposalRecord> _allRecords = new();

        private bool _isInitialized;
        private int _applyYear = DateTime.Today.Year;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private HardDiskDisposalRecord? _selectedRecord;

        public HardDiskDisposalPageViewModel(
            IHardDiskDisposalService disposalService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _disposalService = disposalService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            SearchCommand = new RelayCommand(async _ => await RefreshAsync());
            AddCommand = new RelayCommand(async _ => await AddAsync(), _ => CanOperate);
            OpenCommand = new RelayCommand(async _ => await OpenAsync(), _ => SelectedRecord != null && CanOperate);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdrawSelected);
        }

        public ObservableCollection<HardDiskDisposalRecord> Records { get; } = new();

        public ObservableCollection<int> ApplyYears { get; } = new();

        public ObservableCollection<string> StatusOptions { get; } = new();

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        public int ApplyYear
        {
            get => _applyYear;
            set
            {
                if (SetProperty(ref _applyYear, value) && _isInitialized)
                {
                    ApplyFilters();
                }
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value) && _isInitialized)
                {
                    ApplyFilters();
                }
            }
        }

        public HardDiskDisposalRecord? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (SetProperty(ref _selectedRecord, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanOperate =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanWithdrawSelected =>
            CanOperate
            && SelectedRecord != null
            && SelectedRecord.Status is not HardDiskDisposalRecord.StatusCompleted
                and not HardDiskDisposalRecord.StatusWithdrawn
                and not HardDiskDisposalRecord.StatusForceWithdrawn;

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand WithdrawCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            StatusOptions.Clear();
            StatusOptions.Add("全部");
            foreach (var option in ApplicationWorkflowStatus.AllOptions)
            {
                StatusOptions.Add(option.Label);
            }

            ApplyYears.Clear();
            int currentYear = DateTime.Today.Year;
            for (int year = currentYear; year >= currentYear - 5; year--)
            {
                ApplyYears.Add(year);
            }

            await RefreshAsync();
            _isInitialized = true;
        }

        private async Task RefreshAsync()
        {
            try
            {
                int? selectedId = SelectedRecord?.Id;
                string? keyword = string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword.Trim();
                var list = await _disposalService.SearchRecordsAsync(keyword, status: null, applyYear: null);
                _allRecords.Clear();
                _allRecords.AddRange(list);
                ApplyFilters();

                if (selectedId.HasValue)
                {
                    SelectedRecord = Records.FirstOrDefault(item => item.Id == selectedId.Value);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<HardDiskDisposalRecord> query = _allRecords;

            query = query.Where(item => item.ApplyTime.Year == ApplyYear);

            if (!string.Equals(SelectedStatus, "全部", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(SelectedStatus))
            {
                var matched = ApplicationWorkflowStatus.AllOptions
                    .FirstOrDefault(item => string.Equals(item.Label, SelectedStatus, StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(matched.Label))
                {
                    query = query.Where(item => item.Status == matched.Value);
                }
            }

            Records.Clear();
            foreach (var item in query.OrderByDescending(record => record.ApplyTime).ThenByDescending(record => record.Id))
            {
                Records.Add(item);
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private async Task AddAsync()
        {
            if (!CanOperate)
            {
                _dialogService.ShowError("仅资料室资料管理员可办理离库处置。");
                return;
            }

            var draft = new HardDiskDisposalRecord
            {
                ApplyTime = DateTime.Now,
                ApplicantName = _userContextService.CurrentUser?.RealName?.Trim() ?? string.Empty,
                ApplicantDept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty,
                Status = HardDiskDisposalRecord.StatusDraft
            };

            if (_dialogService.ShowHardDiskDisposalEditDialog(draft))
            {
                await RefreshAsync();
            }
        }

        private async Task OpenAsync()
        {
            if (SelectedRecord == null)
            {
                return;
            }

            var latest = await _disposalService.GetRecordByIdAsync(SelectedRecord.Id);
            if (latest == null)
            {
                _dialogService.ShowError("未找到处置单。");
                await RefreshAsync();
                return;
            }

            if (_dialogService.ShowHardDiskDisposalEditDialog(latest))
            {
                await RefreshAsync();
            }
        }

        private async Task WithdrawAsync()
        {
            if (SelectedRecord == null)
            {
                return;
            }

            try
            {
                if (!_dialogService.ShowConfirm($"确认撤回作废处置单【{SelectedRecord.DisposalNo}】？"))
                {
                    return;
                }

                await _disposalService.WithdrawAsync(
                    SelectedRecord.Id,
                    null,
                    _userContextService.CurrentUser
                        ?? throw new InvalidOperationException("当前用户无效。"));
                await RefreshAsync();
                _dialogService.ShowMessage("已撤回作废。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }
    }
}
