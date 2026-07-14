using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Shared;
using DocMgr.Models.Cabinets;
using DocMgr.Services.Interfaces;
using NPOI.SS.UserModel;

namespace DocMgr.Services.SystemSettings
{
    public class TestPreparationService
    {
        private const string TopoWorkbookFileName = "历史存档地形图台账.xlsx";
        private const string AerialWorkbookFileName = "历史存档航片台账.xlsx";
        private const string HardDiskWorkbookFileName = "硬盘工作簿.xlsx";
        private const string BlankHardDiskSheetName = "无数据硬盘";

        private readonly ITopoMapService _topoMapService;
        private readonly IAerialPhotoService _aerialPhotoService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IUserContextService _userContextService;

        public TestPreparationService(
            ITopoMapService topoMapService,
            IAerialPhotoService aerialPhotoService,
            IHardDiskMediaService hardDiskMediaService,
            IUserContextService userContextService)
        {
            _topoMapService = topoMapService;
            _aerialPhotoService = aerialPhotoService;
            _hardDiskMediaService = hardDiskMediaService;
            _userContextService = userContextService;
        }

        /// <summary>
        /// 从测试数据目录导入历史存档地形图工作簿中的全部工作表。
        /// </summary>
        public async Task<string> ImportTopoMapsAsync()
        {
            string filePath = ResolveTestDataFilePath(TopoWorkbookFileName);
            string currentUser = ResolveCurrentUserName();
            string nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var importedSheetNames = new List<string>();
            var allMaps = await Task.Run(() => LoadTopoMaps(filePath, currentUser, nowText, importedSheetNames));

            if (allMaps.Count == 0)
            {
                throw new InvalidOperationException("未从地形图测试工作簿中解析到有效记录。");
            }

            await Task.Run(() => _topoMapService.ImportTopoMaps(allMaps, isRecreate: true));
            return $"地形图测试数据填充完成：已处理 {importedSheetNames.Count} 个工作表，共导入 {allMaps.Count} 条记录。";
        }

        /// <summary>
        /// 从测试数据目录导入历史存档航摄影像工作簿中的全部工作表。
        /// </summary>
        public async Task<string> ImportAerialPhotosAsync()
        {
            string filePath = ResolveTestDataFilePath(AerialWorkbookFileName);
            string currentUser = ResolveCurrentUserName();
            string nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var sheetImports = await Task.Run(() => LoadAerialPhotoImports(filePath, currentUser, nowText));
            if (sheetImports.Count == 0)
            {
                throw new InvalidOperationException("未从航摄影像测试工作簿中解析到有效记录。");
            }

            foreach (var sheetImport in sheetImports)
            {
                await Task.Run(() => _aerialPhotoService.ImportAerialPhotos(sheetImport.Items, sheetImport.SheetName, isRecreate: true));
            }

            int totalCount = sheetImports.Sum(item => item.Items.Count);
            return $"航摄影像测试数据填充完成：已处理 {sheetImports.Count} 个工作表，共导入 {totalCount} 条记录。";
        }

        /// <summary>
        /// 从测试数据目录导入硬盘工作簿中的“无数据硬盘”工作表。
        /// </summary>
        public async Task<string> ImportBlankHardDisksAsync()
        {
            if (await _hardDiskMediaService.HasMediaRecordsAsync())
            {
                throw new InvalidOperationException("当前硬盘台账已存在数据。请在删除数据库或清空硬盘台账后再执行“填入硬盘（无数据）”。");
            }

            string filePath = ResolveTestDataFilePath(HardDiskWorkbookFileName);
            var sheetNames = await _hardDiskMediaService.GetImportSheetNamesAsync(filePath);
            if (!sheetNames.Contains(BlankHardDiskSheetName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"测试工作簿中未找到工作表“{BlankHardDiskSheetName}”。");
            }

            HardDiskMediaImportResult result = await _hardDiskMediaService.ImportMediaAsync(
                filePath,
                BlankHardDiskSheetName,
                ImportMode.Append,
                _userContextService.CurrentUser);

            int assignedLocationCount = await _hardDiskMediaService.AssignBlankInStockMediaToBlankSlotsInOrderAsync();
            if (assignedLocationCount < result.ImportedCount)
            {
                throw new InvalidOperationException(
                    $"已导入 {result.ImportedCount} 条无数据硬盘，但仅 {assignedLocationCount} 条按空白专用档口顺序入位，请检查档口容量或 Excel 中是否预填了存放位置。");
            }

            return $"无数据硬盘测试数据填充完成：已从工作表“{BlankHardDiskSheetName}”导入 {result.ImportedCount} 条记录，并按空白专用档口顺序入位 {assignedLocationCount} 条。";
        }

