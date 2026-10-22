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

        Task<List<YearlyArchiveFilingFact>> SearchByRegisterCriteriaAsync(
            string mediaKind,
            RegisterDirectionSearchCriteria criteria);

        Task<List<YearlyArchiveFilingFact>> SearchLedgerAsync(FilingLedgerSearchCriteria criteria);

        Task<List<int>> GetDistinctLedgerArchiveYearsAsync();

        Task<List<FilingLedgerProjectFilterItem>> GetLedgerProjectsByArchiveYearAsync(string? archiveYear);

        Task<List<YearlyArchiveRegisterMediaItem>> GetRegisterMediaItemsWithSupplementsAsync(
            IReadOnlyCollection<int> mediaItemIds);

        Task<List<YearlyArchiveRegisterMedia>> GetRegisterMediasByIdsAsync(
            IReadOnlyCollection<int> registerMediaIds);

        Task<IReadOnlyDictionary<int, string>> GetArchivePurposesByRegisterRecordIdsAsync(
            IReadOnlyCollection<int> registerRecordIds);

        /// <summary>按建档申请 ID 批量判断是否关联出网申请（网内资料立档）。</summary>
        Task<IReadOnlyDictionary<int, bool>> GetHasSourceNetworkOutboundByRegisterRecordIdsAsync(
            IReadOnlyCollection<int> registerRecordIds);

        /// <summary>
        /// 批量识别存量直办建档申请。
        /// 存量硬盘、光盘、文本直办写入时直接办结，审批链签字位保持空白；
        /// 申请审批在办结前必填生产科、资料室、分管资料院长、部门负责人与移交人。
        /// </summary>
        Task<IReadOnlySet<int>> GetStockDirectRegisterRecordIdsAsync(
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
