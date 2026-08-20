using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 立档台账页面 ViewModel：按年度查询、浏览立档事实。
    /// </summary>
    public sealed class ArchiveFilingLedgerViewModel : ViewModelBase
    {
        private const string AllYearsOption = "全部年度";

        private readonly IArchiveFilingLedgerService _ledgerService;
        private readonly IArchiveMaterialTransactionService _materialTransactionService;
        private readonly IDialogService _dialogService;

        private bool _isInitialized;
        private string _selectedYear = AllYearsOption;
        private int? _selectedProjectId;
        private string _selectedMediaKind = string.Empty;
        private string _selectedLifecycleStatus = string.Empty;
        private string _selectedArchiveCopyRole = string.Empty;
        private string _keyword = string.Empty;
        private string _contentEntryKeyword = string.Empty;
        private DateTime? _filedFrom;
        private DateTime? _filedTo;
        private FilingLedgerRow? _selectedRow;
        private string _summaryText = "共 0 条";
        private string _detailSummary = string.Empty;
        private bool _isFilingInfoExpanded;
        private bool _isStorageExpanded;
        private bool _isLifecycleExpanded;
        private bool _isMaterialTransactionsExpanded;
        private bool _isOutboundProcessNodesExpanded;
        private bool _isContentEntriesExpanded;

        public ArchiveFilingLedgerViewModel(
            IArchiveFilingLedgerService ledgerService,
            IArchiveMaterialTransactionService materialTransactionService,
            IDialogService dialogService)
        {
            _ledgerService = ledgerService;
            _materialTransactionService = materialTransactionService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            ResetCommand = new RelayCommand(_ => ResetCriteria());
            RefreshCommand = new RelayCommand(async _ => await SearchAsync());
            ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => LedgerRows.Count > 0);
            ViewRegisterDetailCommand = new RelayCommand(
                _ => ViewRegisterDetail(),
                _ => SelectedRow != null && SelectedRow.RegisterRecordId > 0);

            ContentEntriesPanel = new ItemDetailsListPresenter<FilingLedgerContentEntryInfo>(
                "电子介质目录 / 文件明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.EntryName,
                    "暂无目录/文件明细"));
            ContentEntriesPanel.RefreshItems(SelectedContentEntries);
        }

        public event Action<ArchiveDetailOpenRequest>? ViewRegisterDetailRequested;

        public ObservableCollection<string> Years { get; } = new();

        public string SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (!SetProperty(ref _selectedYear, value))
                {
                    return;
                }

                SelectedProjectId = null;
                _ = LoadProjectOptionsAsync();
            }
        }

        public ObservableCollection<ProjectFilterOption> ProjectOptions { get; } = new();

        public int? SelectedProjectId
        {
            get => _selectedProjectId;
            set => SetProperty(ref _selectedProjectId, value);
        }

        public ObservableCollection<FilterOption> MediaKindOptions { get; } =
        [
            new FilterOption { Label = "全部介质", Value = string.Empty },
            new FilterOption { Label = ArchiveRegisterDomainValues.MediaKindSimulated, Value = ArchiveRegisterDomainValues.MediaKindSimulated },
            new FilterOption { Label = ArchiveRegisterDomainValues.MediaKindElectronic, Value = ArchiveRegisterDomainValues.MediaKindElectronic }
        ];

        public string SelectedMediaKind
        {
            get => _selectedMediaKind;
            set
            {
                if (!SetProperty(ref _selectedMediaKind, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(ShowSimulatedOnlyColumns));
                OnPropertyChanged(nameof(ShowElectronicOnlyColumns));
                OnPropertyChanged(nameof(ShowClassificationColumns));
            }
        }

        /// <summary>介质筛为「模拟」时显示专属列（介质类型、份数等）。</summary>
        public bool ShowSimulatedOnlyColumns => string.Equals(
            SelectedMediaKind,
            ArchiveRegisterDomainValues.MediaKindSimulated,
            StringComparison.Ordinal);

        /// <summary>介质筛为「电子」时显示专属列（目录、数据量等）。</summary>
        public bool ShowElectronicOnlyColumns => string.Equals(
            SelectedMediaKind,
            ArchiveRegisterDomainValues.MediaKindElectronic,
            StringComparison.Ordinal);

        /// <summary>模拟与电子均展示资料类型 / 所属子类 / 组织形式。</summary>
        public bool ShowClassificationColumns =>
            string.IsNullOrWhiteSpace(SelectedMediaKind)
            || ShowSimulatedOnlyColumns
            || ShowElectronicOnlyColumns;

        public ObservableCollection<FilterOption> LifecycleStatusOptions { get; } =
        [
            new FilterOption { Label = "全部状态", Value = string.Empty },
            new FilterOption { Label = "在库", Value = FilingFactLifecycleStatus.InArchive },
            new FilterOption { Label = "借出中", Value = FilingFactLifecycleStatus.Borrowed },
            new FilterOption { Label = "已转移", Value = FilingFactLifecycleStatus.Transferred },
            new FilterOption { Label = "已销毁", Value = FilingFactLifecycleStatus.Destroyed },
            new FilterOption { Label = "已处置", Value = FilingFactLifecycleStatus.Disposed }
        ];

        public string SelectedLifecycleStatus
        {
            get => _selectedLifecycleStatus;
            set => SetProperty(ref _selectedLifecycleStatus, value);
        }

        public ObservableCollection<FilterOption> ArchiveCopyRoleOptions { get; } =
        [
            new FilterOption { Label = "全部", Value = string.Empty },
            new FilterOption { Label = "原件", Value = FilingFactArchiveCopyRole.Original },
            new FilterOption { Label = "备份", Value = FilingFactArchiveCopyRole.Backup }
        ];

        public string SelectedArchiveCopyRole
        {
            get => _selectedArchiveCopyRole;
            set => SetProperty(ref _selectedArchiveCopyRole, value);
        }

        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        public string ContentEntryKeyword
        {
            get => _contentEntryKeyword;
            set => SetProperty(ref _contentEntryKeyword, value);
        }

        public DateTime? FiledFrom
        {
            get => _filedFrom;
            set => SetProperty(ref _filedFrom, value);
        }

        public DateTime? FiledTo
        {
            get => _filedTo;
            set => SetProperty(ref _filedTo, value);
        }

        public ObservableCollection<FilingLedgerRow> LedgerRows { get; } = new();

        public ObservableCollection<FilingLedgerContentEntryInfo> SelectedContentEntries { get; } = new();

        public ItemDetailsListPresenter<FilingLedgerContentEntryInfo> ContentEntriesPanel { get; }

        public ObservableCollection<MaterialTransactionTimelineRow> SelectedMaterialTransactions { get; } = new();

        public FilingLedgerRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (!SetProperty(ref _selectedRow, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(HasSelectedRow));
                _ = LoadSelectedDetailAsync();
            }
        }

        public bool HasSelectedRow => SelectedRow != null;

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        public string DetailSummary
        {
            get => _detailSummary;
            private set => SetProperty(ref _detailSummary, value);
        }

        public bool HasSelectedContentEntries => SelectedContentEntries.Count > 0;

        public bool HasSelectedMaterialTransactions => SelectedMaterialTransactions.Count > 0;

        public ObservableCollection<MaterialOutboundProcessNodeRow> SelectedOutboundProcessNodes { get; } = new();

        public bool HasSelectedOutboundProcessNodes => SelectedOutboundProcessNodes.Count > 0;

        public bool IsFilingInfoExpanded
        {
            get => _isFilingInfoExpanded;
            set => SetProperty(ref _isFilingInfoExpanded, value);
        }

        public bool IsStorageExpanded
        {
            get => _isStorageExpanded;
            set => SetProperty(ref _isStorageExpanded, value);
        }

        public bool IsLifecycleExpanded
        {
            get => _isLifecycleExpanded;
            set => SetProperty(ref _isLifecycleExpanded, value);
        }

        public bool IsMaterialTransactionsExpanded
        {
            get => _isMaterialTransactionsExpanded;
            set => SetProperty(ref _isMaterialTransactionsExpanded, value);
        }

        public bool IsOutboundProcessNodesExpanded
        {
            get => _isOutboundProcessNodesExpanded;
            set => SetProperty(ref _isOutboundProcessNodesExpanded, value);
        }

        public bool IsContentEntriesExpanded
        {
            get => _isContentEntriesExpanded;
            set => SetProperty(ref _isContentEntriesExpanded, value);
        }

        public string MaterialTransactionsExpanderHeader =>
            SelectedMaterialTransactions.Count > 0
                ? $"流转履历（{SelectedMaterialTransactions.Count}）"
                : "流转履历";

        public string OutboundProcessNodesExpanderHeader =>
            SelectedOutboundProcessNodes.Count > 0
                ? $"关联出库单 / 流程节点（{SelectedOutboundProcessNodes.Count}）"
                : "关联出库单 / 流程节点";

        public string ContentEntriesExpanderHeader =>
            SelectedContentEntries.Count > 0
                ? $"电子介质目录 / 文件明细（{SelectedContentEntries.Count}）"
                : "电子介质目录 / 文件明细";

        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ViewRegisterDetailCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadYearsAsync();
            await LoadProjectOptionsAsync();
            await SearchAsync();
            _isInitialized = true;
        }

        /// <summary>
        /// 从迁档/流转台账跳转后定位立档记录。
        /// </summary>
        public async Task ApplyPendingNavigationFocusAsync()
        {
            if (ArchiveFilingLedgerNavigationState.PendingFilingFactId is not int filingFactId || filingFactId <= 0)
            {
                return;
            }

            ArchiveFilingLedgerNavigationState.PendingFilingFactId = null;

            try
            {
                var rows = await _ledgerService.SearchAsync(new FilingLedgerSearchCriteria
                {
                    FilingFactId = filingFactId
                });

                if (rows.Count == 0)
                {
                    _dialogService.ShowError("未在立档台账中找到对应的立档记录。");
                    return;
                }

                var target = rows[0];
                string targetYear = FilingFactNoSupport.TryParseArchiveYear(target.FilingFactNo)?.ToString()
                    ?? target.FiledAt.Year.ToString();
                if (Years.Contains(targetYear))
                {
                    SelectedYear = targetYear;
                }

                await LoadProjectOptionsAsync();
                await SearchAsync();
                SelectedRow = LedgerRows.FirstOrDefault(row => row.FilingFactId == filingFactId)
                    ?? target;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"定位立档记录失败：{ex.Message}");
            }
        }

        private async Task LoadYearsAsync()
        {
            try
            {
                var yearsList = await _ledgerService.GetExistingLedgerYearsAsync();
                Years.Clear();
                Years.Add(AllYearsOption);
                foreach (int year in yearsList)
                {
                    Years.Add(year.ToString());
                }

                if (!Years.Contains(_selectedYear))
                {
                    SelectedYear = AllYearsOption;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载年份失败：{ex.Message}");
                if (Years.Count == 0)
                {
                    Years.Add(AllYearsOption);
                    SelectedYear = AllYearsOption;
                }
            }
        }

        private async Task LoadProjectOptionsAsync()
        {
            try
            {
                ProjectOptions.Clear();
                ProjectOptions.Add(new ProjectFilterOption { Id = null, Name = "全部项目" });

                var projects = await _ledgerService.GetProjectOptionsForYearAsync(ResolveSelectedArchiveYear());
                foreach (var project in projects)
                {
                    if (project.ProjectId is not > 0 && string.IsNullOrWhiteSpace(project.ProjectName))
                    {
                        continue;
                    }

                    ProjectOptions.Add(new ProjectFilterOption
                    {
                        Id = project.ProjectId,
                        Name = project.ProjectName.Trim()
                    });
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载项目列表失败：{ex.Message}");
            }
        }

        private async Task SearchAsync()
        {
            try
            {
                int? selectedId = SelectedRow?.FilingFactId;
                var criteria = BuildCriteria();
                var rows = await _ledgerService.SearchAsync(criteria);

                LedgerRows.Clear();
                foreach (var row in rows)
                {
                    LedgerRows.Add(row);
                }

                SelectedRow = selectedId.HasValue
                    ? LedgerRows.FirstOrDefault(row => row.FilingFactId == selectedId.Value)
                    : LedgerRows.FirstOrDefault();

                UpdateSummary(rows);
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查询立档台账失败：{ex.Message}");
            }
        }

        private FilingLedgerSearchCriteria BuildCriteria()
        {
            return new FilingLedgerSearchCriteria
            {
                Year = ResolveSelectedArchiveYear(),
                ProjectId = SelectedProjectId,
                MediaKind = SelectedMediaKind?.Trim() ?? string.Empty,
                Keyword = Keyword?.Trim() ?? string.Empty,
                ContentEntryKeyword = ContentEntryKeyword?.Trim() ?? string.Empty,
                LifecycleStatus = string.IsNullOrWhiteSpace(SelectedLifecycleStatus)
                    ? null
                    : SelectedLifecycleStatus,
                ArchiveCopyRole = SelectedArchiveCopyRole?.Trim() ?? string.Empty,
                FiledFrom = FiledFrom,
                FiledTo = FiledTo
            };
        }

        private void UpdateSummary(IReadOnlyList<FilingLedgerRow> rows)
        {
            int simulatedCount = rows.Count(row => string.Equals(
                row.MediaKind,
                ArchiveRegisterDomainValues.MediaKindSimulated,
                StringComparison.Ordinal));
            int electronicCount = rows.Count(row => string.Equals(
                row.MediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal));
            int backupCount = rows.Count(row => string.Equals(
                row.ArchiveCopyRole,
                FilingFactArchiveCopyRole.Backup,
                StringComparison.Ordinal));

            SummaryText = $"共 {rows.Count} 条（模拟 {simulatedCount} / 电子 {electronicCount}，备份 {backupCount}）";
        }

        private async Task LoadSelectedDetailAsync()
        {
            SelectedContentEntries.Clear();
            ContentEntriesPanel.RefreshItems(SelectedContentEntries);
            SelectedMaterialTransactions.Clear();
            SelectedOutboundProcessNodes.Clear();
            OnPropertyChanged(nameof(HasSelectedContentEntries));
            OnPropertyChanged(nameof(HasSelectedMaterialTransactions));
            OnPropertyChanged(nameof(HasSelectedOutboundProcessNodes));
            OnPropertyChanged(nameof(MaterialTransactionsExpanderHeader));
            OnPropertyChanged(nameof(OutboundProcessNodesExpanderHeader));
            OnPropertyChanged(nameof(ContentEntriesExpanderHeader));

            if (SelectedRow == null)
            {
                DetailSummary = string.Empty;
                return;
            }

            var row = SelectedRow;
            string storageSummary = row.HasCurrentStorageChanged
                ? $"当前容器 {row.CurrentContainerCode} · 当前位置 {row.CurrentStorageLocation}（已变更）"
                : "存放与立档时一致";
            DetailSummary =
                $"立档事实 [{row.FilingFactNo}] · {row.MediaKind} · {row.FormNo} · {row.ItemName} · " +
                $"{storageSummary} · {row.LifecycleStatusDisplay}";

            try
            {
                var transactions = await _materialTransactionService.GetTimelineByFilingFactIdAsync(row.FilingFactId);
                foreach (var transaction in transactions)
                {
                    SelectedMaterialTransactions.Add(transaction);
                }

                OnPropertyChanged(nameof(HasSelectedMaterialTransactions));
                OnPropertyChanged(nameof(MaterialTransactionsExpanderHeader));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载流转履历失败：{ex.Message}");
            }

            try
            {
                var processNodes = await _materialTransactionService.GetOutboundProcessNodesByFilingFactIdAsync(row.FilingFactId);
                foreach (var node in processNodes)
                {
                    SelectedOutboundProcessNodes.Add(node);
                }

                OnPropertyChanged(nameof(HasSelectedOutboundProcessNodes));
                OnPropertyChanged(nameof(OutboundProcessNodesExpanderHeader));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载出库流程节点失败：{ex.Message}");
            }

            if (!row.IsElectronicMedia || row.MediaItemId <= 0)
            {
                return;
            }

            try
            {
                var entries = await _ledgerService.GetContentEntriesByMediaItemIdAsync(
                    row.MediaItemId,
                    row.FilingStoragePath);
                foreach (var entry in entries)
                {
                    SelectedContentEntries.Add(entry);
                }

                ContentEntriesPanel.RefreshItems(SelectedContentEntries, preserveExpanded: ContentEntriesPanel.IsExpanded);
                OnPropertyChanged(nameof(HasSelectedContentEntries));
                OnPropertyChanged(nameof(ContentEntriesExpanderHeader));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载目录/文件明细失败：{ex.Message}");
            }
        }

        private void ResetCriteria()
        {
            SelectedYear = AllYearsOption;
            SelectedProjectId = null;
            SelectedMediaKind = string.Empty;
            SelectedLifecycleStatus = string.Empty;
            SelectedArchiveCopyRole = string.Empty;
            Keyword = string.Empty;
            ContentEntryKeyword = string.Empty;
            FiledFrom = null;
            FiledTo = null;
            LedgerRows.Clear();
            SelectedRow = null;
            SelectedContentEntries.Clear();
            ContentEntriesPanel.RefreshItems(SelectedContentEntries);
            SelectedMaterialTransactions.Clear();
            SelectedOutboundProcessNodes.Clear();
            ResetDetailSectionExpanders();
            OnPropertyChanged(nameof(HasSelectedContentEntries));
            OnPropertyChanged(nameof(HasSelectedMaterialTransactions));
            OnPropertyChanged(nameof(HasSelectedOutboundProcessNodes));
            OnPropertyChanged(nameof(MaterialTransactionsExpanderHeader));
            OnPropertyChanged(nameof(OutboundProcessNodesExpanderHeader));
            OnPropertyChanged(nameof(ContentEntriesExpanderHeader));
            OnPropertyChanged(nameof(HasSelectedRow));
            SummaryText = "共 0 条";
            DetailSummary = string.Empty;
            CommandManager.InvalidateRequerySuggested();
        }

        private void ResetDetailSectionExpanders()
        {
            IsFilingInfoExpanded = false;
            IsStorageExpanded = false;
            IsLifecycleExpanded = false;
            IsMaterialTransactionsExpanded = false;
            IsOutboundProcessNodesExpanded = false;
            IsContentEntriesExpanded = false;
        }

        private async Task ExportAsync()
        {
            string yearLabel = string.Equals(SelectedYear, AllYearsOption, StringComparison.Ordinal)
                ? AllYearsOption
                : SelectedYear;
            string defaultFileName = $"立档台账_{yearLabel}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出立档台账", defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _dialogService.SetBusyState(true);
            try
            {
                await _ledgerService.ExportAsync(filePath, LedgerRows.ToList());
                _dialogService.ShowMessage($"立档台账导出完成：\n{filePath}", "完成");
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _dialogService.ShowError($"没有权限写入目标文件：{ex.Message}");
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"写入导出文件失败：{ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void ViewRegisterDetail()
        {
            if (SelectedRow == null || SelectedRow.RegisterRecordId <= 0)
            {
                return;
            }

            ViewRegisterDetailRequested?.Invoke(new ArchiveDetailOpenRequest(
                SelectedRow.RegisterRecordId,
                null,
                SelectedRow.MediaKind,
                SelectedRow.FilingFactId));
        }

        private string? ResolveSelectedArchiveYear()
        {
            return string.Equals(SelectedYear, AllYearsOption, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(SelectedYear)
                ? null
                : SelectedYear.Trim();
        }

        public sealed class FilterOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }
    }
}
