using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料离库处置列表页 ViewModel（模拟/电子共用）。
    /// </summary>
    public sealed class ArchiveDisposalPageViewModel : ViewModelBase
    {
        public const string PendingInProgressStatus = "进行中（待办结前）";

        private readonly IArchiveDisposalService _disposalService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly List<YearlyArchiveDisposalRecord> _allRecords = new();

        private bool _isConfigured;
        private bool _isInitialized;
        private bool _matchAllYears;
        private string _mediaKind = ArchiveRegisterDomainValues.MediaKindSimulated;
        private int _applyYear = DateTime.Today.Year;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private YearlyArchiveDisposalRecord? _selectedRecord;

        public ArchiveDisposalPageViewModel(
            IArchiveDisposalService disposalService,
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

        public void Configure(string mediaKind)
        {
            string normalized = mediaKind?.Trim() ?? string.Empty;
            if (!ArchiveInventoryRegisterDomainValues.IsValidMediaKind(normalized))
            {
                throw new ArgumentException("介质类别无效。", nameof(mediaKind));
            }

            _mediaKind = normalized;
            _isConfigured = true;
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(BannerText));
            OnPropertyChanged(nameof(IsSimulated));
        }

        public bool IsSimulated =>
            string.Equals(_mediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);

        public string PageTitle => IsSimulated ? "模拟资料离库处置办理表" : "电子资料离库处置办理表";

        public string BannerText => IsSimulated
            ? "仅清账盘库登记产生的丢失/拟销资料。流程对齐硬盘离库处置：草稿→提交→审批→确认可上传→上传签批→办结。办结将事实置为已处置；空盒释档前须确认物理移除。"
            : "仅清账电子袋内已盘库登记为损坏/盘失/拟销的硬盘或光盘。拟销硬盘可选低格留存（空白硬盘专用档口）或介质销毁。空袋释档前须确认物理移除。";

        public ObservableCollection<YearlyArchiveDisposalRecord> Records { get; } = new();

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
                    _matchAllYears = false;
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

        public YearlyArchiveDisposalRecord? SelectedRecord
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
            && SelectedRecord.Status is not YearlyArchiveDisposalRecord.StatusCompleted
                and not YearlyArchiveDisposalRecord.StatusWithdrawn
                and not YearlyArchiveDisposalRecord.StatusForceWithdrawn;

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand WithdrawCommand { get; }

        public async Task InitializeAsync(bool pendingInProgress = false, bool matchAllYears = false)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("请先调用 Configure(mediaKind)。");
            }

            if (_isInitialized)
            {
                _matchAllYears = matchAllYears;
                if (pendingInProgress)
                {
                    SelectedStatus = PendingInProgressStatus;
                }

                ApplyFilters();
                return;
            }

            StatusOptions.Clear();
            StatusOptions.Add("全部");
            StatusOptions.Add(PendingInProgressStatus);
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

            _matchAllYears = matchAllYears;
            if (pendingInProgress)
            {
                _selectedStatus = PendingInProgressStatus;
                OnPropertyChanged(nameof(SelectedStatus));
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
                var list = await _disposalService.SearchRecordsAsync(keyword, status: null, applyYear: null, _mediaKind);
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
            IEnumerable<YearlyArchiveDisposalRecord> query = _allRecords;

            if (!_matchAllYears)
            {
                query = query.Where(item => item.ApplyTime.Year == ApplyYear);
            }

            if (string.Equals(SelectedStatus, PendingInProgressStatus, StringComparison.Ordinal))
            {
                query = query.Where(item =>
                    item.Status is YearlyArchiveDisposalRecord.StatusSubmitted
                        or YearlyArchiveDisposalRecord.StatusApproved
                        or YearlyArchiveDisposalRecord.StatusSignedUploaded);
            }
            else if (!string.Equals(SelectedStatus, "全部", StringComparison.Ordinal)
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
            foreach (var item in query.OrderByDescending(r => r.ApplyTime).ThenByDescending(r => r.Id))
            {
                Records.Add(item);
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private async Task AddAsync()
        {
            if (!CanOperate)
            {
                _dialogService.ShowError("仅资料室资料管理员可办理资料离库处置。");
                return;
            }

            var draft = new YearlyArchiveDisposalRecord
            {
                MediaKind = _mediaKind,
                ApplyTime = DateTime.Now,
                ApplicantName = _userContextService.CurrentUser?.RealName?.Trim() ?? string.Empty,
                ApplicantDept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty,
                Status = YearlyArchiveDisposalRecord.StatusDraft
            };

            if (_dialogService.ShowArchiveDisposalEditDialog(draft))
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

            if (_dialogService.ShowArchiveDisposalEditDialog(latest))
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
