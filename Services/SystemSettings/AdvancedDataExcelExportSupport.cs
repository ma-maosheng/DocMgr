using DocMgr.Models.SystemSettings;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;

namespace DocMgr.Services.SystemSettings
{
    /// <summary>
    /// 高级数据管理表数据 Excel 导出辅助。
    /// </summary>
    internal static class AdvancedDataExcelExportSupport
    {
        /// <summary>
        /// 将数据表写入 Excel：第 1 行为英文字段名，第 2 行为中文字段名，其后为数据行。
        /// </summary>
        public static void Write(
            string filePath,
            string sheetName,
            IReadOnlyList<TableFieldStructureDto> fields,
            DataTable data)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(fields);
            ArgumentNullException.ThrowIfNull(data);

            var directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            Directory.CreateDirectory(directoryPath);

            using var workbook = new XSSFWorkbook();
            var safeSheetName = ExcelSheetNameSupport.Sanitize(sheetName);
            var sheet = workbook.CreateSheet(safeSheetName);

            var englishHeaderRow = sheet.CreateRow(0);
            var chineseHeaderRow = sheet.CreateRow(1);
            for (int columnIndex = 0; columnIndex < fields.Count; columnIndex++)
            {
                var field = fields[columnIndex];
                englishHeaderRow.CreateCell(columnIndex).SetCellValue(field.FieldName);
                chineseHeaderRow.CreateCell(columnIndex).SetCellValue(field.DisplayName);
                sheet.SetColumnWidth(columnIndex, 18 * 256);
            }

            for (int rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
            {
                var sourceRow = data.Rows[rowIndex];
                var targetRow = sheet.CreateRow(rowIndex + 2);

                for (int columnIndex = 0; columnIndex < fields.Count; columnIndex++)
                {
                    var fieldName = fields[columnIndex].FieldName;
                    var cellValue = data.Columns.Contains(fieldName)
                        ? sourceRow[fieldName]
                        : DBNull.Value;
                    SetCellValue(targetRow.CreateCell(columnIndex), cellValue);
                }
            }

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(fileStream, leaveOpen: false);
        }

        /// <summary>
        /// 清理文件名中的非法字符。
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "export";
            }

            var sanitized = fileName.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "export" : sanitized;
        }

        private static void SetCellValue(ICell cell, object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                cell.SetBlank();
                return;
            }

            switch (value)
            {
                case byte[] bytes:
                    cell.SetCellValue($"[二进制 {bytes.Length} 字节]");
                    break;
                case bool boolValue:
                    cell.SetCellValue(boolValue);
                    break;
                case DateTime dateTimeValue:
                    cell.SetCellValue(dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    break;
                case DateTimeOffset dateTimeOffsetValue:
                    cell.SetCellValue(dateTimeOffsetValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    break;
                case IFormattable formattable when value is not string:
                    cell.SetCellValue(formattable.ToString(null, CultureInfo.InvariantCulture));
                    break;
                default:
                    cell.SetCellValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                    break;
            }
        }
    }
}
