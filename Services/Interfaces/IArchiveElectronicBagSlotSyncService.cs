using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 电子介质袋占格同步：出库不还/归还灭失后释放档口；有待还时保留离柜位置语义。
    /// </summary>
    public interface IArchiveElectronicBagSlotSyncService
    {
        /// <summary>
        /// 按电子介质袋 Id 重新评估并同步占格状态。
        /// </summary>
        /// <returns>本次标记为已清空的电子介质袋列表。</returns>
        Task<IReadOnlyList<EmptiedArchiveBagHint>> SyncUnitsByIdsAsync(
            IReadOnlyCollection<int> unitIds,
            DateTime operatedAt);
    }
}
