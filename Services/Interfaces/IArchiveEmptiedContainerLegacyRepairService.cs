namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 空盒/空袋历史数据纠偏：启动期幂等修复残留「在库」立档事实。
    /// </summary>
    public interface IArchiveEmptiedContainerLegacyRepairService
    {
        /// <summary>
        /// 纠偏非在用容器下仍标记为在库/借出中、且库内与待还均为 0 的立档事实。
        /// </summary>
        /// <returns>实际改写的立档事实条数。</returns>
        Task<int> RepairAsync(CancellationToken cancellationToken = default);
    }
}
