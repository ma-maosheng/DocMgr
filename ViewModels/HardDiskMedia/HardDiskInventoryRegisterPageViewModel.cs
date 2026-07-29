using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
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
        private bool _matchAllYears;
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

        public ObservableCollection<HardDiskInventoryRegisterDetailRow> DetailItems { get; } = new();

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

        public HardDiskInventoryRegisterRecord? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (SetProperty(ref _selectedRecord, value))
                {
                    RefreshDetailPane();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool HasSelectedRecord => SelectedRecord != null;

        public Visibility DetailEmptyVisibility =>
            HasSelectedRecord ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DetailContentVisibility =>
            HasSelectedRecord ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>右侧详情区摘要文字。</summary>
        public string DetailSummaryText
        {
            get
            {
                if (SelectedRecord == null)
                {
                    return string.Empty;
                }

                var record = SelectedRecord;
                var builder = new StringBuilder();
                builder.Append(string.IsNullOrWhiteSpace(record.RegisterNo) ? "待编单" : record.RegisterNo.Trim());
                builder.Append(" · ");
                builder.Append(string.IsNullOrWhiteSpace(record.RegisterKind) ? "未选类型" : record.RegisterKind.Trim());
                builder.Append(" · ");
                builder.Append(record.StatusDisplay);
                builder.Append(" · 登记人 ");
                builder.Append(string.IsNullOrWhiteSpace(record.ApplicantName) ? "-" : record.ApplicantName.Trim());
                builder.Append(" · ");
                builder.Append(record.ApplyTime.ToString("yyyy-MM-dd"));
                builder.Append(" · 硬盘 ");
                builder.Append(record.ItemCount);
                builder.Append(" 块");

                if (!string.IsNullOrWhiteSpace(record.DiskCodesSummary))
                {
                    builder.Append(" · 编号 ");
                    builder.Append(record.DiskCodesSummary);
                }

                if (!string.IsNullOrWhiteSpace(record.Reason))
                {
                    builder.Append(" · 说明：");
                    builder.Append(record.Reason.Trim());
                }

                if (!string.IsNullOrWhiteSpace(record.Remark))
                {
                    builder.Append(" · 备注：");
                    builder.Append(record.Remark.Trim());
                }

                if (!string.IsNullOrWhiteSpace(record.CompletedBy))
                {
                    builder.Append(" · 办结人 ");
                    builder.Append(record.CompletedBy.Trim());
                }

                return builder.ToString();
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

        public async Task InitializeAsync(string? initialStatus = null, bool matchAllYears = false)
        {
            if (_isInitialized)
            {
                _matchAllYears = matchAllYears;
                if (!string.IsNullOrWhiteSpace(initialStatus))
                {
                    SelectedStatus = initialStatus.Trim();
                }

                ApplyFilters();
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

            _matchAllYears = matchAllYears;
            if (!string.IsNullOrWhiteSpace(initialStatus)
                && StatusOptions.Contains(initialStatus))
            {
                _selectedStatus = initialStatus.Trim();
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
                var list = await _registerService.SearchRecordsAsync(keyword, status: null, applyYear: null);
                _allRecords.Clear();
                _allRecords.AddRange(list);
                ApplyFilters();

                if (selectedId.HasValue)
                {
                    SelectedRecord = Records.FirstOrDefault(item => item.Id == selectedId.Value);
                }
                else
                {
                    RefreshDetailPane();
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
            if (!_matchAllYears)
            {
                query = query.Where(item => item.ApplyTime.Year == ApplyYear);
            }

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

            if (SelectedRecord != null && !Records.Contains(SelectedRecord))
            {
                SelectedRecord = null;
            }
            else
            {
                RefreshDetailPane();
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshDetailPane()
        {
            DetailItems.Clear();
            if (SelectedRecord?.Items != null)
            {
                foreach (var item in SelectedRecord.Items.OrderBy(detail => detail.SortOrder).ThenBy(detail => detail.Id))
                {
                    DetailItems.Add(HardDiskInventoryRegisterDetailRow.FromItem(item));
                }
            }

            OnPropertyChanged(nameof(HasSelectedRecord));
            OnPropertyChanged(nameof(DetailEmptyVisibility));
            OnPropertyChanged(nameof(DetailContentVisibility));
            OnPropertyChanged(nameof(DetailSummaryText));
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

    /// <summary>硬盘盘库登记办理表右侧明细行。</summary>
    public sealed class HardDiskInventoryRegisterDetailRow
    {
        public string DiskCode { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public string BeforeMediaStatus { get; init; } = string.Empty;

        public string BeforeStorageLocation { get; init; } = string.Empty;

        public string TargetStorageLocation { get; init; } = string.Empty;

        public string TargetStorageLocationDisplay =>
            string.IsNullOrWhiteSpace(TargetStorageLocation) ? "-" : TargetStorageLocation.Trim();

        public static HardDiskInventoryRegisterDetailRow FromItem(HardDiskInventoryRegisterItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            return new HardDiskInventoryRegisterDetailRow
            {
                DiskCode = item.DiskCode?.Trim() ?? string.Empty,
                SerialNumber = item.SerialNumber?.Trim() ?? string.Empty,
                BeforeMediaStatus = item.BeforeMediaStatus?.Trim() ?? string.Empty,
                BeforeStorageLocation = item.BeforeStorageLocation?.Trim() ?? string.Empty,
                TargetStorageLocation = item.TargetStorageLocation?.Trim() ?? string.Empty
            };
        }
    }
}
