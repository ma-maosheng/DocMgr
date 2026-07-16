namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘归还工作台：申请与审批分流，与出库侧菜单对称。
    /// </summary>
    public enum HardDiskReturnWorkspaceMode
    {
        /// <summary>申请人侧：填写归还申请并提交。</summary>
        Application = 1,

        /// <summary>审批/办理侧：审批、实物交接、上传签批单、办结。</summary>
        Approval = 2
    }
}
