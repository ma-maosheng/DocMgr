using System.Linq;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Infrastructure.Seeding;

/// <summary>
/// 本单位固定身份种子：部门、角色、用户、示例项目（空库/开发同步权威源）。
/// </summary>
public static class SystemIdentitySeedCatalog
{
    /// <summary>部门种子项。</summary>
    public sealed record DepartmentSeed(string Name, string Description = "");

    /// <summary>角色种子项。</summary>
    public sealed record RoleSeed(string Name, string Description = "");

    /// <summary>用户种子项（Password 为初始明文，仅首次建账号时哈希写入）。</summary>
    public sealed record UserSeed(
        string LoginName,
        string RealName,
        string Department,
        string Role,
        string Password);

    /// <summary>项目信息种子项。</summary>
    public sealed record ProjectInfoSeed(
        string ProjectName,
        string ProjectCode,
        string ImplementYear,
        string CapitalMgrDept,
        string Remark = "");

    /// <summary>院内科室编制。</summary>
    public static IReadOnlyList<DepartmentSeed> Departments { get; } =
    [
        new("院领导"),
        new("办公室"),
        new("党委办公室"),
        new("财务科"),
        new("人事科"),
        new("综合业务科"),
        new("生产管理科"),
        new("技术管理科"),
        new("质量检验室"),
        new("基础测绘室"),
        new("航测外业室"),
        new("地理信息室"),
        new("自然资源调查室"),
        new("国土空间核查室"),
        new("空天遥感室"),
        new("应急保障办公室"),
        new("地籍室"),
        new("科研开发室"),
        new(UserRoleDomainValues.ArchiveRoomDepartment)
    ];

    /// <summary>系统角色（权限相关名称须与 <see cref="UserRoleDomainValues"/> 一致）。</summary>
    public static IReadOnlyList<RoleSeed> Roles { get; } =
    [
        new(UserRoleDomainValues.SystemAdministrator, "系统运维（用户/部门/高级数据等）；不办理资料业务"),
        new(UserRoleDomainValues.DepartmentArchiveClerk, "各部门申请经办；所属部门不能是资料室"),
        new(UserRoleDomainValues.ArchiveAdmin, "资料室办理（审批/立档/盘库等）；所属部门必须是资料室"),
        new(UserRoleDomainValues.NetworkAdmin, "维护服务器路径设置；不办理资料申请与审批"),
        new(UserRoleDomainValues.DepartmentHead, "签字栏参考角色（申请部门负责人）；登录不获申请/审批菜单"),
        new(UserRoleDomainValues.ProductionHead, "签字栏参考角色；所属部门必须是生产管理科；对应提示词「生产科负责人」"),
        new(UserRoleDomainValues.ArchiveRoomHead, "签字栏参考角色；所属部门必须是资料室；对应提示词「资料室负责人」"),
        new(UserRoleDomainValues.ProductionVicePresident, "签字栏参考角色；所属部门必须是院领导"),
        new(UserRoleDomainValues.ArchiveDeputyPresident, "签字栏参考角色；所属部门必须是院领导"),
        new(UserRoleDomainValues.President, "签字栏参考角色")
    ];

