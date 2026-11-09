using System.IO;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace DocMgr.Services.Projects;

public partial class ProjectService
{
    private const string ExcelSheetName = "项目信息";
    private const string ExcelColumnName = "项目名称";
    private const string ExcelColumnCode = "项目编码";
    private const string ExcelColumnYear = "实施年度";
    private const string ExcelColumnDept = "资金管理部门";
    private const string ExcelColumnRemark = "备注";

    private static readonly string[] ExcelColumns =
    [
        ExcelColumnName,
        ExcelColumnCode,
        ExcelColumnYear,
        ExcelColumnDept,
        ExcelColumnRemark
    ];

    /// <inheritdoc />
    public void ExportProjects(string filePath, IReadOnlyList<ProjectInfo> projects)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(projects);

        using var workbook = new XSSFWorkbook();
        ISheet sheet = workbook.CreateSheet(ExcelSheetName);
        IRow headerRow = sheet.CreateRow(0);
        for (int index = 0; index < ExcelColumns.Length; index++)
        {
            headerRow.CreateCell(index).SetCellValue(ExcelColumns[index]);
        }

        for (int index = 0; index < projects.Count; index++)
        {
            ProjectInfo project = projects[index];
            IRow row = sheet.CreateRow(index + 1);
            row.CreateCell(0).SetCellValue(project.ProjectName ?? string.Empty);
            row.CreateCell(1).SetCellValue(project.ProjectCode ?? string.Empty);
            row.CreateCell(2).SetCellValue(project.ImplementYear ?? string.Empty);
            row.CreateCell(3).SetCellValue(project.CapitalMgrDept ?? string.Empty);
            row.CreateCell(4).SetCellValue(project.Remark ?? string.Empty);
        }

        int[] widths = [36, 18, 14, 22, 40];
        for (int index = 0; index < widths.Length; index++)
        {
            sheet.SetColumnWidth(index, widths[index] * 256);
        }

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        workbook.Write(stream, leaveOpen: false);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetProjectExcelSheetNames(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return UseWorkbook(filePath, workbook =>
        {
            var names = new List<string>(workbook.NumberOfSheets);
            for (int index = 0; index < workbook.NumberOfSheets; index++)
            {
                names.Add(workbook.GetSheetName(index));
            }

            return (IReadOnlyList<string>)names;
        });
    }

    /// <inheritdoc />
    public string ImportProjects(string filePath, string sheetName)
    {
        ProjectSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);

        ProjectExcelReadResult read = ReadProjectRows(filePath, sheetName);
        if (read.Rows.Count == 0 && read.IncompleteMessages.Count == 0)
        {
            return "Excel 中没有可导入的项目。";
        }

        List<ProjectExcelRow> rows = read.Rows;
        List<string> problems = new();
        List<ProjectInfo> toAdd = new();
        int skippedSameCount = 0;
        Dictionary<(string Name, string Year), List<ProjectInfo>> existingByKey = _dbContext.ProjectInfos
            .AsNoTracking()
            .AsEnumerable()
            .GroupBy(project => ProjectKey(project.ProjectName, project.ImplementYear))
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (IGrouping<(string Name, string Year), ProjectExcelRow> group in rows
            .GroupBy(row => ProjectKey(row.Name, row.Year))
            .OrderBy(group => group.Min(row => row.RowNumber)))
        {
            List<ProjectExcelRow> groupRows = group.ToList();
            if (groupRows.Select(PayloadKey).Distinct().Count() > 1)
            {
                problems.Add($"{FormatRows(groupRows)}同为「{group.Key.Name}」（{group.Key.Year}），文件内内容不一致，未导入。");
                continue;
            }

            ProjectExcelRow row = groupRows[0];
            if (!existingByKey.TryGetValue(group.Key, out List<ProjectInfo>? matches))
            {
                toAdd.Add(new ProjectInfo
                {
                    ProjectName = row.Name,
                    ProjectCode = row.Code,
                    ImplementYear = row.Year,
                    CapitalMgrDept = row.Dept,
                    Remark = row.Remark
                });
                skippedSameCount += groupRows.Count - 1;
                continue;
            }

            if (matches.Count > 1)
            {
                problems.Add($"{FormatRows(groupRows)}「{row.Name}」（{row.Year}）在库内已有 {matches.Count} 条，无法对应，未导入。");
                continue;
            }

            if (SamePayload(matches[0], row))
            {
                skippedSameCount += groupRows.Count;
                continue;
            }

            problems.Add($"{FormatRows(groupRows)}「{row.Name}」（{row.Year}）已在库内，但项目编码、资金管理部门或备注与库内不同，未导入，也未修改库内记录。");
        }

        if (toAdd.Count > 0)
        {
            foreach (ProjectInfo project in toAdd)
            {
                _projectRepository.Add(project);
            }

            _projectRepository.SaveChanges();
        }

