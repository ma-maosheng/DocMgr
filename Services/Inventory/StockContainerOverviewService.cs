using System.IO;
using DocMgr.Models.Inventory;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using NPOI.XSSF.UserModel;

namespace DocMgr.Services.Inventory
{
    /// <summary>
    /// 库管资料总览查询与 Excel 导出。
    /// </summary>
    public sealed class StockContainerOverviewService : IStockContainerOverviewService
    {
        private readonly IStockContainerOverviewRepository _repository;

        public StockContainerOverviewService(IStockContainerOverviewRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<int>> GetDistinctYearsAsync()
        {
            return _repository.GetDistinctYearsAsync();
        }

        public Task<IReadOnlyList<StockContainerOverviewRow>> SearchContainersAsync(
            StockContainerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            return _repository.SearchContainersAsync(criteria);
        }

        public Task<IReadOnlyList<StockContainerContentItem>> GetContentsAsync(
            string containerKind,
            int containerId)
        {
            return _repository.GetContentsAsync(containerKind, containerId);
        }

        public async Task<IReadOnlyList<ElectronicMediaItemEntryDisplayItem>> GetElectronicContentEntriesAsync(
            int mediaItemId)
        {
            if (mediaItemId <= 0)
            {
                return Array.Empty<ElectronicMediaItemEntryDisplayItem>();
            }

            var entries = await _repository.GetElectronicContentEntriesAsync(mediaItemId);
            return entries
                .Select(entry => new ElectronicMediaItemEntryDisplayItem(
                    entry.EntryKind?.Trim() ?? string.Empty,
                    entry.EntryName?.Trim() ?? string.Empty,
                    ElectronicMediaItemSupport.FormatModifiedDate(entry.CreatedAt),
                    ElectronicMediaItemSupport.FormatModifiedDate(entry.ModifiedAt),
                    entry.SizeMb))
                .ToList();
        }

        public async Task ExportAsync(
            string filePath,
            IReadOnlyList<StockContainerOverviewRow> rows,
            bool includeContents)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("导出文件路径不能为空。", nameof(filePath));
            }

            ArgumentNullException.ThrowIfNull(rows);

            string? directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            Directory.CreateDirectory(directoryPath);

            IReadOnlyList<StockContainerContentItem> contentItems = Array.Empty<StockContainerContentItem>();
            if (includeContents && rows.Count > 0)
            {
                var allContents = new List<StockContainerContentItem>();
                foreach (var row in rows)
                {
                    var items = await _repository.GetContentsAsync(row.ContainerKind, row.ContainerId);
                    allContents.AddRange(items);
                }

                contentItems = allContents;
            }

            await Task.Run(() =>
            {
                using var workbook = new XSSFWorkbook();
                WriteContainerSheet(workbook, rows);
                if (includeContents)
                {
                    WriteContentSheet(workbook, contentItems);
                }

                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                workbook.Write(stream, leaveOpen: false);
            });
        }

        private static void WriteContainerSheet(
            XSSFWorkbook workbook,
            IReadOnlyList<StockContainerOverviewRow> rows)
        {
            var sheet = workbook.CreateSheet("库管容器总览");
            string[] headers =
            [
                "容器类型",
                "容器编号",
                "项目/类别",
                "年度",
                "存放位置",
                "状态",
                "内容条数",
                "内容摘要",
                "规格/载体",
                "放置方式",
                "归档人",
                "归档日期",
                "备注"
            ];

            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(headers[i]);
                sheet.SetColumnWidth(i, 16 * 256);
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var item = rows[rowIndex];
                var row = sheet.CreateRow(rowIndex + 1);
                int col = 0;
                row.CreateCell(col++).SetCellValue(item.ContainerKindDisplay);
                row.CreateCell(col++).SetCellValue(item.ContainerCode);
                row.CreateCell(col++).SetCellValue(item.ProjectOrCategory);
                row.CreateCell(col++).SetCellValue(item.Year);
                row.CreateCell(col++).SetCellValue(item.StorageLocation);
                row.CreateCell(col++).SetCellValue(item.LifecycleStatusDisplay);
                row.CreateCell(col++).SetCellValue(item.ItemCount);
                row.CreateCell(col++).SetCellValue(item.ContentSummary);
                row.CreateCell(col++).SetCellValue(item.Specs);
                row.CreateCell(col++).SetCellValue(item.PlacementModeDisplay);
                row.CreateCell(col++).SetCellValue(item.ArchivedBy);
                row.CreateCell(col++).SetCellValue(item.ArchivedDateDisplay);
                row.CreateCell(col++).SetCellValue(item.Remarks);
            }
        }

        private static void WriteContentSheet(
            XSSFWorkbook workbook,
            IReadOnlyList<StockContainerContentItem> items)
        {
            var sheet = workbook.CreateSheet("容器内容明细");
            string[] headers =
            [
                "容器类型",
                "容器ID",
                "资料类别",
                "资料名称",
                "明细名称",
                "密级",
                "份数",
                "生命周期",
                "原件/备份",
                "介质编号",
                "表单号",
                "补充信息",
                "来源实体",
                "来源ID"
            ];

            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(headers[i]);
                sheet.SetColumnWidth(i, 16 * 256);
            }

            for (int rowIndex = 0; rowIndex < items.Count; rowIndex++)
            {
                var item = items[rowIndex];
                var row = sheet.CreateRow(rowIndex + 1);
                int col = 0;
                row.CreateCell(col++).SetCellValue(StockContainerKind.MapDisplay(item.ContainerKind));
                row.CreateCell(col++).SetCellValue(item.ContainerId);
                row.CreateCell(col++).SetCellValue(item.MaterialKindDisplay);
                row.CreateCell(col++).SetCellValue(item.Title);
                row.CreateCell(col++).SetCellValue(item.ItemName);
                row.CreateCell(col++).SetCellValue(item.ConfidentialLevel);
                row.CreateCell(col++).SetCellValue(item.ContentCount);
                row.CreateCell(col++).SetCellValue(item.LifecycleStatusDisplay);
                row.CreateCell(col++).SetCellValue(item.ArchiveCopyRoleDisplay);
                row.CreateCell(col++).SetCellValue(item.MediumCode);
                row.CreateCell(col++).SetCellValue(item.FormNo);
                row.CreateCell(col++).SetCellValue(item.ExtraInfo);
                row.CreateCell(col++).SetCellValue(item.SourceEntity);
                row.CreateCell(col++).SetCellValue(item.SourceId);
            }
        }
    }
}
