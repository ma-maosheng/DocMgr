namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 线下签批办理壳类型：申请交接流（A）与离库处置流（B）共用阶段机，文案与底栏按钮略有差异。
    /// </summary>
    public enum ApprovalWorkflowShellKind
    {
        /// <summary>申请审批类：确认实物交接 → 上传签批交接单 → 办结 → 打印交接单。</summary>
        ApplicationHandover = 0,

        /// <summary>离库处置类：确认可上传 → 上传签批单 → 办结 → 打印签批单。</summary>
        DisposalUnlockUpload = 1
    }
}