        private static List<TopoMap> LoadTopoMaps(string filePath, string currentUser, string nowText, ICollection<string> importedSheetNames)
        {
            var allMaps = new List<TopoMap>();

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var workbook = WorkbookFactory.Create(stream);
            for (int i = 0; i < workbook.NumberOfSheets; i++)
            {
                var sheet = workbook.GetSheetAt(i);
                if (sheet == null)
                {
                    continue;
                }

                var items = ParseTopoMapSheet(sheet, currentUser, nowText);
                if (items.Count == 0)
                {
                    continue;
                }

                importedSheetNames.Add(sheet.SheetName);
                allMaps.AddRange(items);
            }

            return allMaps;
        }

        private static List<AerialPhotoSheetImport> LoadAerialPhotoImports(string filePath, string currentUser, string nowText)
        {
            var imports = new List<AerialPhotoSheetImport>();

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var workbook = WorkbookFactory.Create(stream);
            for (int i = 0; i < workbook.NumberOfSheets; i++)
            {
                var sheet = workbook.GetSheetAt(i);
                if (sheet == null)
                {
                    continue;
                }

                var items = ParseAerialPhotoSheet(sheet, currentUser, nowText);
                if (items.Count == 0)
                {
                    continue;
                }

                imports.Add(new AerialPhotoSheetImport(sheet.SheetName, items));
            }

            return imports;
        }

        private static List<TopoMap> ParseTopoMapSheet(ISheet sheet, string currentUser, string nowText)
        {
            var data = new List<TopoMap>();
            if (sheet.LastRowNum <= 0)
            {
                return data;
            }

            var headerMap = ParseHeader(sheet.GetRow(0));
            if (headerMap.Count == 0)
            {
                return data;
            }

            string lastBox = string.Empty;
            string lastBoxSpecification = string.Empty;

            for (int i = 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null)
                {
                    continue;
                }

                string box = GetCellValue(row, headerMap, "档案盒编号");
                if (string.IsNullOrWhiteSpace(box))
                {
                    box = lastBox;
                }
                else
                {
                    lastBox = box;
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

                string scale = GetCellValue(row, headerMap, "比例尺");
                if (string.IsNullOrWhiteSpace(scale))
                {
                    continue;
                }

                var item = new TopoMap
                {
                    BoxNumber = box,
                    BoxSpecification = boxSpecification,
                    Scale = scale,
                    MapNumber = GetCellValue(row, headerMap, "图号"),
                    MapName = GetCellValue(row, headerMap, "图名"),
                    CoordinateSystem = GetCellValue(row, headerMap, "坐标系统"),
                    ElevationDatum = GetCellValue(row, headerMap, "高程基准"),
                    Region = GetCellValue(row, headerMap, "涉及省市县"),
                    Remark = GetCellValue(row, headerMap, "备注"),
                    Registrant = currentUser,
                    RegistrationDate = nowText,
                    CreationDate = ParseDate(FirstNonEmptyValue(row, headerMap, "成图日期", "成图时间")),
                    SurveyDate = ParseDate(FirstNonEmptyValue(row, headerMap, "调绘日期", "调绘时间"))
                };

                string sheetCountText = GetCellValue(row, headerMap, "幅数");
                if (int.TryParse(sheetCountText, out int sheetCount))
                {
                    item.SheetCount = sheetCount;
                }

                data.Add(item);
            }

            return data;
        }

