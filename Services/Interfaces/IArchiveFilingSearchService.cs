using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 立档事实写入契约：将立档结果写入立档事实（filing fact）存储。
    /// </summary>
    public interface IFilingFactWriter
    {
        Task WriteForSimulatedLinksAsync(
            YearlyArchiveBox box,
            IReadOnlyList<YearlyArchiveBoxMediaItemLink> links,
            IReadOnlyList<YearlyArchiveRegisterMediaItem> mediaItems,
            DateTime filedAt,
            string filedBy,
            int? numberingYear = null);

        Task WriteForElectronicLinksAsync(
            YearlyElectronicArchiveUnit unit,
            IReadOnlyList<YearlyElectronicArchiveUnitMediaItemLink> links,
            DateTime filedAt,
            string filedBy,
            int? numberingYear = null);

        Task WriteBackupElectronicLinksAsync(
            YearlyElectronicArchiveUnit unit,
            IReadOnlyList<BackupElectronicLinkWriteItem> links,
            IReadOnlyDictionary<int, int> primaryFilingFactIdByOriginalLinkId,
            DateTime filedAt,
            string filedBy,
            string backupRemark);
    }

    /// <summary>
    /// 立档检索服务契约：检索池构建、条件检索与结果集管理。
    /// </summary>
    public interface IArchiveFilingSearchService
    {
        Task<List<FiledArchiveSearchHit>> SearchByRegisterAsync(
            string mediaKind,
            RegisterDirectionSearchCriteria criteria);

        Task<List<FiledArchiveSearchGroupHit>> SearchByRegisterGroupedAsync(
            string mediaKind,
            RegisterDirectionSearchCriteria criteria);

        /// <summary>模拟介质登记方向检索：先按资料子项分组，再按档案盒归组。</summary>
        Task<List<FiledArchiveSearchBoxGroupHit>> SearchByRegisterGroupedByArchiveBoxAsync(
            string mediaKind,
            RegisterDirectionSearchCriteria criteria);

        Task<List<FiledArchiveSearchHit>> SearchByContainerAsync(
            string mediaKind,
            ContainerDirectionSearchCriteria criteria);

        Task<SearchResultSetSaveResult> SaveResultSetAsync(
            SaveArchiveSearchResultSetRequest request,
            User currentUser,
            bool isArchiveAdmin);

        Task<List<SearchPoolListItem>> ListSearchPoolsAsync(
            SearchPoolListCriteria criteria,
            User currentUser,
            bool isArchiveAdmin);

        Task<YearlyArchiveSearchResultSet?> GetSearchPoolAsync(
            int resultSetId,
            User currentUser,
            bool isArchiveAdmin);

        /// <summary>按 Id 读取检索集及明细，不做创建人权限校验（入网等已关联业务展示用）。</summary>
        Task<YearlyArchiveSearchResultSet?> GetSearchPoolByIdAsync(int resultSetId);

        Task<YearlyArchiveSearchResultSet> UpdateSearchPoolAsync(
            UpdateSearchPoolRequest request,
            User currentUser,
            bool isArchiveAdmin);

        Task DeleteSearchPoolAsync(
            int resultSetId,
            User currentUser,
            bool isArchiveAdmin);

        Task<FiledArchiveSearchHit?> GetSearchHitByFilingFactIdAsync(int filingFactId);

        /// <summary>按立档事实 Id 批量读取检索命中（检索池明细展示用）。</summary>
        Task<IReadOnlyDictionary<int, FiledArchiveSearchHit>> GetSearchHitsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds);

        /// <summary>按立档事实 Id 批量读取资料子项库存份数展示文案。</summary>
        Task<IReadOnlyDictionary<int, string>> GetStockCopyCountDisplaysByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds);

        Task<IReadOnlyList<MatchedContentEntryInfo>> GetContentEntriesByMediaItemIdAsync(
            int mediaItemId,
            string? filingStoragePath = null);

        Task<IReadOnlyDictionary<int, string>> GetCurrentStorageLocationsByFilingFactIdsAsync(
            IReadOnlyList<int> filingFactIds);

        /// <summary>
        /// 模拟介质：按立档事实返回在库份数信息（立档/待还/不还/灭失/当前库内，展示如 2/5）。
        /// </summary>
        Task<IReadOnlyDictionary<int, SimulatedInArchiveCopyCountInfo>> GetSimulatedInArchiveCopyCountInfoByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds);
    }
}
