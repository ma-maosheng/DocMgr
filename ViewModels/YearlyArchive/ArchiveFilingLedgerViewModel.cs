using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DocMgr.Models.Shared;
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

        /// <summary>按当前容器编号折叠。</summary>
        public const string FoldFieldContainer = "Container";

        /// <summary>按申请表单编号折叠。</summary>
        public const string FoldFieldFormNo = "FormNo";

        /// <summary>按立档编号排列。一行一号，分组后不会收起任何行。</summary>
        public const string FoldFieldFilingFactNo = "FilingFactNo";

        private readonly IArchiveFilingLedgerService _ledgerService;
        private readonly IArchiveMaterialTransactionService _materialTransactionService;
        private readonly IDialogService _dialogService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PageDbOperationQueue _pageDb = new();

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
        private string _selectedFoldField = FoldFieldFormNo;
        private int? _selectedProjectId;
        private string _selectedMediaKind = string.Empty;
        private string _selectedFilingMethod = string.Empty;
        private string _selectedHandlingForm = string.Empty;
        private string _selectedLifecycleStatus = string.Empty;
        private string _selectedArchiveCopyRole = string.Empty;
        private string _keyword = string.Empty;
        private string _contentEntryKeyword = string.Empty;
        private string _appliedContentEntryKeyword = string.Empty;
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

            SearchCommand = new RelayCommand(async _ => await _pageDb.RunAsync(() => SearchAsync()), _ => !IsBusy);
            ResetCommand = new RelayCommand(_ => ResetCriteria(), _ => !IsBusy);
            RefreshCommand = new RelayCommand(async _ => await _pageDb.RunAsync(() => SearchAsync()), _ => !IsBusy);
            ExportCommand = new RelayCommand(async _ => await _pageDb.RunAsync(ExportAsync), _ => !IsBusy && _sourceRows.Count > 0);
            ViewRegisterDetailCommand = new RelayCommand(
                _ => ViewRegisterDetail(),
                _ => SelectedRow != null && SelectedRow.RegisterRecordId > 0);
            ToggleFoldAllCommand = new RelayCommand(_ => ToggleFoldAll(), _ => HasFoldableGroups);
            ToggleFoldGroupCommand = new RelayCommand<FilingLedgerRow>(
                ToggleFoldGroup,
                row => row is { ShowFoldButton: true });
            ShowIntroCommand = new RelayCommand(_ => _dialogService.ShowLedgerIntro(LedgerIntroKind.Filing));

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
                _pageDb.RunInBackground(LoadProjectOptionsAsync);
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

        public ObservableCollection<FilterOption> FilingMethodOptions { get; } =
        [
            new FilterOption { Label = "全部", Value = string.Empty },
            new FilterOption
            {
                Label = FilingLedgerFilingMethod.OffNetworkLabel,
                Value = FilingLedgerFilingMethod.OffNetwork
            },
            new FilterOption
            {
                Label = FilingLedgerFilingMethod.OnNetworkLabel,
                Value = FilingLedgerFilingMethod.OnNetwork
            }
        ];

        public string SelectedFilingMethod
        {
            get => _selectedFilingMethod;
            set => SetProperty(ref _selectedFilingMethod, value);
        }

        public ObservableCollection<FilterOption> HandlingFormOptions { get; } =
        [
            new FilterOption { Label = "全部", Value = string.Empty },
            new FilterOption
            {
                Label = FilingLedgerHandlingForm.StockDirect,
                Value = FilingLedgerHandlingForm.StockDirect
            },
            new FilterOption
            {
                Label = FilingLedgerHandlingForm.ApplicationApproval,
                Value = FilingLedgerHandlingForm.ApplicationApproval
            }
        ];

        public string SelectedHandlingForm
        {
            get => _selectedHandlingForm;
            set => SetProperty(ref _selectedHandlingForm, value);
        }

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
                    _pageDb.RunInBackground(LoadSelectedDetailAsync);
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

        public string ContentEntriesExpanderHeader
        {
            get
            {
                if (SelectedContentEntries.Count == 0)
                {
                    return "电子介质目录 / 文件明细";
                }

                int hitCount = SelectedContentEntries.Count(entry => entry.IsSearchHighlighted);
                return hitCount > 0
                    ? $"电子介质目录 / 文件明细（{SelectedContentEntries.Count}，命中 {hitCount}）"
                    : $"电子介质目录 / 文件明细（{SelectedContentEntries.Count}）";
            }
        }

        /// <summary>最近一次查询使用了「目录/文件」条件，明细表需要标出命中行。</summary>
        public bool HasContentEntryKeywordHighlight =>
            !string.IsNullOrWhiteSpace(_appliedContentEntryKeyword);

        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ViewRegisterDetailCommand { get; }
        public RelayCommand ToggleFoldAllCommand { get; }
        public RelayCommand<FilingLedgerRow> ToggleFoldGroupCommand { get; }
        public RelayCommand ShowIntroCommand { get; }

        /// <summary>列表折叠字段：容器编号、申请表单编号或立档编号。同一时间只按一个字段分组。</summary>
        public string SelectedFoldField
        {
            get => _selectedFoldField;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                string normalized = value.Trim();
                if (!IsKnownFoldField(normalized) || !SetProperty(ref _selectedFoldField, normalized))
                {
                    return;
                }

                NotifyFoldFieldBindings();
                if (_sourceRows.Count == 0)
                {
                    return;
                }

                _expandedFoldGroupKeys.Clear();
                List<FilingLedgerRow> ordered = OrderRowsForContiguousFoldGroups(_sourceRows, normalized);
                _sourceRows.Clear();
                _sourceRows.AddRange(ordered);
                int? selectedId = SelectedRow?.FilingFactId;
                AnnotateFoldGroups();
                EnsureFoldGroupVisible(selectedId);
                ApplyFoldDisplay(selectedId);
                UpdateSummary();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>当前按容器编号折叠。</summary>
        public bool IsFoldByContainer => string.Equals(_selectedFoldField, FoldFieldContainer, StringComparison.Ordinal);

        /// <summary>当前按申请表单编号折叠。</summary>
        public bool IsFoldByFormNo => string.Equals(_selectedFoldField, FoldFieldFormNo, StringComparison.Ordinal);

        /// <summary>当前按立档编号排列。</summary>
        public bool IsFoldByFilingFactNo => string.Equals(_selectedFoldField, FoldFieldFilingFactNo, StringComparison.Ordinal);

        /// <summary>列表标题旁的折叠说明。</summary>
        public string ListSectionHint =>
            "点击行可在下方查看完整立档信息；专属列随上方「介质」筛选显示（全部时仅共用列）。"
            + FoldModeHint;

        /// <summary>行内折叠按钮提示。</summary>
        public string FoldRowToolTip => _selectedFoldField switch
        {
            FoldFieldContainer => "按容器编号折叠/展开同容器其余记录",
            FoldFieldFilingFactNo => "按立档编号折叠/展开同号其余记录",
            _ => "按申请表单编号折叠/展开同单其余子项"
        };

        /// <summary>列表是否存在可折叠的同组多行。</summary>
        public bool HasFoldableGroups => _sourceRows.Any(row => row.ShowFoldButton);

        /// <summary>列头折叠按钮文案：默认折叠态为「展开」，全部展开后为「折叠」。</summary>
        public string FoldAllButtonText => AreAllFoldableGroupsExpanded ? "折叠" : "展开";

        public string FoldAllButtonToolTip => _selectedFoldField switch
        {
            FoldFieldFilingFactNo => "立档编号一行一号，没有可折叠的同组记录",
            FoldFieldContainer when AreAllFoldableGroupsExpanded => "按容器编号折叠：每个容器只保留第一行",
            FoldFieldContainer => "展开全部同容器记录",
            _ when AreAllFoldableGroupsExpanded => "按申请表单编号折叠：每个申请表单编号只保留第一行",
            _ => "展开全部同单子项"
        };

        private string FoldModeHint => _selectedFoldField switch
        {
            FoldFieldContainer => "按容器编号折叠，同一容器默认只显示第一行；编号为空的记录各自单独显示。",
            FoldFieldFilingFactNo => "按立档编号显示，一行一号，不折叠。",
            _ => "按申请表单编号折叠，同一申请单默认只显示第一行。"
        };

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
                string foldField = _selectedFoldField;
                var expandedSnapshot = new HashSet<string>(_expandedFoldGroupKeys, StringComparer.Ordinal);
                var orderedRows = await Task.Run(() =>
                {
                    // 同一折叠字段的行先聚拢再标注，避免展开后被其它组打断。
                    var contiguous = OrderRowsForContiguousFoldGroups(rows, foldField);
                    AnnotateFoldGroups(contiguous, expandedSnapshot, foldField);
                    return contiguous;
                }).ConfigureAwait(true);
                if (!string.Equals(foldField, _selectedFoldField, StringComparison.Ordinal))
                {
                    foldField = _selectedFoldField;
                    expandedSnapshot = new HashSet<string>(_expandedFoldGroupKeys, StringComparer.Ordinal);
                    orderedRows = OrderRowsForContiguousFoldGroups(rows, foldField);
                    AnnotateFoldGroups(orderedRows, expandedSnapshot, foldField);
                }

                _sourceRows.Clear();
                _sourceRows.AddRange(orderedRows);
                SetAppliedContentEntryKeyword(criteria.ContentEntryKeyword);
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
        /// 按当前折叠字段分组：同组默认只展示第一行。
        /// 立档编号一行一号，该模式下不会产生可折叠组。
        /// </summary>
        private void AnnotateFoldGroups()
        {
            AnnotateFoldGroups(_sourceRows, _expandedFoldGroupKeys, _selectedFoldField);
            OnPropertyChanged(nameof(HasFoldableGroups));
            OnPropertyChanged(nameof(FoldAllButtonText));
            OnPropertyChanged(nameof(FoldAllButtonToolTip));
        }

        /// <summary>
        /// 将同一折叠键的行聚拢为连续段。
        /// 组间按组内最新立档时间降序；组内按立档时间、立档事实 Id 降序。
        /// </summary>
        private static List<FilingLedgerRow> OrderRowsForContiguousFoldGroups(
            IReadOnlyList<FilingLedgerRow> rows,
            string foldField)
        {
            if (rows.Count <= 1)
            {
                return rows.ToList();
            }

            return rows
                .Select(row => (Row: row, Key: ResolveFoldGroupKey(row, foldField)))
                .GroupBy(item => item.Key, StringComparer.Ordinal)
                .OrderByDescending(group => group.Max(item => item.Row.FiledAt))
                .ThenByDescending(group => group.Max(item => item.Row.FilingFactId))
                .SelectMany(group => group
                    .OrderByDescending(item => item.Row.FiledAt)
                    .ThenByDescending(item => item.Row.FilingFactId)
                    .Select(item => item.Row))
                .ToList();
        }

        private static void AnnotateFoldGroups(
            IReadOnlyList<FilingLedgerRow> rows,
            HashSet<string> expandedKeys,
            string foldField)
        {
            foreach (var row in rows)
            {
                row.FoldGroupKey = ResolveFoldGroupKey(row, foldField);
                row.IsFoldGroupLeader = false;
                row.ShowFoldButton = false;
                row.FoldButtonText = string.Empty;
                ApplyFoldChrome(row, foldField, showButton: false, reserveIndent: false);
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
                ApplyFoldChrome(leader, foldField, showButton: true, reserveIndent: false);
                for (int i = 1; i < members.Count; i++)
                {
                    ApplyFoldChrome(members[i], foldField, showButton: false, reserveIndent: true);
                }
            }
        }

        /// <summary>
        /// 分组键带字段前缀。容器编号或申请表单编号为空时按立档事实单独成组，避免无关行折在一起。
        /// </summary>
        private static string ResolveFoldGroupKey(FilingLedgerRow row, string foldField)
        {
            if (string.Equals(foldField, FoldFieldContainer, StringComparison.Ordinal))
            {
                string containerCode = row.CurrentContainerCode?.Trim() ?? string.Empty;
                return string.IsNullOrEmpty(containerCode)
                    ? "container-id:" + row.FilingFactId.ToString()
                    : "container:" + containerCode;
            }

            if (string.Equals(foldField, FoldFieldFilingFactNo, StringComparison.Ordinal))
            {
                string filingFactNo = row.FilingFactNo?.Trim() ?? string.Empty;
                return string.IsNullOrEmpty(filingFactNo)
                    ? "fact-id:" + row.FilingFactId.ToString()
                    : "fact:" + filingFactNo;
            }

            string formNo = row.FormNo?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(formNo)
                ? "form-id:" + row.FilingFactId.ToString()
                : "form:" + formNo;
        }

        private static void ApplyFoldChrome(FilingLedgerRow row, string foldField, bool showButton, bool reserveIndent)
        {
            row.ShowContainerFoldButton = false;
            row.ShowFormFoldButton = false;
            row.ShowFilingFactFoldButton = false;
            row.ReserveContainerFoldIndent = false;
            row.ReserveFormFoldIndent = false;
            row.ReserveFilingFactFoldIndent = false;

            if (string.Equals(foldField, FoldFieldContainer, StringComparison.Ordinal))
            {
                row.ShowContainerFoldButton = showButton;
                row.ReserveContainerFoldIndent = reserveIndent;
                return;
            }

            if (string.Equals(foldField, FoldFieldFilingFactNo, StringComparison.Ordinal))
            {
                row.ShowFilingFactFoldButton = showButton;
                row.ReserveFilingFactFoldIndent = reserveIndent;
                return;
            }

            row.ShowFormFoldButton = showButton;
            row.ReserveFormFoldIndent = reserveIndent;
        }

        private static bool IsKnownFoldField(string foldField)
            => string.Equals(foldField, FoldFieldContainer, StringComparison.Ordinal)
               || string.Equals(foldField, FoldFieldFormNo, StringComparison.Ordinal)
               || string.Equals(foldField, FoldFieldFilingFactNo, StringComparison.Ordinal);

        private void NotifyFoldFieldBindings()
        {
            OnPropertyChanged(nameof(IsFoldByContainer));
            OnPropertyChanged(nameof(IsFoldByFormNo));
            OnPropertyChanged(nameof(IsFoldByFilingFactNo));
            OnPropertyChanged(nameof(ListSectionHint));
            OnPropertyChanged(nameof(FoldRowToolTip));
            OnPropertyChanged(nameof(FoldAllButtonToolTip));
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

            _pageDb.RunInBackground(LoadSelectedDetailAsync);
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
                FilingMethod = SelectedFilingMethod?.Trim() ?? string.Empty,
                HandlingForm = SelectedHandlingForm?.Trim() ?? string.Empty,
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

            // 独立 scope：快速切换选中行时旧请求可能未结束，不可与页面级 DbContext 叠查。
            using IServiceScope scope = _scopeFactory.CreateScope();
            IArchiveMaterialTransactionService materialTransactionService =
                scope.ServiceProvider.GetRequiredService<IArchiveMaterialTransactionService>();
            IArchiveFilingLedgerService ledgerService =
                scope.ServiceProvider.GetRequiredService<IArchiveFilingLedgerService>();

            try
            {
                var transactions = await materialTransactionService
                    .GetTimelineByFilingFactIdAsync(row.FilingFactId)
                    .ConfigureAwait(true);
                if (version != Volatile.Read(ref _detailLoadVersion))
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
                if (version == Volatile.Read(ref _detailLoadVersion))
                {
                    _dialogService.ShowError($"加载流转履历失败：{ex.GetBaseException().Message}");
                }
            }

            if (version != Volatile.Read(ref _detailLoadVersion))
            {
                return;
            }

            try
            {
                var processNodes = await materialTransactionService
                    .GetOutboundProcessNodesByFilingFactIdAsync(row.FilingFactId)
                    .ConfigureAwait(true);
                if (version != Volatile.Read(ref _detailLoadVersion))
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
                if (version == Volatile.Read(ref _detailLoadVersion))
                {
                    _dialogService.ShowError($"加载出库流程节点失败：{ex.GetBaseException().Message}");
                }
            }

            if (version != Volatile.Read(ref _detailLoadVersion))
            {
                return;
            }

            if (!row.IsElectronicMedia || row.MediaItemId <= 0)
            {
                return;
            }

            try
            {
                var entries = await ledgerService.GetContentEntriesByMediaItemIdAsync(
                    row.MediaItemId,
                    row.FilingStoragePath).ConfigureAwait(true);
                if (version != Volatile.Read(ref _detailLoadVersion))
                {
                    return;
                }

                string appliedKeyword = _appliedContentEntryKeyword;
                foreach (var entry in entries
                    .Select(entry =>
                    {
                        entry.IsSearchHighlighted = ContentEntrySearchSupport.MatchesEntryNameKeyword(
                            entry.EntryName,
                            appliedKeyword);
                        return entry;
                    })
                    .OrderByDescending(entry => entry.IsSearchHighlighted))
                {
                    SelectedContentEntries.Add(entry);
                }

                if (SelectedContentEntries.Any(entry => entry.IsSearchHighlighted))
                {
                    IsContentEntriesExpanded = true;
                }

                ContentEntriesPanel.RefreshItems(SelectedContentEntries, preserveExpanded: ContentEntriesPanel.IsExpanded);
                OnPropertyChanged(nameof(HasSelectedContentEntries));
                OnPropertyChanged(nameof(ContentEntriesExpanderHeader));
            }
            catch (Exception ex)
            {
                if (version == Volatile.Read(ref _detailLoadVersion))
                {
                    _dialogService.ShowError($"加载目录/文件明细失败：{ex.GetBaseException().Message}");
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

        private void SetAppliedContentEntryKeyword(string? keyword)
        {
            string normalized = keyword?.Trim() ?? string.Empty;
            if (string.Equals(_appliedContentEntryKeyword, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _appliedContentEntryKeyword = normalized;
            OnPropertyChanged(nameof(HasContentEntryKeywordHighlight));
        }

        private void ResetCriteria()
        {
            SelectedYear = DateTime.Now.Year.ToString();
            SelectedProjectId = null;
            SelectedMediaKind = string.Empty;
            SelectedFilingMethod = string.Empty;
            SelectedHandlingForm = string.Empty;
            SelectedLifecycleStatus = string.Empty;
            SelectedArchiveCopyRole = string.Empty;
            Keyword = string.Empty;
            ContentEntryKeyword = string.Empty;
            SetAppliedContentEntryKeyword(string.Empty);
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
