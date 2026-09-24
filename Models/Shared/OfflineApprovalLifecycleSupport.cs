namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 线下签批生命周期：门禁与状态迁移（不含台账/撤柜等业务效应）。
    /// Service 用法：领域校验 → <see cref="TryTransition"/> / 附件评估 → 写回 NextStatus → 执行本业效应。
    /// </summary>
    public static class OfflineApprovalLifecycleSupport
    {
        /// <summary>流种类（与壳 <see cref="ApprovalWorkflowShellKind"/> 对齐）。</summary>
        public enum FlowKind
        {
            ApplicationHandover = 0,
            DisposalUnlockUpload = 1
        }

        /// <summary>操作者角色（门禁用；领域校验仍由 Service 负责）。</summary>
        public enum ActorRole
        {
            Applicant = 0,
            ArchiveAdmin = 1,
            System = 2
        }

        /// <summary>生命周期动作。</summary>
        public enum Action
        {
            Submit = 0,
            ApprovePass = 1,
            /// <summary>A：确认实物交接；B：确认可上传。</summary>
            ConfirmMidStep = 2,
            Complete = 3,
            Withdraw = 4,
            ForceVoid = 5
        }

        /// <summary>门禁上下文。</summary>
        public readonly struct GateContext
        {
            public GateContext(
                int status,
                bool signedAttachmentUploaded,
                FlowKind flowKind,
                ActorRole actor,
                bool forceVoidEligible = false)
            {
                Status = status;
                SignedAttachmentUploaded = signedAttachmentUploaded;
                FlowKind = flowKind;
                Actor = actor;
                ForceVoidEligible = forceVoidEligible;
            }

            public int Status { get; }
            public bool SignedAttachmentUploaded { get; }
            public FlowKind FlowKind { get; }
            public ActorRole Actor { get; }
            public bool ForceVoidEligible { get; }
        }

        /// <summary>门禁结果。</summary>
        public readonly struct GateResult
        {
            public GateResult(bool allowed, string? denyMessage, int? nextStatus)
            {
                Allowed = allowed;
                DenyMessage = denyMessage;
                NextStatus = nextStatus;
            }

            public bool Allowed { get; }
            public string? DenyMessage { get; }
            public int? NextStatus { get; }

            public static GateResult Ok(int? nextStatus = null) => new(true, null, nextStatus);

            public static GateResult Deny(string message) => new(false, message, null);
        }

        /// <summary>
        /// 评估状态迁移是否允许，并给出下一状态。
        /// 不含明细/签字链/必备附件等业务校验——那些仍由 Service 在调用前后完成。
        /// </summary>
        public static GateResult TryTransition(GateContext ctx, Action action)
        {
            if (IsTerminalVoid(ctx.Status) || ctx.Status == ApplicationWorkflowStatus.Completed)
            {
                if (action is Action.Withdraw or Action.ForceVoid)
                {
                    return GateResult.Deny("当前状态不可撤回或作废。");
                }

                if (action != Action.Complete || ctx.Status == ApplicationWorkflowStatus.Completed)
                {
                    return GateResult.Deny("当前状态不允许该操作。");
                }
            }

            return action switch
            {
                Action.Submit => EvaluateSubmit(ctx),
                Action.ApprovePass => EvaluateApprovePass(ctx),
                Action.ConfirmMidStep => EvaluateConfirmMidStep(ctx),
                Action.Complete => EvaluateComplete(ctx),
                Action.Withdraw => EvaluateWithdraw(ctx),
                Action.ForceVoid => EvaluateForceVoid(ctx),
                _ => GateResult.Deny("不支持的操作。")
            };
        }

        /// <summary>
        /// 附件上传门禁：SignedUploaded 可传；Completed 仅资料管理员增补「其他附件」。
        /// Approved 态默认拒绝签批/照片类（处置流可先确认可上传）；其他附件是否放行由 <paramref name="allowOtherWhileApproved"/> 控制。
        /// </summary>
        public static GateResult EvaluateAttachmentUpload(
            GateContext ctx,
            bool isOtherCategory,
            bool isArchiveAdmin,
            bool allowOtherWhileApproved = false)
        {
            if (IsTerminalVoid(ctx.Status) || ctx.Status == ApplicationWorkflowStatus.Draft
                || ctx.Status == ApplicationWorkflowStatus.Submitted)
            {
                return GateResult.Deny(
                    ctx.FlowKind == FlowKind.DisposalUnlockUpload
                        ? "当前状态不允许上传附件（请在审批通过并确认可上传后操作）。"
                        : "请先确认实物交接后再上传附件。");
            }

            if (ctx.Status == ApplicationWorkflowStatus.Completed)
            {
                if (!isOtherCategory)
                {
                    return GateResult.Deny("办结后仅可增补「其他附件」。");
                }

                if (!isArchiveAdmin)
                {
                    return GateResult.Deny("仅资料管理员可在办结后增补其他附件。");
                }

                return GateResult.Ok();
            }

            if (ctx.Status == ApplicationWorkflowStatus.Approved)
            {
                if (allowOtherWhileApproved && isOtherCategory)
                {
                    return GateResult.Ok();
                }

                return GateResult.Deny(
                    ctx.FlowKind == FlowKind.DisposalUnlockUpload
                        ? "请先确认可上传签批单，再上传签批单或照片。"
                        : "请先确认实物交接后再上传附件。");
            }

            if (ctx.Status == ApplicationWorkflowStatus.SignedUploaded)
            {
                return GateResult.Ok();
            }

            return GateResult.Deny("当前状态不允许上传附件。");
        }

        /// <summary>办结后禁止删除附件。</summary>
        public static GateResult EvaluateAttachmentDelete(int status)
        {
            if (status == ApplicationWorkflowStatus.Completed)
            {
                return GateResult.Deny("办结后不可删除附件。");
            }

            if (IsTerminalVoid(status))
            {
                return GateResult.Deny("已作废单据不可删除附件。");
            }

            return GateResult.Ok();
        }

        /// <summary>与 UI 阶段解析同源。</summary>
        public static ApprovalWorkflowButtonSupport.Phase ResolvePhase(int status, bool signedAttachmentUploaded)
            => ApprovalWorkflowButtonSupport.ResolvePhase(status, signedAttachmentUploaded);

        private static GateResult EvaluateSubmit(GateContext ctx)
        {
            if (ctx.Status != ApplicationWorkflowStatus.Draft)
            {
                return GateResult.Deny("仅草稿状态可提交。");
            }

            return GateResult.Ok(ApplicationWorkflowStatus.Submitted);
        }

        private static GateResult EvaluateApprovePass(GateContext ctx)
        {
            if (ctx.Actor != ActorRole.ArchiveAdmin && ctx.Actor != ActorRole.System)
            {
                return GateResult.Deny("仅资料管理员可执行审批通过。");
            }

            if (ctx.Status != ApplicationWorkflowStatus.Submitted)
            {
                return GateResult.Deny(
                    ctx.FlowKind == FlowKind.DisposalUnlockUpload
                        ? "仅已提交状态可审批。"
                        : "只有“已提交-待审批”的申请单才能执行审批通过。");
            }

            return GateResult.Ok(ApplicationWorkflowStatus.Approved);
        }

        private static GateResult EvaluateConfirmMidStep(GateContext ctx)
        {
            if (ctx.Actor != ActorRole.ArchiveAdmin && ctx.Actor != ActorRole.System)
            {
                return GateResult.Deny(
                    ctx.FlowKind == FlowKind.DisposalUnlockUpload
                        ? "仅资料管理员可确认可上传。"
                        : "仅资料管理员可确认实物交接。");
            }

            if (ctx.Status != ApplicationWorkflowStatus.Approved)
            {
                return GateResult.Deny(
                    ctx.FlowKind == FlowKind.DisposalUnlockUpload
                        ? "请先完成审批后再确认可上传签批单。"
                        : "只有“已审批-待实物交接”的申请单才能确认实物交接。");
            }

            return GateResult.Ok(ApplicationWorkflowStatus.SignedUploaded);
        }

        private static GateResult EvaluateComplete(GateContext ctx)
        {
            if (ctx.Actor != ActorRole.ArchiveAdmin && ctx.Actor != ActorRole.System)
            {
                return GateResult.Deny("仅资料管理员可确认办结。");
            }

            if (ctx.Status != ApplicationWorkflowStatus.SignedUploaded)
            {
                return GateResult.Deny(
                    ctx.FlowKind == FlowKind.DisposalUnlockUpload
                        ? "请先确认可上传签批单后再办结。"
                        : "请先完成实物交接并上传签批交接单后再确认办结。");
            }

            if (!ctx.SignedAttachmentUploaded)
            {
                return GateResult.Deny(
                    ctx.FlowKind == FlowKind.DisposalUnlockUpload
                        ? "请先上传签批单后再办结。"
                        : "请先上传签批交接单后再办理。");
            }

            return GateResult.Ok(ApplicationWorkflowStatus.Completed);
        }

        private static GateResult EvaluateWithdraw(GateContext ctx)
        {
            // 处置流：资料管理员可撤回未办结/未作废单；申请流：申请人仅草稿/已提交可撤回。
            if (ctx.FlowKind == FlowKind.DisposalUnlockUpload)
            {
                if (ctx.Actor != ActorRole.ArchiveAdmin && ctx.Actor != ActorRole.System)
                {
                    return GateResult.Deny("仅资料管理员可撤回作废处置单。");
                }

                if (ctx.Status is ApplicationWorkflowStatus.Completed
                    or ApplicationWorkflowStatus.Withdrawn
                    or ApplicationWorkflowStatus.ForceWithdrawn)
                {
                    return GateResult.Deny("当前状态不可撤回作废。");
                }

                return GateResult.Ok(ApplicationWorkflowStatus.Withdrawn);
            }

            if (ctx.Status is not (ApplicationWorkflowStatus.Draft or ApplicationWorkflowStatus.Submitted))
            {
                return GateResult.Deny("当前申请单已进入或完成审批信息阶段，不允许申请人撤回作废。");
            }

            return GateResult.Ok(ApplicationWorkflowStatus.Withdrawn);
        }

        private static GateResult EvaluateForceVoid(GateContext ctx)
        {
            if (ctx.Actor != ActorRole.ArchiveAdmin && ctx.Actor != ActorRole.System)
            {
                return GateResult.Deny("仅资料管理员可执行强制撤回作废。");
            }

            if (ctx.Status is ApplicationWorkflowStatus.Completed
                or ApplicationWorkflowStatus.Withdrawn
                or ApplicationWorkflowStatus.ForceWithdrawn)
            {
                return GateResult.Deny("当前申请单状态不允许强制撤回作废。");
            }

            if (!ctx.ForceVoidEligible)
            {
                return GateResult.Deny("当前申请单未达到可强制作废的条件。");
            }

            // 已审批及之后：通常不允许强制撤回（避免半办理态被抹掉）；与 HD-OB 一致。
            if (ctx.Status is ApplicationWorkflowStatus.Approved
                or ApplicationWorkflowStatus.SignedUploaded)
            {
                return GateResult.Deny("当前申请单已录入审批信息或已上传附件，不允许强制撤回作废。");
            }

            return GateResult.Ok(ApplicationWorkflowStatus.ForceWithdrawn);
        }

        private static bool IsTerminalVoid(int status)
            => status is ApplicationWorkflowStatus.Withdrawn or ApplicationWorkflowStatus.ForceWithdrawn;
    }
}
