using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces
{
    /// <summary>
    /// 资料出库数据访问契约：出库申请、审批与交接数据读写。
    /// </summary>
    public interface IArchiveOutboundRepository
    {
        /// <summary>开启资料出库业务的数据库事务，保证多表写入原子提交。</summary>
        Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync();

        Task<YearlyArchiveOutboundRecord?> GetByIdWithDetailsAsync(int id);

        Task<YearlyArchiveOutboundRecord?> GetByOutboundNoWithDetailsAsync(string outboundNo);

        Task<List<YearlyArchiveOutboundRecord>> ListByYearAsync(int year);

        Task<List<YearlyArchiveOutboundRecord>> ListByApplicantUserIdAsync(int userId, int year);

        /// <summary>返回已有出库申请记录涉及的申请年度（降序）。</summary>
        Task<List<int>> GetExistingApplyYearsAsync();

        Task<List<string>> GetOutboundNosByPrefixAsync(string prefix);

        Task<int> SaveOrUpdateRecordGraphAsync(YearlyArchiveOutboundRecord record);

        Task SaveChangesAsync();

        Task<YearlyArchiveFilingFact?> GetFilingFactByIdAsync(int filingFactId);

        Task<Dictionary<int, YearlyArchiveFilingFact>> GetFilingFactsByIdsForUpdateAsync(IReadOnlyCollection<int> filingFactIds);

        Task<Dictionary<int, YearlyArchiveRegisterMedia>> GetRegisterMediasByIdsForUpdateAsync(IReadOnlyCollection<int> registerMediaIds);

        Task<string?> GetRegisterMediaTypeAsync(int registerMediaId);

        Task<int> GetRegisterMediaStockCopyCountAsync(int registerMediaId);

        /// <summary>
        /// 查询其他在途出库单对指定立档事实的有效提档预订（Active），供提交时冲突校验。
        /// </summary>
        Task<IReadOnlyList<ActiveWithdrawalReservationSnapshot>> GetActiveWithdrawalReservationsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds,
            int? excludeOutboundRecordId);

        /// <summary>
        /// 按立档事实汇总已办结、尚未归还的提档份数（<see cref="YearlyArchiveOutboundItem.CopyCount"/>）。
        /// </summary>
        Task<IReadOnlyDictionary<int, int>> GetCompletedOutstandingWithdrawalCopyCountsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds);

        /// <summary>
        /// 按立档事实汇总模拟介质出库相关份数（待还、不还、灭失），供库内可用份数计算。
        /// </summary>
        Task<IReadOnlyDictionary<int, SimulatedFilingFactCopyCountSnapshot>> GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds);

        Task<List<YearlyArchiveOutboundSyncEntry>> GetActiveSyncEntriesByRecordIdAsync(int recordId);

        Task<List<SystemAttachment>> GetAttachmentsByBusinessIdAsync(int businessId);

        Task<List<SystemAttachment>> GetOrphanAttachmentsByBusinessNoAsync(string businessNo, string businessType);

        void AddAttachment(SystemAttachment attachment);

        void RemoveAttachment(SystemAttachment attachment);

        Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

        Task LinkOrphanAttachmentsToRecordAsync(string businessNo, string businessType, int recordId);

        Task<List<YearlyArchiveOutboundRecord>> GetSubmittedRecordsPastDeadlineAsync(DateTime asOf);

        /// <summary>列出资料室尚未办结出库（已提交/已审批/已办结审批）的借出申请，供待办提醒使用。</summary>
        Task<List<YearlyArchiveOutboundRecord>> GetPendingRecordsForToDoAsync(int takeCount);

        /// <summary>按电子立档单元 Id 加载关联的数据光盘（含台账与流转，供出库/归还同步更新）。</summary>
        Task<List<OpticalDiscMedium>> GetOpticalDiscMediaByElectronicUnitIdForUpdateAsync(int unitId);

        /// <summary>按光盘编号加载数据光盘（含台账与流转，供出库/归还同步更新）。</summary>
        Task<OpticalDiscMedium?> GetOpticalDiscMediumByCodeForUpdateAsync(string discCode);

        /// <summary>按电子立档单元 Id 加载关联的入袋硬盘（含台账，供出库/归还同步更新）。</summary>
        Task<List<HardDiskMedium>> GetHardDiskMediaByElectronicUnitIdForUpdateAsync(int unitId);

        /// <summary>按硬盘编号加载入袋硬盘（含台账，供出库/归还同步更新）。</summary>
        Task<HardDiskMedium?> GetHardDiskMediumByCodeForUpdateAsync(string diskCode);

        /// <summary>按 Id 加载电子介质袋（供出库/归还同步更新物理位置）。</summary>
        Task<YearlyElectronicArchiveUnit?> GetElectronicArchiveUnitByIdForUpdateAsync(int unitId);

        /// <summary>按 Id 加载年度档案盒（供模拟介质占格同步）。</summary>
        Task<YearlyArchiveBox?> GetYearlyArchiveBoxByIdForUpdateAsync(int boxId);

        /// <summary>按 Id 只读加载年度档案盒（含生命周期，供归还容器评估）。</summary>
        Task<YearlyArchiveBox?> GetYearlyArchiveBoxByIdAsync(int boxId);

        /// <summary>列出在用模拟档案盒（供归还异常指定目标盒）。</summary>
        Task<List<YearlyArchiveBox>> ListInUseSimulatedArchiveBoxesAsync(string? projectName, string? year);

        /// <summary>新增年度档案盒（归还异常新建空盒）。</summary>
        void AddYearlyArchiveBox(YearlyArchiveBox box);

        /// <summary>按档案盒 Id 查询已办结、待归还的模拟提档出库明细（可跟踪更新）。</summary>
        Task<List<YearlyArchiveOutboundItem>> GetPendingReturnSimulatedOutboundItemsByBoxIdAsync(int boxId);

        /// <summary>按立档事实 Id 集合查询已办结、待归还的模拟提档出库明细（可跟踪更新）。</summary>
        Task<List<YearlyArchiveOutboundItem>> GetPendingReturnSimulatedOutboundItemsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds);

        /// <summary>按档案盒加载资料子项份数行（含可跟踪的立档事实，供占格同步）。</summary>
        Task<List<YearlyArchiveBoxMediaItemRow>> GetYearlyArchiveBoxMediaItemRowsForSyncAsync(YearlyArchiveBox box);

        /// <summary>移除档案盒在开柜布局中的占位记录。</summary>
        void RemoveArchiveBoxPlacementByBoxCode(string boxCode);

        /// <summary>按盒/袋编号加载在库立档事实（供提档完整性校验）。</summary>
        Task<List<YearlyArchiveFilingFact>> GetInArchiveFilingFactsByContainerAsync(string mediaKind, string containerCode);

        /// <summary>按硬盘编号汇总已立档占用数据量（MB）。</summary>
        Task<decimal> GetUsedDataSizeMbByHardDiskCodeAsync(string diskCode);
    }
}
