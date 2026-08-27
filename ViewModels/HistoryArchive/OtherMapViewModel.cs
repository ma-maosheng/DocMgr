using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DocMgr.ViewModels.HistoryArchive
{
    public class OtherMapViewModel : ViewModelBase
    {
        private readonly IOtherMapService _otherMapService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

        private const int DefaultPageSize = 100;
        private const string UnselectedTableName = "（未选择）";
        private const string GlobalBrowseTableName = "全部其他图件";

        private ObservableCollection<OtherMap> _otherMaps = new ObservableCollection<OtherMap>();
        private List<OtherMap> _allOtherMaps = new List<OtherMap>();
        private List<OtherMap> _filteredOtherMaps = new List<OtherMap>();
        private List<OtherMap> _cachedAllMaps = new List<OtherMap>();
        private bool _hasFullCache;
        private string _lastPagedTableName = UnselectedTableName;
        private bool _isGlobalBrowse;
        private int _currentPage = 1;
        private int _pageSize = DefaultPageSize;
        private bool _isSwitchingBrowseMode;

        public ObservableCollection<OtherMap> OtherMaps
        {
            get => _otherMaps;
            set => SetProperty(ref _otherMaps, value);
        }

        private OtherMap? _selectedOtherMap;
        public OtherMap? SelectedOtherMap
        {
            get => _selectedOtherMap;
            set
            {
                if (SetProperty(ref _selectedOtherMap, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _searchKeyword = string.Empty;
        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        private bool _includeDisposed;
        /// <summary>默认排除已离库；勾选后可查看已离库记录。</summary>
        public bool IncludeDisposed
        {
            get => _includeDisposed;
            set
            {
                if (SetProperty(ref _includeDisposed, value))
                {
                    ApplySearchFilter();
                }
            }
        }

        private string _currentTableName = UnselectedTableName;
        public string CurrentTableName
        {
            get => _currentTableName;
            set
            {
                if (SetProperty(ref _currentTableName, value))
                {
                    OnPropertyChanged(nameof(CurrentBrowseCaption));
                }
            }
        }

        /// <summary>当前浏览范围与数据名称，供页面状态条显示。</summary>
        public string CurrentBrowseCaption
        {
            get
            {
                string modeName = IsGlobalBrowse ? "全局浏览" : "分页浏览";
                string dataName = string.IsNullOrWhiteSpace(CurrentTableName)
                    ? UnselectedTableName
                    : CurrentTableName;

                if (_allOtherMaps.Count == 0)
                {
                    return $"{modeName}  ·  {dataName}";
                }

                if (IsPagedBrowse)
                {
                    return $"{modeName}  ·  {dataName}  ·  {PageInfo}";
                }

                return $"{modeName}  ·  {dataName}  ·  共 {TotalCount} 条";
            }
        }

        /// <summary>分页浏览：按分类表加载，并按页展示。</summary>
        public bool IsPagedBrowse
        {
            get => !_isGlobalBrowse;
            set
            {
                if (value)
                {
                    _ = SwitchBrowseModeAsync(false);
                }
            }
        }

        /// <summary>全局浏览：一次加载全部其他图件，列表不分页。</summary>
        public bool IsGlobalBrowse
        {
            get => _isGlobalBrowse;
            set
            {
                if (value)
                {
                    _ = SwitchBrowseModeAsync(true);
                }
            }
        }

        public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 50, 100, 200, 500 };

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value <= 0 || _pageSize == value)
                {
                    return;
                }

                _pageSize = value;
                OnPropertyChanged();
                CurrentPage = 1;
                RefreshDisplayedMaps();
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(CanGoPrevious));
                    OnPropertyChanged(nameof(CanGoNext));
                    OnPropertyChanged(nameof(PageStartIndex));
                }
            }
        }

        public int TotalCount => _filteredOtherMaps.Count;

        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public string PageInfo => TotalCount == 0
            ? "暂无记录"
            : $"第 {CurrentPage} / {TotalPages} 页，本页 {OtherMaps.Count} 条，共 {TotalCount} 条";

        public bool CanGoPrevious => IsPagedBrowse && CurrentPage > 1;

        public bool CanGoNext => IsPagedBrowse && CurrentPage < TotalPages;

        public bool ShowPaginationBar => IsPagedBrowse && _allOtherMaps.Count > 0;

        /// <summary>当前页起始序号（0 基），供行号列使用。</summary>
        public int PageStartIndex => IsPagedBrowse ? (CurrentPage - 1) * PageSize : 0;

        private Visibility _noDataHintVisibility = Visibility.Visible;
        public Visibility NoDataHintVisibility
        {
            get => _noDataHintVisibility;
            set => SetProperty(ref _noDataHintVisibility, value);
        }

        public RelayCommand ImportCommand { get; }
        public RelayCommand BrowseCommand { get; }
        public RelayCommand DeleteCurrentRowCommand { get; }
        public RelayCommand DeleteTableCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetSearchCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        public OtherMapViewModel(
            IOtherMapService otherMapService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _otherMapService = otherMapService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            ImportCommand = new RelayCommand(async _ => await ImportAsync());
            BrowseCommand = new RelayCommand(async _ => await BrowseAsync());
            DeleteCurrentRowCommand = new RelayCommand(async _ => await DeleteCurrentRowAsync(),
                _ => SelectedOtherMap != null
                     && !HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(SelectedOtherMap.LifecycleStatus));
            DeleteTableCommand = new RelayCommand(async _ => await DeleteTableAsync());
            EditCommand = new RelayCommand(async _ => await EditAsync(), _ => SelectedOtherMap != null);
            SearchCommand = new RelayCommand(_ => ApplySearchFilter());
            ResetSearchCommand = new RelayCommand(_ => ResetSearch());
            ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => _filteredOtherMaps.Count > 0);
            FirstPageCommand = new RelayCommand(_ => GoToPage(1), _ => CanGoPrevious);
            PreviousPageCommand = new RelayCommand(_ => GoToPage(CurrentPage - 1), _ => CanGoPrevious);
            NextPageCommand = new RelayCommand(_ => GoToPage(CurrentPage + 1), _ => CanGoNext);
            LastPageCommand = new RelayCommand(_ => GoToPage(TotalPages), _ => CanGoNext);
        }

        private async Task BrowseAsync()
        {
            try
            {
                if (IsGlobalBrowse)
                {
                    await LoadAllDataAsync();
                    if (_allOtherMaps.Count == 0)
                    {
                        _dialogService.ShowMessage("当前没有其他图件数据，请先导入。");
                    }
                    else
                    {
                        _dialogService.ShowMessage($"已加载全部其他图件，共 {_allOtherMaps.Count} 条。", "完成");
                    }

                    return;
                }

                var tables = await Task.Run(() => _otherMapService.GetOtherMapTables());
                if (tables.Count == 0)
                {
                    _dialogService.ShowMessage("当前数据库中没有找到任何其他资料存档表，请先进行导入。");
                    return;
                }

                string? selectedTable = _dialogService.ShowSheetSelectionDialog(tables, "选择存档数据表")?.SheetName;
                if (!string.IsNullOrEmpty(selectedTable))
                {
                    await LoadDataAsync(selectedTable);
                    _dialogService.ShowMessage($"已加载表 [{selectedTable}]。", "完成");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"浏览数据失败: {ex.Message}");
            }
        }

        private async Task SwitchBrowseModeAsync(bool isGlobal)
        {
            if (_isSwitchingBrowseMode || _isGlobalBrowse == isGlobal)
            {
                return;
            }

            _isSwitchingBrowseMode = true;
            try
            {
                _isGlobalBrowse = isGlobal;
                OnPropertyChanged(nameof(IsGlobalBrowse));
                OnPropertyChanged(nameof(IsPagedBrowse));
                OnPropertyChanged(nameof(ShowPaginationBar));
                OnPropertyChanged(nameof(PageStartIndex));
                OnPropertyChanged(nameof(CurrentBrowseCaption));

                await YieldUiAsync();

                if (isGlobal)
                {
                    if (_hasFullCache)
                    {
                        _allOtherMaps = _cachedAllMaps;
                        CurrentTableName = _allOtherMaps.Count > 0 ? GlobalBrowseTableName : UnselectedTableName;
                        ApplySearchFilter();
                    }
                    else
                    {
                        await LoadAllDataAsync();
                    }

                    return;
                }

                await ApplyPagedScopeFromCacheOrKeepCurrentAsync();
            }
            finally
            {
                _isSwitchingBrowseMode = false;
            }
        }

        private async Task ApplyPagedScopeFromCacheOrKeepCurrentAsync()
        {
            if (!IsUsablePagedTableName(_lastPagedTableName))
            {
                CurrentPage = 1;
                ApplySearchFilter();
                return;
            }

            if (_hasFullCache)
            {
                _allOtherMaps = _cachedAllMaps
                    .Where(item => string.Equals(item.Category?.Trim(), _lastPagedTableName, StringComparison.Ordinal))
                    .ToList();
                CurrentTableName = _lastPagedTableName;
                ApplySearchFilter();
                return;
            }

            await LoadDataAsync(_lastPagedTableName);
        }

        private static bool IsUsablePagedTableName(string tableName)
        {
            return !string.IsNullOrWhiteSpace(tableName)
                && tableName != UnselectedTableName
                && tableName != GlobalBrowseTableName;
        }

        private static async Task YieldUiAsync()
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        private void InvalidateFullCache()
        {
            _hasFullCache = false;
            _cachedAllMaps = new List<OtherMap>();
        }

        private async Task ImportAsync()
        {
            string? filePath = _dialogService.OpenFileDialog("Excel Files|*.xlsx;*.xls", "选择其他历史资料 Excel 存档文件");
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            try
            {
                List<string> sheetNames = new List<string>();
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var workbook = WorkbookFactory.Create(fs);
                    for (int i = 0; i < workbook.NumberOfSheets; i++)
                    {
                        sheetNames.Add(workbook.GetSheetName(i));
                    }
                }

                SheetSelectionResult? sheetSelection = _dialogService.ShowSheetSelectionDialog(
                    sheetNames,
                    "选择要导入的工作表",
                    showExpandItemsByTextLineOption: true,
                    expandItemsByTextLineContent: "以文本行为单位记录资料内容",
                    expandItemsByTextLineToolTip:
                        "勾选后，「资料内容」单元格内每一非空文本行导入为一条记录；不勾选则以 Excel 表格行整格内容作为一条记录。");
                if (sheetSelection == null || string.IsNullOrWhiteSpace(sheetSelection.SheetName))
                {
                    return;
                }

                await ProcessImportLogicAsync(
                    filePath,
                    sheetSelection.SheetName.Trim(),
                    sheetSelection.ExpandItemsByTextLine);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"读取文件失败: {ex.Message}");
            }
        }

        private async Task ProcessImportLogicAsync(string filePath, string sheetName, bool expandContentByTextLine)
        {
            string currentUser = _userContextService.CurrentUser?.RealName ?? "Unknown";
            string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            // 存档表名：其他资料 + Excel 工作表名。
            string targetTableName = HistoryArchiveImportTableNameSupport.BuildOtherMapTableName(sheetName);

            List<OtherMap> dataList = new List<OtherMap>();

            using (var progress = _dialogService.ShowOperationProgress("其他资料 Excel 导入", "正在读取工作表…"))
            {
                await Task.Run(() =>
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var workbook = WorkbookFactory.Create(fs);
                    var sheet = workbook.GetSheet(sheetName);
                    if (sheet == null || sheet.LastRowNum <= 0)
                    {
                        return;
                    }

                    var headerMap = ParseHeader(sheet.GetRow(0));
                    string lastSequenceNumber = string.Empty;
                    string lastMaterialCategory = string.Empty;
                    string lastBoxNumber = string.Empty;
                    string lastBoxSpecification = string.Empty;
                    int lastRow = sheet.LastRowNum;

                    for (int i = 1; i <= lastRow; i++)
                    {
                        ExcelImportProgressSupport.ReportReadRow(progress, i, lastRow);
                        var row = sheet.GetRow(i);
                        if (row == null)
                        {
                            continue;
                        }

                        string sequenceNumber = GetCellValue(row, headerMap, "序号");
                        if (string.IsNullOrWhiteSpace(sequenceNumber))
                        {
                            sequenceNumber = lastSequenceNumber;
                        }
                        else
                        {
                            lastSequenceNumber = sequenceNumber;
                        }

                        string materialCategory = GetCellValue(row, headerMap, "资料分类");
                        if (string.IsNullOrWhiteSpace(materialCategory))
                        {
                            materialCategory = lastMaterialCategory;
                        }
                        else
                        {
                            lastMaterialCategory = materialCategory;
                        }

                        // 起始/截止年度：允许为空，允许同值；不向下继承空值。
                        string startYear = NormalizeYearText(GetCellValue(row, headerMap, "起始年度"));
                        string endYear = NormalizeYearText(GetCellValue(row, headerMap, "截止年度"));

                        string content = GetCellValue(row, headerMap, "资料内容", preserveNewlines: true);
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            // 兼容旧模版「图名」列
                            content = GetCellValue(row, headerMap, "图名", preserveNewlines: true);
                        }

                        string boxNumber = GetCellValue(row, headerMap, "档案盒编号");
                        if (string.IsNullOrWhiteSpace(boxNumber))
                        {
                            boxNumber = GetCellValue(row, headerMap, "盒号");
                        }

                        if (string.IsNullOrWhiteSpace(boxNumber))
                        {
                            boxNumber = lastBoxNumber;
                        }
                        else
                        {
                            lastBoxNumber = boxNumber;
                        }

                        string boxSpecification = GetCellValue(row, headerMap, "档案盒规格");
                        if (string.IsNullOrWhiteSpace(boxSpecification))
                        {
                            boxSpecification = lastBoxSpecification;
                        }
                        else
                        {
                            lastBoxSpecification = boxSpecification;
                        }

                        if (string.IsNullOrWhiteSpace(content)
                            && string.IsNullOrWhiteSpace(boxNumber)
                            && string.IsNullOrWhiteSpace(materialCategory)
                            && string.IsNullOrWhiteSpace(sequenceNumber))
                        {
                            continue;
                        }

                        IReadOnlyList<string> contentLines = expandContentByTextLine
                            ? SplitContentLines(content)
                            : new[] { content?.Trim() ?? string.Empty };

                        if (expandContentByTextLine && contentLines.Count == 0)
                        {
                            // 勾选按行拆分但内容为空时，仍保留一行空内容记录（若有盒号等有效字段）。
                            contentLines = new[] { string.Empty };
                        }

                        foreach (string contentLine in contentLines)
                        {
                            dataList.Add(new OtherMap
                            {
                                SequenceNumber = sequenceNumber,
                                MaterialCategory = materialCategory,
                                StartYear = startYear,
                                EndYear = endYear,
                                BoxNumber = boxNumber,
                                BoxSpecification = boxSpecification,
                                MapName = contentLine,
                                Registrant = currentUser,
                                RegistrationDate = nowStr
                            });
                        }
                    }
                });
            }

            if (dataList.Count == 0)
            {
                _dialogService.ShowError("未解析到有效数据，请检查 Excel 是否包含“序号、资料分类、资料内容、档案盒编号”等列。");
                return;
            }

            bool needAsk = await Task.Run(() => _otherMapService.IsTableExist(targetTableName));
            bool isRecreate = false;
            if (needAsk)
            {
                var mode = _dialogService.ShowImportOptionDialog(targetTableName);
                if (mode == null)
                {
                    return;
                }

                isRecreate = mode == ImportMode.Recreate;
            }

            try
            {
                using (_dialogService.ShowOperationProgress(
                    "其他资料 Excel 导入",
                    $"正在核验档口并写入 {dataList.Count} 条…"))
                {
                    await _otherMapService.ImportOtherMapsAsync(dataList, sheetName, isRecreate);
                }
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
                return;
            }

            _dialogService.ShowMessage($"成功导入 {dataList.Count} 条数据到表 [{targetTableName}]！");

            InvalidateFullCache();
            if (IsGlobalBrowse)
            {
                await LoadAllDataAsync();
            }
            else
            {
                await LoadDataAsync(targetTableName);
            }
        }

        private async Task DeleteCurrentRowAsync()
        {
            if (SelectedOtherMap == null)
            {
                _dialogService.ShowMessage("请先选择要删除的记录。");
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentTableName) || CurrentTableName == UnselectedTableName)
            {
                _dialogService.ShowMessage("当前未加载数据表，无法删除。");
                return;
            }

            var target = SelectedOtherMap;
            string mapLabel = string.IsNullOrWhiteSpace(target.MapName)
                ? (string.IsNullOrWhiteSpace(target.SequenceNumber) ? $"ID={target.Id}" : target.SequenceNumber)
                : target.MapName;

            if (!_dialogService.ShowConfirm(
                    $"确定要删除当前行吗？\n\n资料内容/序号：{mapLabel}\n档案盒编号：{target.BoxNumber}\n\n此操作不可恢复！",
                    "确认删除"))
            {
                return;
            }

            try
            {
                int deletedId = target.Id;
                await Task.Run(() => _otherMapService.DeleteOtherMap(deletedId));

                _allOtherMaps.RemoveAll(item => item.Id == deletedId);
                if (_hasFullCache)
                {
                    _cachedAllMaps.RemoveAll(item => item.Id == deletedId);
                }

                SelectedOtherMap = null;
                ApplySearchFilter();

                if (_allOtherMaps.Count == 0)
                {
                    CurrentTableName = UnselectedTableName;
                    NoDataHintVisibility = Visibility.Visible;
                }

                _dialogService.ShowMessage("当前行已删除。", "完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"删除当前行失败: {ex.Message}");
            }
        }

        private async Task DeleteTableAsync()
        {
            var tables = await Task.Run(() => _otherMapService.GetOtherMapTables());
            if (tables.Count == 0)
            {
                return;
            }

            string? selected = _dialogService.ShowSheetSelectionDialog(tables, "选择要删除的存档表")?.SheetName;
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            if (_dialogService.ShowConfirm($"确定要永久删除数据表 [{selected}] 吗？\n\n此操作不可恢复！", "危险操作确认"))
            {
                await Task.Run(() => _otherMapService.DropTable(selected));
                InvalidateFullCache();
                _dialogService.ShowMessage($"数据表 [{selected}] 已成功删除。");

                if (IsGlobalBrowse)
                {
                    await LoadAllDataAsync();
                }
                else if (CurrentTableName == selected)
                {
                    _allOtherMaps.Clear();
                    _filteredOtherMaps.Clear();
                    OtherMaps.Clear();
                    SearchKeyword = string.Empty;
                    SelectedOtherMap = null;
                    CurrentTableName = UnselectedTableName;
                    _lastPagedTableName = UnselectedTableName;
                    CurrentPage = 1;
                    NoDataHintVisibility = Visibility.Visible;
                    NotifyPaginationChanged();
                }
            }
        }

        private async Task EditAsync()
        {
            if (SelectedOtherMap == null)
            {
                _dialogService.ShowMessage("请先选择要编辑的记录。");
                return;
            }

            bool result = _dialogService.ShowOtherMapEditDialog(SelectedOtherMap);
            if (result && !string.IsNullOrWhiteSpace(CurrentTableName) && CurrentTableName != UnselectedTableName)
            {
                InvalidateFullCache();
                if (IsGlobalBrowse)
                {
                    await LoadAllDataAsync();
                }
                else
                {
                    await LoadDataAsync(CurrentTableName);
                }

                _dialogService.ShowMessage("记录已更新。", "完成");
            }
        }

        private async Task ExportAsync()
        {
            if (_filteredOtherMaps.Count == 0)
            {
                _dialogService.ShowMessage("当前没有可导出的记录。");
                return;
            }

            string defaultFileName = BuildDefaultExportFileName();
            string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出其他资料数据", defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                await Task.Run(() => ExportToExcel(filePath, _filteredOtherMaps.ToList()));
                _dialogService.ShowMessage($"导出完成：{filePath}", "完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"导出失败: {ex.Message}");
            }
        }

        private async Task LoadAllDataAsync()
        {
            _dialogService.SetBusyState(true);
            try
            {
                var list = await Task.Run(() => _otherMapService.GetAllOtherMaps());
                _cachedAllMaps = list;
                _hasFullCache = true;
                _allOtherMaps = list;
                CurrentTableName = list.Count > 0 ? GlobalBrowseTableName : UnselectedTableName;
                SelectedOtherMap = null;
                ApplySearchFilter();
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private async Task LoadDataAsync(string tableName)
        {
            _dialogService.SetBusyState(true);
            try
            {
                var list = await Task.Run(() => _otherMapService.GetOtherMapsByTable(tableName));
                _allOtherMaps = list;
                CurrentTableName = tableName;
                _lastPagedTableName = tableName;
                SelectedOtherMap = null;
                ApplySearchFilter();
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void ApplySearchFilter()
        {
            IEnumerable<OtherMap> query = _allOtherMaps;
            if (!IncludeDisposed)
            {
                query = query.Where(x => !HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(x.LifecycleStatus));
            }

            string keyword = SearchKeyword?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    Contains(x.SequenceNumber, keyword) ||
                    Contains(x.MaterialCategory, keyword) ||
                    Contains(x.StartYear, keyword) ||
                    Contains(x.EndYear, keyword) ||
                    Contains(x.BoxNumber, keyword) ||
                    Contains(x.BoxSpecification, keyword) ||
                    Contains(x.MapName, keyword) ||
                    Contains(x.Registrant, keyword) ||
                    Contains(x.RegistrationDate, keyword) ||
                    Contains(x.Remark, keyword));
            }

            _filteredOtherMaps = query.ToList();
            CurrentPage = 1;
            RefreshDisplayedMaps();
        }

        private void RefreshDisplayedMaps()
        {
            IEnumerable<OtherMap> displayQuery = _filteredOtherMaps;
            if (IsPagedBrowse && _filteredOtherMaps.Count > 0)
            {
                int clampedPage = Math.Clamp(CurrentPage, 1, TotalPages);
                if (clampedPage != CurrentPage)
                {
                    CurrentPage = clampedPage;
                }

                displayQuery = _filteredOtherMaps
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize);
            }

            var list = displayQuery.ToList();
            OtherMaps = new ObservableCollection<OtherMap>(list);
            NoDataHintVisibility = _filteredOtherMaps.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            NotifyPaginationChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void GoToPage(int page)
        {
            CurrentPage = Math.Clamp(page, 1, TotalPages);
            RefreshDisplayedMaps();
        }

        private void NotifyPaginationChanged()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(ShowPaginationBar));
            OnPropertyChanged(nameof(PageStartIndex));
            OnPropertyChanged(nameof(CurrentBrowseCaption));
        }

        private void ResetSearch()
        {
            SearchKeyword = string.Empty;
            ApplySearchFilter();
        }

        private static bool Contains(string? value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private string BuildDefaultExportFileName()
        {
            string baseName = string.IsNullOrWhiteSpace(CurrentTableName) || CurrentTableName == UnselectedTableName
                ? "其他历史资料导出"
                : CurrentTableName;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(invalidChar, '_');
            }

            return $"{baseName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        }

        private void ExportToExcel(string filePath, List<OtherMap> list)
        {
            using var workbook = new XSSFWorkbook();
            string sheetName = CurrentTableName;
            if (string.IsNullOrWhiteSpace(sheetName) || sheetName == UnselectedTableName)
            {
                sheetName = "其他历史资料";
            }

            var sheet = workbook.CreateSheet(ExcelSheetNameSupport.Sanitize(sheetName, "其他历史资料"));

            string[] headers =
            {
                "序号", "资料分类", "起始年度", "截止年度", "资料内容", "档案盒编号", "档案盒规格",
                "登记人", "登记日期", "备注"
            };
            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(headers[i]);
            }

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var row = sheet.CreateRow(i + 1);
                row.CreateCell(0).SetCellValue(item.SequenceNumber ?? string.Empty);
                row.CreateCell(1).SetCellValue(item.MaterialCategory ?? string.Empty);
                row.CreateCell(2).SetCellValue(item.StartYear ?? string.Empty);
                row.CreateCell(3).SetCellValue(item.EndYear ?? string.Empty);
                row.CreateCell(4).SetCellValue(item.MapName ?? string.Empty);
                row.CreateCell(5).SetCellValue(item.BoxNumber ?? string.Empty);
                row.CreateCell(6).SetCellValue(item.BoxSpecification ?? string.Empty);
                row.CreateCell(7).SetCellValue(item.Registrant ?? string.Empty);
                row.CreateCell(8).SetCellValue(item.RegistrationDate ?? string.Empty);
                row.CreateCell(9).SetCellValue(item.Remark ?? string.Empty);
            }

            for (int i = 0; i < headers.Length; i++)
            {
                sheet.AutoSizeColumn(i);
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(fs, leaveOpen: false);
        }

        private Dictionary<string, int> ParseHeader(IRow row)
        {
            var map = new Dictionary<string, int>();
            if (row == null)
            {
                return map;
            }

            for (int i = 0; i < row.LastCellNum; i++)
            {
                string val = row.GetCell(i)?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(val) && !map.ContainsKey(val))
                {
                    map.Add(val, i);
                }
            }

            return map;
        }

        private string GetCellValue(IRow row, Dictionary<string, int> map, string col, bool preserveNewlines = false)
        {
            if (!map.TryGetValue(col, out int columnIndex))
            {
                return string.Empty;
            }

            var cell = row.GetCell(columnIndex);
            if (cell == null)
            {
                return string.Empty;
            }

            string cellText = cell.ToString() ?? string.Empty;
            if (preserveNewlines)
            {
                // 仅去掉首尾空白，保留单元格内换行，供按文本行拆分。
                return cellText.Trim();
            }

            cellText = cellText.Trim();
            return cell.CellType == CellType.Numeric
                ? cellText.Replace(".0", string.Empty)
                : cellText;
        }

        /// <summary>
        /// 将「资料内容」按文本行拆分；空行丢弃。
        /// </summary>
        private static IReadOnlyList<string> SplitContentLines(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return text
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();
        }

        /// <summary>
        /// 规范化年度文本：去空白；数字单元格去掉尾随 .0。允许空串。
        /// </summary>
        private static string NormalizeYearText(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            string text = raw.Trim();
            if (text.EndsWith(".0", StringComparison.Ordinal) && text.Length > 2)
            {
                text = text[..^2];
            }

            return text;
        }
    }
}
