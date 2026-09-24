using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 年度资料盘库登记列表页 ViewModel（模拟/电子共用，按 MediaKind 分流）。
    /// </summary>
    public sealed class ArchiveInventoryRegisterPageViewModel : ViewModelBase
    {
        private readonly IArchiveInventoryRegisterService _registerService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;
        private readonly List<YearlyArchiveInventoryRegisterRecord> _allRecords = new();

        private bool _isConfigured;
        private bool _isInitialized;
        private bool _matchAllYears;
        private string _mediaKind = ArchiveInventoryRegisterDomainValues.MediaKindSimulated;
        private int _applyYear = DateTime.Today.Year;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private YearlyArchiveInventoryRegisterRecord? _selectedRecord;
        private string _applicationOverdueSettingCode = string.Empty;

        public ArchiveInventoryRegisterPageViewModel(
            IArchiveInventoryRegisterService registerService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IBusinessLogicSettingsService businessLogicSettingsService)
        {
            _registerService = registerService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _businessLogicSettingsService = businessLogicSettingsService;

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            SearchCommand = new RelayCommand(async _ => await RefreshAsync());
            AddCommand = new RelayCommand(async _ => await AddAsync(), _ => CanCreateDraft);
            OpenCommand = new RelayCommand(async _ => await OpenAsync(), _ => SelectedRecord != null);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdrawSelected);
            ForceVoidCommand = new RelayCommand(async _ => await ForceVoidAsync(), _ => CanForceVoidSelected);
        }

        public const string PendingInProgressStatus = "进行中（待办结前）";

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
            OnPropertyChanged(nameof(SimulatedColumnVisibility));
            OnPropertyChanged(nameof(ElectronicColumnVisibility));
            RefreshDetailPane();
        }

        public bool IsSimulated =>
            string.Equals(_mediaKind, ArchiveInventoryRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);

        public string PageTitle => IsSimulated ? "模拟资料盘库登记办理表" : "电子资料盘库登记办理表";

        public string BannerText => IsSimulated
            ? "按资料子项登记库内丢失或拟销份数。拟销用于无存档价值资料。流程：草稿→提交→打印签批→线下签字→审批通过→确认可上传→上传签批单→办结。办结后即时扣减可借份数并写履历；空盒仍占档口并标「失」「销」，正式清账请后期走「离库处置」。"
            : "按电子袋内硬盘/光盘登记损坏、盘失或拟销。拟销用于无存档价值资料。流程：草稿→提交→打印签批→线下签字→审批通过→确认可上传→上传签批单→办结。办结后即时改介质台账（保留档口）并禁用关联资料借出；袋不移走，正式清账请后期走「离库处置」。";

        /// <summary>仅模拟轨可见的列表/详情区域。</summary>
        public Visibility SimulatedColumnVisibility => IsSimulated ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>仅电子轨可见的列表/详情区域。</summary>
        public Visibility ElectronicColumnVisibility => IsSimulated ? Visibility.Collapsed : Visibility.Visible;

        public ObservableCollection<YearlyArchiveInventoryRegisterRecord> Records { get; } = new();

        public ObservableCollection<ArchiveInventoryRegisterDetailRow> DetailItems { get; } = new();

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

        public YearlyArchiveInventoryRegisterRecord? SelectedRecord
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

                if (IsSimulated)
                {
                    builder.Append(" · 明细 ");
                    builder.Append(record.ItemCount);
                    builder.Append(" 条 · 丢失份数 ");
                    builder.Append(record.SimulatedLostCopySummary);
                    if (!string.IsNullOrWhiteSpace(record.SimulatedBoxSummary))
                    {
                        builder.Append(" · 盒号 ");
                        builder.Append(record.SimulatedBoxSummary);
                    }

                    if (!string.IsNullOrWhiteSpace(record.SimulatedSlotSummary))
                    {
                        builder.Append(" · 档口 ");
                        builder.Append(record.SimulatedSlotSummary);
                    }
                }
                else
                {
                    builder.Append(" · 介质 ");
                    builder.Append(record.ItemCount);
                    builder.Append(" 块");
                    if (!string.IsNullOrWhiteSpace(record.ElectronicMediumKindSummary))
                    {
                        builder.Append("（");
                        builder.Append(record.ElectronicMediumKindSummary);
                        builder.Append('）');
                    }

                    if (!string.IsNullOrWhiteSpace(record.ElectronicArchiveBagSummary))
                    {
                        builder.Append(" · 电子袋 ");
                        builder.Append(record.ElectronicArchiveBagSummary);
                    }

                    if (!string.IsNullOrWhiteSpace(record.ElectronicSlotSummary))
                    {
                        builder.Append(" · 档口 ");
                        builder.Append(record.ElectronicSlotSummary);
                    }
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

        public bool CanCreateDraft =>
            OfflineApprovalPermissionSupport.CanCreateDraft(
                _userContextService.CurrentUser,
                ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister);

        public bool CanWithdrawSelected =>
            ApplicationListActionSupport.CanAdminWithdrawInventoryRegister(
                SelectedRecord?.Status ?? ApplicationWorkflowStatus.Completed,
                CanCreateDraft);

        public bool CanForceVoidSelected =>
            SelectedRecord != null
            && ApplicationListActionSupport.CanForceVoid(
                SelectedRecord.Status,
                CanCreateDraft,
                _businessLogicSettingsService.IsEligibleForAdminForceVoid(
                    SelectedRecord.ApplyTime,
                    _applicationOverdueSettingCode));

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand WithdrawCommand { get; }
        public RelayCommand ForceVoidCommand { get; }

        public async Task InitializeAsync(string? initialStatus = null, bool matchAllYears = false)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("请先调用 Configure 设置介质类别。");
            }

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
            StatusOptions.Add(PendingInProgressStatus);
            foreach (var option in ApplicationWorkflowStatus.AllOptions)
                StatusOptions.Add(option.Label);

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
                _applicationOverdueSettingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
                int? selectedId = SelectedRecord?.Id;
                string? keyword = string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword.Trim();
                var list = await _registerService.SearchRecordsAsync(_mediaKind, keyword, status: null, applyYear: null);
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
            IEnumerable<YearlyArchiveInventoryRegisterRecord> query = _allRecords;
            if (!_matchAllYears)
            {
                query = query.Where(item => item.ApplyTime.Year == ApplyYear);
            }

            if (!string.Equals(SelectedStatus, "全部", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(SelectedStatus))
            {
                if (string.Equals(SelectedStatus, PendingInProgressStatus, StringComparison.Ordinal))
                {
                    query = query.Where(item =>
                        item.Status is YearlyArchiveInventoryRegisterRecord.StatusSubmitted
                            or YearlyArchiveInventoryRegisterRecord.StatusApproved
                            or YearlyArchiveInventoryRegisterRecord.StatusSignedUploaded);
                }
                else
                {
                    var matched = ApplicationWorkflowStatus.AllOptions
                        .FirstOrDefault(item => string.Equals(item.Label, SelectedStatus, StringComparison.Ordinal));
                    if (!string.IsNullOrWhiteSpace(matched.Label))
                        query = query.Where(item => item.Status == matched.Value);
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
                bool isScrap = string.Equals(
                    SelectedRecord.RegisterKind?.Trim(),
                    ArchiveInventoryRegisterDomainValues.KindScrap,
                    StringComparison.Ordinal);

                foreach (var item in SelectedRecord.Items.OrderBy(detail => detail.SortOrder).ThenBy(detail => detail.Id))
                {
                    DetailItems.Add(ArchiveInventoryRegisterDetailRow.FromItem(item, isScrap));
                }
            }

            OnPropertyChanged(nameof(HasSelectedRecord));
            OnPropertyChanged(nameof(DetailEmptyVisibility));
            OnPropertyChanged(nameof(DetailContentVisibility));
            OnPropertyChanged(nameof(DetailSummaryText));
        }

        private async Task AddAsync()
        {
            if (!CanCreateDraft)
            {
                _dialogService.ShowError("仅资料管理员可办理盘库登记。");
                return;
            }

            var draft = new YearlyArchiveInventoryRegisterRecord
            {
                ApplyTime = DateTime.Now,
                ApplicantName = _userContextService.CurrentUser?.RealName?.Trim() ?? string.Empty,
                ApplicantDept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty,
                Status = YearlyArchiveInventoryRegisterRecord.StatusDraft,
                MediaKind = _mediaKind,
                RegisterKind = ArchiveInventoryRegisterDomainValues.KindLost
            };

            if (ShowEditDialog(draft))
            {
                await RefreshAsync();
            }
        }

        private bool ShowEditDialog(YearlyArchiveInventoryRegisterRecord record) =>
            IsSimulated
                ? _dialogService.ShowSimulatedArchiveInventoryRegisterEditDialog(record)
                : _dialogService.ShowElectronicArchiveInventoryRegisterEditDialog(record);

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

            if (ShowEditDialog(latest))
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

            if (!_dialogService.ShowConfirm("确认撤回作废当前盘库登记单？仅草稿或已提交状态可撤回。", "撤回作废"))
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

        private async Task ForceVoidAsync()
        {
            if (SelectedRecord == null || !CanForceVoidSelected)
            {
                return;
            }

            if (!_dialogService.ShowConfirm(
                    "确定要强制作废该盘库登记单吗？仅草稿或已提交且已达逾期时限的单据可强制作废。",
                    "强制作废确认"))
            {
                return;
            }

            try
            {
                await _registerService.ForceVoidAsync(
                    SelectedRecord.Id,
                    reason: "列表强制作废",
                    _userContextService.CurrentUser!);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }
    }

    /// <summary>盘库登记办理表右侧明细行（模拟/电子分列绑定）。</summary>
    public sealed class ArchiveInventoryRegisterDetailRow
    {
        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string ContainerCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string AvailableCopyCountDisplay { get; init; } = string.Empty;

        public string LostCopyCountDisplay { get; init; } = string.Empty;

        public string MediumKind { get; init; } = string.Empty;

        public string MediumCodeDisplay { get; init; } = string.Empty;

        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string MediaStatus { get; init; } = string.Empty;

        public static ArchiveInventoryRegisterDetailRow FromItem(
            YearlyArchiveInventoryRegisterItem item,
            bool isScrapRegisterKind)
        {
            ArgumentNullException.ThrowIfNull(item);

            return new ArchiveInventoryRegisterDetailRow
            {
                ProjectName = item.ProjectName?.Trim() ?? string.Empty,
                Year = item.Year?.Trim() ?? string.Empty,
                ContainerCode = item.ContainerCode?.Trim() ?? string.Empty,
                StorageLocation = item.BeforeStorageLocation?.Trim() ?? string.Empty,
                MaterialName = item.MaterialName?.Trim() ?? string.Empty,
                ItemName = item.ItemName?.Trim() ?? string.Empty,
                AvailableCopyCountDisplay = item.BeforeAvailableCopyCount.ToString(),
                LostCopyCountDisplay = isScrapRegisterKind ? "-" : Math.Max(0, item.LostCopyCount).ToString(),
                MediumKind = item.MediumKind?.Trim() ?? string.Empty,
                MediumCodeDisplay = ArchiveInventoryRegisterDomainValues.ResolveMediumCodeDisplay(
                    item.MediumKind,
                    item.MediumCode),
                ElectronicArchiveNo = item.ElectronicArchiveNo?.Trim() ?? string.Empty,
                MediaStatus = item.BeforeMediaStatus?.Trim() ?? string.Empty
            };
        }
    }
}
