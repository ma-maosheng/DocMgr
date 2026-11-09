namespace DocMgr.ViewModels.Projects;

/// <summary>
/// 项目信息列表行。库内引用与删除前校验使用同一范围。
/// </summary>
public sealed class ProjectSettingRow
{
    public ProjectSettingRow(ProjectInfo project, bool isReferenced)
    {
        Project = project;
        IsReferenced = isReferenced;
    }

    public ProjectInfo Project { get; }

    public int Id => Project.Id;

    public string ProjectName => Project.ProjectName ?? string.Empty;

    public string ProjectCode => Project.ProjectCode ?? string.Empty;

    public string ImplementYear => Project.ImplementYear ?? string.Empty;

    public string CapitalMgrDept => Project.CapitalMgrDept ?? string.Empty;

    public string Remark => Project.Remark ?? string.Empty;

    /// <summary>建档、立档、出库、归还、档案盒、电子袋、盘库或出入网中仍有该项目。</summary>
    public bool IsReferenced { get; }

    public string ReferenceDisplay => IsReferenced ? "已引用" : "未引用";
}
