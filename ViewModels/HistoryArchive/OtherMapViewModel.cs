using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DocMgr.ViewModels.Base;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DocMgr.ViewModels.HistoryArchive
{
    public class OtherMapViewModel : ViewModelBase
    {
        private readonly IOtherMapService _otherMapService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

        private ObservableCollection<OtherMap> _otherMaps = new ObservableCollection<OtherMap>();
        private List<OtherMap> _allOtherMaps = new List<OtherMap>();

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

        private string _currentTableName = "（未选择）";
        public string CurrentTableName
        {
            get => _currentTableName;
            set => SetProperty(ref _currentTableName, value);
        }

        private Visibility _noDataHintVisibility = Visibility.Visible;
        public Visibility NoDataHintVisibility
        {
            get => _noDataHintVisibility;
            set => SetProperty(ref _noDataHintVisibility, value);
        }

        public RelayCommand ImportCommand { get; }
        public RelayCommand BrowseCommand { get; }
        public RelayCommand DeleteTableCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetSearchCommand { get; }
        public RelayCommand ExportCommand { get; }

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
            DeleteTableCommand = new RelayCommand(async _ => await DeleteTableAsync());
            EditCommand = new RelayCommand(async _ => await EditAsync(), _ => SelectedOtherMap != null);
            SearchCommand = new RelayCommand(_ => ApplySearchFilter());
            ResetSearchCommand = new RelayCommand(_ => ResetSearch());
            ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => _allOtherMaps.Count > 0);
        }

        private async Task BrowseAsync()
        {
            try
            {
                var tables = await Task.Run(() => _otherMapService.GetOtherMapTables());
                if (tables.Count == 0)
                {
                    _dialogService.ShowMessage("当前数据库中没有找到任何其他图件存档表，请先进行导入。");
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

        private async Task ImportAsync()
        {
            string? filePath = _dialogService.OpenFileDialog("Excel Files|*.xlsx;*.xls", "选择其他图件Excel存档文件");
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

                string? selectedSheet = _dialogService.ShowSheetSelectionDialog(sheetNames);
                if (string.IsNullOrEmpty(selectedSheet))
                {
                    return;
                }

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
            string targetTableName = $"历史存档其他图件{sheetName}";

            List<OtherMap> dataList = new List<OtherMap>();

            using (var progress = _dialogService.ShowOperationProgress("其他图件 Excel 导入", "正在读取工作表…"))
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
                    string lastScale = string.Empty;
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

                    string mapName = GetCellValue(row, headerMap, "图名");
                    string sequenceNumber = GetCellValue(row, headerMap, "序号");
                    if (string.IsNullOrWhiteSpace(sequenceNumber))
                    {
                        sequenceNumber = lastSequenceNumber;
                    }
                    else
                    {
                        lastSequenceNumber = sequenceNumber;
                    }

                    string scale = GetCellValue(row, headerMap, "比例尺");
                    if (string.IsNullOrWhiteSpace(scale))
                    {
                        scale = lastScale;
                    }
                    else
                    {
                        lastScale = scale;
                    }

                    string boxNumber = GetCellValue(row, headerMap, "档案盒编号");
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

                    if (string.IsNullOrWhiteSpace(mapName) && string.IsNullOrWhiteSpace(scale) && string.IsNullOrWhiteSpace(boxNumber))
                    {
                        continue;
                    }

                    var item = new OtherMap
                    {
                        SequenceNumber = sequenceNumber,
                        Scale = scale,
                        BoxNumber = boxNumber,
                        BoxSpecification = boxSpecification,
                        MapName = mapName,
                        Remark = GetCellValue(row, headerMap, "备注"),
                        Registrant = currentUser,
                        RegistrationDate = nowStr
                    };

                    string sheetCountValue = GetCellValue(row, headerMap, "幅数");
                    if (int.TryParse(sheetCountValue, out int sheetCount))
                    {
                        item.SheetCount = sheetCount;
                    }

                    dataList.Add(item);
                }
                });
            }

            if (dataList.Count == 0)
            {
                _dialogService.ShowError("未解析到有效数据，请检查 Excel 是否包含“序号、比例尺、档案盒编号、图名、幅数”等列。");
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
                    "其他图件 Excel 导入",
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
            await LoadDataAsync(targetTableName);
        }

        private async Task DeleteTableAsync()
        {
            var tables = await Task.Run(() => _otherMapService.GetOtherMapTables());
            if (tables.Count == 0)
            {
                return;
            }

            string? selected = _dialogService.ShowSheetSelectionDialog(tables, "选择要删除的存档表");
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            if (_dialogService.ShowConfirm($"确定要永久删除数据表 [{selected}] 吗？\n\n此操作不可恢复！", "危险操作确认"))
            {
                await Task.Run(() => _otherMapService.DropTable(selected));
                _dialogService.ShowMessage($"数据表 [{selected}] 已成功删除。");

                if (CurrentTableName == selected)
                {
                    _allOtherMaps.Clear();
                    OtherMaps.Clear();
                    SearchKeyword = string.Empty;
                    SelectedOtherMap = null;
                    CurrentTableName = "（未选择）";
                    NoDataHintVisibility = Visibility.Visible;
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
            if (result && !string.IsNullOrWhiteSpace(CurrentTableName) && CurrentTableName != "（未选择）")
            {
                await LoadDataAsync(CurrentTableName);
                _dialogService.ShowMessage("记录已更新。", "完成");
            }
        }

        private async Task ExportAsync()
        {
            if (OtherMaps.Count == 0)
            {
                _dialogService.ShowMessage("当前没有可导出的记录。");
                return;
            }

            string defaultFileName = BuildDefaultExportFileName();
            string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出其他图件数据", defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                await Task.Run(() => ExportToExcel(filePath, OtherMaps.ToList()));
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

        private async Task LoadDataAsync(string tableName)
        {
            _dialogService.SetBusyState(true);
            try
            {
                var list = await Task.Run(() => _otherMapService.GetOtherMapsByTable(tableName));
                _allOtherMaps = list;
                CurrentTableName = tableName;
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
            string keyword = SearchKeyword?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    Contains(x.SequenceNumber, keyword) ||
                    Contains(x.Scale, keyword) ||
                    Contains(x.BoxNumber, keyword) ||
                    Contains(x.MapName, keyword) ||
                    Contains(x.Remark, keyword));
            }

            var list = query.ToList();
            OtherMaps = new ObservableCollection<OtherMap>(list);
            NoDataHintVisibility = list.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            CommandManager.InvalidateRequerySuggested();
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
            string baseName = string.IsNullOrWhiteSpace(CurrentTableName) || CurrentTableName == "（未选择）"
                ? "其他图件导出"
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
            if (string.IsNullOrWhiteSpace(sheetName) || sheetName == "（未选择）")
            {
                sheetName = "其他图件";
            }

            sheetName = sheetName.Length > 31 ? sheetName[..31] : sheetName;
            var sheet = workbook.CreateSheet(sheetName);

            string[] headers = { "序号", "比例尺", "档案盒编号", "档案盒规格", "图名", "幅数", "登记人", "登记日期", "备注" };
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
                row.CreateCell(1).SetCellValue(item.Scale ?? string.Empty);
                row.CreateCell(2).SetCellValue(item.BoxNumber ?? string.Empty);
                row.CreateCell(3).SetCellValue(item.BoxSpecification ?? string.Empty);
                row.CreateCell(4).SetCellValue(item.MapName ?? string.Empty);
                row.CreateCell(5).SetCellValue(item.SheetCount);
                row.CreateCell(6).SetCellValue(item.Registrant ?? string.Empty);
                row.CreateCell(7).SetCellValue(item.RegistrationDate ?? string.Empty);
                row.CreateCell(8).SetCellValue(item.Remark ?? string.Empty);
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

        private string GetCellValue(IRow row, Dictionary<string, int> map, string col)
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

            string cellText = cell.ToString()?.Trim() ?? string.Empty;
            return cell.CellType == CellType.Numeric
                ? cellText.Replace(".0", string.Empty)
                : cellText;
        }
    }
}
