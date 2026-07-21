namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还工作台：申请 / 审批入库分流，与借出侧「借出申请 / 审批出库」对称。
    /// </summary>
    public enum ArchiveReturnWorkspaceMode
    {
        /// <summary>申请人侧：填写归还申请并提交。</summary>
        Application = 1,

        /// <summary>资料室侧：审批 → 实物交接 → 办结入库（合并原审批与入库菜单）。</summary>
        Approval = 2,

        /// <summary>兼容旧「资料归还」入口；行为与 <see cref="Approval"/> 相同。</summary>
        Handover = 3
    }
}
