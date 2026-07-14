using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 待归还出库明细的容器状态标记：迁档/销号前提醒统计，实施后打「盒位已变/盒已失效」标记（不进待办）。
    /// </summary>
    public interface IArchiveOutboundPendingReturnContainerService
    {
        /// <summary>统计指定模拟档案盒上仍待归还的提档明细条数。</summary>
        Task<int> CountPendingReturnItemsForSimulatedBoxAsync(int boxId);

        /// <summary>统计多个模拟档案盒上仍待归还的提档明细条数。</summary>
        Task<int> CountPendingReturnItemsForSimulatedBoxesAsync(IReadOnlyCollection<int> boxIds);

        /// <summary>组装迁档/销号前的确认提示；无待还时返回 null。</summary>
        Task<string?> BuildPendingReturnConfirmMessageAsync(int boxId, string actionLabel);

        /// <summary>组装多盒批量迁档前的确认提示；无待还时返回 null。</summary>
        Task<string?> BuildPendingReturnConfirmMessageForBoxesAsync(
            IReadOnlyCollection<int> boxIds,
            string actionLabel);

        /// <summary>整盒物理迁档后：待还明细标记为盒位已变，并刷新当前位置。</summary>
        Task MarkPendingReturnsLocationChangedAsync(
            int boxId,
            string currentContainerCode,
            string currentStorageLocation);

        /// <summary>
        /// 源盒销号（并档/迁入空盒）后：按受影响立档事实标记待还明细为盒已失效；
        /// 若事实已挂到新盒，同时写入新盒号/位置便于归还对照。
        /// </summary>
        Task MarkPendingReturnsBoxInvalidAsync(
            IReadOnlyCollection<int> filingFactIds,
            string? currentContainerCode = null,
            string? currentStorageLocation = null);
    }
}
