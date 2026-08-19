namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 业务编号与策略聚合服务。
    /// </summary>
    public sealed class BusinessRuleService : IBusinessRuleService
    {
        private readonly IBusinessNoGenerator _businessNoGenerator;
        private readonly IBusinessPolicyProvider _businessPolicyProvider;

        public BusinessRuleService(
            IBusinessNoGenerator businessNoGenerator,
            IBusinessPolicyProvider businessPolicyProvider)
        {
            _businessNoGenerator = businessNoGenerator;
            _businessPolicyProvider = businessPolicyProvider;
        }

        /// <summary>
        /// 生成业务编号。
        /// </summary>
        public Task<string> GenerateBusinessNoAsync(
            BusinessNoCategory category,
            int? numberingYear = null,
            CancellationToken cancellationToken = default)
        {
            return _businessNoGenerator.GenerateNextNoAsync(category, numberingYear, cancellationToken);
        }

        /// <summary>
        /// 获取业务规则快照。
        /// </summary>
        public BusinessRuleSnapshot GetRuleSnapshot(BusinessNoCategory category)
        {
            return _businessPolicyProvider.GetRuleSnapshot(category);
        }
    }
}
