namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 默认业务策略提供器。
    /// </summary>
    public sealed class DefaultBusinessPolicyProvider : IBusinessPolicyProvider
    {
        private static readonly BusinessAttachmentPolicy ApplyAttachmentPolicy = new(
            NeedAttachment: true,
            AllowedExtensions: [".pdf", ".jpg", ".jpeg", ".png"],
            MinCount: 1,
            MaxCount: 5,
            MaxFileSizeInBytes: 10 * 1024 * 1024,
            RequiredCategory: "审批扫描件");

        private static readonly BusinessAttachmentPolicy RegisterAttachmentPolicy = new(
            NeedAttachment: false,
            AllowedExtensions: [],
            MinCount: 0,
            MaxCount: 0,
            MaxFileSizeInBytes: 0,
            RequiredCategory: string.Empty);

        private static readonly BusinessApprovalPolicy ApplyApprovalPolicy = new(
            NeedApproval: true,
            ApprovalTemplateCode: "DEFAULT_APPLY",
            DefaultOpinion: "同意");

        private static readonly BusinessApprovalPolicy RegisterApprovalPolicy = new(
            NeedApproval: false,
            ApprovalTemplateCode: string.Empty,
            DefaultOpinion: string.Empty);

        private static readonly IReadOnlyDictionary<BusinessNoCategory, BusinessNoRuleDefinition> RuleMap =
            new Dictionary<BusinessNoCategory, BusinessNoRuleDefinition>
            {
                [BusinessNoCategory.AssetInboundApply] = new(
                    BusinessNoCategory.AssetInboundApply,
                    Prefix: "资-入-申",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 4,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.AssetOutboundApply] = new(
                    BusinessNoCategory.AssetOutboundApply,
                    Prefix: "资-出-申",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 4,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.AssetReturnRegister] = new(
                    BusinessNoCategory.AssetReturnRegister,
                    Prefix: "资-还-登",
                    WorkflowMode: BusinessWorkflowMode.Register,
                    SequenceLength: 4,
                    ApprovalPolicy: RegisterApprovalPolicy,
                    AttachmentPolicy: RegisterAttachmentPolicy),
                [BusinessNoCategory.AssetDestroyApply] = new(
                    BusinessNoCategory.AssetDestroyApply,
                    Prefix: "资-销-申",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 4,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.DiskInboundRegister] = new(
                    BusinessNoCategory.DiskInboundRegister,
                    Prefix: "盘-入-登",
                    WorkflowMode: BusinessWorkflowMode.Register,
                    SequenceLength: 4,
                    ApprovalPolicy: RegisterApprovalPolicy,
                    AttachmentPolicy: RegisterAttachmentPolicy),
                [BusinessNoCategory.DiskOutboundApply] = new(
                    BusinessNoCategory.DiskOutboundApply,
                    Prefix: "盘-出-申",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 4,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.DiskInventoryRegister] = new(
                    BusinessNoCategory.DiskInventoryRegister,
                    Prefix: "盘库-登",
                    WorkflowMode: BusinessWorkflowMode.Register,
                    SequenceLength: 3,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.DiskDisposalApply] = new(
                    BusinessNoCategory.DiskDisposalApply,
                    Prefix: "盘离-申",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 3,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.ArchiveInventoryRegister] = new(
                    BusinessNoCategory.ArchiveInventoryRegister,
                    Prefix: "资盘-登",
                    WorkflowMode: BusinessWorkflowMode.Register,
                    SequenceLength: 3,
                    ApprovalPolicy: RegisterApprovalPolicy,
                    AttachmentPolicy: RegisterAttachmentPolicy),
                [BusinessNoCategory.ArchiveDisposalApply] = new(
                    BusinessNoCategory.ArchiveDisposalApply,
                    Prefix: "资离-处",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 3,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.NetworkInboundApply] = new(
                    BusinessNoCategory.NetworkInboundApply,
                    Prefix: "网-入-申",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 4,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.NetworkOutboundApply] = new(
                    BusinessNoCategory.NetworkOutboundApply,
                    Prefix: "网-出-申",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 4,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.NetworkDisposalApply] = new(
                    BusinessNoCategory.NetworkDisposalApply,
                    Prefix: "网-处-申",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 4,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy),
                [BusinessNoCategory.HistoryArchiveDisposalApply] = new(
                    BusinessNoCategory.HistoryArchiveDisposalApply,
                    Prefix: "史离-处",
                    WorkflowMode: BusinessWorkflowMode.Apply,
                    SequenceLength: 3,
                    ApprovalPolicy: ApplyApprovalPolicy,
                    AttachmentPolicy: ApplyAttachmentPolicy)
            };

        /// <summary>
        /// 获取业务规则快照。
        /// </summary>
        public BusinessRuleSnapshot GetRuleSnapshot(BusinessNoCategory category)
        {
            var rule = GetRule(category);
            return new BusinessRuleSnapshot(
                category,
                rule.Prefix,
                rule.WorkflowMode,
                rule.ApprovalPolicy,
                rule.AttachmentPolicy);
        }

        /// <summary>
        /// 获取审批策略。
        /// </summary>
        public BusinessApprovalPolicy GetApprovalPolicy(BusinessNoCategory category)
        {
            return GetRule(category).ApprovalPolicy;
        }

        /// <summary>
        /// 获取附件策略。
        /// </summary>
        public BusinessAttachmentPolicy GetAttachmentPolicy(BusinessNoCategory category)
        {
            return GetRule(category).AttachmentPolicy;
        }

        internal static BusinessNoRuleDefinition GetRule(BusinessNoCategory category)
        {
            if (!RuleMap.TryGetValue(category, out var rule))
            {
                throw new ArgumentException($"不支持的业务编号类别：{category}", nameof(category));
            }

            return rule;
        }
    }
}
