using DocMgr.Models.Shared;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘出库申请审批弹窗五按钮可用状态规则。
    /// 委托统一的 <see cref="ApprovalWorkflowButtonSupport"/>，保持硬盘出库与其它申请业务一致。
    /// </summary>
    public static class HardDiskOutboundApprovalButtonSupport
    {
        public enum Phase
        {
            PendingApproval = 0,
            PendingPhysicalHandover = 1,
            PendingSignedUpload = 2,
            PendingComplete = 3,
            Completed = 4
        }

        public readonly struct ButtonState
        {
            public ButtonState(
                bool canApprovePass,
                bool canConfirmPhysicalHandover,
                bool canUploadSignedAttachment,
                bool canConfirmComplete,
                bool canPrintHandoverSheet)
            {
                CanApprovePass = canApprovePass;
                CanConfirmPhysicalHandover = canConfirmPhysicalHandover;
                CanUploadSignedAttachment = canUploadSignedAttachment;
                CanConfirmComplete = canConfirmComplete;
                CanPrintHandoverSheet = canPrintHandoverSheet;
            }

            internal ButtonState(ApprovalWorkflowButtonSupport.ButtonState inner)
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
        }

        public static ButtonState Resolve(Phase phase, bool isOperatorAllowed) =>
            new(ApprovalWorkflowButtonSupport.Resolve(
                (ApprovalWorkflowButtonSupport.Phase)(int)phase,
                isOperatorAllowed));

        public static Phase ResolvePhase(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);

            return (Phase)(int)ApprovalWorkflowButtonSupport.ResolvePhase(
                application.ApplicationStatus,
                application.SignedAttachmentUploaded);
        }
    }
}
