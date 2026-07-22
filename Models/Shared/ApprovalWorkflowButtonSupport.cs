namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 审批弹窗五按钮统一规则（与硬盘出库审批对齐）：
    /// 审批通过 → 确认实物交接 → 上传签批交接单 → 确认办结 → 打印交接单。
    /// 「待确认办结」阶段仍允许继续上传附件，确认办结后才关闭上传。
    /// </summary>
    public static class ApprovalWorkflowButtonSupport
    {
        /// <summary>审批办理阶段。</summary>
        public enum Phase
        {
            /// <summary>已提交，待审批。</summary>
            PendingApproval,

            /// <summary>已审批，待实物交接。</summary>
            PendingPhysicalHandover,

            /// <summary>已实物交接，待上传签批交接单。</summary>
            PendingSignedUpload,

            /// <summary>必备附件已齐，待确认办结（仍可继续上传附件）。</summary>
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

            /// <summary>兼容旧四按钮命名。</summary>
            public bool CanPrintApprovalForm => CanPrintHandoverSheet;
        }

        /// <summary>
        /// 解析五按钮可用状态。
        /// </summary>
        /// <param name="phase">当前阶段。</param>
        /// <param name="isOperatorAllowed">当前用户是否具备办理权限。</param>
        /// <param name="canExecuteApprovePass">除阶段外，审批通过的前置条件是否满足。</param>
        /// <param name="enableComplete">是否启用「确认办结」（部分业务在独立出库页办结时可传 false）。</param>
        public static ButtonState Resolve(
            Phase phase,
            bool isOperatorAllowed,
            bool canExecuteApprovePass = true,
            bool enableComplete = true)
        {
            if (!isOperatorAllowed)
            {
                return new ButtonState(false, false, false, false, false);
            }

            return phase switch
            {
                Phase.PendingApproval => new ButtonState(canExecuteApprovePass, false, false, false, false),
                Phase.PendingPhysicalHandover => new ButtonState(false, true, false, false, true),
                Phase.PendingSignedUpload => new ButtonState(false, false, true, false, true),
                // 必备附件齐全后仍可补传（如其他附件），确认办结后再关闭上传。
                Phase.PendingComplete => new ButtonState(false, false, true, enableComplete, true),
                Phase.Completed => new ButtonState(false, false, false, false, true),
                _ => new ButtonState(false, false, false, false, false)
            };
        }

        /// <summary>
        /// 按统一 7 态 int 状态与签字件上传标记解析阶段。
        /// </summary>
        public static Phase ResolvePhase(int status, bool signedAttachmentUploaded)
        {
            if (status == ApplicationWorkflowStatus.Completed)
            {
                return Phase.Completed;
            }

            if (status == ApplicationWorkflowStatus.Approved)
            {
                return Phase.PendingPhysicalHandover;
            }

            if (status == ApplicationWorkflowStatus.SignedUploaded)
            {
                return signedAttachmentUploaded
                    ? Phase.PendingComplete
                    : Phase.PendingSignedUpload;
            }

            if (status == ApplicationWorkflowStatus.Submitted)
            {
                return Phase.PendingApproval;
            }

            return Phase.PendingApproval;
        }
    }
}
