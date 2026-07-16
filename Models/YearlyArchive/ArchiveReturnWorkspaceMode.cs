namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还工作台：申请 / 审批 / 入库办结分流，与借出侧菜单对称。
    /// </summary>
    public enum ArchiveReturnWorkspaceMode
    {
        /// <summary>申请人侧：填写归还申请并提交。</summary>
        Application = 1,

        /// <summary>审批侧：审批归还申请。</summary>
        Approval = 2,

        /// <summary>入库侧：实物交接、上传签批单、办结入库。</summary>
        Handover = 3
    }
}
