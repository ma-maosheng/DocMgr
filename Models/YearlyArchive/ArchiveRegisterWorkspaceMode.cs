namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料登记页面工作台：申请与审批分流，便于导航与权限控制。
    /// </summary>
    public enum ArchiveRegisterWorkspaceMode
    {
        /// <summary>申请人侧：填写与提交申请，不展示审批/附件办理区。</summary>
        Application = 1,

        /// <summary>资料室审批侧：办理审批、附件与「保存审批，启动立档」。</summary>
        Approval = 2,
    }
}
