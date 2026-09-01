using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.SystemSettings;

/// <summary>
/// 服务器路径设置权限：仅角色「网管负责人」可进入维护页并增删改。
/// 出入网申请中选择已有路径不走本校验。系统管理员不替代本角色。
/// </summary>
public static class ServerPathSettingPermissionSupport
{
    /// <summary>用户角色名称，须与角色表完全一致。</summary>
    public const string RoleName = "网管负责人";

    public const string DeniedMessage = "仅网管负责人可维护服务器路径设置。";

    /// <summary>当前用户是否可维护服务器路径。</summary>
    public static bool CanMaintain(User? user)
    {
        if (user == null)
        {
            return false;
        }

        string role = user.Role?.Trim() ?? string.Empty;
        return string.Equals(role, RoleName, StringComparison.Ordinal);
    }

    /// <summary>无权限时抛出业务异常。</summary>
    public static void EnsureCanMaintain(User? user)
    {
        if (!CanMaintain(user))
        {
            throw new InvalidOperationException(DeniedMessage);
        }
    }
}
