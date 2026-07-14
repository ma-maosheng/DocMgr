using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 立档台账服务：按年度查询、浏览与导出立档事实。
    /// </summary>
    public interface IArchiveFilingLedgerService
    {
        Task<IReadOnlyList<FilingLedgerRow>> SearchAsync(FilingLedgerSearchCriteria criteria);

        Task<IReadOnlyList<FilingLedgerContentEntryInfo>> GetContentEntriesByMediaItemIdAsync(
            int mediaItemId,
            string? filingStoragePath);

        Task ExportAsync(string filePath, IReadOnlyList<FilingLedgerRow> rows);
    }
}
