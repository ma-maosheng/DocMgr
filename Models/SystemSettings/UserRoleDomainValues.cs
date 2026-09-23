namespace DocMgr.Models.SystemSettings;

/// <summary>
/// 登录用户业务角色名称与部门约束（权限仅按角色判定；部门用于开账号校验与业务归属）。
/// </summary>
public static class UserRoleDomainValues
{
    /// <summary>各部门申请经办：仅发起建档/借出/归还/出入网/硬盘申请等。</summary>
    public const string DepartmentArchiveClerk = "部门资料员";

    /// <summary>资料室办理：审批、立档、盘库、离库、开柜等。</summary>
    public const string ArchiveAdmin = "资料管理员";

    /// <summary>资料室部门名称；担任「资料管理员」时必须所属此部门，「部门资料员」不得属于此部门。</summary>
    public const string ArchiveRoomDepartment = "资料室";

    /// <summary>
    /// 校验角色与所属部门是否匹配；返回错误文案，合法时返回 null。
    /// </summary>
    public static string? ValidateRoleDepartment(string? role, string? department)
    {
        string normalizedRole = role?.Trim() ?? string.Empty;
        string normalizedDept = department?.Trim() ?? string.Empty;
        bool isArchiveRoom = string.Equals(
            normalizedDept,
            ArchiveRoomDepartment,
            StringComparison.Ordinal);

        if (string.Equals(normalizedRole, DepartmentArchiveClerk, StringComparison.Ordinal))
        {
            return isArchiveRoom
                ? $"角色「{DepartmentArchiveClerk}」的所属部门不能是「{ArchiveRoomDepartment}」。"
                : null;
        }

        if (string.Equals(normalizedRole, ArchiveAdmin, StringComparison.Ordinal))
        {
            return isArchiveRoom
                ? null
                : $"角色「{ArchiveAdmin}」的所属部门必须是「{ArchiveRoomDepartment}」。";
        }

        return null;
    }
}
