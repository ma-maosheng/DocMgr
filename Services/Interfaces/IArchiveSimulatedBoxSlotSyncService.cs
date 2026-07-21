using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 模拟介质档案盒占格同步：出库/归还办结后按盒内份数释放或保留档口占位。
    /// </summary>
    public interface IArchiveSimulatedBoxSlotSyncService
    {
        /// <summary>
        /// 按档案盒 Id 重新评估并同步占格状态。
        /// </summary>
        /// <returns>本次被释放档口并标记为已清空的档案盒列表。</returns>
        Task<IReadOnlyList<EmptiedArchiveBoxHint>> SyncBoxesByIdsAsync(
            IReadOnlyCollection<int> boxIds,
            DateTime operatedAt);
    }
}
