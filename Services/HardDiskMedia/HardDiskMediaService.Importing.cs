using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质导入模板与批量导入相关逻辑。
    /// </summary>
    public partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetImportSheetNamesAsync(string filePath)
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

        /// <inheritdoc/>
        public Task<bool> HasMediaRecordsAsync()
        {
            return _hardDiskMediaRepository.HasMediaRecordsAsync();
        }

        /// <inheritdoc/>
        public string GetMediaImportTemplateDescription()
        {
            return "导入模板字段：硬盘编号*、序列号*、硬盘类型*、品牌*、容量、接口类型、出厂日期、当前存放位置、备注。"
                + " 若未填写当前存放位置，系统将按防磁磁盘柜空白专用档口用途与档口容量（10盘/档口）自动入位；"
                + " 导入完成后请资料室管理员前往【硬盘台账】核对存放位置。";
        }

        /// <inheritdoc/>
        public async Task ExportMediaImportTemplateAsync(string filePath)
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

            var diskTypeOptions = await GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.DiskType));
            var brandOptions = await GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.Brand));
            var interfaceTypeOptions = await GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.InterfaceType));
            var statusOptions = await GetDomainOptionLabelsAsync(nameof(HardDiskLedger), nameof(HardDiskLedger.MediaStatus));
            var natureOptions = await GetDomainOptionLabelsAsync(nameof(HardDiskLedger), nameof(HardDiskLedger.MediaNature));

            await Task.Run(() =>
            {
                using var workbook = new XSSFWorkbook();
                BuildTemplateSheet(workbook);
                BuildInstructionSheet(workbook, diskTypeOptions, brandOptions, interfaceTypeOptions, statusOptions, natureOptions);

                using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                workbook.Write(outputStream, true);
            });
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaImportResult> ImportMediaAsync(string filePath, string sheetName, ImportMode importMode, User? currentUser)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("请选择导入文件。", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("导入文件不存在。", filePath);
            }

            if (string.IsNullOrWhiteSpace(sheetName))
            {
                throw new ArgumentException("请选择导入工作表。", nameof(sheetName));
            }

            var importedRows = await Task.Run(() => ParseImportMedia(filePath, sheetName, currentUser));
            if (importedRows.Count == 0)
            {
                throw new HardDiskMediaImportException("未解析到有效的硬盘介质数据。");
            }

            ValidateImportedMedia(importedRows);

            int clearedCount = 0;
            await using var transaction = await _hardDiskMediaRepository.BeginTransactionAsync();

            try
            {
                if (importMode == ImportMode.Recreate)
                {
                    bool hasApplications = await _hardDiskMediaRepository.HasAnyApplicationsAsync();
                    bool hasTransactions = await _hardDiskMediaRepository.HasAnyTransactionsAsync();
                    if (hasApplications || hasTransactions)
                    {
                        throw new InvalidOperationException("当前已存在申请单或流转记录，不允许执行覆盖导入。请使用追加导入。");
                    }

                    clearedCount = await _hardDiskMediaRepository.GetMediaCountAsync();
                    await _hardDiskMediaRepository.DeleteAllMediaAsync();
                }
                else
                {
                    await EnsureNoConflictsWithExistingMediaAsync(importedRows);
                }

                var importedItems = importedRows.Select(item => item.Medium).ToList();
                await _hardDiskMediaRepository.AddMediaRangeAsync(importedItems);
                foreach (string diskType in importedItems.Select(item => item.DiskType).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await _hardDiskMediaRepository.EnsureEnabledDomainOptionAsync(
                        nameof(HardDiskMedium),
                        nameof(HardDiskMedium.DiskType),
                        "硬盘类型",
                        diskType);
                }

                foreach (string brand in importedItems.Select(item => item.Brand).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await _hardDiskMediaRepository.EnsureEnabledDomainOptionAsync(
                        nameof(HardDiskMedium),
                        nameof(HardDiskMedium.Brand),
                        "品牌",
                        brand);
                }

                foreach (string interfaceType in importedItems.Select(item => item.InterfaceType).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await _hardDiskMediaRepository.EnsureEnabledDomainOptionAsync(
                        nameof(HardDiskMedium),
                        nameof(HardDiskMedium.InterfaceType),
                        "接口类型",
                        interfaceType);
                }

                await _hardDiskMediaRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                int assignedSlotCount = await AssignBlankInStockMediaToBlankSlotsInOrderAsync();

                return new HardDiskMediaImportResult
                {
                    Mode = importMode,
                    ImportedCount = importedItems.Count,
                    ClearedCount = clearedCount,
                    AssignedSlotCount = assignedSlotCount
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> AssignBlankInStockMediaToBlankSlotsInOrderAsync()
        {
            var orderedLocations = await GetOrderedBlankDedicatedSlotLocationCodesAsync();
            if (orderedLocations.Count == 0)
            {
                throw new InvalidOperationException("未找到空白硬盘专用档口，请确认防磁磁盘柜档口用途已配置。");
            }

            var mediaItems = await _hardDiskMediaRepository.GetBlankInStockMediaNeedingLocationAssignmentAsync();
            if (mediaItems.Count == 0)
            {
                return 0;
            }

            var occupancyBySlot = await _hardDiskMediaRepository.GetInStockBlankLedgerCountsBySlotCodesAsync(orderedLocations);
            int locationIndex = 0;
            int assignedCount = 0;
            DateTime now = DateTime.Now;

            foreach (var medium in mediaItems)
            {
                if (medium.Ledger == null)
                {
                    continue;
                }

                while (locationIndex < orderedLocations.Count)
                {
                    string slotCode = orderedLocations[locationIndex];
                    int currentCount = occupancyBySlot.TryGetValue(slotCode, out int count) ? count : 0;
                    if (currentCount < HardDiskBlankSlotLocationSupport.DefaultSlotCapacity)
                    {
                        medium.Ledger.StorageLocation = slotCode;
                        medium.Ledger.UpdatedTime = now;
                        medium.UpdatedTime = now;
                        occupancyBySlot[slotCode] = currentCount + 1;
                        assignedCount++;
                        break;
                    }

                    locationIndex++;
                }

                if (locationIndex >= orderedLocations.Count)
                {
                    throw new InvalidOperationException(
                        $"空白专用档口可用容量不足，尚有 {mediaItems.Count - assignedCount} 块硬盘未能按次序入位。");
                }
            }

            await _hardDiskMediaRepository.SaveChangesAsync();
            return assignedCount;
        }

        private async Task EnsureNoConflictsWithExistingMediaAsync(IReadOnlyCollection<ImportedMediumRow> importedItems)
        {
            var diskCodes = importedItems.Select(item => item.Medium.DiskCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var serialNumbers = importedItems.Select(item => item.Medium.SerialNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

            string? duplicateDiskCode = await _hardDiskMediaRepository.FindFirstDuplicateDiskCodeAsync(diskCodes);
            if (!string.IsNullOrWhiteSpace(duplicateDiskCode))
            {
                int rowNumber = importedItems.First(item => string.Equals(item.Medium.DiskCode, duplicateDiskCode, StringComparison.OrdinalIgnoreCase)).RowNumber;
                throw new HardDiskMediaImportException($"第 {rowNumber} 行的硬盘编号 [{duplicateDiskCode}] 与现有台账重复，无法执行追加导入。", rowNumber, "硬盘编号");
            }

            string? duplicateSerialNumber = await _hardDiskMediaRepository.FindFirstDuplicateSerialNumberAsync(serialNumbers);
            if (!string.IsNullOrWhiteSpace(duplicateSerialNumber))
            {
                int rowNumber = importedItems.First(item => string.Equals(item.Medium.SerialNumber, duplicateSerialNumber, StringComparison.OrdinalIgnoreCase)).RowNumber;
                throw new HardDiskMediaImportException($"第 {rowNumber} 行的序列号 [{duplicateSerialNumber}] 与现有台账重复，无法执行追加导入。", rowNumber, "序列号");
            }
        }

        private static void ValidateImportedMedia(IReadOnlyList<ImportedMediumRow> importedItems)
        {
            var duplicateDiskCode = importedItems.GroupBy(item => item.Medium.DiskCode, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
            if (duplicateDiskCode != null)
            {
                string rowNumbers = string.Join("、", duplicateDiskCode.Select(item => item.RowNumber));
                throw new HardDiskMediaImportException($"导入文件中硬盘编号 [{duplicateDiskCode.Key}] 重复，涉及第 {rowNumbers} 行。", duplicateDiskCode.First().RowNumber, "硬盘编号");
            }

            var duplicateSerialNumber = importedItems.GroupBy(item => item.Medium.SerialNumber, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
            if (duplicateSerialNumber != null)
            {
                string rowNumbers = string.Join("、", duplicateSerialNumber.Select(item => item.RowNumber));
                throw new HardDiskMediaImportException($"导入文件中序列号 [{duplicateSerialNumber.Key}] 重复，涉及第 {rowNumbers} 行。", duplicateSerialNumber.First().RowNumber, "序列号");
            }
        }

        private static List<ImportedMediumRow> ParseImportMedia(string filePath, string sheetName, User? currentUser)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = WorkbookFactory.Create(stream);
            var sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
            {
                throw new HardDiskMediaImportException($"未找到工作表 [{sheetName}]。");
            }

            var items = new List<ImportedMediumRow>();
            if (sheet.LastRowNum < 1)
            {
                return items;
            }

            var headerMap = ParseHeader(sheet.GetRow(0));
            ValidateRequiredHeaders(headerMap);
            var now = DateTime.Now;

            for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                if (row == null || RowIsEmpty(row))
                {
                    continue;
                }

                int excelRowNumber = rowIndex + 1;
                string diskCode = GetRequiredCellValue(row, headerMap, excelRowNumber, "硬盘编号", "介质编号");
                string serialNumber = GetRequiredCellValue(row, headerMap, excelRowNumber, "序列号", "硬盘序列号");
                string diskType = GetRequiredCellValue(row, headerMap, excelRowNumber, "硬盘类型", "介质类型");
                string brand = GetRequiredCellValue(row, headerMap, excelRowNumber, "品牌");

                items.Add(new ImportedMediumRow(excelRowNumber, new HardDiskMedium
                {
                    DiskCode = diskCode,
                    SerialNumber = serialNumber,
                    DiskType = diskType,
                    Brand = brand,
                    Capacity = GetOptionalCellValue(row, headerMap, "容量"),
                    InterfaceType = GetOptionalCellValue(row, headerMap, "接口类型", "接口"),
                    RegisterPerson = currentUser?.RealName?.Trim() ?? string.Empty,
                    RegisterDate = now,
                    FactoryDate = GetOptionalDateCellValue(row, headerMap, "出厂日期", "生产日期"),
                    RegistrationMethod = HardDiskMedium.RegistrationMethodImported,
                    Ledger = new HardDiskLedger
                    {
                        DiskCode = diskCode,
                        MediaStatus = HardDiskMedium.StatusInStockBlank,
                        MediaNature = HardDiskMedium.NatureBlank,
                        StorageLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(
                            GetOptionalCellValue(row, headerMap, "当前存放位置", "存放位置", "当前位置")),
                        HolderOrOrganization = "资料室",
                        NeedReturn = false,
                        RegisterPerson = currentUser?.RealName?.Trim() ?? string.Empty,
                        RegisterDate = now,
                        Remark = string.Empty,
                        CreatedTime = now,
                        UpdatedTime = now
                    },
                    IsDeleted = false,
                    CreatedTime = now,
                    UpdatedTime = now
                }));
            }

            return items;
        }

        private static Dictionary<string, int> ParseHeader(IRow? headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headerRow == null)
            {
                return map;
            }

            for (int i = 0; i < headerRow.LastCellNum; i++)
            {
                string name = headerRow.GetCell(i)?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name) && !map.ContainsKey(name))
                {
                    map.Add(name, i);
                }
            }

            return map;
        }

        private static bool RowIsEmpty(IRow row)
        {
            for (int i = row.FirstCellNum; i < row.LastCellNum; i++)
            {
                if (!string.IsNullOrWhiteSpace(row.GetCell(i)?.ToString()))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetRequiredCellValue(IRow row, IReadOnlyDictionary<string, int> headerMap, int rowNumber, params string[] aliases)
        {
            string value = GetOptionalCellValue(row, headerMap, aliases);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new HardDiskMediaImportException($"第 {rowNumber} 行的 [{aliases[0]}] 不能为空。", rowNumber, aliases[0]);
            }

            return value;
        }

        private static string GetOptionalCellValue(IRow row, IReadOnlyDictionary<string, int> headerMap, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                if (headerMap.TryGetValue(alias, out int index))
                {
                    return row.GetCell(index)?.ToString()?.Trim() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static DateTime? GetOptionalDateCellValue(IRow row, IReadOnlyDictionary<string, int> headerMap, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                if (!headerMap.TryGetValue(alias, out int index))
                {
                    continue;
                }

                var cell = row.GetCell(index);
                if (cell == null)
                {
                    return null;
                }

                if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                {
                    DateTime? dateValue = cell.DateCellValue;
                    return dateValue?.Date;
                }

                string text = cell.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (DateTime.TryParse(text, out DateTime parsed))
                {
                    return parsed.Date;
                }
            }

            return null;
        }

        private static void ValidateRequiredHeaders(IReadOnlyDictionary<string, int> headerMap)
        {
            ValidateRequiredHeader(headerMap, "硬盘编号", "介质编号");
            ValidateRequiredHeader(headerMap, "序列号", "硬盘序列号");
            ValidateRequiredHeader(headerMap, "硬盘类型", "介质类型");
            ValidateRequiredHeader(headerMap, "品牌");
        }

        private static void ValidateRequiredHeader(IReadOnlyDictionary<string, int> headerMap, params string[] aliases)
        {
            if (aliases.Any(alias => headerMap.ContainsKey(alias)))
            {
                return;
            }

            throw new HardDiskMediaImportException($"导入模板缺少必填表头 [{string.Join(" / ", aliases)}]。", null, aliases[0]);
        }

        private static void BuildTemplateSheet(XSSFWorkbook workbook)
        {
            var sheet = workbook.CreateSheet("介质台账模板");
            var header = sheet.CreateRow(0);
            string[] headers = { "硬盘编号", "序列号", "硬盘类型", "品牌", "容量", "接口类型", "出厂日期", "当前存放位置", "备注" };
            for (int i = 0; i < headers.Length; i++)
            {
                header.CreateCell(i).SetCellValue(headers[i]);
                sheet.SetColumnWidth(i, 20 * 256);
            }
        }

        private void BuildInstructionSheet(
            XSSFWorkbook workbook,
            IReadOnlyList<string> diskTypeOptions,
            IReadOnlyList<string> brandOptions,
            IReadOnlyList<string> interfaceTypeOptions,
            IReadOnlyList<string> statusOptions,
            IReadOnlyList<string> natureOptions)
        {
            var sheet = workbook.CreateSheet("字段说明");
            var row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue("硬盘类型");
            row.CreateCell(1).SetCellValue(string.Join("、", diskTypeOptions) + "（也可填写新值，导入后自动加入域值）");
            var rowBrand = sheet.CreateRow(1);
            rowBrand.CreateCell(0).SetCellValue("品牌");
            rowBrand.CreateCell(1).SetCellValue(string.Join("、", brandOptions) + "（也可填写新值，导入后自动加入域值）");
            var row2 = sheet.CreateRow(2);
            row2.CreateCell(0).SetCellValue("接口类型");
            row2.CreateCell(1).SetCellValue(string.Join("、", interfaceTypeOptions) + "（也可填写新值，导入后自动加入域值）");
            var row3 = sheet.CreateRow(3);
            row3.CreateCell(0).SetCellValue("状态");
            row3.CreateCell(1).SetCellValue(string.Join("、", statusOptions));
            var row4 = sheet.CreateRow(4);
            row4.CreateCell(0).SetCellValue("属性");
            row4.CreateCell(1).SetCellValue(string.Join("、", natureOptions));
            var row5 = sheet.CreateRow(5);
            row5.CreateCell(0).SetCellValue("登记方式");
            row5.CreateCell(1).SetCellValue("系统自动写入：导入=文件导入登记；新增介质=手工录入登记；资料存档外来硬盘=资料存档登记");
            var row6 = sheet.CreateRow(6);
            row6.CreateCell(0).SetCellValue("出厂日期");
            row6.CreateCell(1).SetCellValue("可选；格式 yyyy-MM-dd");
            var row7 = sheet.CreateRow(7);
            row7.CreateCell(0).SetCellValue("当前存放位置");
            row7.CreateCell(1).SetCellValue("可选；留空时系统按空白专用档口用途与容量自动入位（10盘/档口）");
        }

        private sealed record ImportedMediumRow(int RowNumber, HardDiskMedium Medium);
    }
}
