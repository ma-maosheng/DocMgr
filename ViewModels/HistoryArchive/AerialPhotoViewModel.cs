using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
// Project

using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HistoryArchive
{
    public class AerialPhotoViewModel : ViewModelBase
    {
        private readonly IAerialPhotoService _aerialPhotoService;
        private readonly IDialogService _dialogService;
        // [新增] 注入用户上下文服务
        private readonly IUserContextService _userContextService;

        // === Properties ===
        private ObservableCollection<AerialPhoto> _aerialPhotos = new ObservableCollection<AerialPhoto>();
        public ObservableCollection<AerialPhoto> AerialPhotos
        {
            get => _aerialPhotos;
            set => SetProperty(ref _aerialPhotos, value);
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

        private List<AerialPhoto> _allAerialPhotos = new List<AerialPhoto>();

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

        // === Commands ===
        public RelayCommand ImportCommand { get; }
        public RelayCommand BrowseCommand { get; }
        public RelayCommand DeleteTableCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetSearchCommand { get; }
        public RelayCommand ExportCommand { get; }

        public AerialPhotoViewModel(
            IAerialPhotoService aerialPhotoService,
            IDialogService dialogService,
            IUserContextService userContextService) // [修改] 注入参数
        {
            _aerialPhotoService = aerialPhotoService;
            _dialogService = dialogService;
            _userContextService = userContextService; // [修改] 赋值

            ImportCommand = new RelayCommand(async _ => await ImportAsync());
            BrowseCommand = new RelayCommand(async _ => await BrowseAsync());
            DeleteTableCommand = new RelayCommand(async _ => await DeleteTableAsync());

            EditCommand = new RelayCommand(async _ => await EditAsync(), _ => SelectedAerialPhoto != null);
            SearchCommand = new RelayCommand(_ => ApplySearchFilter());
            ResetSearchCommand = new RelayCommand(_ => ResetSearch());
            ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => _allAerialPhotos.Count > 0);
        }

        private async Task BrowseAsync()
        {
            try
            {
                var tables = await Task.Run(() => _aerialPhotoService.GetAerialPhotoTables());
                if (tables.Count == 0)
                {
                    _dialogService.ShowMessage("当前没有存档数据表，请先导入。");
                    return;
                }

                if (tables.Count == 1)
                {
                    string tableName = tables[0];
                    await LoadDataAsync(tableName);
                    _dialogService.ShowMessage($"已自动加载唯一数据表 [{tableName}]。", "提示");
                }
                else
                {
                    string? selectedTable = _dialogService.ShowSheetSelectionDialog(tables, "选择存档数据表");
                    if (!string.IsNullOrEmpty(selectedTable))
                    {
                        await LoadDataAsync(selectedTable);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"浏览失败: {ex.Message}");
            }
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

                string? selectedSheet = _dialogService.ShowSheetSelectionDialog(sheetNames);
                if (string.IsNullOrEmpty(selectedSheet)) return;

                _dialogService.SetBusyState(true);
                try
                {
                    await ProcessImportLogicAsync(filePath, selectedSheet);
                }
                finally
                {
                    _dialogService.SetBusyState(false);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"读取文件失败: {ex.Message}");
            }
        }

        private async Task ProcessImportLogicAsync(string filePath, string sheetName)
        {
            // [优化] 使用 Service 获取当前用户 (彻底去除了 Application.Current.MainWindow 依赖)
            string currentUser = _userContextService.CurrentUser?.RealName ?? "Unknown";

            string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string targetTableName = $"历史存档航摄影像{sheetName}";

            List<AerialPhoto> dataList = new List<AerialPhoto>();

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

                    for (int i = 1; i <= sheet.LastRowNum; i++)
                    {
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

                            // 使用上方获取的变量
                            Registrant = currentUser,
                            RegistrationDate = nowStr
                            // 注意：Modifier 等字段由 Model 默认值 "" 处理，无需手动赋值
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

            await Task.Run(() => _aerialPhotoService.ImportAerialPhotos(dataList, sheetName, isRecreate));

            _dialogService.ShowMessage($"成功导入 {dataList.Count} 条数据到表 [{targetTableName}]！");

            await LoadDataAsync(targetTableName);
        }

        private async Task DeleteTableAsync()
        {
            var tables = await Task.Run(() => _aerialPhotoService.GetAerialPhotoTables());
            if (tables.Count == 0) return;

            string? selected = _dialogService.ShowSheetSelectionDialog(tables, "选择要删除的存档表");
            if (string.IsNullOrEmpty(selected)) return;

            if (_dialogService.ShowConfirm($"确定要永久删除数据表 [{selected}] 吗？\n\n此操作不可恢复！", "危险操作确认"))
            {
                await Task.Run(() => _aerialPhotoService.DropTable(selected));
                _dialogService.ShowMessage($"数据表 [{selected}] 已成功删除。");

                if (CurrentTableName == selected)
                {
                    _allAerialPhotos.Clear();
                    AerialPhotos.Clear();
                    SearchKeyword = string.Empty;
                    SelectedAerialPhoto = null;
                    CurrentTableName = "（未选择）";
                    NoDataHintVisibility = Visibility.Visible;
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
            if (result && !string.IsNullOrWhiteSpace(CurrentTableName) && CurrentTableName != "（未选择）")
            {
                await LoadDataAsync(CurrentTableName);
                _dialogService.ShowMessage("记录已更新。", "完成");
            }
        }

        private async Task ExportAsync()
        {
            if (AerialPhotos.Count == 0)
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
                await Task.Run(() => ExportToExcel(filePath, AerialPhotos.ToList()));
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
                var data = await Task.Run(() => _aerialPhotoService.GetAerialPhotosByTable(tableName));
                _allAerialPhotos = data;
                CurrentTableName = tableName;
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
            string keyword = SearchKeyword?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    Contains(x.BoxNumber, keyword) ||
                    Contains(x.SurveyArea, keyword) ||
                    Contains(x.Scale, keyword) ||
                    Contains(x.PhotographyDate, keyword) ||
                    Contains(x.BoxContents, keyword) ||
                    Contains(x.Remark, keyword));
            }

            var list = query.ToList();
            AerialPhotos = new ObservableCollection<AerialPhoto>(list);
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
            if (string.IsNullOrWhiteSpace(sheetName) || sheetName == "（未选择）")
            {
                sheetName = "航摄影像";
            }

            sheetName = sheetName.Length > 31 ? sheetName[..31] : sheetName;
            var sheet = workbook.CreateSheet(sheetName);

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

        // --- Helpers 保持不变 ---
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