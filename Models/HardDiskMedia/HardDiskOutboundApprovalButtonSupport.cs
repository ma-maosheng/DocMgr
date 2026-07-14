using System;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘出库申请审批弹窗五按钮可用状态规则。
    /// 流程：审批通过 → 确认实物交接 → 上传签批交接单 → 确认办结 → 打印交接单。
    /// </summary>
    public static class HardDiskOutboundApprovalButtonSupport
    {
        /// <summary>出库审批办理阶段。</summary>
        public enum Phase
        {
            /// <summary>已提交，待审批。</summary>
            PendingApproval,

            /// <summary>已审批，待实物交接。</summary>
            PendingPhysicalHandover,

            /// <summary>已实物交接，待上传签批交接单。</summary>
            PendingSignedUpload,

            /// <summary>已上传签批交接单，待确认办结。</summary>
            PendingComplete,

            /// <summary>已办结。</summary>
            Completed
        }

        /// <summary>五按钮可用状态。</summary>
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

            public bool CanApprovePass { get; }
            public bool CanConfirmPhysicalHandover { get; }
            public bool CanUploadSignedAttachment { get; }
            public bool CanConfirmComplete { get; }
            public bool CanPrintHandoverSheet { get; }
        }

        /// <summary>
        /// 解析出库审批弹窗按钮可用状态。
        /// </summary>
        public static ButtonState Resolve(Phase phase, bool isOperatorAllowed)
        {
            if (!isOperatorAllowed)
            {
                return new ButtonState(false, false, false, false, false);
            }

            return phase switch
            {
                Phase.PendingApproval => new ButtonState(true, false, false, false, false),
                Phase.PendingPhysicalHandover => new ButtonState(false, true, false, false, true),
                Phase.PendingSignedUpload => new ButtonState(false, false, true, false, true),
                Phase.PendingComplete => new ButtonState(false, false, false, true, true),
                Phase.Completed => new ButtonState(false, false, false, false, true),
                _ => new ButtonState(false, false, false, false, false)
            };
        }

        /// <summary>
        /// 根据申请单状态与签字件上传标记解析办理阶段。
        /// </summary>
        public static Phase ResolvePhase(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);

            if (string.Equals(application.ApplicationStatus, HardDiskMediaApplication.StatusCompleted, StringComparison.Ordinal))
            {
                return Phase.Completed;
            }

            if (string.Equals(application.ApplicationStatus, HardDiskMediaApplication.StatusApproved, StringComparison.Ordinal))
            {
                return Phase.PendingPhysicalHandover;
            }

            if (string.Equals(application.ApplicationStatus, HardDiskMediaApplication.StatusSignedUploaded, StringComparison.Ordinal))
            {
                return application.SignedAttachmentUploaded
                    ? Phase.PendingComplete
                    : Phase.PendingSignedUpload;
            }

            if (string.Equals(application.ApplicationStatus, HardDiskMediaApplication.StatusSubmitted, StringComparison.Ordinal))
            {
                return Phase.PendingApproval;
            }

            return Phase.PendingApproval;
        }
    }
}