        var lines = new List<string>
        {
            $"新增 {toAdd.Count} 条。",
            $"与库内完全相同、已跳过 {skippedSameCount} 条。"
        };
        if (read.IncompleteMessages.Count > 0)
        {
            lines.Add($"有 {read.IncompleteMessages.Count} 处因缺少项目名称或实施年度未导入：");
            lines.AddRange(read.IncompleteMessages.Take(30));
            if (read.IncompleteMessages.Count > 30)
            {
                lines.Add($"其余 {read.IncompleteMessages.Count - 30} 处未列出。");
            }
        }

        if (problems.Count == 0)
        {
            lines.Add("没有冲突行。");
        }
        else
        {
            lines.Add($"未导入 {problems.Count} 组冲突行，库内原记录保持不变：");
            lines.AddRange(problems.Take(30));
            if (problems.Count > 30)
            {
                lines.Add($"其余 {problems.Count - 30} 组未列出。");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ProjectExcelReadResult ReadProjectRows(string filePath, string sheetName)
    {
        return UseWorkbook(filePath, workbook =>
        {
            ISheet? sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
            {
                throw new InvalidOperationException($"未找到工作表「{sheetName}」。");
            }

            IRow? headerRow = sheet.GetRow(sheet.FirstRowNum);
            if (headerRow == null)
            {
                throw new InvalidOperationException("Excel 工作表是空的。");
            }

            var formatter = new DataFormatter();
            Dictionary<string, int> headers = ReadHeaders(headerRow, formatter);
            string[] missing = [ExcelColumnName, ExcelColumnYear];
            string[] absent = missing.Where(name => !headers.ContainsKey(name)).ToArray();
            if (absent.Length > 0)
            {
                throw new InvalidOperationException($"导入表缺少列：{string.Join("、", absent)}。请使用本页导出的 Excel。");
            }

            Dictionary<(int Row, int Column), string> mergedValues = ReadMergedCellValues(sheet, formatter);
            bool hasYearColumn = headers.TryGetValue(ExcelColumnYear, out int yearColumn);
            var rows = new List<ProjectExcelRow>();
            var incomplete = new List<IncompleteProjectExcelRow>();
            for (int index = headerRow.RowNum + 1; index <= sheet.LastRowNum; index++)
            {
                IRow? row = sheet.GetRow(index);
                if (row == null)
                {
                    continue;
                }

                string name = CollapseLineBreaks(ReadCell(row, headers, ExcelColumnName, formatter, mergedValues));
                string code = ReadCell(row, headers, ExcelColumnCode, formatter, mergedValues);
                string year = NormalizeYear(CollapseLineBreaks(ReadCell(row, headers, ExcelColumnYear, formatter, mergedValues)));
                string dept = ReadCell(row, headers, ExcelColumnDept, formatter, mergedValues);
                string remark = ReadCell(row, headers, ExcelColumnRemark, formatter, mergedValues);
                if (string.IsNullOrWhiteSpace(name)
                    && string.IsNullOrWhiteSpace(code)
                    && string.IsNullOrWhiteSpace(year)
                    && string.IsNullOrWhiteSpace(dept)
                    && string.IsNullOrWhiteSpace(remark))
                {
                    continue;
                }

                bool yearMissing = string.IsNullOrWhiteSpace(year);
                bool nameMissing = string.IsNullOrWhiteSpace(name);
                if (yearMissing || nameMissing)
                {
                    bool yearMerged = yearMissing
                        && hasYearColumn
                        && IsInMergedRegion(sheet, index, yearColumn);
                    incomplete.Add(new IncompleteProjectExcelRow(index + 1, yearMissing, nameMissing, yearMerged));
                    continue;
                }

                rows.Add(new ProjectExcelRow(index + 1, name, code, year, dept, remark));
            }

            return new ProjectExcelReadResult(rows, FormatIncompleteRows(incomplete));
        });
    }

    /// <summary>
    /// 合并单元格的值只写在左上角。把该值填到区域内其余格子，供年度列跨行合并时读取。
    /// </summary>
    private static Dictionary<(int Row, int Column), string> ReadMergedCellValues(ISheet sheet, DataFormatter formatter)
    {
        var values = new Dictionary<(int Row, int Column), string>();
        for (int regionIndex = 0; regionIndex < sheet.NumMergedRegions; regionIndex++)
        {
            CellRangeAddress region = sheet.GetMergedRegion(regionIndex);
            IRow? anchorRow = sheet.GetRow(region.FirstRow);
            string anchor = formatter.FormatCellValue(anchorRow?.GetCell(region.FirstColumn)) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(anchor))
            {
                continue;
            }

            for (int rowIndex = region.FirstRow; rowIndex <= region.LastRow; rowIndex++)
            {
                for (int columnIndex = region.FirstColumn; columnIndex <= region.LastColumn; columnIndex++)
                {
                    values[(rowIndex, columnIndex)] = anchor;
                }
            }
        }

        return values;
    }

    private static bool IsInMergedRegion(ISheet sheet, int rowIndex, int columnIndex)
    {
        for (int regionIndex = 0; regionIndex < sheet.NumMergedRegions; regionIndex++)
        {
            if (sheet.GetMergedRegion(regionIndex).IsInRange(rowIndex, columnIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> FormatIncompleteRows(IReadOnlyList<IncompleteProjectExcelRow> rows)
    {
        var messages = new List<string>();
        int index = 0;
        while (index < rows.Count)
        {
            IncompleteProjectExcelRow first = rows[index];
            int end = index;
            while (end + 1 < rows.Count
                && rows[end + 1].RowNumber == rows[end].RowNumber + 1
                && rows[end + 1].YearMissing == first.YearMissing
                && rows[end + 1].NameMissing == first.NameMissing
                && rows[end + 1].YearMerged == first.YearMerged)
            {
                end++;
            }

            messages.Add(DescribeIncompleteRange(rows[index].RowNumber, rows[end].RowNumber, first));
            index = end + 1;
        }

        return messages;
    }

    private static string DescribeIncompleteRange(int firstRow, int lastRow, IncompleteProjectExcelRow sample)
    {
        string rows = firstRow == lastRow
            ? $"第 {firstRow} 行"
            : $"第 {firstRow}–{lastRow} 行";
        if (sample.NameMissing && sample.YearMissing)
        {
            return $"{rows}缺少项目名称和实施年度，未导入。";
        }

        if (sample.YearMissing && sample.YearMerged)
        {
            return $"{rows}已有项目名称，但「实施年度」是合并单元格且没有填写年度，未导入。";
        }

        if (sample.YearMissing)
        {
            return $"{rows}缺少实施年度，未导入。";
        }

        return $"{rows}缺少项目名称，未导入。";
    }

    /// <summary>去掉单元格内的换行，避免合并换行或尾部回车被当成项目名称的一部分。</summary>
    private static string CollapseLineBreaks(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("_x000D_", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static T UseWorkbook<T>(string filePath, Func<IWorkbook, T> read)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using IWorkbook workbook = WorkbookFactory.Create(stream);
        return read(workbook);
    }

    private static Dictionary<string, int> ReadHeaders(IRow headerRow, DataFormatter formatter)
    {
        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < headerRow.LastCellNum; index++)
        {
            string name = formatter.FormatCellValue(headerRow.GetCell(index))?.Trim() ?? string.Empty;
            if (string.Equals(name, "项目编号", StringComparison.Ordinal))
            {
                name = ExcelColumnCode;
            }

            if (string.IsNullOrWhiteSpace(name) || headers.ContainsKey(name))
            {
                continue;
            }

            headers[name] = index;
        }

        return headers;
    }

    private static string ReadCell(
        IRow row,
        IReadOnlyDictionary<string, int> headers,
        string column,
        DataFormatter formatter,
        IReadOnlyDictionary<(int Row, int Column), string> mergedValues)
    {
        if (!headers.TryGetValue(column, out int index))
        {
            return string.Empty;
        }

        string text = formatter.FormatCellValue(row.GetCell(index))?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)
            && mergedValues.TryGetValue((row.RowNum, index), out string? merged))
        {
            text = merged.Trim();
        }

        return text;
    }

    /// <summary>Excel 把年度存成数字时，去掉末尾的 .0。</summary>
    private static string NormalizeYear(string year)
    {
        if (year.EndsWith(".0", StringComparison.Ordinal)
            && year.Length > 2
            && year[..^2].All(char.IsDigit))
        {
            return year[..^2];
        }

        return year;
    }

    private static (string Name, string Year) ProjectKey(string? name, string? year) =>
        (name?.Trim() ?? string.Empty, year?.Trim() ?? string.Empty);

    private static (string Code, string Dept, string Remark) PayloadKey(ProjectExcelRow row) =>
        (row.Code, row.Dept, row.Remark);

    private static bool SamePayload(ProjectInfo existing, ProjectExcelRow row) =>
        string.Equals(existing.ProjectCode?.Trim() ?? string.Empty, row.Code, StringComparison.Ordinal)
        && string.Equals(existing.CapitalMgrDept?.Trim() ?? string.Empty, row.Dept, StringComparison.Ordinal)
        && string.Equals(existing.Remark?.Trim() ?? string.Empty, row.Remark, StringComparison.Ordinal);

    private static string FormatRows(IEnumerable<ProjectExcelRow> rows)
    {
        string numbers = string.Join("、", rows.Select(row => row.RowNumber));
        return $"第 {numbers} 行";
    }

    private sealed record ProjectExcelRow(int RowNumber, string Name, string Code, string Year, string Dept, string Remark);

    private sealed record ProjectExcelReadResult(
        List<ProjectExcelRow> Rows,
        IReadOnlyList<string> IncompleteMessages);

    private sealed record IncompleteProjectExcelRow(int RowNumber, bool YearMissing, bool NameMissing, bool YearMerged);
}
