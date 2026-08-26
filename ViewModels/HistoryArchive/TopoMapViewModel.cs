using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using DocMgr.ViewModels.Base;


namespace DocMgr.ViewModels.HistoryArchive
{
    public class TopoMapViewModel : ViewModelBase
    {
        private readonly ITopoMapService _topoMapService;
        private readonly IDialogService _dialogService;

        // 1. 引入 UserContextService
        private readonly IUserContextService _userContextService;

        // === Properties ===

        private const int DefaultPageSize = 100;
        private const string UnselectedTableName = "（未选择）";
        private const string GlobalBrowseTableName = "全部地形图";

        // 数据源
        private ObservableCollection<TopoMap> _topoMaps = new ObservableCollection<TopoMap>();
        private List<TopoMap> _allTopoMaps = new List<TopoMap>();
        private List<TopoMap> _filteredTopoMaps = new List<TopoMap>();
        private List<TopoMap> _cachedAllMaps = new List<TopoMap>();
        private bool _hasFullCache;
        private string _lastPagedTableName = UnselectedTableName;
        private bool _isGlobalBrowse;
        private int _currentPage = 1;
        private int _pageSize = DefaultPageSize;
        private bool _isSwitchingBrowseMode;

        public ObservableCollection<TopoMap> TopoMaps
        {
            get => _topoMaps;
            set => SetProperty(ref _topoMaps, value);
        }

