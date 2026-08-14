using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using NPOI.XSSF.UserModel;
using System.IO;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 跨域流转台账查询。
    /// </summary>
    public sealed class ArchiveCrossDomainTransferLedgerService : IArchiveCrossDomainTransferLedgerService
    {
        private readonly IArchiveMaterialTransactionRepository _repository;

        public ArchiveCrossDomainTransferLedgerService(IArchiveMaterialTransactionRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<CrossDomainTransferLedgerRow>> SearchAsync(
            CrossDomainTransferLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            return _repository.SearchCrossDomainTransferLedgerAsync(criteria);
        }

        public Task<IReadOnlyList<string>> GetBusinessNoOptionsAsync(int maxCount = 50) =>
            _repository.GetCrossDomainTransferBusinessNoOptionsAsync(maxCount);

        public Task ExportAsync(string filePath, IReadOnlyList<CrossDomainTransferLedgerRow> rows)
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

            return Task.Run(() =>
            {
                using var workbook = new XSSFWorkbook();
                var sheet = workbook.CreateSheet("跨域流转台账");
                string[] headers =
                [
                    "操作时间",
                    "流转类型",
                    "入网单号",
                    "立档编号",
                    "表单编号",
                    "介质",
                    "资料名称",
                    "分项名称",
                    "项目",
                    "离线存放位置",
                    "生产网服务器路径",
                    "跨域路径",
                    "在网资产编号",
                    "操作人",
                    "摘要",
                    "备注"
                ];

                var headerRow = sheet.CreateRow(0);
                for (int i = 0; i < headers.Length; i++)
                {
                    headerRow.CreateCell(i).SetCellValue(headers[i]);
                    sheet.SetColumnWidth(i, 18 * 256);
                }

                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    var item = rows[rowIndex];
                    var row = sheet.CreateRow(rowIndex + 1);
                    int col = 0;
                    row.CreateCell(col++).SetCellValue(item.OperatedAtDisplay);
                    row.CreateCell(col++).SetCellValue(item.TransactionTypeDisplay);
                    row.CreateCell(col++).SetCellValue(item.BusinessNo);
                    row.CreateCell(col++).SetCellValue(item.FilingFactNo);
                    row.CreateCell(col++).SetCellValue(item.FormNo);
                    row.CreateCell(col++).SetCellValue(item.MediaKind);
                    row.CreateCell(col++).SetCellValue(item.MaterialName);
                    row.CreateCell(col++).SetCellValue(item.ItemName);
                    row.CreateCell(col++).SetCellValue(item.ProjectName);
                    row.CreateCell(col++).SetCellValue(item.SourceStorageLocation);
                    row.CreateCell(col++).SetCellValue(item.TargetServerPath);
                    row.CreateCell(col++).SetCellValue(item.TransferPathDisplay);
                    row.CreateCell(col++).SetCellValue(item.OnNetAssetNo);
                    row.CreateCell(col++).SetCellValue(item.OperatorName);
                    row.CreateCell(col++).SetCellValue(item.Summary);
                    row.CreateCell(col).SetCellValue(item.Remark);
                }

                using var stream = File.Create(filePath);
                workbook.Write(stream);
            });
        }
    }
}
