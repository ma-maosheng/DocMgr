using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘盘库登记列表页 ViewModel。
    /// </summary>
    public sealed class HardDiskInventoryRegisterPageViewModel : ViewModelBase
    {
        private readonly IHardDiskInventoryRegisterService _registerService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly List<HardDiskInventoryRegisterRecord> _allRecords = new();

        private bool _isInitialized;
        private int _applyYear = DateTime.Today.Year;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private HardDiskInventoryRegisterRecord? _selectedRecord;

        public HardDiskInventoryRegisterPageViewModel(
            IHardDiskInventoryRegisterService registerService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _registerService = registerService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            SearchCommand = new RelayCommand(async _ => await RefreshAsync());
            AddCommand = new RelayCommand(async _ => await AddAsync(), _ => CanOperate);
            OpenCommand = new RelayCommand(async _ => await OpenAsync(), _ => SelectedRecord != null && CanOperate);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdrawSelected);
        }

        public ObservableCollection<HardDiskInventoryRegisterRecord> Records { get; } = new();

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

        public HardDiskInventoryRegisterRecord? SelectedRecord
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
            && SelectedRecord.Status == HardDiskInventoryRegisterRecord.StatusDraft;

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
            StatusOptions.Add(ApplicationWorkflowStatus.TextDraft);
            StatusOptions.Add(ApplicationWorkflowStatus.TextCompleted);
            StatusOptions.Add(ApplicationWorkflowStatus.TextWithdrawn);

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
                var list = await _registerService.SearchRecordsAsync(keyword, status: null, applyYear: null);
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
            IEnumerable<HardDiskInventoryRegisterRecord> query = _allRecords;
            query = query.Where(item => item.ApplyTime.Year == ApplyYear);

            if (!string.Equals(SelectedStatus, "全部", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(SelectedStatus))
            {
                int? statusValue = SelectedStatus switch
                {
                    var text when string.Equals(text, ApplicationWorkflowStatus.TextDraft, StringComparison.Ordinal)
                        => HardDiskInventoryRegisterRecord.StatusDraft,
                    var text when string.Equals(text, ApplicationWorkflowStatus.TextCompleted, StringComparison.Ordinal)
                        => HardDiskInventoryRegisterRecord.StatusCompleted,
                    var text when string.Equals(text, ApplicationWorkflowStatus.TextWithdrawn, StringComparison.Ordinal)
                        => HardDiskInventoryRegisterRecord.StatusWithdrawn,
                    _ => null
                };

                if (statusValue.HasValue)
                {
                    query = query.Where(item => item.Status == statusValue.Value);
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
                _dialogService.ShowError("仅资料室资料管理员可办理盘库登记。");
                return;
            }

            var draft = new HardDiskInventoryRegisterRecord
            {
                ApplyTime = DateTime.Now,
                ApplicantName = _userContextService.CurrentUser?.RealName?.Trim() ?? string.Empty,
                ApplicantDept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty,
                Status = HardDiskInventoryRegisterRecord.StatusDraft,
                RegisterKind = HardDiskInventoryRegisterDomainValues.KindDamage
            };

            if (_dialogService.ShowHardDiskInventoryRegisterEditDialog(draft))
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

            var latest = await _registerService.GetRecordByIdAsync(SelectedRecord.Id);
            if (latest == null)
            {
                _dialogService.ShowError("未找到登记单。");
                return;
            }

            if (_dialogService.ShowHardDiskInventoryRegisterEditDialog(latest))
            {
                await RefreshAsync();
            }
        }

        private async Task WithdrawAsync()
        {
            if (SelectedRecord == null || !CanWithdrawSelected)
            {
                return;
            }

            if (!_dialogService.ShowConfirm("确认撤回作废当前盘库登记草稿？", "撤回作废"))
            {
                return;
            }

            try
            {
                await _registerService.WithdrawAsync(
                    SelectedRecord.Id,
                    reason: "列表撤回作废",
                    _userContextService.CurrentUser!);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }
    }
}
