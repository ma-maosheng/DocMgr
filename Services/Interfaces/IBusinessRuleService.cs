namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 业务编号与策略聚合服务。
    /// </summary>
    public interface IBusinessRuleService
    {
        /// <summary>
        /// 生成业务编号。
        /// </summary>
        Task<string> GenerateBusinessNoAsync(
            BusinessNoCategory category,
            int? numberingYear = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取业务规则快照。
        /// </summary>
        BusinessRuleSnapshot GetRuleSnapshot(BusinessNoCategory category);
    }
}
