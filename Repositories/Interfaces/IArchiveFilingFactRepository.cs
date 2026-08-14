using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces
{
    /// <summary>
    /// 立档事实数据访问契约：立档检索事实（filing fact）数据读写。
    /// </summary>
    public interface IArchiveFilingFactRepository
    {
        Task<string?> GetLastFilingFactNoByPrefixAsync(string prefix);

        void AddFilingFacts(IEnumerable<YearlyArchiveFilingFact> facts);

        Task<bool> ExistsBySourceLinkAsync(string sourceLinkType, int sourceLinkId);

        Task<int> SaveChangesAsync();

        Task BackfillFromExistingLinksAsync();

        Task<List<YearlyArchiveFilingFact>> SearchByRegisterCriteriaAsync(
            string mediaKind,
            RegisterDirectionSearchCriteria criteria);

        Task<List<YearlyArchiveFilingFact>> SearchLedgerAsync(FilingLedgerSearchCriteria criteria);

        Task<List<YearlyArchiveRegisterMediaItem>> GetRegisterMediaItemsWithSupplementsAsync(
            IReadOnlyCollection<int> mediaItemIds);

        Task<List<YearlyArchiveRegisterMedia>> GetRegisterMediasByIdsAsync(
            IReadOnlyCollection<int> registerMediaIds);

        Task<IReadOnlyDictionary<int, string>> GetArchivePurposesByRegisterRecordIdsAsync(
            IReadOnlyCollection<int> registerRecordIds);

        /// <summary>按项目 ID 批量取实施年度（ImplementYear）。</summary>
        Task<IReadOnlyDictionary<int, string>> GetProjectImplementYearsByIdsAsync(
            IReadOnlyCollection<int> projectIds);

        Task<List<YearlyArchiveRegisterElectronicMediaItemEntry>> GetElectronicContentEntriesByMediaItemIdsAsync(
            IReadOnlyCollection<int> mediaItemIds);

        Task<List<YearlyArchiveRegisterElectronicMediaItemEntry>> GetElectronicContentEntriesByIdsAsync(
            IReadOnlyCollection<int> entryIds);

        Task<List<YearlyArchiveFilingFact>> SearchByContainerCriteriaAsync(
            string mediaKind,
            ContainerDirectionSearchCriteria criteria);

        Task<List<YearlyArchiveFilingFact>> GetFactsByMediaItemIdsAsync(IReadOnlyCollection<int> mediaItemIds);

        Task<List<YearlyArchiveFilingFact>> GetFactsByIdsAsync(IReadOnlyCollection<int> filingFactIds);

        /// <summary>批量读取登记介质当前份数（<see cref="YearlyArchiveRegisterMedia.MediaCount"/>）。</summary>
        Task<IReadOnlyDictionary<int, int>> GetRegisterMediaStockCountsByIdsAsync(IReadOnlyCollection<int> registerMediaIds);

        Task<List<YearlyArchiveFilingFact>> GetBackupFactsByPrimaryIdsAsync(IReadOnlyCollection<int> primaryFilingFactIds);

        Task<string?> GetLastResultSetNoByPrefixAsync(string prefix);

        void AddResultSet(YearlyArchiveSearchResultSet resultSet);

        Task<List<SearchPoolListItem>> SearchResultSetsAsync(
            string mediaKind,
            SearchPoolListCriteria criteria,
            int currentUserId,
            bool isArchiveAdmin);

        Task<YearlyArchiveSearchResultSet?> GetResultSetWithItemsAsync(int resultSetId);

        /// <summary>只读加载检索集及明细，避免跟踪缓存导致 Items 为空。</summary>
        Task<YearlyArchiveSearchResultSet?> GetResultSetWithItemsAsNoTrackingAsync(int resultSetId);

        Task<bool> DeleteResultSetAsync(int resultSetId);

        Task SaveResultSetChangesAsync();

        Task UpdateFilingFactLifecycleAsync(
            int filingFactId,
            string lifecycleStatus,
            string borrowHintLevel,
            string borrowHintText,
            string operatedBy);

        Task UpdateFilingFactLifecyclesAsync(
            IReadOnlyList<FilingFactLifecycleUpdate> updates,
            string operatedBy,
            string? businessLabel = null);
    }
}
