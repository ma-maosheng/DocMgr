using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;

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
        private readonly IServiceScopeFactory _scopeFactory;

        private bool _isInitialized;
        private bool _suppressYearSideEffects;
        private bool _suppressSelectionLoad;
        private int _busyDepth;
        private int _detailLoadVersion;
        private bool _isBusy;
        private string _busyStatus = string.Empty;
        private string _selectedYear = DateTime.Now.Year.ToString();
        private readonly List<FilingLedgerRow> _sourceRows = new();
        private readonly HashSet<string> _expandedFoldGroupKeys = new(StringComparer.Ordinal);
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
            IDialogService dialogService,
            IServiceScopeFactory scopeFactory)
        {
            _ledgerService = ledgerService;
            _materialTransactionService = materialTransactionService;
            _dialogService = dialogService;
            _scopeFactory = scopeFactory;

            SearchCommand = new RelayCommand(async _ => await SearchAsync(), _ => !IsBusy);
            ResetCommand = new RelayCommand(_ => ResetCriteria(), _ => !IsBusy);
            RefreshCommand = new RelayCommand(async _ => await SearchAsync(), _ => !IsBusy);
            ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => !IsBusy && _sourceRows.Count > 0);
            ViewRegisterDetailCommand = new RelayCommand(
                _ => ViewRegisterDetail(),
                _ => SelectedRow != null && SelectedRow.RegisterRecordId > 0);
            ToggleFoldAllCommand = new RelayCommand(_ => ToggleFoldAll(), _ => HasFoldableGroups);
            ToggleFoldGroupCommand = new RelayCommand<FilingLedgerRow>(
                ToggleFoldGroup,
                row => row is { ShowFoldButton: true });

            ContentEntriesPanel = new ItemDetailsListPresenter<FilingLedgerContentEntryInfo>(
                "电子介质目录 / 文件明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.EntryName,
                    "暂无目录/文件明细"));
            ContentEntriesPanel.RefreshItems(SelectedContentEntries);
        }

        public event Action<ArchiveDetailOpenRequest>? ViewRegisterDetailRequested;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string BusyStatus
        {
            get => _busyStatus;
            private set => SetProperty(ref _busyStatus, value);
        }

        public ObservableCollection<string> Years { get; } = new()
        {
            AllYearsOption,
            DateTime.Now.Year.ToString()
        };

        public string SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (!SetProperty(ref _selectedYear, value))
                {
                    return;
                }

                if (_suppressYearSideEffects)
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
                if (!_suppressSelectionLoad)
                {
                    _ = LoadSelectedDetailAsync();
                }
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
        public RelayCommand ToggleFoldAllCommand { get; }
        public RelayCommand<FilingLedgerRow> ToggleFoldGroupCommand { get; }

        /// <summary>列表是否存在可折叠的同单多子项。</summary>
        public bool HasFoldableGroups => _sourceRows.Any(row => row.ShowFoldButton);

        /// <summary>列头折叠按钮文案：默认折叠态为「展开」，全部展开后为「折叠」。</summary>
        public string FoldAllButtonText => AreAllFoldableGroupsExpanded ? "折叠" : "展开";

        public string FoldAllButtonToolTip => AreAllFoldableGroupsExpanded
            ? "按建档表单号折叠：每个表单号只保留第一行"
            : "展开全部同单子项";

        private bool AreAllFoldableGroupsExpanded
        {
            get
            {
                var foldableKeys = _sourceRows
                    .Where(row => row.ShowFoldButton)
                    .Select(row => row.FoldGroupKey)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                return foldableKeys.Count > 0
                    && foldableKeys.All(key => _expandedFoldGroupKeys.Contains(key));
            }
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await WithBusyAsync("正在加载立档台账…", async () =>
            {
                await ReportBusyAsync("正在加载档案年度…");
                await LoadYearsAsync();
                await ReportBusyAsync("正在加载项目列表…");
                await LoadProjectOptionsAsync();
                await ReportBusyAsync("正在查询立档记录…");
                await SearchCoreAsync();
            });
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
                await SearchAsync(filingFactId);
                SelectedRow = LedgerRows.FirstOrDefault(row => row.FilingFactId == filingFactId)
                    ?? _sourceRows.FirstOrDefault(row => row.FilingFactId == filingFactId)
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
                var yearsList = await QueryOnBackgroundAsync(service => service.GetExistingLedgerYearsAsync());
                _suppressYearSideEffects = true;
                try
                {
                    Years.Clear();
                    Years.Add(AllYearsOption);
                    foreach (int year in yearsList)
                    {
                        Years.Add(year.ToString());
                    }

                    string currentYearText = DateTime.Now.Year.ToString();
                    if (Years.Contains(currentYearText))
                    {
                        _selectedYear = currentYearText;
                    }
                    else if (!Years.Contains(_selectedYear))
                    {
                        _selectedYear = AllYearsOption;
                    }

                    OnPropertyChanged(nameof(SelectedYear));
                }
                finally
                {
                    _suppressYearSideEffects = false;
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

                string? archiveYear = ResolveSelectedArchiveYear();
                var projects = await QueryOnBackgroundAsync(service => service.GetProjectOptionsForYearAsync(archiveYear));
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

        private async Task SearchAsync(int? focusFilingFactId = null)
        {
            await WithBusyAsync("正在查询立档记录…", () => SearchCoreAsync(focusFilingFactId));
        }

        private async Task SearchCoreAsync(int? focusFilingFactId = null)
        {
            try
            {
                int? selectedId = focusFilingFactId ?? SelectedRow?.FilingFactId;
                var criteria = BuildCriteria();
                await ReportBusyAsync("正在查询立档记录…");
                var rows = await QueryOnBackgroundAsync(service => service.SearchAsync(criteria));

                await ReportBusyAsync("正在整理折叠列表…");
                var expandedSnapshot = new HashSet<string>(_expandedFoldGroupKeys, StringComparer.Ordinal);
                await Task.Run(() => AnnotateFoldGroups(rows, expandedSnapshot)).ConfigureAwait(true);

                _sourceRows.Clear();
                _sourceRows.AddRange(rows);
                EnsureFoldGroupVisible(selectedId);
                ApplyFoldDisplay(selectedId);
                UpdateSummary();
                OnPropertyChanged(nameof(HasFoldableGroups));
                OnPropertyChanged(nameof(FoldAllButtonText));
                OnPropertyChanged(nameof(FoldAllButtonToolTip));
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查询立档台账失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 按建档表单号分组：同单一组，默认只展示组内第一行。
        /// 立档编号本身一条一号，同单多子项才需要折叠。
        /// </summary>
        private void AnnotateFoldGroups()
        {
            AnnotateFoldGroups(_sourceRows, _expandedFoldGroupKeys);
            OnPropertyChanged(nameof(HasFoldableGroups));
            OnPropertyChanged(nameof(FoldAllButtonText));
            OnPropertyChanged(nameof(FoldAllButtonToolTip));
        }

        private static void AnnotateFoldGroups(IReadOnlyList<FilingLedgerRow> rows, HashSet<string> expandedKeys)
        {
            foreach (var row in rows)
            {
                row.FoldGroupKey = ResolveFoldGroupKey(row);
                row.IsFoldGroupLeader = false;
                row.ShowFoldButton = false;
                row.FoldButtonText = string.Empty;
            }

            foreach (var group in rows.GroupBy(row => row.FoldGroupKey, StringComparer.Ordinal))
            {
                var members = group.ToList();
                var leader = members[0];
                leader.IsFoldGroupLeader = true;
                if (members.Count <= 1)
                {
                    continue;
                }

                leader.ShowFoldButton = true;
                bool expanded = expandedKeys.Contains(leader.FoldGroupKey);
                leader.FoldButtonText = expanded
                    ? "折叠"
                    : $"展开({members.Count - 1})";
            }
        }

        private static string ResolveFoldGroupKey(FilingLedgerRow row)
        {
            string formNo = row.FormNo?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(formNo))
            {
                return formNo;
            }

            return "id:" + row.FilingFactId.ToString();
        }

        private void EnsureFoldGroupVisible(int? filingFactId)
        {
            if (filingFactId is not int id || id <= 0)
            {
                return;
            }

            var row = _sourceRows.FirstOrDefault(item => item.FilingFactId == id);
            if (row == null || row.IsFoldGroupLeader)
            {
                return;
            }

            _expandedFoldGroupKeys.Add(row.FoldGroupKey);
            AnnotateFoldGroups();
        }

        private void ApplyFoldDisplay(int? preferredFilingFactId)
        {
            _suppressSelectionLoad = true;
            try
            {
                LedgerRows.Clear();
                foreach (var row in _sourceRows)
                {
                    if (row.IsFoldGroupLeader || _expandedFoldGroupKeys.Contains(row.FoldGroupKey))
                    {
                        LedgerRows.Add(row);
                    }
                }

                SelectedRow = preferredFilingFactId.HasValue
                    ? LedgerRows.FirstOrDefault(row => row.FilingFactId == preferredFilingFactId.Value)
                        ?? LedgerRows.FirstOrDefault()
                    : LedgerRows.FirstOrDefault();
            }
            finally
            {
                _suppressSelectionLoad = false;
            }

            _ = LoadSelectedDetailAsync();
        }

        private void ToggleFoldGroup(FilingLedgerRow? row)
        {
            if (row == null || !row.ShowFoldButton || string.IsNullOrWhiteSpace(row.FoldGroupKey))
            {
                return;
            }

            int? selectedId = SelectedRow?.FilingFactId;
            if (!_expandedFoldGroupKeys.Add(row.FoldGroupKey))
            {
                _expandedFoldGroupKeys.Remove(row.FoldGroupKey);
            }

            AnnotateFoldGroups();
            ApplyFoldDisplay(selectedId);
            UpdateSummary();
            CommandManager.InvalidateRequerySuggested();
        }

        private void ToggleFoldAll()
        {
            int? selectedId = SelectedRow?.FilingFactId;
            if (AreAllFoldableGroupsExpanded)
            {
                _expandedFoldGroupKeys.Clear();
            }
            else
            {
                foreach (var key in _sourceRows.Where(row => row.ShowFoldButton).Select(row => row.FoldGroupKey))
                {
                    _expandedFoldGroupKeys.Add(key);
                }
            }

            AnnotateFoldGroups();
            ApplyFoldDisplay(selectedId);
            UpdateSummary();
            CommandManager.InvalidateRequerySuggested();
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

        private void UpdateSummary()
        {
            int simulatedCount = _sourceRows.Count(row => string.Equals(
                row.MediaKind,
                ArchiveRegisterDomainValues.MediaKindSimulated,
                StringComparison.Ordinal));
            int electronicCount = _sourceRows.Count(row => string.Equals(
                row.MediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal));
            int backupCount = _sourceRows.Count(row => string.Equals(
                row.ArchiveCopyRole,
                FilingFactArchiveCopyRole.Backup,
                StringComparison.Ordinal));

            string summary = $"共 {_sourceRows.Count} 条（模拟 {simulatedCount} / 电子 {electronicCount}，备份 {backupCount}）";
            if (LedgerRows.Count < _sourceRows.Count)
            {
                summary += $"；折叠后显示 {LedgerRows.Count} 行";
            }

            SummaryText = summary;
        }

        private async Task LoadSelectedDetailAsync()
        {
            int version = ++_detailLoadVersion;
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
                if (version != _detailLoadVersion)
                {
                    return;
                }

                foreach (var transaction in transactions)
                {
                    SelectedMaterialTransactions.Add(transaction);
                }

                OnPropertyChanged(nameof(HasSelectedMaterialTransactions));
                OnPropertyChanged(nameof(MaterialTransactionsExpanderHeader));
            }
            catch (Exception ex)
            {
                if (version == _detailLoadVersion)
                {
                    _dialogService.ShowError($"加载流转履历失败：{ex.Message}");
                }
            }

            if (version != _detailLoadVersion)
            {
                return;
            }

            try
            {
                var processNodes = await _materialTransactionService.GetOutboundProcessNodesByFilingFactIdAsync(row.FilingFactId);
                if (version != _detailLoadVersion)
                {
                    return;
                }

                foreach (var node in processNodes)
                {
                    SelectedOutboundProcessNodes.Add(node);
                }

                OnPropertyChanged(nameof(HasSelectedOutboundProcessNodes));
                OnPropertyChanged(nameof(OutboundProcessNodesExpanderHeader));
            }
            catch (Exception ex)
            {
                if (version == _detailLoadVersion)
                {
                    _dialogService.ShowError($"加载出库流程节点失败：{ex.Message}");
                }
            }

            if (version != _detailLoadVersion)
            {
                return;
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
                if (version != _detailLoadVersion)
                {
                    return;
                }

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
                if (version == _detailLoadVersion)
                {
                    _dialogService.ShowError($"加载目录/文件明细失败：{ex.Message}");
                }
            }
        }

        private async Task WithBusyAsync(string status, Func<Task> action)
        {
            _busyDepth++;
            BusyStatus = status;
            IsBusy = true;
            try
            {
                await PumpUiAsync();
                await action();
            }
            finally
            {
                _busyDepth--;
                if (_busyDepth <= 0)
                {
                    _busyDepth = 0;
                    IsBusy = false;
                    BusyStatus = string.Empty;
                }
            }
        }

        private async Task ReportBusyAsync(string status)
        {
            BusyStatus = status;
            await PumpUiAsync();
        }

        private static async Task PumpUiAsync()
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                await Task.Delay(16);
                return;
            }

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        }

        private Task<T> QueryOnBackgroundAsync<T>(Func<IArchiveFilingLedgerService, Task<T>> query)
        {
            return Task.Run(() =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IArchiveFilingLedgerService service = scope.ServiceProvider.GetRequiredService<IArchiveFilingLedgerService>();
                return query(service).ConfigureAwait(false).GetAwaiter().GetResult();
            });
        }

        private void ResetCriteria()
        {
            SelectedYear = DateTime.Now.Year.ToString();
            SelectedProjectId = null;
            SelectedMediaKind = string.Empty;
            SelectedLifecycleStatus = string.Empty;
            SelectedArchiveCopyRole = string.Empty;
            Keyword = string.Empty;
            ContentEntryKeyword = string.Empty;
            FiledFrom = null;
            FiledTo = null;
            _expandedFoldGroupKeys.Clear();
            _sourceRows.Clear();
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
            OnPropertyChanged(nameof(HasFoldableGroups));
            OnPropertyChanged(nameof(FoldAllButtonText));
            OnPropertyChanged(nameof(FoldAllButtonToolTip));
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
                await _ledgerService.ExportAsync(filePath, _sourceRows.ToList());
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
