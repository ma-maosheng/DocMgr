namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 审批办理壳统一文案（A 申请交接流 / B 处置确认可上传流）。
    /// 阶段语义一律对标 <see cref="ApprovalWorkflowButtonSupport"/>，仅展示文案分叉。
    /// </summary>
    public static class ApprovalWorkflowShellCopySupport
    {
        public const string SectionTitleApprovalHandover = "审批与交接信息";
        public const string SectionTitleAttachments = "附件材料";
        public const string ApproveButtonText = "审批通过";
        public const string CompleteButtonText = "确认办结";
        public const string CloseButtonText = "关闭";
        public const string SaveDraftButtonText = "保存草稿";
        public const string SubmitButtonText = "提交";
        public const string SubmitInventoryRegisterButtonText = "提交盘库信息";
        public const string WithdrawButtonText = "撤回作废";

        public const string ConfirmPhysicalHandoverButtonText = "确认实物交接";
        public const string ConfirmUploadButtonText = "确认可上传";
        /// <summary>盘库登记办理壳中间步骤按钮文案。</summary>
        public const string ConfirmUploadAttachmentInfoButtonText = "确认可上传附件信息";

        public const string PrintHandoverSheetButtonText = "打印交接单";
        public const string PrintApprovalFormButtonText = "打印签批单";

        public const string SignedHandoverZoneTitle = "签批交接单";
        public const string SignedFormZoneTitle = "签批单";
        public const string PhysicalPhotoZoneTitle = "实物照片";
        public const string DiskPhotoZoneTitle = "硬盘照片";
        public const string ScenePhotoZoneTitle = "现场照片";
        public const string DisposalMaterialPhotoZoneTitle = "处置资料照片";
        public const string MaterialPhotoZoneTitle = "资料照片";
        public const string ProofMaterialZoneTitle = "证明材料";
        public const string OtherAttachmentZoneTitle = "其他附件";
        public const string NoUploadNeededText = "无需上传";

        /// <summary>顶部流程提示（办理顺序）。</summary>
        public static string GetWorkspaceBannerText(ApprovalWorkflowShellKind kind) =>
            kind switch
            {
                ApprovalWorkflowShellKind.DisposalUnlockUpload =>
                    "流程：审批通过 → 确认可上传 → 分区上传附件 → 确认办结 → 打印签批单。",
                _ =>
                    "请按“审批通过→确认实物交接→分区上传附件→确认办结→打印交接单”的顺序办理。"
            };

        /// <summary>中间解锁步骤按钮文案。</summary>
        public static string GetConfirmMidStepButtonText(ApprovalWorkflowShellKind kind) =>
            kind == ApprovalWorkflowShellKind.DisposalUnlockUpload
                ? ConfirmUploadButtonText
                : ConfirmPhysicalHandoverButtonText;

        /// <summary>底栏打印按钮文案。</summary>
        public static string GetPrintButtonText(ApprovalWorkflowShellKind kind) =>
            kind == ApprovalWorkflowShellKind.DisposalUnlockUpload
                ? PrintApprovalFormButtonText
                : PrintHandoverSheetButtonText;

        /// <summary>签批附件区标题。</summary>
        public static string GetSignedZoneTitle(ApprovalWorkflowShellKind kind) =>
            kind == ApprovalWorkflowShellKind.DisposalUnlockUpload
                ? SignedFormZoneTitle
                : SignedHandoverZoneTitle;

        /// <summary>
        /// 是否显示草稿/提交/撤回等发起态底栏按钮（处置流常见；申请审批窗通常为办理专用窗，不显示）。
        /// </summary>
        public static bool ShowsDraftSubmitWithdraw(ApprovalWorkflowShellKind kind) =>
            kind == ApprovalWorkflowShellKind.DisposalUnlockUpload;
    }
}
