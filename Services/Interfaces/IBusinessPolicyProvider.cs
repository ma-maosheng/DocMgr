namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 业务审批与附件策略提供器。
    /// </summary>
    public interface IBusinessPolicyProvider
    {
        /// <summary>
        /// 获取业务规则快照。
        /// </summary>
        BusinessRuleSnapshot GetRuleSnapshot(BusinessNoCategory category);

        /// <summary>
        /// 获取审批策略。
        /// </summary>
        BusinessApprovalPolicy GetApprovalPolicy(BusinessNoCategory category);

        /// <summary>
        /// 获取附件策略。
        /// </summary>
        BusinessAttachmentPolicy GetAttachmentPolicy(BusinessNoCategory category);
    }
}
