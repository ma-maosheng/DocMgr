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
            int containerId,
            string? historyMaterialKind = null)
        {
            return _repository.GetContentsAsync(containerKind, containerId, historyMaterialKind);
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
            bool includeContents,
            string browseKind)
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
                    var items = await _repository.GetContentsAsync(
                        row.ContainerKind,
                        row.ContainerId,
                        StockContainerBrowseKind.ToHistoryMaterialKind(browseKind));
                    allContents.AddRange(items);
                }

                contentItems = allContents;
            }

            await Task.Run(() =>
            {
                using var workbook = new XSSFWorkbook();
                WriteContainerSheet(workbook, rows, browseKind);
                if (includeContents)
                {
                    WriteContentSheet(workbook, contentItems, browseKind);
                }

                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                workbook.Write(stream, leaveOpen: false);
            });
        }

        private static void WriteContainerSheet(
            XSSFWorkbook workbook,
            IReadOnlyList<StockContainerOverviewRow> rows,
            string browseKind)
        {
            var columns = StockContainerOverviewColumns.ContainerColumns(browseKind);
            var sheet = workbook.CreateSheet("库管容器总览");
            WriteHeader(sheet, columns);

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var item = rows[rowIndex];
                var row = sheet.CreateRow(rowIndex + 1);
                for (int col = 0; col < columns.Count; col++)
                {
                    var column = columns[col];
                    if (column.TryReadContainerNumber(item, out int number))
                    {
                        row.CreateCell(col).SetCellValue(number);
                    }
                    else
                    {
                        row.CreateCell(col).SetCellValue(column.ReadContainer(item));
                    }
                }
            }
        }

        private static void WriteContentSheet(
            XSSFWorkbook workbook,
            IReadOnlyList<StockContainerContentItem> items,
            string browseKind)
        {
            var columns = StockContainerOverviewColumns.ContentColumns(browseKind);
            var sheet = workbook.CreateSheet("容器内容明细");
            WriteHeader(sheet, columns);

            for (int rowIndex = 0; rowIndex < items.Count; rowIndex++)
            {
                var item = items[rowIndex];
                var row = sheet.CreateRow(rowIndex + 1);
                for (int col = 0; col < columns.Count; col++)
                {
                    var column = columns[col];
                    if (column.TryReadContentNumber(item, out int number))
                    {
                        row.CreateCell(col).SetCellValue(number);
                    }
                    else
                    {
                        row.CreateCell(col).SetCellValue(column.ReadContent(item));
                    }
                }
            }
        }

        private static void WriteHeader(
            NPOI.SS.UserModel.ISheet sheet,
            IReadOnlyList<StockContainerOverviewGridColumn> columns)
        {
            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < columns.Count; i++)
            {
                headerRow.CreateCell(i).SetCellValue(columns[i].Header);
                int width = columns[i].Star ? 36 : Math.Max(10, (int)(columns[i].Width / 8));
                sheet.SetColumnWidth(i, width * 256);
            }
        }
    }
}