    /// <summary>本单位初始账号（空库写入；已有用户不覆盖口令）。</summary>
    public static IReadOnlyList<UserSeed> Users { get; } =
    [
        new(DefaultAdministratorBootstrap.LoginName, "系统管理员", "科研开发室", UserRoleDomainValues.SystemAdministrator, DefaultAdministratorBootstrap.InitialPassword),
        new("shl", "孙惠龙", "科研开发室", UserRoleDomainValues.NetworkAdmin, "shl"),
        new("mxc", "马小超", "航测外业室", UserRoleDomainValues.DepartmentArchiveClerk, "mxc"),
        new("lhw", "刘红文", "航测外业室", UserRoleDomainValues.DepartmentHead, "lhw"),
        new("zc", "张琛", "地理信息室", UserRoleDomainValues.DepartmentArchiveClerk, "zc"),
        new("mbc", "马丙超", "地理信息室", UserRoleDomainValues.DepartmentHead, "mbc"),
        new("zxh", "张新行", "院领导", UserRoleDomainValues.ProductionVicePresident, "zxh"),
        new("mms", "马茂生", "院领导", UserRoleDomainValues.ArchiveDeputyPresident, "mms"),
        new("wjf", "吴建菲", UserRoleDomainValues.ProductionManagementDepartment, UserRoleDomainValues.ProductionHead, "wjf"),
        new("gpf", "郭鹏飞", UserRoleDomainValues.ArchiveRoomDepartment, UserRoleDomainValues.ArchiveRoomHead, "gpf"),
        new("wxl", "吴秀丽", UserRoleDomainValues.ArchiveRoomDepartment, UserRoleDomainValues.ArchiveAdmin, "wxl"),
        new("jn", "及那", UserRoleDomainValues.ArchiveRoomDepartment, UserRoleDomainValues.ArchiveAdmin, "jn"),
        new("gpf_kf", "郭鹏飞", "科研开发室", UserRoleDomainValues.DepartmentHead, "gpf_kf"),
        new("wxl_kf", "吴秀丽", "科研开发室", UserRoleDomainValues.DepartmentArchiveClerk, "wxl_kf")
    ];

    /// <summary>示例项目（开发同步用；空库一并写入）。</summary>
    public static IReadOnlyList<ProjectInfoSeed> ProjectInfos { get; } =
    [
        //new("基础测绘", "2026001", "2026", "财务处", "无"),
        //new("国情普查", "2026002", "2026", "财务处", "无")
    ];

    /// <summary>
    /// 审批业务必有部门：院领导、生产管理科、资料室（对应二级审核/审批节点与角色部门约束）。
    /// 其余种子科室仅为编制示例，可增删改。
    /// </summary>
    public static IReadOnlyList<string> RequiredDepartmentNames { get; } =
    [
        UserRoleDomainValues.LeadershipDepartment,
        UserRoleDomainValues.ProductionManagementDepartment,
        UserRoleDomainValues.ArchiveRoomDepartment
    ];

    private static readonly HashSet<string> RequiredDepartmentNameSet =
        new(RequiredDepartmentNames, StringComparer.Ordinal);

    private static readonly HashSet<string> SeededDepartmentNames =
        new(Departments.Select(d => d.Name), StringComparer.Ordinal);

    private static readonly HashSet<string> SeededUserLoginNames =
        new(Users.Select(u => u.LoginName), StringComparer.Ordinal);

    /// <summary>是否审批业务必有部门（名称勿改、勿删）。</summary>
    public static bool IsRequiredDepartment(string? name)
        => !string.IsNullOrWhiteSpace(name) && RequiredDepartmentNameSet.Contains(name.Trim());

    /// <summary>是否代码种子部门（按名称；含示例科室，不等于必有）。</summary>
    public static bool IsSeededDepartment(string? name)
        => !string.IsNullOrWhiteSpace(name) && SeededDepartmentNames.Contains(name.Trim());

    /// <summary>是否代码种子用户（按登录名）。</summary>
    public static bool IsSeededUser(string? loginName)
        => !string.IsNullOrWhiteSpace(loginName) && SeededUserLoginNames.Contains(loginName.Trim());

    /// <summary>
    /// 部门列表「部门说明」展示：仅必有部门附加提示（仅 UI，不写库）。
    /// </summary>
    public static string FormatDepartmentDescriptionDisplay(string? name, string? description)
    {
        string trimmedDescription = description?.Trim() ?? string.Empty;
        if (!IsRequiredDepartment(name))
        {
            return trimmedDescription;
        }

        const string seedHint = "系统必有部门（名称勿改、勿删）";
        return string.IsNullOrEmpty(trimmedDescription)
            ? seedHint
            : $"{seedHint}；{trimmedDescription}";
    }

}
