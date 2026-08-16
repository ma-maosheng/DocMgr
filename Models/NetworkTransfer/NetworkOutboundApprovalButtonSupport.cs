using DocMgr.Models.Shared;

namespace DocMgr.Models.NetworkTransfer;

/// <summary>
/// 出网申请审批弹窗五按钮规则（委托统一 <see cref="ApprovalWorkflowButtonSupport"/>）。
/// </summary>
public static class NetworkOutboundApprovalButtonSupport
{
    public readonly struct ButtonState
    {
        public ButtonState(ApprovalWorkflowButtonSupport.ButtonState inner)
        {
            CanApprovePass = inner.CanApprovePass;
            CanConfirmPhysicalHandover = inner.CanConfirmPhysicalHandover;
            CanUploadSignedAttachment = inner.CanUploadSignedAttachment;
            CanConfirmComplete = inner.CanConfirmComplete;
            CanPrintHandoverSheet = inner.CanPrintHandoverSheet;
        }

        public bool CanApprovePass { get; }
        public bool CanConfirmPhysicalHandover { get; }
        public bool CanUploadSignedAttachment { get; }
        public bool CanConfirmComplete { get; }
        public bool CanPrintHandoverSheet { get; }

        public bool CanPrintApprovalForm => CanPrintHandoverSheet;
    }

    public static ButtonState Resolve(
        NetworkOutboundRecord record,
        bool isOperatorAllowed,
        bool canExecuteApprovePass,
        bool attachmentsMeetMandatoryRequirements)
    {
        ArgumentNullException.ThrowIfNull(record);

        ApprovalWorkflowButtonSupport.Phase phase = ResolvePhase(record, attachmentsMeetMandatoryRequirements);
        return new ButtonState(ApprovalWorkflowButtonSupport.Resolve(
            phase,
            isOperatorAllowed,
            canExecuteApprovePass));
    }

    public static ApprovalWorkflowButtonSupport.Phase ResolvePhase(
        NetworkOutboundRecord record,
        bool attachmentsMeetMandatoryRequirements)
    {
        if (record.Status == NetworkOutboundRecord.StatusCompleted)
        {
            return ApprovalWorkflowButtonSupport.Phase.Completed;
        }

        if (record.Status == NetworkOutboundRecord.StatusSignedUploaded)
        {
            return attachmentsMeetMandatoryRequirements
                ? ApprovalWorkflowButtonSupport.Phase.PendingComplete
                : ApprovalWorkflowButtonSupport.Phase.PendingSignedUpload;
        }

        if (record.Status == NetworkOutboundRecord.StatusApproved)
        {
            return ApprovalWorkflowButtonSupport.Phase.PendingPhysicalHandover;
        }

        if (record.Status == NetworkOutboundRecord.StatusSubmitted)
        {
            return ApprovalWorkflowButtonSupport.Phase.PendingApproval;
        }

        return ApprovalWorkflowButtonSupport.Phase.PendingApproval;
    }
}
