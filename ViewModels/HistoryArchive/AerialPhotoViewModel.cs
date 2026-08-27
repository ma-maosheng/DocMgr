using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HistoryArchive
{
    public class AerialPhotoViewModel : ViewModelBase
    {
        private readonly IAerialPhotoService _aerialPhotoService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

        private const int DefaultPageSize = 100;
        private const string UnselectedTableName = "（未选择）";
        private const string GlobalBrowseTableName = "全部航摄影像";

        private ObservableCollection<AerialPhoto> _aerialPhotos = new ObservableCollection<AerialPhoto>();
        private List<AerialPhoto> _allAerialPhotos = new List<AerialPhoto>();
        private List<AerialPhoto> _filteredAerialPhotos = new List<AerialPhoto>();
        private List<AerialPhoto> _cachedAllPhotos = new List<AerialPhoto>();
        private bool _hasFullCache;
        private string _lastPagedTableName = UnselectedTableName;
        private bool _isGlobalBrowse;
        private int _currentPage = 1;
        private int _pageSize = DefaultPageSize;
        private bool _isSwitchingBrowseMode;

        public ObservableCollection<AerialPhoto> AerialPhotos
        {
            get => _aerialPhotos;
            set => SetProperty(ref _aerialPhotos, value);
        }

        private AerialPhoto? _selectedAerialPhoto;
        public AerialPhoto? SelectedAerialPhoto
        {
            get => _selectedAerialPhoto;
            set
            {
                if (SetProperty(ref _selectedAerialPhoto, value))
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

                if (_allAerialPhotos.Count == 0)
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

        /// <summary>全局浏览：一次加载全部航摄影像，列表不分页。</summary>
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
                RefreshDisplayedPhotos();
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

        public int TotalCount => _filteredAerialPhotos.Count;

        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public string PageInfo => TotalCount == 0
            ? "暂无记录"
            : $"第 {CurrentPage} / {TotalPages} 页，本页 {AerialPhotos.Count} 条，共 {TotalCount} 条";

        public bool CanGoPrevious => IsPagedBrowse && CurrentPage > 1;

        public bool CanGoNext => IsPagedBrowse && CurrentPage < TotalPages;

        public bool ShowPaginationBar => IsPagedBrowse && _allAerialPhotos.Count > 0;

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

        public AerialPhotoViewModel(
            IAerialPhotoService aerialPhotoService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _aerialPhotoService = aerialPhotoService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            ImportCommand = new RelayCommand(async _ => await ImportAsync());
            BrowseCommand = new RelayCommand(async _ => await BrowseAsync());
            DeleteCurrentRowCommand = new RelayCommand(async _ => await DeleteCurrentRowAsync(),
                _ => SelectedAerialPhoto != null
                     && !HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(SelectedAerialPhoto.LifecycleStatus));
            DeleteTableCommand = new RelayCommand(async _ => await DeleteTableAsync());

            EditCommand = new RelayCommand(async _ => await EditAsync(), _ => SelectedAerialPhoto != null);
            SearchCommand = new RelayCommand(_ => ApplySearchFilter());
            ResetSearchCommand = new RelayCommand(_ => ResetSearch());
            ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => _filteredAerialPhotos.Count > 0);
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
                    if (_allAerialPhotos.Count == 0)
                    {
                        _dialogService.ShowMessage("当前没有航摄影像数据，请先导入。");
                    }
                    else
                    {
                        _dialogService.ShowMessage($"已加载全部航摄影像，共 {_allAerialPhotos.Count} 条。", "完成");
                    }

                    return;
                }

                var tables = await Task.Run(() => _aerialPhotoService.GetAerialPhotoTables());
                if (tables.Count == 0)
                {
                    _dialogService.ShowMessage("当前数据库中没有找到任何存档数据表，请先进行导入。");
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
                        _allAerialPhotos = _cachedAllPhotos;
                        CurrentTableName = _allAerialPhotos.Count > 0 ? GlobalBrowseTableName : UnselectedTableName;
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
                _allAerialPhotos = _cachedAllPhotos
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
            _cachedAllPhotos = new List<AerialPhoto>();
        }

        private async Task ImportAsync()
        {
            string? filePath = _dialogService.OpenFileDialog("Excel Files|*.xlsx;*.xls", "选择航摄影像Excel存档文件");
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                List<string> sheetNames = new List<string>();
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var workbook = WorkbookFactory.Create(fs);
                    for (int i = 0; i < workbook.NumberOfSheets; i++)
                        sheetNames.Add(workbook.GetSheetName(i));
                }

                string? selectedSheet = _dialogService.ShowSheetSelectionDialog(sheetNames)?.SheetName;
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
            string currentUser = _userContextService.CurrentUser?.RealName ?? "Unknown";

            string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string targetTableName = HistoryArchiveImportTableNameSupport.BuildAerialPhotoTableName(sheetName);

            List<AerialPhoto> dataList = new List<AerialPhoto>();

            using (var progress = _dialogService.ShowOperationProgress("航摄影像 Excel 导入", "正在读取工作表…"))
            {
                await Task.Run(() =>
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var workbook = WorkbookFactory.Create(fs);
                        var sheet = workbook.GetSheet(sheetName);
                        if (sheet == null || sheet.LastRowNum <= 0) return;

                        var headerMap = ParseHeader(sheet.GetRow(0));
                        string lastBoxNum = "";
                        string lastScale = "";
                        string lastBoxSpecification = "";
                        int lastRow = sheet.LastRowNum;

                        for (int i = 1; i <= lastRow; i++)
                        {
                            ExcelImportProgressSupport.ReportReadRow(progress, i, lastRow);
                            var row = sheet.GetRow(i);
                            if (row == null) continue;

                        string boxNum = GetCellValue(row, headerMap, "档案盒编号");
                        if (string.IsNullOrWhiteSpace(boxNum)) boxNum = lastBoxNum; else lastBoxNum = boxNum;

                        string boxSpecification = GetCellValue(row, headerMap, "档案盒规格");
                        if (string.IsNullOrWhiteSpace(boxSpecification)) boxSpecification = lastBoxSpecification; else lastBoxSpecification = boxSpecification;

                        string surveyArea = GetCellValue(row, headerMap, "测区名称");
                        if (string.IsNullOrWhiteSpace(surveyArea) && string.IsNullOrWhiteSpace(boxNum)) continue;

                        var item = new AerialPhoto
                        {
                            BoxNumber = boxNum,
                            BoxSpecification = boxSpecification,
                            SurveyArea = surveyArea,
                            BoxContents = GetCellValue(row, headerMap, "档案盒内物品"),
                            Remark = GetCellValue(row, headerMap, "备注"),
                            Registrant = currentUser,
                            RegistrationDate = nowStr
                        };

                        string scale = GetCellValue(row, headerMap, "航摄比例尺");
                        if (string.IsNullOrEmpty(scale)) scale = GetCellValue(row, headerMap, "比例尺");
                        if (string.IsNullOrWhiteSpace(scale)) scale = lastScale; else lastScale = scale;
                        item.Scale = scale;

                        string photoDate = GetCellValue(row, headerMap, "航摄日期");
                        if (string.IsNullOrEmpty(photoDate)) photoDate = GetCellValue(row, headerMap, "航摄时间");
                        item.PhotographyDate = ParseDate(photoDate);

                        string cntStr = GetCellValue(row, headerMap, "相片张数");
                        if (string.IsNullOrEmpty(cntStr)) cntStr = GetCellValue(row, headerMap, "像片张数");
                        if (int.TryParse(cntStr, out int cnt)) item.PhotoCount = cnt;

                        dataList.Add(item);
                    }
                }
                });
            }

            if (dataList.Count == 0)
            {
                _dialogService.ShowError("未解析到有效数据，请检查必填列（如：测区名称、档案盒编号）。");
                return;
            }

            bool needAsk = false;
            await Task.Run(() => needAsk = _aerialPhotoService.IsTableExist(targetTableName));

            bool isRecreate = false;
            if (needAsk)
            {
                var mode = _dialogService.ShowImportOptionDialog(targetTableName);
                if (mode == null) return;
                isRecreate = (mode == ImportMode.Recreate);
            }

            try
            {
                using (_dialogService.ShowOperationProgress(
                    "航摄影像 Excel 导入",
                    $"正在核验档口并写入 {dataList.Count} 条…"))
                {
                    await _aerialPhotoService.ImportAerialPhotosAsync(dataList, sheetName, isRecreate);
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
            if (SelectedAerialPhoto == null)
            {
                _dialogService.ShowMessage("请先选择要删除的记录。");
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentTableName) || CurrentTableName == UnselectedTableName)
            {
                _dialogService.ShowMessage("当前未加载数据表，无法删除。");
                return;
            }

            var target = SelectedAerialPhoto;
            string photoLabel = string.IsNullOrWhiteSpace(target.SurveyArea)
                ? (string.IsNullOrWhiteSpace(target.BoxContents) ? $"ID={target.Id}" : target.BoxContents)
                : target.SurveyArea;

            if (!_dialogService.ShowConfirm(
                    $"确定要删除当前行吗？\n\n测区/盒内内容：{photoLabel}\n档案盒编号：{target.BoxNumber}\n\n此操作不可恢复！",
                    "确认删除"))
            {
                return;
            }

            try
            {
                int deletedId = target.Id;
                await Task.Run(() => _aerialPhotoService.DeleteAerialPhoto(deletedId));

                _allAerialPhotos.RemoveAll(item => item.Id == deletedId);
                if (_hasFullCache)
                {
                    _cachedAllPhotos.RemoveAll(item => item.Id == deletedId);
                }

                SelectedAerialPhoto = null;
                ApplySearchFilter();

                if (_allAerialPhotos.Count == 0)
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
            var tables = await Task.Run(() => _aerialPhotoService.GetAerialPhotoTables());
            if (tables.Count == 0) return;

            string? selected = _dialogService.ShowSheetSelectionDialog(tables, "选择要删除的存档表")?.SheetName;
            if (string.IsNullOrEmpty(selected)) return;

            if (_dialogService.ShowConfirm($"确定要永久删除数据表 [{selected}] 吗？\n\n此操作不可恢复！", "危险操作确认"))
            {
                await Task.Run(() => _aerialPhotoService.DropTable(selected));
                InvalidateFullCache();
                _dialogService.ShowMessage($"数据表 [{selected}] 已成功删除。");

                if (IsGlobalBrowse)
                {
                    await LoadAllDataAsync();
                }
                else if (CurrentTableName == selected)
                {
                    _allAerialPhotos.Clear();
                    _filteredAerialPhotos.Clear();
                    AerialPhotos.Clear();
                    SearchKeyword = string.Empty;
                    SelectedAerialPhoto = null;
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
            if (SelectedAerialPhoto == null)
            {
                _dialogService.ShowMessage("请先选择要编辑的记录。");
                return;
            }

            bool result = _dialogService.ShowAerialPhotoEditDialog(SelectedAerialPhoto);
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
            if (_filteredAerialPhotos.Count == 0)
            {
                _dialogService.ShowMessage("当前没有可导出的记录。");
                return;
            }

            string defaultFileName = BuildDefaultExportFileName();
            string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出航摄影像数据", defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                await Task.Run(() => ExportToExcel(filePath, _filteredAerialPhotos.ToList()));
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
                var list = await Task.Run(() => _aerialPhotoService.GetAllAerialPhotos());
                _cachedAllPhotos = list;
                _hasFullCache = true;
                _allAerialPhotos = list;
                CurrentTableName = list.Count > 0 ? GlobalBrowseTableName : UnselectedTableName;
                SelectedAerialPhoto = null;
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
                var data = await Task.Run(() => _aerialPhotoService.GetAerialPhotosByTable(tableName));
                _allAerialPhotos = data;
                CurrentTableName = tableName;
                _lastPagedTableName = tableName;
                SelectedAerialPhoto = null;
                ApplySearchFilter();
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void ApplySearchFilter()
        {
            IEnumerable<AerialPhoto> query = _allAerialPhotos;
            if (!IncludeDisposed)
            {
                query = query.Where(x => !HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(x.LifecycleStatus));
            }

            string keyword = SearchKeyword?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    Contains(x.BoxNumber, keyword) ||
                    Contains(x.BoxSpecification, keyword) ||
                    Contains(x.SurveyArea, keyword) ||
                    Contains(x.Scale, keyword) ||
                    Contains(x.PhotographyDate, keyword) ||
                    Contains(x.BoxContents, keyword) ||
                    Contains(x.Registrant, keyword) ||
                    Contains(x.RegistrationDate, keyword) ||
                    Contains(x.Remark, keyword) ||
                    x.PhotoCount.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            _filteredAerialPhotos = query.ToList();
            CurrentPage = 1;
            RefreshDisplayedPhotos();
        }

        private void RefreshDisplayedPhotos()
        {
            IEnumerable<AerialPhoto> displayQuery = _filteredAerialPhotos;
            if (IsPagedBrowse && _filteredAerialPhotos.Count > 0)
            {
                int clampedPage = Math.Clamp(CurrentPage, 1, TotalPages);
                if (clampedPage != CurrentPage)
                {
                    CurrentPage = clampedPage;
                }

                displayQuery = _filteredAerialPhotos
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize);
            }

            var list = displayQuery.ToList();
            AerialPhotos = new ObservableCollection<AerialPhoto>(list);
            NoDataHintVisibility = _filteredAerialPhotos.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            NotifyPaginationChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void GoToPage(int page)
        {
            CurrentPage = Math.Clamp(page, 1, TotalPages);
            RefreshDisplayedPhotos();
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
                ? "航摄影像导出"
                : CurrentTableName;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(invalidChar, '_');
            }

            return $"{baseName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        }

        private void ExportToExcel(string filePath, List<AerialPhoto> list)
        {
            using var workbook = new XSSFWorkbook();
            string sheetName = CurrentTableName;
            if (string.IsNullOrWhiteSpace(sheetName) || sheetName == UnselectedTableName)
            {
                sheetName = "航摄影像";
            }

            var sheet = workbook.CreateSheet(ExcelSheetNameSupport.Sanitize(sheetName, "航摄影像"));

            string[] headers = { "档案盒编号", "档案盒规格", "测区名称", "比例尺", "航摄日期", "档案盒内物品", "相片张数", "登记人", "登记日期", "备注" };
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
                row.CreateCell(2).SetCellValue(item.SurveyArea ?? string.Empty);
                row.CreateCell(3).SetCellValue(item.Scale ?? string.Empty);
                row.CreateCell(4).SetCellValue(item.PhotographyDate ?? string.Empty);
                row.CreateCell(5).SetCellValue(item.BoxContents ?? string.Empty);
                row.CreateCell(6).SetCellValue(item.PhotoCount);
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

        private string ParseDate(string input)
        {
            if (DateTime.TryParse(input, out DateTime dt)) return dt.ToString("yyyy-MM-dd");
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
            if (map.ContainsKey(col))
            {
                var cell = row.GetCell(map[col]);
                if (cell == null) return "";
                if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                    return cell.DateCellValue.ToString() ?? string.Empty;
                return cell.ToString()?.Trim() ?? string.Empty;
            }
            return "";
        }
    }
}
