using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocMgr.Models.YearlyArchive;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 存档文本直办：Excel 解析、模板导出与子类名称映射。
    /// </summary>
    public static class StockTextArchiveExcelImportSupport
    {
        public const string SheetNameImport = "存档文本直办录入";
        public const string SheetNameInstructions = "字段说明";

        public const string ColumnSequence = "序号";
        public const string ColumnYear = "年度";
        public const string ColumnProjectName = "项目名称";
        public const string ColumnProjectCode = "项目编号";
        public const string ColumnMaterialName = "资料名称";
        public const string ColumnSourceType = "来源";
        public const string ColumnProvideUnit = "提供单位";
        public const string ColumnArchivePurpose = "库管模式";
        public const string ColumnMediaType = "载体类型";
        public const string ColumnItemName = "子项名称";
        public const string ColumnCopyCount = "份数";
        public const string ColumnConfidentialLevel = "密级";
        public const string ColumnMaterialCategory = "资料类型";
        public const string ColumnSubCategory = "所属子类";
        public const string ColumnOrganizationForm = "组织形式";
        public const string ColumnItemNote = "子项备注";
        public const string ColumnBoxCount = "盒数";
        public const string ColumnBoxLocation = "档案盒编号";
        public const string ColumnBoxSpecification = "档案盒规格";
        public const string ColumnBoxRemarks = "盒备注";

        /// <summary>导入模板表头顺序（与交互式录入字段对齐）。</summary>
        public static readonly string[] TemplateHeaders =
        {
            ColumnSequence,
            ColumnYear,
            ColumnProjectName,
            ColumnProjectCode,
            ColumnMaterialName,
            ColumnSourceType,
            ColumnProvideUnit,
            ColumnArchivePurpose,
            ColumnMediaType,
            ColumnBoxCount,
            ColumnBoxLocation,
            ColumnBoxSpecification,
            ColumnBoxRemarks,
            ColumnItemName,
            ColumnCopyCount,
            ColumnConfidentialLevel,
            ColumnMaterialCategory,
            ColumnSubCategory,
            ColumnOrganizationForm,
            ColumnItemNote
        };

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
        /// 导出 Excel 导入模板（录入表 + 字段说明）。
        /// </summary>
        public static void ExportImportTemplate(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("导出文件路径不能为空。", nameof(filePath));
            }

            string? directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using var workbook = new XSSFWorkbook();
            BuildTemplateSheet(workbook);
            BuildInstructionSheet(workbook);

            using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(outputStream, leaveOpen: true);
        }

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
            string projectCode = string.Empty;
            string materialName = string.Empty;
            string sourceType = string.Empty;
            string provideUnit = string.Empty;
            string archivePurpose = string.Empty;
            string mediaType = string.Empty;
            string boxCountText = string.Empty;
            string boxLocation = string.Empty;
            string boxSpecification = string.Empty;
            string boxRemarks = string.Empty;

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
                projectCode = FillDown(projectCode, ReadCell(row, headerMap, ColumnProjectCode, formatter));
                materialName = FillDown(materialName, ReadCell(row, headerMap, ColumnMaterialName, formatter));
                sourceType = FillDown(sourceType, ReadCell(row, headerMap, ColumnSourceType, formatter));
                provideUnit = FillDown(provideUnit, ReadCell(row, headerMap, ColumnProvideUnit, formatter));
                archivePurpose = FillDown(archivePurpose, ReadCell(row, headerMap, ColumnArchivePurpose, formatter));
                mediaType = FillDown(mediaType, ReadCell(row, headerMap, ColumnMediaType, formatter));
                boxCountText = FillDown(boxCountText, ReadCell(row, headerMap, ColumnBoxCount, formatter));
                boxLocation = FillDown(boxLocation, ReadCell(row, headerMap, ColumnBoxLocation, formatter));
                boxSpecification = FillDown(boxSpecification, ReadCell(row, headerMap, ColumnBoxSpecification, formatter));
                boxRemarks = FillDown(boxRemarks, ReadCell(row, headerMap, ColumnBoxRemarks, formatter));

                string itemName = ReadCell(row, headerMap, ColumnItemName, formatter);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                string copyText = ReadCell(row, headerMap, ColumnCopyCount, formatter);
                string confidentialLevel = ReadCell(row, headerMap, ColumnConfidentialLevel, formatter);
                string materialCategory = ReadCell(row, headerMap, ColumnMaterialCategory, formatter);
                string subCategory = ReadCell(row, headerMap, ColumnSubCategory, formatter);
                string organizationForm = ReadCell(row, headerMap, ColumnOrganizationForm, formatter);
                string itemNote = ReadCell(row, headerMap, ColumnItemNote, formatter);

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
                        ProjectCode = projectCode,
                        MaterialName = materialName,
                        SourceType = sourceType,
                        ProvideUnit = provideUnit,
                        ArchivePurpose = archivePurpose,
                        MediaType = mediaType,
                        ItemName = name.Trim(),
                        CopyText = copyText,
                        ConfidentialLevel = confidentialLevel,
                        MaterialCategory = materialCategory,
                        SubCategory = subCategory,
                        OrganizationForm = organizationForm,
                        ItemNote = itemNote,
                        BoxCountText = boxCountText,
                        BoxLocation = boxLocation,
                        BoxSpecification = boxSpecification,
                        BoxRemarks = boxRemarks
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
                    ArchiveRegisterDomainValues.SimulatedSubCategoryInspectionRecord);
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

        private static void BuildTemplateSheet(XSSFWorkbook workbook)
        {
            var sheet = workbook.CreateSheet(SheetNameImport);
            var header = sheet.CreateRow(0);
            for (int i = 0; i < TemplateHeaders.Length; i++)
            {
                header.CreateCell(i).SetCellValue(TemplateHeaders[i]);
                sheet.SetColumnWidth(i, ResolveColumnWidth(TemplateHeaders[i]));
            }

            // 示例行：同盒两条子项，演示盒级字段可下填留空。
            WriteExampleRow(
                sheet.CreateRow(1),
                sequence: "1",
                year: "2024",
                projectName: "示例项目名称",
                projectCode: "XM-2024-001",
                materialName: "归档资料",
                sourceType: ArchiveRegisterDomainValues.SourceTypeInternal,
                provideUnit: ArchiveRegisterDomainValues.ProvideUnitArchiveRoom,
                archivePurpose: ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage,
                mediaType: ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper,
                boxCount: "1",
                boxLocation: "丙A-3-2-01",
                boxSpecification: "标准(5cm)",
                boxRemarks: string.Empty,
                itemName: "技术设计书",
                copyCount: "1",
                confidentialLevel: "秘密",
                materialCategory: ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                subCategory: ArchiveRegisterDomainValues.SimulatedSubCategoryPlanningDesign,
                organizationForm: ArchiveRegisterDomainValues.SimulatedOrganizationFormBound,
                itemNote: string.Empty);

            WriteExampleRow(
                sheet.CreateRow(2),
                sequence: string.Empty,
                year: string.Empty,
                projectName: string.Empty,
                projectCode: string.Empty,
                materialName: string.Empty,
                sourceType: string.Empty,
                provideUnit: string.Empty,
                archivePurpose: string.Empty,
                mediaType: string.Empty,
                boxCount: string.Empty,
                boxLocation: string.Empty,
                boxSpecification: string.Empty,
                boxRemarks: string.Empty,
                itemName: "检查记录",
                copyCount: "1",
                confidentialLevel: "秘密",
                materialCategory: ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                subCategory: ArchiveRegisterDomainValues.SimulatedSubCategoryInspectionRecord,
                organizationForm: ArchiveRegisterDomainValues.SimulatedOrganizationFormBound,
                itemNote: string.Empty);
        }

        private static void WriteExampleRow(
            IRow row,
            string sequence,
            string year,
            string projectName,
            string projectCode,
            string materialName,
            string sourceType,
            string provideUnit,
            string archivePurpose,
            string mediaType,
            string boxCount,
            string boxLocation,
            string boxSpecification,
            string boxRemarks,
            string itemName,
            string copyCount,
            string confidentialLevel,
            string materialCategory,
            string subCategory,
            string organizationForm,
            string itemNote)
        {
            string[] values =
            {
                sequence,
                year,
                projectName,
                projectCode,
                materialName,
                sourceType,
                provideUnit,
                archivePurpose,
                mediaType,
                boxCount,
                boxLocation,
                boxSpecification,
                boxRemarks,
                itemName,
                copyCount,
                confidentialLevel,
                materialCategory,
                subCategory,
                organizationForm,
                itemNote
            };

            for (int i = 0; i < values.Length; i++)
            {
                row.CreateCell(i).SetCellValue(values[i]);
            }
        }

        private static void BuildInstructionSheet(XSSFWorkbook workbook)
        {
            var sheet = workbook.CreateSheet(SheetNameInstructions);
            sheet.SetColumnWidth(0, 18 * 256);
            sheet.SetColumnWidth(1, 72 * 256);

            var lines = new (string Field, string Hint)[]
            {
                ("填写说明", "一行一条资料子项；同一档案盒的盒级字段可只在首行填写，后续行留空自动下填。导入前请删除示例行或改成真实数据。"),
                (ColumnSequence, "可选；用于核对同一项目下的盒数。"),
                (ColumnYear, "必填；四位数字年份（实施年度）。"),
                (ColumnProjectName, "必填；可手输新项目，系统按年度+名称匹配或新建。"),
                (ColumnProjectCode, "可选；新建项目时可一并写入项目编号。"),
                (ColumnMaterialName, "必填；资料名称。"),
                (ColumnSourceType, $"必填；{ArchiveRegisterDomainValues.SourceTypeInternal} / {ArchiveRegisterDomainValues.SourceTypeExternal}。空白时默认「{ArchiveRegisterDomainValues.SourceTypeInternal}」。"),
                (ColumnProvideUnit, $"必填；来源为「{ArchiveRegisterDomainValues.SourceTypeInternal}」时填「{ArchiveRegisterDomainValues.ProvideUnitArchiveRoom}」；来源为「{ArchiveRegisterDomainValues.SourceTypeExternal}」时填写具体提供单位。"),
                (ColumnArchivePurpose, $"必填；院管资料、短期存档 / {ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage} / {ArchiveRegisterDomainValues.ArchivePurposeExternalEntrusted}。空白时默认「{ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage}」。外部委托仅当来源为外来时可选。"),
                (ColumnMediaType, "必填；" + string.Join("、", ArchiveRegisterDomainValues.StockTextArchiveMediaTypes) + $"。空白时默认「{ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper}」。同一盒须同一种载体类型。"),
                (ColumnBoxCount, "可选；同一序号下声明的盒数，用于与实际档案盒编号数量核对。"),
                (ColumnBoxLocation, "必填；物理位置，格式如 丙A-3-2-01（柜面-层-列-盒序号）。"),
                (ColumnBoxSpecification, "必填；标准(10cm)、标准(5cm)、标准(3cm)、标准(2cm)、非标(10cm)。"),
                (ColumnBoxRemarks, "可选；写入档案盒备注。"),
                (ColumnItemName, "必填；资料子项名称。可勾选「以文本行为单位展开」将单元格内多行拆成多条子项。"),
                (ColumnCopyCount, "必填；整数，不少于 1。"),
                (ColumnConfidentialLevel, "必填；否 / 秘密 / 机密 / 绝密。空白时默认「秘密」。"),
                (ColumnMaterialCategory, $"必填；{ArchiveRegisterDomainValues.SimulatedMaterialCategoryText} / {ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap}。空白时按子项名称自动推断。"),
                (ColumnSubCategory, "必填；文本类：策划设计类、检查记录类、总结报告类、其他；图件类：原始图件类、过程图件类、成果图件类、其他。空白时按子项名称自动推断。"),
                (ColumnOrganizationForm, $"必填；{ArchiveRegisterDomainValues.SimulatedOrganizationFormLoose} / {ArchiveRegisterDomainValues.SimulatedOrganizationFormBound}。空白时默认「{ArchiveRegisterDomainValues.SimulatedOrganizationFormBound}」。"),
                (ColumnItemNote, "可选；写入资料子项备注。")
            };

            for (int i = 0; i < lines.Length; i++)
            {
                var row = sheet.CreateRow(i);
                row.CreateCell(0).SetCellValue(lines[i].Field);
                row.CreateCell(1).SetCellValue(lines[i].Hint);
            }
        }

        private static int ResolveColumnWidth(string header)
        {
            return header switch
            {
                ColumnProjectName or ColumnMaterialName or ColumnItemName or ColumnProvideUnit
                    or ColumnArchivePurpose or ColumnBoxLocation or ColumnBoxRemarks or ColumnItemNote
                    => 22 * 256,
                ColumnProjectCode or ColumnMediaType or ColumnBoxSpecification or ColumnMaterialCategory
                    or ColumnSubCategory or ColumnOrganizationForm
                    => 16 * 256,
                _ => 12 * 256
            };
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

                boxes.Add(CloneBox(box, actualCount, errors));
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
                    return CloneBox(box, box.ActualBoxCountInSequence, errors);
                }).ToList();
            }

            return new StockTextArchiveExcelParseResult { Boxes = boxes };
        }

        private static StockTextArchiveExcelBoxDraft CloneBox(
            StockTextArchiveExcelBoxDraft box,
            int actualBoxCountInSequence,
            IReadOnlyList<string> errors)
        {
            return new StockTextArchiveExcelBoxDraft
            {
                SequenceNo = box.SequenceNo,
                FirstRowNumber = box.FirstRowNumber,
                Year = box.Year,
                ProjectName = box.ProjectName,
                ProjectCode = box.ProjectCode,
                MaterialName = box.MaterialName,
                SourceType = box.SourceType,
                ProvideUnit = box.ProvideUnit,
                ArchivePurpose = box.ArchivePurpose,
                MediaType = box.MediaType,
                BoxSpecification = box.BoxSpecification,
                Remarks = box.Remarks,
                SourceBoxLocationCode = box.SourceBoxLocationCode,
                CabinetName = box.CabinetName,
                Side = box.Side,
                Row = box.Row,
                Column = box.Column,
                BoxIndex = box.BoxIndex,
                NormalizedBoxLocationCode = box.NormalizedBoxLocationCode,
                ClaimedBoxCount = box.ClaimedBoxCount,
                ActualBoxCountInSequence = actualBoxCountInSequence,
                ParseErrors = errors,
                Items = box.Items
            };
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

            string sourceType = string.IsNullOrWhiteSpace(first.SourceType)
                ? ArchiveRegisterDomainValues.SourceTypeInternal
                : first.SourceType.Trim();
            if (!string.Equals(sourceType, ArchiveRegisterDomainValues.SourceTypeInternal, StringComparison.Ordinal)
                && !string.Equals(sourceType, ArchiveRegisterDomainValues.SourceTypeExternal, StringComparison.Ordinal))
            {
                errors.Add($"来源 [{sourceType}] 须为「内部」或「外来」。");
            }

            string provideUnit = first.ProvideUnit?.Trim() ?? string.Empty;
            if (string.Equals(sourceType, ArchiveRegisterDomainValues.SourceTypeInternal, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(provideUnit))
                {
                    provideUnit = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom;
                }
                else if (!string.Equals(provideUnit, ArchiveRegisterDomainValues.ProvideUnitArchiveRoom, StringComparison.Ordinal))
                {
                    errors.Add("内部资料的提供单位必须为「资料室」。");
                }
            }
            else if (string.Equals(sourceType, ArchiveRegisterDomainValues.SourceTypeExternal, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(provideUnit))
            {
                errors.Add("外来资料必须填写提供单位。");
            }

            string archivePurpose = string.IsNullOrWhiteSpace(first.ArchivePurpose)
                ? ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage
                : first.ArchivePurpose.Trim();
            if (ArchiveRegisterDomainValues.IsExternalEntrustedArchivePurpose(archivePurpose)
                && !string.Equals(sourceType, ArchiveRegisterDomainValues.SourceTypeExternal, StringComparison.Ordinal))
            {
                errors.Add(
                    $"库管模式「{ArchiveRegisterDomainValues.ArchivePurposeExternalEntrusted}」仅当来源为「{ArchiveRegisterDomainValues.SourceTypeExternal}」时可选。");
            }

            string mediaType = string.IsNullOrWhiteSpace(first.MediaType)
                ? ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper
                : first.MediaType.Trim();
            if (!ArchiveRegisterDomainValues.IsSimulatedDataMediaType(mediaType))
            {
                errors.Add($"载体类型 [{mediaType}] 不在允许范围内。");
            }

            var distinctMediaTypes = rows
                .Select(item => string.IsNullOrWhiteSpace(item.MediaType)
                    ? mediaType
                    : item.MediaType.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (distinctMediaTypes.Count > 1)
            {
                errors.Add("同一档案盒只能使用同一种载体类型。");
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

                string confidential = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(raw.ConfidentialLevel);
                if (string.IsNullOrWhiteSpace(confidential))
                {
                    confidential = "秘密";
                }

                string materialCategory = raw.MaterialCategory?.Trim() ?? string.Empty;
                string subCategory = raw.SubCategory?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(materialCategory) || string.IsNullOrWhiteSpace(subCategory))
                {
                    var mapped = MapItemClassification(raw.ItemName);
                    if (string.IsNullOrWhiteSpace(materialCategory))
                    {
                        materialCategory = mapped.MaterialCategory;
                    }

                    if (string.IsNullOrWhiteSpace(subCategory))
                    {
                        subCategory = mapped.SubCategory;
                    }
                }

                string organizationForm = string.IsNullOrWhiteSpace(raw.OrganizationForm)
                    ? ArchiveRegisterDomainValues.SimulatedOrganizationFormBound
                    : raw.OrganizationForm.Trim();

                items.Add(new StockTextArchiveMediaItemDraft
                {
                    ContentDesc = raw.ItemName,
                    ConfidentialLevel = confidential,
                    ContentCount = copyCount,
                    Note = raw.ItemNote?.Trim() ?? string.Empty,
                    MaterialCategory = materialCategory,
                    SubCategory = subCategory,
                    OrganizationForm = organizationForm,
                    SourceType = sourceType,
                    ProvideUnit = provideUnit
                });
            }

            if (items.Count == 0)
            {
                errors.Add("该档案盒没有资料子项。");
            }

            string remarks = first.BoxRemarks?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(remarks))
            {
                remarks = "Excel导入";
            }

            return new StockTextArchiveExcelBoxDraft
            {
                SequenceNo = sequenceNo,
                FirstRowNumber = first.ExcelRowNumber,
                Year = first.Year?.Trim() ?? string.Empty,
                ProjectName = first.ProjectName?.Trim() ?? string.Empty,
                ProjectCode = first.ProjectCode?.Trim() ?? string.Empty,
                MaterialName = first.MaterialName?.Trim() ?? string.Empty,
                SourceType = sourceType,
                ProvideUnit = provideUnit,
                ArchivePurpose = archivePurpose,
                MediaType = mediaType,
                BoxSpecification = spec,
                Remarks = remarks,
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

            public string ProjectCode { get; init; } = string.Empty;

            public string MaterialName { get; init; } = string.Empty;

            public string SourceType { get; init; } = string.Empty;

            public string ProvideUnit { get; init; } = string.Empty;

            public string ArchivePurpose { get; init; } = string.Empty;

            public string MediaType { get; init; } = string.Empty;

            public string ItemName { get; init; } = string.Empty;

            public string CopyText { get; init; } = string.Empty;

            public string ConfidentialLevel { get; init; } = string.Empty;

            public string MaterialCategory { get; init; } = string.Empty;

            public string SubCategory { get; init; } = string.Empty;

            public string OrganizationForm { get; init; } = string.Empty;

            public string ItemNote { get; init; } = string.Empty;

            public string BoxCountText { get; init; } = string.Empty;

            public string BoxLocation { get; init; } = string.Empty;

            public string BoxSpecification { get; init; } = string.Empty;

            public string BoxRemarks { get; init; } = string.Empty;
        }
    }
}