        private static List<AerialPhoto> ParseAerialPhotoSheet(ISheet sheet, string currentUser, string nowText)
        {
            var items = new List<AerialPhoto>();
            if (sheet.LastRowNum <= 0)
            {
                return items;
            }

            var headerMap = ParseHeader(sheet.GetRow(0));
            if (headerMap.Count == 0)
            {
                return items;
            }

            string lastBoxNumber = string.Empty;
            string lastScale = string.Empty;
            string lastBoxSpecification = string.Empty;

            for (int i = 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null)
                {
                    continue;
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

                string surveyArea = GetCellValue(row, headerMap, "测区名称");
                if (string.IsNullOrWhiteSpace(surveyArea) && string.IsNullOrWhiteSpace(boxNumber))
                {
                    continue;
                }

                string scale = FirstNonEmptyValue(row, headerMap, "航摄比例尺", "比例尺");
                if (string.IsNullOrWhiteSpace(scale))
                {
                    scale = lastScale;
                }
                else
                {
                    lastScale = scale;
                }

                var item = new AerialPhoto
                {
                    BoxNumber = boxNumber,
                    BoxSpecification = boxSpecification,
                    SurveyArea = surveyArea,
                    Scale = scale,
                    PhotographyDate = ParseDate(FirstNonEmptyValue(row, headerMap, "航摄日期", "航摄时间")),
                    BoxContents = GetCellValue(row, headerMap, "档案盒内物品"),
                    Remark = GetCellValue(row, headerMap, "备注"),
                    Registrant = currentUser,
                    RegistrationDate = nowText
                };

                string photoCountText = FirstNonEmptyValue(row, headerMap, "相片张数", "像片张数");
                if (int.TryParse(photoCountText, out int photoCount))
                {
                    item.PhotoCount = photoCount;
                }

                items.Add(item);
            }

            return items;
        }

        private static Dictionary<string, int> ParseHeader(IRow? row)
        {
            var map = new Dictionary<string, int>();
            if (row == null)
            {
                return map;
            }

            for (int i = 0; i < row.LastCellNum; i++)
            {
                string? value = row.GetCell(i)?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !map.ContainsKey(value))
                {
                    map.Add(value, i);
                }
            }

            return map;
        }

        private static string GetCellValue(IRow row, IReadOnlyDictionary<string, int> headerMap, string columnName)
        {
            if (!headerMap.TryGetValue(columnName, out int cellIndex))
            {
                return string.Empty;
            }

            var cell = row.GetCell(cellIndex);
            if (cell == null)
            {
                return string.Empty;
            }

            if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
            {
                return cell.DateCellValue.ToString() ?? string.Empty;
            }

            return cell.ToString()?.Trim() ?? string.Empty;
        }

        private static string FirstNonEmptyValue(IRow row, IReadOnlyDictionary<string, int> headerMap, params string[] columnNames)
        {
            foreach (string columnName in columnNames)
            {
                string value = GetCellValue(row, headerMap, columnName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string ParseDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return DateTime.TryParse(input, out DateTime date)
                ? date.ToString("yyyy-MM-dd")
                : input;
        }

        private static string ResolveTestDataFilePath(string fileName)
        {
            foreach (string root in EnumerateCandidateRoots())
            {
                string candidate = Path.Combine(root, "测试数据", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException($"未找到测试数据文件：{fileName}");
        }

        private static IEnumerable<string> EnumerateCandidateRoots()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string basePath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
                {
                    continue;
                }

                DirectoryInfo? current = new DirectoryInfo(basePath);
                while (current != null)
                {
                    if (roots.Add(current.FullName))
                    {
                        yield return current.FullName;
                    }

                    current = current.Parent;
                }
            }
        }

        private string ResolveCurrentUserName()
        {
            string? realName = _userContextService.CurrentUser?.RealName;
            return string.IsNullOrWhiteSpace(realName) ? "Unknown" : realName.Trim();
        }

        private sealed record AerialPhotoSheetImport(string SheetName, List<AerialPhoto> Items);
    }
}
