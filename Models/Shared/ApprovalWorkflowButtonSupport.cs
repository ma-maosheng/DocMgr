namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 审批弹窗四按钮（审批通过 / 上传签字件 / 确认办结 / 打印审批单）可用状态统一规则。
    /// </summary>
    public static class ApprovalWorkflowButtonSupport
    {
        /// <summary>审批办理阶段。</summary>
        public enum Phase
        {
            /// <summary>已提交，审批尚未通过。</summary>
            PendingApproval,

            /// <summary>审批已通过，尚未确认办结。</summary>
            ApprovalInProgress,

            /// <summary>确认办结已完成。</summary>
            ApprovalCompleted
        }

        /// <summary>四按钮可用状态。</summary>
        public readonly struct ButtonState
        {
            public ButtonState(
                bool canApprovePass,
                bool canUploadSignedAttachment,
                bool canConfirmComplete,
                bool canPrintApprovalForm)
            {
                CanApprovePass = canApprovePass;
                CanUploadSignedAttachment = canUploadSignedAttachment;
                CanConfirmComplete = canConfirmComplete;
                CanPrintApprovalForm = canPrintApprovalForm;
            }

            /// <summary>是否可执行「审批通过」。</summary>
            public bool CanApprovePass { get; }

            /// <summary>是否可执行「上传签字件」。</summary>
            public bool CanUploadSignedAttachment { get; }

            /// <summary>是否可执行「确认办结」。</summary>
            public bool CanConfirmComplete { get; }

            /// <summary>是否可执行「打印审批单」（或业务等价的打印按钮）。</summary>
            public bool CanPrintApprovalForm { get; }
        }

        /// <summary>
        /// 解析四按钮可用状态。
        /// </summary>
        /// <param name="phase">当前审批阶段。</param>
        /// <param name="isOperatorAllowed">当前用户是否具备办理权限（如资料室管理员）。</param>
        /// <param name="canExecuteApprovePass">除阶段外，审批通过的前置条件是否满足（如审批项已完整）。</param>
        public static ButtonState Resolve(Phase phase, bool isOperatorAllowed, bool canExecuteApprovePass = true)
        {
            if (!isOperatorAllowed)
            {
                return new ButtonState(false, false, false, false);
            }

            return phase switch
            {
                Phase.PendingApproval => new ButtonState(canExecuteApprovePass, false, false, false),
                Phase.ApprovalInProgress => new ButtonState(false, true, true, false),
                Phase.ApprovalCompleted => new ButtonState(false, false, false, true),
                _ => new ButtonState(false, false, false, false)
            };
        }
    }
}
