using DocMgr.Models.SystemSettings;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.Projects;

/// <summary>
/// 项目信息设置权限：新增、编辑、删除仅资料室资料管理员；其他人仅可浏览与检索。
/// </summary>
public static class ProjectSettingPermissionSupport
{
    public const string MaintainDeniedMessage = "仅资料室资料管理员可编辑项目信息。";

    /// <summary>
    /// 资料室资料管理员可维护项目信息。系统管理员不替代本角色。
    /// </summary>
    public static bool CanMaintain(User? user) =>
        ArchiveRegisterBusinessRules.IsArchiveAdminUser(user);

    public static void EnsureCanMaintain(User? user)
    {
        if (!CanMaintain(user))
        {
            throw new InvalidOperationException(MaintainDeniedMessage);
        }
    }
}
