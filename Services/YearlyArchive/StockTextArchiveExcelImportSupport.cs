using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocMgr.Models.YearlyArchive;
using NPOI.SS.UserModel;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 存档文本直办：Excel 解析与子类名称映射。
    /// </summary>
    public static class StockTextArchiveExcelImportSupport
    {
        public const string ColumnSequence = "序号";
        public const string ColumnYear = "年度";
        public const string ColumnProjectName = "项目名称";
        public const string ColumnMaterialName = "资料名称";
        public const string ColumnItemName = "子项名称";
        public const string ColumnCopyCount = "份数";
        public const string ColumnBoxCount = "盒数";
        public const string ColumnBoxLocation = "档案盒编号";
        public const string ColumnBoxSpecification = "档案盒规格";

        private static readonly string[] RequiredHeaders =
        {
            ColumnYear,
            ColumnProjectName,
            ColumnMaterialName,
            ColumnItemName,
            ColumnCopyCount,
            ColumnBoxLocation,
            ColumnBoxSpecification
        };

        private static readonly Regex LeadingIntegerRegex = new(@"^\s*(\d+)", RegexOptions.CultureInvariant);

        /// <summary>
        /// 列出工作簿中的工作表名称。
        /// </summary>
        public static IReadOnlyList<string> ListSheetNames(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("请选择导入文件。", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("导入文件不存在。", filePath);
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = WorkbookFactory.Create(stream);
            var names = new List<string>();
            for (int i = 0; i < workbook.NumberOfSheets; i++)
            {
                names.Add(workbook.GetSheetName(i));
            }

            return names;
        }

        /// <summary>
        /// 读取指定工作表，按档案盒编号分组。
        /// </summary>
        /// <param name="expandItemsByTextLine">
        /// 为 true 时，「子项名称」单元格内每一非空文本行作为一条资料子项；否则整行 Excel 作为一条资料子项。
        /// </param>
        public static StockTextArchiveExcelParseResult Parse(string filePath, string sheetName, bool expandItemsByTextLine = false)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return StockTextArchiveExcelParseResult.Fail("请选择导入文件。");
            }

            if (!File.Exists(filePath))
            {
                return StockTextArchiveExcelParseResult.Fail("导入文件不存在。");
            }

            if (string.IsNullOrWhiteSpace(sheetName))
            {
                return StockTextArchiveExcelParseResult.Fail("请选择要导入的工作表。");
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = WorkbookFactory.Create(stream);
            if (workbook.NumberOfSheets <= 0)
            {
                return StockTextArchiveExcelParseResult.Fail("工作簿中没有工作表。");
            }

            var sheet = workbook.GetSheet(sheetName.Trim());
            if (sheet == null)
            {
                return StockTextArchiveExcelParseResult.Fail($"找不到工作表 [{sheetName.Trim()}]。");
            }

            var headerRow = sheet.GetRow(sheet.FirstRowNum);
            if (headerRow == null)
            {
                return StockTextArchiveExcelParseResult.Fail("工作表缺少表头行。");
            }

            var formatter = new DataFormatter();
            var headerMap = BuildHeaderMap(headerRow, formatter);
            var missing = RequiredHeaders
                .Where(name => !headerMap.ContainsKey(name))
                .ToList();
            if (missing.Count > 0)
            {
                return StockTextArchiveExcelParseResult.Fail(
                    "表头缺少必要列：" + string.Join("、", missing) + "。");
            }

            var rawItems = new List<RawItemRow>();
            string sequence = string.Empty;
            string year = string.Empty;
            string projectName = string.Empty;
            string materialName = string.Empty;
            string boxCountText = string.Empty;
            string boxLocation = string.Empty;
            string boxSpecification = string.Empty;

            int lastRow = sheet.LastRowNum;
            for (int rowIndex = sheet.FirstRowNum + 1; rowIndex <= lastRow; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                if (row == null)
                {
                    continue;
                }

                int excelRowNumber = rowIndex + 1;
                sequence = FillDown(sequence, ReadCell(row, headerMap, ColumnSequence, formatter));
                year = FillDown(year, NormalizeYearCell(ReadCell(row, headerMap, ColumnYear, formatter)));
                projectName = FillDown(projectName, ReadCell(row, headerMap, ColumnProjectName, formatter));
                materialName = FillDown(materialName, ReadCell(row, headerMap, ColumnMaterialName, formatter));
                boxCountText = FillDown(boxCountText, ReadCell(row, headerMap, ColumnBoxCount, formatter));
                boxLocation = FillDown(boxLocation, ReadCell(row, headerMap, ColumnBoxLocation, formatter));
                boxSpecification = FillDown(boxSpecification, ReadCell(row, headerMap, ColumnBoxSpecification, formatter));

                string itemName = ReadCell(row, headerMap, ColumnItemName, formatter);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                string copyText = ReadCell(row, headerMap, ColumnCopyCount, formatter);
                IReadOnlyList<string> itemNames = expandItemsByTextLine
                    ? SplitItemNameLines(itemName)
                    : new[] { itemName.Trim() };
                foreach (string name in itemNames)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    rawItems.Add(new RawItemRow
                    {
                        ExcelRowNumber = excelRowNumber,
                        SequenceText = sequence,
                        Year = year,
                        ProjectName = projectName,
                        MaterialName = materialName,
                        ItemName = name.Trim(),
                        CopyText = copyText,
                        BoxCountText = boxCountText,
                        BoxLocation = boxLocation,
                        BoxSpecification = boxSpecification
                    });
                }
            }

            if (rawItems.Count == 0)
            {
                return StockTextArchiveExcelParseResult.Fail("未解析到有效的资料子项。");
            }

            return GroupBoxes(rawItems);
        }

        /// <summary>
        /// 按子项名称映射资料类型与所属子类；未命中则文本/其他。
        /// </summary>
        public static (string MaterialCategory, string SubCategory) MapItemClassification(string? itemName)
        {
            string compact = CompactForMatch(itemName);
            if (ContainsAny(compact, "联测网图", "联测图", "观测网图", "观测图", "布设图", "分布图"))
            {
                return (
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap,
                    ArchiveRegisterDomainValues.SimulatedSubCategoryProcessMap);
            }

            if (ContainsAny(compact, "检验报告", "质检", "检定证书", "仪器检定"))
            {
                return (
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                    ArchiveRegisterDomainValues.SimulatedSubCategoryExternalMaterial);
            }

            if (ContainsAny(compact, "点之记", "手簿", "i角", "检查记录", "检测记录", "检查报告", "检测统计", "检查统计"))
            {
                return (
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                    ArchiveRegisterDomainValues.SimulatedSubCategoryInspectionRecord);
            }

            if (ContainsAny(compact, "设计书", "专业技术书", "需求", "规范", "规程", "规定", "手册", "指南", "说明书"))
            {
                return (
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                    ArchiveRegisterDomainValues.SimulatedSubCategoryPlanningDesign);
            }

            if (ContainsAny(compact, "总结", "工作报告", "试运行", "试运营", "测试报告", "平差", "技术报告", "运行报告", "成果清单", "成果表", "控制点成果"))
            {
                return (
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                    ArchiveRegisterDomainValues.SimulatedSubCategorySummaryReport);
            }

            return (
                ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                ArchiveRegisterDomainValues.SimulatedSubCategoryOther);
        }

        private static StockTextArchiveExcelParseResult GroupBoxes(IReadOnlyList<RawItemRow> rawItems)
        {
            var boxes = new List<StockTextArchiveExcelBoxDraft>();
            var grouped = new List<List<RawItemRow>>();
            string? currentKey = null;
            List<RawItemRow>? current = null;
            foreach (var item in rawItems)
            {
                string key = item.BoxLocation?.Trim() ?? string.Empty;
                if (current == null
                    || !string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    current = new List<RawItemRow>();
                    grouped.Add(current);
                    currentKey = key;
                }

                current.Add(item);
            }

            var sequenceBoxCounts = grouped
                .Select(group => BuildBox(group))
                .GroupBy(box => box.SequenceNo)
                .ToDictionary(group => group.Key, group => group.Count());

            foreach (var group in grouped)
            {
                var box = BuildBox(group);
                int actualCount = sequenceBoxCounts.TryGetValue(box.SequenceNo, out int count) ? count : 0;
                var errors = box.ParseErrors.ToList();
                if (box.ClaimedBoxCount.HasValue && box.ClaimedBoxCount.Value != actualCount)
                {
                    errors.Add($"表中盒数为 {box.ClaimedBoxCount.Value}，实际解析到 {actualCount} 个档案盒编号。");
                }

                boxes.Add(new StockTextArchiveExcelBoxDraft
                {
                    SequenceNo = box.SequenceNo,
                    FirstRowNumber = box.FirstRowNumber,
                    Year = box.Year,
                    ProjectName = box.ProjectName,
                    MaterialName = box.MaterialName,
                    BoxSpecification = box.BoxSpecification,
                    SourceBoxLocationCode = box.SourceBoxLocationCode,
                    CabinetName = box.CabinetName,
                    Side = box.Side,
                    Row = box.Row,
                    Column = box.Column,
                    BoxIndex = box.BoxIndex,
                    NormalizedBoxLocationCode = box.NormalizedBoxLocationCode,
                    ClaimedBoxCount = box.ClaimedBoxCount,
                    ActualBoxCountInSequence = actualCount,
                    ParseErrors = errors,
                    Items = box.Items
                });
            }

            var duplicateLocations = boxes
                .Where(item => !string.IsNullOrWhiteSpace(item.NormalizedBoxLocationCode))
                .GroupBy(item => item.NormalizedBoxLocationCode, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (duplicateLocations.Count > 0)
            {
                boxes = boxes.Select(box =>
                {
                    if (!duplicateLocations.Contains(box.NormalizedBoxLocationCode))
                    {
                        return box;
                    }

                    var errors = box.ParseErrors.ToList();
                    errors.Add($"档案盒编号 [{box.NormalizedBoxLocationCode}] 在表中重复。");
                    return new StockTextArchiveExcelBoxDraft
                    {
                        SequenceNo = box.SequenceNo,
                        FirstRowNumber = box.FirstRowNumber,
                        Year = box.Year,
                        ProjectName = box.ProjectName,
                        MaterialName = box.MaterialName,
                        BoxSpecification = box.BoxSpecification,
                        SourceBoxLocationCode = box.SourceBoxLocationCode,
                        CabinetName = box.CabinetName,
                        Side = box.Side,
                        Row = box.Row,
                        Column = box.Column,
                        BoxIndex = box.BoxIndex,
                        NormalizedBoxLocationCode = box.NormalizedBoxLocationCode,
                        ClaimedBoxCount = box.ClaimedBoxCount,
                        ActualBoxCountInSequence = box.ActualBoxCountInSequence,
                        ParseErrors = errors,
                        Items = box.Items
                    };
                }).ToList();
            }

            return new StockTextArchiveExcelParseResult { Boxes = boxes };
        }

        private static StockTextArchiveExcelBoxDraft BuildBox(IReadOnlyList<RawItemRow> rows)
        {
            var first = rows[0];
            var errors = new List<string>();
            int sequenceNo = 0;
            if (!string.IsNullOrWhiteSpace(first.SequenceText)
                && !int.TryParse(first.SequenceText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out sequenceNo))
            {
                errors.Add($"序号 [{first.SequenceText}] 无法识别。");
            }

            if (string.IsNullOrWhiteSpace(first.Year) || first.Year.Trim().Length != 4 || !first.Year.Trim().All(char.IsDigit))
            {
                errors.Add("实施年度必须是四位数字年份。");
            }

            if (string.IsNullOrWhiteSpace(first.ProjectName))
            {
                errors.Add("项目名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(first.MaterialName))
            {
                errors.Add("资料名称不能为空。");
            }

            string spec = first.BoxSpecification?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(spec))
            {
                errors.Add("档案盒规格不能为空。");
            }
            else if (!IsAllowedBoxSpecification(spec))
            {
                errors.Add($"档案盒规格 [{spec}] 不在允许范围内。");
            }

            string cabinetName = string.Empty;
            string side = string.Empty;
            int row = 0;
            int column = 0;
            int boxIndex = 0;
            string normalized = string.Empty;
            string sourceLocation = first.BoxLocation?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sourceLocation))
            {
                errors.Add("档案盒编号不能为空。");
            }
            else if (!ArchiveSlotLocationSupport.TryParseSlotLocation(sourceLocation, out cabinetName, out side, out row, out column)
                || !ArchiveSlotLocationSupport.TryParseSequenceIndex(sourceLocation, out boxIndex))
            {
                errors.Add($"档案盒编号 [{sourceLocation}] 无法解析为柜面-层-列-序号。");
            }
            else
            {
                normalized = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                    cabinetName,
                    side,
                    row,
                    column,
                    boxIndex);
            }

            var items = new List<StockTextArchiveMediaItemDraft>();
            for (int index = 0; index < rows.Count; index++)
            {
                var raw = rows[index];
                int copyCount = ParseCopyCount(raw.CopyText);
                if (copyCount < 1)
                {
                    errors.Add($"第 {raw.ExcelRowNumber} 行份数无法识别。");
                    copyCount = 1;
                }

                var (category, subCategory) = MapItemClassification(raw.ItemName);
                items.Add(new StockTextArchiveMediaItemDraft
                {
                    ContentDesc = raw.ItemName,
                    ConfidentialLevel = "秘密",
                    ContentCount = copyCount,
                    MaterialCategory = category,
                    SubCategory = subCategory,
                    OrganizationForm = ArchiveRegisterDomainValues.SimulatedOrganizationFormBound
                });
            }

            if (items.Count == 0)
            {
                errors.Add("该档案盒没有资料子项。");
            }

            return new StockTextArchiveExcelBoxDraft
            {
                SequenceNo = sequenceNo,
                FirstRowNumber = first.ExcelRowNumber,
                Year = first.Year?.Trim() ?? string.Empty,
                ProjectName = first.ProjectName?.Trim() ?? string.Empty,
                MaterialName = first.MaterialName?.Trim() ?? string.Empty,
                BoxSpecification = spec,
                SourceBoxLocationCode = sourceLocation,
                CabinetName = cabinetName,
                Side = side,
                Row = row,
                Column = column,
                BoxIndex = boxIndex,
                NormalizedBoxLocationCode = normalized,
                ClaimedBoxCount = ParseBoxCount(first.BoxCountText),
                ParseErrors = errors,
                Items = items
            };
        }

        private static bool IsAllowedBoxSpecification(string spec)
        {
            return spec is "标准(10cm)" or "标准(5cm)" or "标准(3cm)" or "标准(2cm)" or "非标(10cm)";
        }

        private static int ParseCopyCount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 1;
            }

            Match match = LeadingIntegerRegex.Match(text);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                && value >= 1)
            {
                return value;
            }

            return 0;
        }

        private static int? ParseBoxCount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            Match match = LeadingIntegerRegex.Match(text);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                && value >= 1)
            {
                return value;
            }

            return null;
        }

        private static Dictionary<string, int> BuildHeaderMap(IRow headerRow, DataFormatter formatter)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            short lastCell = headerRow.LastCellNum;
            for (int index = 0; index < lastCell; index++)
            {
                string name = formatter.FormatCellValue(headerRow.GetCell(index))?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) || map.ContainsKey(name))
                {
                    continue;
                }

                map[name] = index;
            }

            return map;
        }

        private static string ReadCell(IRow row, IReadOnlyDictionary<string, int> headerMap, string header, DataFormatter formatter)
        {
            if (!headerMap.TryGetValue(header, out int columnIndex))
            {
                return string.Empty;
            }

            return formatter.FormatCellValue(row.GetCell(columnIndex))?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 将 Excel「年度」单元格规范为四位数字年份。
        /// </summary>
        private static string NormalizeYearCell(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (trimmed.Length == 4 && trimmed.All(char.IsDigit))
            {
                return trimmed;
            }

            Match match = Regex.Match(trimmed, @"\d{4}");
            return match.Success ? match.Value : trimmed;
        }

        private static IReadOnlyList<string> SplitItemNameLines(string text)
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

        private static string FillDown(string previous, string current)
            => string.IsNullOrWhiteSpace(current) ? previous : current.Trim();

        private static string CompactForMatch(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return Regex.Replace(text, @"\s+", string.Empty);
        }

        private static bool ContainsAny(string compact, params string[] keywords)
            => keywords.Any(keyword => compact.Contains(keyword, StringComparison.Ordinal));

        private sealed class RawItemRow
        {
            public int ExcelRowNumber { get; init; }

            public string SequenceText { get; init; } = string.Empty;

            public string Year { get; init; } = string.Empty;

            public string ProjectName { get; init; } = string.Empty;

            public string MaterialName { get; init; } = string.Empty;

            public string ItemName { get; init; } = string.Empty;

            public string CopyText { get; init; } = string.Empty;

            public string BoxCountText { get; init; } = string.Empty;

            public string BoxLocation { get; init; } = string.Empty;

            public string BoxSpecification { get; init; } = string.Empty;
        }
    }
}
