namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 业务编号类别。
    /// </summary>
    public enum BusinessNoCategory
    {
        AssetInboundApply,
        AssetOutboundApply,
        AssetReturnRegister,
        AssetDestroyApply,
        DiskInboundRegister,
        DiskOutboundApply,
        DiskInventoryRegister,
        DiskDisposalApply,
        ArchiveInventoryRegister,
        ArchiveDisposalApply,
        NetworkInboundApply,
        NetworkOutboundApply,
        NetworkDisposalApply
    }

    /// <summary>
    /// 业务办理模式。
    /// </summary>
    public enum BusinessWorkflowMode
    {
        Apply,
        Register
    }

    /// <summary>
    /// 附件规则。
    /// </summary>
    public sealed record BusinessAttachmentPolicy(
        bool NeedAttachment,
        IReadOnlyList<string> AllowedExtensions,
        int MinCount,
        int MaxCount,
        long MaxFileSizeInBytes,
        string RequiredCategory);

    /// <summary>
    /// 审批规则。
    /// </summary>
    public sealed record BusinessApprovalPolicy(
        bool NeedApproval,
        string ApprovalTemplateCode,
        string DefaultOpinion);

    /// <summary>
    /// 编号规则定义。
    /// </summary>
    public sealed record BusinessNoRuleDefinition(
        BusinessNoCategory Category,
        string Prefix,
        BusinessWorkflowMode WorkflowMode,
        int SequenceLength,
        BusinessApprovalPolicy ApprovalPolicy,
        BusinessAttachmentPolicy AttachmentPolicy);

    /// <summary>
    /// 业务规则快照。
    /// </summary>
    public sealed record BusinessRuleSnapshot(
        BusinessNoCategory Category,
        string Prefix,
        BusinessWorkflowMode WorkflowMode,
        BusinessApprovalPolicy ApprovalPolicy,
        BusinessAttachmentPolicy AttachmentPolicy);
}
