namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 列表/工作台工具栏动作门禁：与 <see cref="OfflineApprovalLifecycleSupport"/> 状态规则对齐。
    /// 所有权、逾期资格、HasApprovalInput 等业务附加条件由调用方传入布尔参数。
    /// </summary>
    public static class ApplicationListActionSupport
    {
        /// <summary>新建草稿（权限由调用方判定）。</summary>
        public static bool CanCreateDraft(bool mayCreate) => mayCreate;

        /// <summary>申请人提交：仅草稿。</summary>
        public static bool CanSubmit(int status, bool isOwnerApplicant)
            => isOwnerApplicant && status == ApplicationWorkflowStatus.Draft;

        /// <summary>
        /// A 流申请人撤回：Draft/Submitted。
        /// <paramref name="domainAllows"/> 用于 YA-OB/YA-REG 等「已录入审批信息不可撤」附加规则。
        /// </summary>
        public static bool CanApplicantWithdraw(int status, bool isOwnerApplicant, bool domainAllows = true)
        {
            if (!isOwnerApplicant || !domainAllows)
            {
                return false;
            }

            return OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    status,
                    signedAttachmentUploaded: false,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.Applicant),
                OfflineApprovalLifecycleSupport.Action.Withdraw).Allowed;
        }

        /// <summary>B 流资料管理员撤回：办结/作废前均可（与生命周期引擎一致；离库处置用）。</summary>
        public static bool CanAdminWithdrawDisposal(int status, bool isArchiveAdmin)
        {
            if (!isArchiveAdmin)
            {
                return false;
            }

            return OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    status,
                    signedAttachmentUploaded: false,
                    OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
                OfflineApprovalLifecycleSupport.Action.Withdraw).Allowed;
        }

        /// <summary>
        /// 盘库登记资料管理员撤回：仅草稿/已提交（审批通过后禁止；与离库处置办结前可撤不同）。
        /// </summary>
        public static bool CanAdminWithdrawInventoryRegister(int status, bool isArchiveAdmin)
        {
            if (!isArchiveAdmin)
            {
                return false;
            }

            return status is ApplicationWorkflowStatus.Draft or ApplicationWorkflowStatus.Submitted;
        }

        /// <summary>
        /// A/B 流强制作废：状态须过引擎；逾期等资格并入 <paramref name="forceVoidEligible"/>。
        /// </summary>
        public static bool CanForceVoid(int status, bool isArchiveAdmin, bool forceVoidEligible)
        {
            if (!isArchiveAdmin)
            {
                return false;
            }

            return OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    status,
                    signedAttachmentUploaded: false,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin,
                    forceVoidEligible),
                OfflineApprovalLifecycleSupport.Action.ForceVoid).Allowed;
        }

        /// <summary>审批工作台可打开办理窗：已提交～已上传签批（含办结后查看由调用方另判）。</summary>
        public static bool CanOpenApprovalProcessing(int status)
            => status is ApplicationWorkflowStatus.Submitted
                or ApplicationWorkflowStatus.Approved
                or ApplicationWorkflowStatus.SignedUploaded;
    }
}
