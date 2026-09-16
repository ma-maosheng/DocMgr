namespace DocMgr.Services.SystemSettings;

/// <summary>
/// 权限设置页支撑：汇总各角色类别可访问的菜单分组。
/// 权限由「部门 + 角色」按业务规则判定（见 ArchiveRegisterBusinessRules 与各 *PermissionSupport），
/// 本矩阵仅作查阅展示，修改入口为「用户管理」「角色设置」。
/// </summary>
public static class PermissionSettingSupport
{
    /// <summary>权限矩阵行：菜单分组与各角色类别的可访问性。</summary>
    public sealed record PermissionMatrixRow(
        string GroupName,
        string MenuSummary,
        bool SystemAdmin,
        bool ArchiveRoomAdmin,
        bool DepartmentAdmin,
        bool NetworkManager,
        bool Others);

    /// <summary>按主界面导航分组的权限矩阵。</summary>
    public static IReadOnlyList<PermissionMatrixRow> BuildMatrix() => new[]
    {
        new PermissionMatrixRow(
            "年度资料·申请", "建档申请、资料借出（出库）申请、归还申请",
            SystemAdmin: false, ArchiveRoomAdmin: false, DepartmentAdmin: true, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "年度资料·办理", "申请审批、资料立档、库存硬盘/存档文本直办立档、立档/迁档/流转/跨域流转台账、出库审批、归还审批、模拟/电子迁档、模拟/电子盘库、离库处置",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "跨域出入网·申请", "入网申请、出网申请",
            SystemAdmin: false, ArchiveRoomAdmin: false, DepartmentAdmin: true, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "跨域出入网·办理", "入网审批、出网审批、在网数据处置",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "硬盘·申请", "硬盘出库申请、硬盘归还申请",
            SystemAdmin: false, ArchiveRoomAdmin: false, DepartmentAdmin: true, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "硬盘·办理", "硬盘初始登记（台账）、出库审批、归还审批、盘库登记、离库处置",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "历史存档", "地形图、航摄、其他图件的导入、编辑、删除",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "档案柜", "档案柜登记、开柜改档口用途/迁档/摆放",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "项目信息", "新增、编辑、删除项目",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "服务器路径设置", "维护服务器路径（新增、编辑、删除）",
            SystemAdmin: false, ArchiveRoomAdmin: false, DepartmentAdmin: false, NetworkManager: true, Others: false),
        new PermissionMatrixRow(
            "系统运维", "用户管理、部门设置、角色设置、权限设置、高级数据管理（全量维护）、逾期设置、数据库操作日志",
            SystemAdmin: true, ArchiveRoomAdmin: false, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "全员浏览", "操作手册、个人设置、资料检索与检索池、历史三页浏览、档案柜检索、硬盘/光盘概览与台账、高级数据管理（仅浏览）",
            SystemAdmin: true, ArchiveRoomAdmin: true, DepartmentAdmin: true, NetworkManager: true, Others: true),
    };
}
