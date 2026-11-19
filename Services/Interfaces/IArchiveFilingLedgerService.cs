using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 立档台账服务：按年度查询、浏览与导出立档事实。
    /// </summary>
    public interface IArchiveFilingLedgerService
    {
        Task<IReadOnlyList<int>> GetExistingLedgerYearsAsync();

        Task<IReadOnlyList<FilingLedgerProjectFilterItem>> GetProjectOptionsForYearAsync(string? archiveYear);

        Task<IReadOnlyList<FilingLedgerRow>> SearchAsync(FilingLedgerSearchCriteria criteria);

        /// <summary>按页查询立档台账。页码和页大小写在条件里；页大小小于等于 0 时返回全部命中。</summary>
        Task<FilingLedgerSearchPage> SearchPageAsync(FilingLedgerSearchCriteria criteria);

        /// <summary>当前排序下，排在指定立档事实之前的命中条数。</summary>
        Task<int> CountRowsBeforeAsync(FilingLedgerSearchCriteria criteria, DateTime filedAt, int filingFactId);

        Task<IReadOnlyList<FilingLedgerContentEntryInfo>> GetContentEntriesByMediaItemIdAsync(
            int mediaItemId,
            string? filingStoragePath);

        Task ExportAsync(string filePath, IReadOnlyList<FilingLedgerRow> rows);
    }
}