        private TopoMap? _selectedTopoMap;
        public TopoMap? SelectedTopoMap
        {
            get => _selectedTopoMap;
            set
            {
                if (SetProperty(ref _selectedTopoMap, value))
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

        // 当前显示的表名
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

                if (_allTopoMaps.Count == 0)
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

        /// <summary>分页浏览：按比例尺表加载，并按页展示。</summary>
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

        /// <summary>全局浏览：一次加载全部地形图，列表不分页。</summary>
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

        public int TotalCount => _filteredTopoMaps.Count;

        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public string PageInfo => TotalCount == 0
            ? "暂无记录"
            : $"第 {CurrentPage} / {TotalPages} 页，本页 {TopoMaps.Count} 条，共 {TotalCount} 条";

        public bool CanGoPrevious => IsPagedBrowse && CurrentPage > 1;

        public bool CanGoNext => IsPagedBrowse && CurrentPage < TotalPages;

        public bool ShowPaginationBar => IsPagedBrowse && _allTopoMaps.Count > 0;

        /// <summary>当前页起始序号（0 基），供行号列使用。</summary>
        public int PageStartIndex => IsPagedBrowse ? (CurrentPage - 1) * PageSize : 0;

        // 无数据提示的显示状态
        private Visibility _noDataHintVisibility = Visibility.Visible;
        public Visibility NoDataHintVisibility
        {
            get => _noDataHintVisibility;
            set => SetProperty(ref _noDataHintVisibility, value);
        }

        // === Commands ===
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

        public TopoMapViewModel(
            ITopoMapService topoMapService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _topoMapService = topoMapService;
            _dialogService = dialogService;
            _userContextService = userContextService; // 保存引用

            ImportCommand = new RelayCommand(async _ => await ImportAsync());
            BrowseCommand = new RelayCommand(async _ => await BrowseAsync());
            DeleteCurrentRowCommand = new RelayCommand(async _ => await DeleteCurrentRowAsync(), _ => SelectedTopoMap != null);
            DeleteTableCommand = new RelayCommand(async _ => await DeleteTableAsync());

            EditCommand = new RelayCommand(async _ => await EditAsync(), _ => SelectedTopoMap != null);
            SearchCommand = new RelayCommand(_ => ApplySearchFilter());
            ResetSearchCommand = new RelayCommand(_ => ResetSearch());
            ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => _filteredTopoMaps.Count > 0);
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
                    if (_allTopoMaps.Count == 0)
                    {
                        _dialogService.ShowMessage("当前没有地形图数据，请先导入。");
                    }
                    else
                    {
                        _dialogService.ShowMessage($"已加载全部地形图，共 {_allTopoMaps.Count} 条。", "完成");
                    }

                    return;
                }

                var tables = await Task.Run(() => _topoMapService.GetTopoMapTables());
                if (tables.Count == 0)
                {
                    _dialogService.ShowMessage("当前数据库中没有找到任何存档数据表，请先进行导入。");
                    return;
                }

                string? selectedTable = _dialogService.ShowSheetSelectionDialog(tables, "选择存档数据表");
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
                        _allTopoMaps = _cachedAllMaps;
                        CurrentTableName = _allTopoMaps.Count > 0 ? GlobalBrowseTableName : UnselectedTableName;
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
                string scale = GetScaleFromTableName(_lastPagedTableName);
                _allTopoMaps = _cachedAllMaps
                    .Where(item => string.Equals(item.Scale?.Trim(), scale, StringComparison.Ordinal))
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

        private static string GetScaleFromTableName(string tableName)
        {
            return tableName.Replace("历史存档纸质地形图", string.Empty, StringComparison.Ordinal).Trim();
        }

        private static async Task YieldUiAsync()
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        private void InvalidateFullCache()
        {
            _hasFullCache = false;
            _cachedAllMaps = new List<TopoMap>();
        }

        private async Task ImportAsync()
        {
            string? filePath = _dialogService.OpenFileDialog("Excel Files|*.xlsx;*.xls", "选择地形图Excel存档文件");
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                // 1. 读取Sheet名
                List<string> sheetNames = new List<string>();
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var workbook = WorkbookFactory.Create(fs);
                    for (int i = 0; i < workbook.NumberOfSheets; i++)
                        sheetNames.Add(workbook.GetSheetName(i));
                }

                // 2. 选择Sheet
                string? selectedSheet = _dialogService.ShowSheetSelectionDialog(sheetNames);
                if (string.IsNullOrEmpty(selectedSheet)) return;

                await ProcessImportLogicAsync(filePath, selectedSheet);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"读取文件失败: {ex.Message}");
            }
        }

        private async Task ProcessImportLogicAsync(string filePath, string sheetName)
        {
            // [优化] 直接从 Service 获取，删除所有关于 Application.Current.MainWindow 的引用
            string currentUserRealName = _userContextService.CurrentUser?.RealName ?? "Unknown";

            // 捕获当前时间（在主线程）
            string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            List<TopoMap> data = new List<TopoMap>();
            List<string> involvedScales = new List<string>();

            using (var progress = _dialogService.ShowOperationProgress("地形图 Excel 导入", "正在读取工作表…"))
            {
                await Task.Run(() =>
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var workbook = WorkbookFactory.Create(fs);
                        var sheet = workbook.GetSheet(sheetName);
                        if (sheet != null && sheet.LastRowNum > 0)
                        {
                            var headerMap = ParseHeader(sheet.GetRow(0));
                            string lastBox = "";
                            string lastBoxSpecification = "";
                            int lastRow = sheet.LastRowNum;

                            for (int i = 1; i <= lastRow; i++)
                            {
                                ExcelImportProgressSupport.ReportReadRow(progress, i, lastRow);
                                var row = sheet.GetRow(i);
                                if (row == null) continue;

                            string box = GetCellValue(row, headerMap, "档案盒编号");
                            if (string.IsNullOrWhiteSpace(box)) box = lastBox; else lastBox = box;

                            string boxSpecification = GetCellValue(row, headerMap, "档案盒规格");
                            if (string.IsNullOrWhiteSpace(boxSpecification)) boxSpecification = lastBoxSpecification; else lastBoxSpecification = boxSpecification;

                            string scale = GetCellValue(row, headerMap, "比例尺");
                            if (string.IsNullOrWhiteSpace(scale)) continue;

                            var item = new TopoMap
                            {
                                BoxNumber = box,
                                Scale = scale,
                                BoxSpecification = boxSpecification,
                                MapNumber = NormalizeMapNumber(GetCellValue(row, headerMap, "图号")),
                                MapName = GetCellValue(row, headerMap, "图名"),
                                CoordinateSystem = GetCellValue(row, headerMap, "坐标系统"),
                                ElevationDatum = GetCellValue(row, headerMap, "高程基准"),
                                Region = GetCellValue(row, headerMap, "涉及省市县"),
                                Remark = GetCellValue(row, headerMap, "备注"),
                                Registrant = currentUserRealName, // 使用最上面获取的值
                                RegistrationDate = nowStr
                            };

                            // === 补全开始：处理其他字段 ===

                            // 1. 幅数 (int)
                            string sheetStr = GetCellValue(row, headerMap, "幅数");
                            if (int.TryParse(sheetStr, out int sheetCount))
                                item.SheetCount = sheetCount;

                            // 2. 成图日期 (兼容 "成图日期" 和 "成图时间" 列名)
                            string cDate = GetCellValue(row, headerMap, "成图日期");
                            if (string.IsNullOrEmpty(cDate))
                                cDate = GetCellValue(row, headerMap, "成图时间");
                            item.CreationDate = ParseDate(cDate);

                            // 3. 调绘日期 (兼容 "调绘日期" 和 "调绘时间" 列名)
                            string sDate = GetCellValue(row, headerMap, "调绘日期");
                            if (string.IsNullOrEmpty(sDate))
                                sDate = GetCellValue(row, headerMap, "调绘时间");
                            item.SurveyDate = ParseDate(sDate);

                            // === 补全结束 ===

                            data.Add(item);
                        }
                    }
                }
                });
            }

            if (data.Count == 0)
            {
                _dialogService.ShowError("未解析到有效数据。");
                return;
            }

            involvedScales = data.Select(x => x.Scale).Distinct().ToList();

            // 2. 检查是否存在 (切回 UI 线程由 DialogService 处理)
            bool needAsk = false;
            string existingEx = "";
            foreach (var s in involvedScales)
            {
                string tName = $"历史存档纸质地形图{s}";
                if (_topoMapService.IsTableExist(tName))
                {
                    needAsk = true;
                    existingEx = tName;
                    break;
                }
            }

            bool isRecreate = false;
            if (needAsk)
            {
                var mode = _dialogService.ShowImportOptionDialog($"{existingEx} 等");
                if (mode == null) return; // Cancel
                isRecreate = (mode == ImportMode.Recreate);
            }

            // 3. 入库
            try
            {
                using (_dialogService.ShowOperationProgress(
                    "地形图 Excel 导入",
                    $"正在核验档口并写入 {data.Count} 条…"))
                {
                    await _topoMapService.ImportTopoMapsAsync(data, isRecreate);
                }
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
                return;
            }

            _dialogService.ShowMessage($"成功导入 {data.Count} 条数据！");

            // 4. 自动刷新
            InvalidateFullCache();
            if (IsGlobalBrowse)
            {
                await LoadAllDataAsync();
            }
            else if (involvedScales.Any())
            {
                await LoadDataAsync($"历史存档纸质地形图{involvedScales.First()}");
            }
        }

        private async Task DeleteCurrentRowAsync()
        {
            if (SelectedTopoMap == null)
            {
                _dialogService.ShowMessage("请先选择要删除的记录。");
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentTableName) || CurrentTableName == "（未选择）")
            {
                _dialogService.ShowMessage("当前未加载数据表，无法删除。");
                return;
            }

            var target = SelectedTopoMap;
            string mapLabel = string.IsNullOrWhiteSpace(target.MapName)
                ? (string.IsNullOrWhiteSpace(target.MapNumber) ? $"ID={target.Id}" : target.MapNumber)
                : target.MapName;

            if (!_dialogService.ShowConfirm(
                    $"确定要删除当前行吗？\n\n图名/图号：{mapLabel}\n档案盒编号：{target.BoxNumber}\n\n此操作不可恢复！",
                    "确认删除"))
            {
                return;
            }

            try
            {
                int deletedId = target.Id;
                await Task.Run(() => _topoMapService.DeleteTopoMap(deletedId));

                _allTopoMaps.RemoveAll(item => item.Id == deletedId);
                SelectedTopoMap = null;
                ApplySearchFilter();

                // 当前比例尺表若已无数据，同步刷新表名状态
                if (_allTopoMaps.Count == 0)
                {
                    CurrentTableName = "（未选择）";
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
            var tables = await Task.Run(() => _topoMapService.GetTopoMapTables());
            if (tables.Count == 0) return;

            string? selected = _dialogService.ShowSheetSelectionDialog(tables, "选择要删除的表");
            if (string.IsNullOrEmpty(selected)) return;

            if (_dialogService.ShowConfirm($"确定要永久删除数据表 [{selected}] 吗？", "危险操作确认"))
            {
                await Task.Run(() => _topoMapService.DropTable(selected));
                InvalidateFullCache();
                _dialogService.ShowMessage($"数据表 [{selected}] 已删除。");

                if (IsGlobalBrowse)
                {
                    await LoadAllDataAsync();
                }
                else if (CurrentTableName == selected)
                {
                    _allTopoMaps.Clear();
                    _filteredTopoMaps.Clear();
                    TopoMaps.Clear();
                    SearchKeyword = string.Empty;
                    SelectedTopoMap = null;
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
            if (SelectedTopoMap == null)
            {
                _dialogService.ShowMessage("请先选择要编辑的记录。");
                return;
            }

            bool result = _dialogService.ShowTopoMapEditDialog(SelectedTopoMap);
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
            if (_filteredTopoMaps.Count == 0)
            {
                _dialogService.ShowMessage("当前没有可导出的记录。");
                return;
            }

            string defaultFileName = BuildDefaultExportFileName();
            string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出地形图数据", defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                await Task.Run(() => ExportToExcel(filePath, _filteredTopoMaps.ToList()));
                _dialogService.ShowMessage($"导出完成：{filePath}", "完成");
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"导出失败: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _dialogService.ShowError($"导出失败: {ex.Message}");
            }
        }

        private async Task LoadAllDataAsync()
        {
            _dialogService.SetBusyState(true);
            try
            {
                var list = await Task.Run(() => _topoMapService.GetAllTopoMaps());
                _cachedAllMaps = list;
                _hasFullCache = true;
                _allTopoMaps = list;
                CurrentTableName = list.Count > 0 ? GlobalBrowseTableName : UnselectedTableName;
                SelectedTopoMap = null;
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
                var list = await Task.Run(() => _topoMapService.GetTopoMapsByTable(tableName));
                _allTopoMaps = list;
                CurrentTableName = tableName;
                _lastPagedTableName = tableName;
                SelectedTopoMap = null;
                ApplySearchFilter();
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void ApplySearchFilter()
        {
            IEnumerable<TopoMap> query = _allTopoMaps;
            string keyword = SearchKeyword?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    Contains(x.BoxNumber, keyword) ||
                    Contains(x.Scale, keyword) ||
                    Contains(x.MapNumber, keyword) ||
                    Contains(x.MapName, keyword) ||
                    Contains(x.CreationDate, keyword) ||
                    Contains(x.SurveyDate, keyword) ||
                    Contains(x.CoordinateSystem, keyword) ||
                    Contains(x.ElevationDatum, keyword) ||
                    Contains(x.Region, keyword) ||
                    Contains(x.Remark, keyword));
            }

            _filteredTopoMaps = query.ToList();
            CurrentPage = 1;
            RefreshDisplayedMaps();
        }

        private void RefreshDisplayedMaps()
        {
            IEnumerable<TopoMap> displayQuery = _filteredTopoMaps;
            if (IsPagedBrowse && _filteredTopoMaps.Count > 0)
            {
                int clampedPage = Math.Clamp(CurrentPage, 1, TotalPages);
                if (clampedPage != CurrentPage)
                {
                    CurrentPage = clampedPage;
                }

                displayQuery = _filteredTopoMaps
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize);
            }

            var list = displayQuery.ToList();
            TopoMaps = new ObservableCollection<TopoMap>(list);
            NoDataHintVisibility = _filteredTopoMaps.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
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
                ? "地形图导出"
                : CurrentTableName;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(invalidChar, '_');
            }

            return $"{baseName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        }

        private void ExportToExcel(string filePath, List<TopoMap> list)
        {
            using var workbook = new XSSFWorkbook();
            string sheetName = CurrentTableName;
            if (string.IsNullOrWhiteSpace(sheetName) || sheetName == UnselectedTableName)
            {
                sheetName = "地形图";
            }

            sheetName = sheetName.Length > 31 ? sheetName[..31] : sheetName;
            var sheet = workbook.CreateSheet(sheetName);

            string[] headers = { "档案盒编号", "档案盒规格", "比例尺", "图号", "图名", "幅数", "成图日期", "调绘日期", "坐标系统", "高程基准", "涉及省市县", "登记人", "登记日期", "备注" };
            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(headers[i]);
            }

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var row = sheet.CreateRow(i + 1);
                row.CreateCell(0).SetCellValue(item.BoxNumber ?? string.Empty);
                row.CreateCell(1).SetCellValue(item.BoxSpecification ?? string.Empty);
                row.CreateCell(2).SetCellValue(item.Scale ?? string.Empty);
                row.CreateCell(3).SetCellValue(item.MapNumber ?? string.Empty);
                row.CreateCell(4).SetCellValue(item.MapName ?? string.Empty);
                row.CreateCell(5).SetCellValue(item.SheetCount);
                row.CreateCell(6).SetCellValue(item.CreationDate ?? string.Empty);
                row.CreateCell(7).SetCellValue(item.SurveyDate ?? string.Empty);
                row.CreateCell(8).SetCellValue(item.CoordinateSystem ?? string.Empty);
                row.CreateCell(9).SetCellValue(item.ElevationDatum ?? string.Empty);
                row.CreateCell(10).SetCellValue(item.Region ?? string.Empty);
                row.CreateCell(11).SetCellValue(item.Registrant ?? string.Empty);
                row.CreateCell(12).SetCellValue(item.RegistrationDate ?? string.Empty);
                row.CreateCell(13).SetCellValue(item.Remark ?? string.Empty);
            }

            for (int i = 0; i < headers.Length; i++)
            {
                sheet.AutoSizeColumn(i);
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(fs, leaveOpen: false);
        }

        // --- Helpers ---

        // 此方法之前可能漏掉了，补上
        private string ParseDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            if (DateTime.TryParse(input, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd");
            }
            return input;
        }

        private Dictionary<string, int> ParseHeader(IRow row)
        {
            var map = new Dictionary<string, int>();
            if (row == null) return map;
            for (int i = 0; i < row.LastCellNum; i++)
            {
                string? val = row.GetCell(i)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(val) && !map.ContainsKey(val)) map.Add(val, i);
            }
            return map;
        }

        private string GetCellValue(IRow row, Dictionary<string, int> map, string col)
        {
            if (map.ContainsKey(col)) return row.GetCell(map[col])?.ToString()?.Trim() ?? "";
            return "";
        }

        /// <summary>
        /// 规范化图号：去除空白，并将全角括号替换为半角括号。
        /// </summary>
        private static string NormalizeMapNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return string.Concat(value.Where(c => !char.IsWhiteSpace(c)))
                .Replace('（', '(')
                .Replace('）', ')');
        }
    }
}
