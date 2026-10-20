namespace DocMgr.Models.SystemSettings;

/// <summary>
/// 登录用户业务角色名称与部门约束（权限仅按角色判定；部门用于开账号校验与业务归属）。
/// 空库种子与权限判定共用本类常量，避免双端漂移。
/// </summary>
public static class UserRoleDomainValues
{
    /// <summary>系统运维（英文角色名，与种子/历史数据一致）。</summary>
    public const string SystemAdministrator = "Administrator";

    /// <summary>系统运维中文别名（权限判定兼容）。</summary>
    public const string SystemAdministratorAlias = "管理员";

    /// <summary>各部门申请经办：仅发起建档/借出/归还/出入网/硬盘申请等。</summary>
    public const string DepartmentArchiveClerk = "部门资料员";

    /// <summary>资料室办理：审批、立档、盘库、离库、开柜等。</summary>
    public const string ArchiveAdmin = "资料管理员";

    /// <summary>仅维护服务器路径设置。</summary>
    public const string NetworkAdmin = "网管负责人";

    /// <summary>签字栏参考角色：申请部门负责人（登录不获申请/审批菜单）。</summary>
    public const string DepartmentHead = "部门负责人";

    /// <summary>签字栏参考角色：生产管理科部门负责人（对应审批节点「生产科负责人」）。</summary>
    public const string ProductionHead = "生产负责人";

    /// <summary>签字栏参考角色：资料室部门负责人（对应审批节点「资料室负责人」）。</summary>
    public const string ArchiveRoomHead = "资料负责人";

    /// <summary>签字栏参考角色：分管生产副院长。</summary>
    public const string ProductionVicePresident = "分管生产副院长";

    /// <summary>签字栏参考角色：分管资料副院长。</summary>
    public const string ArchiveDeputyPresident = "分管资料副院长";

    /// <summary>签字栏参考角色：院长。</summary>
    public const string President = "院长";

    /// <summary>资料室部门名称。</summary>
    public const string ArchiveRoomDepartment = "资料室";

    /// <summary>生产管理科部门名称。</summary>
    public const string ProductionManagementDepartment = "生产管理科";

    /// <summary>院领导部门名称。</summary>
    public const string LeadershipDepartment = "院领导";

    /// <summary>
    /// 用户表应至少各有一条对应账号的角色（运维、网管、审批签字参考、资料室办理）。
    /// 不含部门资料员/部门负责人等可按科室增删的角色。
    /// </summary>
    public static IReadOnlyList<string> RequiredRoles { get; } =
    [
        SystemAdministrator,
        NetworkAdmin,
        ProductionVicePresident,
        ArchiveDeputyPresident,
        ProductionHead,
        ArchiveRoomHead,
        ArchiveAdmin
    ];

    /// <summary>用户管理页底端说明（仅 UI）。</summary>
    public static string RequiredRolesPageHint { get; } =
        "本表须至少各有一条与下列角色对应的用户："
        + string.Join("、", RequiredRoles)
        + "。上述角色用于系统运维、路径维护及审核审批签字；其余角色可按科室需要增减。";

    /// <summary>是否系统管理员角色（含中文别名）。</summary>
    public static bool IsSystemAdministratorRole(string? role)
    {
        string normalized = role?.Trim() ?? string.Empty;
        return string.Equals(normalized, SystemAdministrator, StringComparison.Ordinal)
            || string.Equals(normalized, SystemAdministratorAlias, StringComparison.Ordinal);
    }

    /// <summary>
    /// 校验角色与所属部门是否匹配；返回错误文案，合法时返回 null。
    /// </summary>
    public static string? ValidateRoleDepartment(string? role, string? department)
    {
        string normalizedRole = role?.Trim() ?? string.Empty;
        string normalizedDept = department?.Trim() ?? string.Empty;

        if (string.Equals(normalizedRole, DepartmentArchiveClerk, StringComparison.Ordinal))
        {
            return IsDepartment(normalizedDept, ArchiveRoomDepartment)
                ? $"角色「{DepartmentArchiveClerk}」的所属部门不能是「{ArchiveRoomDepartment}」。"
                : null;
        }

        if (string.Equals(normalizedRole, ArchiveAdmin, StringComparison.Ordinal))
        {
            return IsDepartment(normalizedDept, ArchiveRoomDepartment)
                ? null
                : $"角色「{ArchiveAdmin}」的所属部门必须是「{ArchiveRoomDepartment}」。";
        }

        if (string.Equals(normalizedRole, ArchiveRoomHead, StringComparison.Ordinal))
        {
            return IsDepartment(normalizedDept, ArchiveRoomDepartment)
                ? null
                : $"角色「{ArchiveRoomHead}」的所属部门必须是「{ArchiveRoomDepartment}」。";
        }

        if (string.Equals(normalizedRole, ProductionHead, StringComparison.Ordinal))
        {
            return IsDepartment(normalizedDept, ProductionManagementDepartment)
                ? null
                : $"角色「{ProductionHead}」的所属部门必须是「{ProductionManagementDepartment}」。";
        }

        if (string.Equals(normalizedRole, ProductionVicePresident, StringComparison.Ordinal)
            || string.Equals(normalizedRole, ArchiveDeputyPresident, StringComparison.Ordinal))
        {
            return IsDepartment(normalizedDept, LeadershipDepartment)
                ? null
                : $"角色「{normalizedRole}」的所属部门必须是「{LeadershipDepartment}」。";
        }

        return null;
    }

    private static bool IsDepartment(string department, string expected)
        => string.Equals(department, expected, StringComparison.Ordinal);
}
