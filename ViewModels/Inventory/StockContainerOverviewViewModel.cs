using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocMgr.Models.Inventory;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Inventory
{
    /// <summary>
    /// 库管资料总览：按档案盒 / 电子介质袋 / 历史存档盒查询、下钻与导出。
    /// </summary>
    public sealed class StockContainerOverviewViewModel : ViewModelBase
    {
        private const string AllYearsOption = "全部年度";

        private readonly IStockContainerOverviewService _overviewService;
        private readonly IDialogService _dialogService;

        private bool _isInitialized;
        private bool _isBusy;
        private string _busyStatus = string.Empty;
        private string _selectedYear = AllYearsOption;
        private string _keyword = string.Empty;
        private bool _keywordCaseSensitive;
        private bool _includeElectronicEntryNames;
        private string _storageLocationKeyword = string.Empty;
        private string _selectedLifecycleFilter = string.Empty;
        private bool _includeYearlyBox = true;
        private bool _includeElectronicBag = true;
        private bool _includeHistoryBox = true;
        private bool _includeInactive;
        private bool _exportIncludeContents = true;
        private DateTime? _archivedFrom;
        private DateTime? _archivedTo;
        private StockContainerOverviewRow? _selectedRow;
        private string _summaryText = "共 0 个容器";
        private string _contentSummaryText = "选择容器后显示内容明细";
        private readonly List<StockContainerOverviewRow> _sourceRows = new();

        public StockContainerOverviewViewModel(
            IStockContainerOverviewService overviewService,
            IDialogService dialogService)
        {
            _overviewService = overviewService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync(), _ => !IsBusy);
            ResetCommand = new RelayCommand(_ => ResetCriteria(), _ => !IsBusy);
            RefreshCommand = new RelayCommand(async _ => await SearchAsync(), _ => !IsBusy);
            ExportCommand = new RelayCommand(
                async _ => await ExportAsync(),
                _ => !IsBusy && _sourceRows.Count > 0);
            OpenFilingLedgerCommand = new RelayCommand(
                _ => OpenFilingLedger(),
                _ => SelectedContentItem is { SourceEntity: StockContainerContentSourceEntity.FilingFact, SourceId: > 0 });
            ViewElectronicEntriesCommand = new RelayCommand(
                async _ => await ViewElectronicEntriesAsync(),
                _ => !IsBusy && SelectedContentItem is { IsElectronicMedia: true });
        }

        public event Action<int>? OpenFilingLedgerRequested;

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
            AllYearsOption
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

                SetProperty(ref _selectedYear, value);
            }
        }

        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value ?? string.Empty);
        }

        /// <summary>勾选后关键词区分大小写；默认不勾选（忽略大小写）。</summary>
        public bool KeywordCaseSensitive
        {
            get => _keywordCaseSensitive;
            set => SetProperty(ref _keywordCaseSensitive, value);
        }

        /// <summary>
        /// 勾选后关键词额外匹配电子介质目录/文件明细名称；默认不勾选。
        /// </summary>
        public bool IncludeElectronicEntryNames
        {
            get => _includeElectronicEntryNames;
            set => SetProperty(ref _includeElectronicEntryNames, value);
        }

        public string StorageLocationKeyword
        {
            get => _storageLocationKeyword;
            set => SetProperty(ref _storageLocationKeyword, value ?? string.Empty);
        }

        public ObservableCollection<FilterOption> LifecycleFilterOptions { get; } =
        [
            new FilterOption { Label = "全部状态", Value = string.Empty },
            new FilterOption { Label = "在柜在用", Value = StockContainerLifecycleDisplay.FilterActive },
            new FilterOption { Label = "含借出内容", Value = StockContainerLifecycleDisplay.FilterHasBorrowed },
            new FilterOption { Label = "已停用/已离库", Value = StockContainerLifecycleDisplay.FilterInactive }
        ];

        public string SelectedLifecycleFilter
        {
            get => _selectedLifecycleFilter;
            set => SetProperty(ref _selectedLifecycleFilter, value ?? string.Empty);
        }

        public bool IncludeYearlyBox
        {
            get => _includeYearlyBox;
            set => SetProperty(ref _includeYearlyBox, value);
        }

        public bool IncludeElectronicBag
        {
            get => _includeElectronicBag;
            set => SetProperty(ref _includeElectronicBag, value);
        }

        public bool IncludeHistoryBox
        {
            get => _includeHistoryBox;
            set => SetProperty(ref _includeHistoryBox, value);
        }

        public bool IncludeInactive
        {
            get => _includeInactive;
            set => SetProperty(ref _includeInactive, value);
        }

        public bool ExportIncludeContents
        {
            get => _exportIncludeContents;
            set => SetProperty(ref _exportIncludeContents, value);
        }

        public DateTime? ArchivedFrom
        {
            get => _archivedFrom;
            set => SetProperty(ref _archivedFrom, value);
        }

        public DateTime? ArchivedTo
        {
            get => _archivedTo;
            set => SetProperty(ref _archivedTo, value);
        }

        public ObservableCollection<StockContainerOverviewRow> ContainerRows { get; } = new();

        public ObservableCollection<StockContainerContentItem> ContentItems { get; } = new();

        public StockContainerOverviewRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (!SetProperty(ref _selectedRow, value))
                {
                    return;
                }

                _ = LoadContentsAsync(value);
            }
        }

        private StockContainerContentItem? _selectedContentItem;

        public StockContainerContentItem? SelectedContentItem
        {
            get => _selectedContentItem;
            set
            {
                if (SetProperty(ref _selectedContentItem, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        public string ContentSummaryText
        {
            get => _contentSummaryText;
            private set => SetProperty(ref _contentSummaryText, value);
        }

        public ICommand SearchCommand { get; }

        public ICommand ResetCommand { get; }

        public ICommand RefreshCommand { get; }

        public ICommand ExportCommand { get; }

        public ICommand OpenFilingLedgerCommand { get; }

        public ICommand ViewElectronicEntriesCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            await LoadYearsAsync();
            await SearchAsync();
        }

        private async Task LoadYearsAsync()
        {
            try
            {
                var years = await _overviewService.GetDistinctYearsAsync();
                Years.Clear();
                Years.Add(AllYearsOption);
                foreach (int year in years)
                {
                    Years.Add(year.ToString());
                }

                if (!Years.Contains(SelectedYear))
                {
                    SelectedYear = AllYearsOption;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载年度选项失败：{ex.Message}");
            }
        }

        private async Task SearchAsync()
        {
            if (!IncludeYearlyBox && !IncludeElectronicBag && !IncludeHistoryBox)
            {
                _dialogService.ShowMessage("请至少勾选一种容器类型。", "提示");
                return;
            }

            IsBusy = true;
            BusyStatus = "正在查询库管容器…";
            try
            {
                var criteria = BuildCriteria();
                var rows = await _overviewService.SearchContainersAsync(criteria);

                _sourceRows.Clear();
                _sourceRows.AddRange(rows);

                ContainerRows.Clear();
                foreach (var row in rows)
                {
                    ContainerRows.Add(row);
                }

                SummaryText = BuildSummaryText(rows);
                SelectedRow = null;
                ContentItems.Clear();
                ContentSummaryText = rows.Count == 0
                    ? "无匹配容器"
                    : "选择容器后显示内容明细";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查询失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                BusyStatus = string.Empty;
            }
        }

        private async Task LoadContentsAsync(StockContainerOverviewRow? row)
        {
            ContentItems.Clear();
            SelectedContentItem = null;

            if (row == null)
            {
                ContentSummaryText = "选择容器后显示内容明细";
                return;
            }

            IsBusy = true;
            BusyStatus = "正在加载容器内容…";
            try
            {
                var items = await _overviewService.GetContentsAsync(row.ContainerKind, row.ContainerId);
                foreach (var item in items)
                {
                    ContentItems.Add(item);
                }

                string containerLabel = ResolveContainerLabel(row);
                ContentSummaryText = items.Count == 0
                    ? $"容器 {containerLabel}：暂无挂载内容"
                    : $"容器 {containerLabel}：共 {items.Count} 条内容";
            }
            catch (Exception ex)
            {
                ContentSummaryText = "加载内容失败";
                _dialogService.ShowError($"加载容器内容失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                BusyStatus = string.Empty;
            }
        }

        private async Task ExportAsync()
        {
            if (_sourceRows.Count == 0)
            {
                return;
            }

            string yearLabel = string.Equals(SelectedYear, AllYearsOption, StringComparison.Ordinal)
                ? AllYearsOption
                : SelectedYear;
            string defaultFileName = $"库管资料总览_{yearLabel}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            string? filePath = _dialogService.SaveFileDialog(
                "Excel Files|*.xlsx",
                "导出库管资料总览",
                defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _dialogService.SetBusyState(true);
            IsBusy = true;
            BusyStatus = ExportIncludeContents ? "正在导出容器与明细…" : "正在导出容器列表…";
            try
            {
                await _overviewService.ExportAsync(filePath, _sourceRows.ToList(), ExportIncludeContents);
                _dialogService.ShowMessage($"库管资料总览导出完成：\n{filePath}", "完成");
            }
            catch (ArgumentException ex)
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
            catch (Exception ex)
            {
                _dialogService.ShowError($"导出失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                BusyStatus = string.Empty;
                _dialogService.SetBusyState(false);
            }
        }

        private void OpenFilingLedger()
        {
            if (SelectedContentItem == null
                || !string.Equals(
                    SelectedContentItem.SourceEntity,
                    StockContainerContentSourceEntity.FilingFact,
                    StringComparison.Ordinal)
                || SelectedContentItem.SourceId <= 0)
            {
                return;
            }

            OpenFilingLedgerRequested?.Invoke(SelectedContentItem.SourceId);
        }

        private async Task ViewElectronicEntriesAsync()
        {
            if (SelectedContentItem is not { IsElectronicMedia: true } item)
            {
                return;
            }

            IsBusy = true;
            BusyStatus = "正在加载目录/文件明细…";
            try
            {
                var entries = await _overviewService.GetElectronicContentEntriesAsync(item.MediaItemId);
                if (entries.Count == 0)
                {
                    _dialogService.ShowMessage("该电子资料子项暂无目录/文件明细。", "提示");
                    return;
                }

                string titleBase = PreferTitle(item.ItemName, item.Title);
                string title = string.IsNullOrWhiteSpace(titleBase)
                    ? "目录/文件明细"
                    : $"{titleBase} - 目录/文件明细";

                decimal totalSizeMb = entries.Sum(entry => entry.SizeMb ?? 0m);
                int directoryCount = entries.Count(entry =>
                    string.Equals(
                        entry.EntryKind,
                        ArchiveRegisterDomainValues.ElectronicEntryKindDirectory,
                        StringComparison.Ordinal));
                int fileCount = entries.Count(entry =>
                    string.Equals(
                        entry.EntryKind,
                        ArchiveRegisterDomainValues.ElectronicEntryKindFile,
                        StringComparison.Ordinal));
                string summary = ElectronicMediaItemSupport.BuildContentScanSummary(
                    ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                    entries.Count,
                    directoryCount,
                    fileCount,
                    nestedFileCount: 0,
                    totalSizeMb);

                _dialogService.ShowElectronicMediaItemEntriesDialog(title, entries, summary);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载目录/文件明细失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                BusyStatus = string.Empty;
            }
        }

        private static string PreferTitle(string? primary, string? fallback)
        {
            if (!string.IsNullOrWhiteSpace(primary))
            {
                return primary.Trim();
            }

            return fallback?.Trim() ?? string.Empty;
        }

        private void ResetCriteria()
        {
            SelectedYear = AllYearsOption;
            Keyword = string.Empty;
            KeywordCaseSensitive = false;
            IncludeElectronicEntryNames = false;
            StorageLocationKeyword = string.Empty;
            SelectedLifecycleFilter = string.Empty;
            IncludeYearlyBox = true;
            IncludeElectronicBag = true;
            IncludeHistoryBox = true;
            IncludeInactive = false;
            ArchivedFrom = null;
            ArchivedTo = null;
        }

        private StockContainerSearchCriteria BuildCriteria()
        {
            var kinds = new List<string>();
            if (IncludeYearlyBox)
            {
                kinds.Add(StockContainerKind.YearlyBox);
            }

            if (IncludeElectronicBag)
            {
                kinds.Add(StockContainerKind.ElectronicBag);
            }

            if (IncludeHistoryBox)
            {
                kinds.Add(StockContainerKind.HistoryBox);
            }

            return new StockContainerSearchCriteria
            {
                ContainerKinds = kinds,
                Year = string.Equals(SelectedYear, AllYearsOption, StringComparison.Ordinal)
                    ? null
                    : SelectedYear,
                Keyword = Keyword,
                KeywordCaseSensitive = KeywordCaseSensitive,
                IncludeElectronicEntryNames = IncludeElectronicEntryNames,
                StorageLocationKeyword = StorageLocationKeyword,
                LifecycleFilter = SelectedLifecycleFilter,
                IncludeInactive = IncludeInactive
                    || string.Equals(
                        SelectedLifecycleFilter,
                        StockContainerLifecycleDisplay.FilterInactive,
                        StringComparison.Ordinal),
                ArchivedFrom = ArchivedFrom,
                ArchivedTo = ArchivedTo
            };
        }

        private static string BuildSummaryText(IReadOnlyList<StockContainerOverviewRow> rows)
        {
            int yearly = rows.Count(row => row.ContainerKind == StockContainerKind.YearlyBox);
            int bag = rows.Count(row => row.ContainerKind == StockContainerKind.ElectronicBag);
            int history = rows.Count(row => row.ContainerKind == StockContainerKind.HistoryBox);
            int borrowed = rows.Count(row => row.HasBorrowedContent);
            return $"共 {rows.Count} 个容器（年度盒 {yearly} · 电子袋 {bag} · 历史盒 {history} · 含借出 {borrowed}）";
        }

        /// <summary>历史盒容器编号为「-」，下钻摘要改用存放位置（盒号）。</summary>
        private static string ResolveContainerLabel(StockContainerOverviewRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.ContainerCode)
                && !string.Equals(row.ContainerCode.Trim(), "-", StringComparison.Ordinal))
            {
                return row.ContainerCode.Trim();
            }

            return string.IsNullOrWhiteSpace(row.StorageLocation)
                ? "-"
                : row.StorageLocation.Trim();
        }

        public sealed class FilterOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }
    }
}
