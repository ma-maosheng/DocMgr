namespace DocMgr.Services.SystemSettings;

/// <summary>
/// 角色概览页权限矩阵支撑：汇总各角色类别可访问的菜单分组。
/// 导航全员可进浏览；写入/办理仍按角色；业务列表范围仍为仅本人/本部门（资料管理员看全库）。
/// 本矩阵仅作查阅展示，调整入口为「用户管理」。
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

    /// <summary>按主界面导航分组的权限矩阵（浏览=全员；办理/维护=角色）。</summary>
    public static IReadOnlyList<PermissionMatrixRow> BuildMatrix() => new[]
    {
        new PermissionMatrixRow(
            "全员可浏览", "左侧全部业务与系统设置菜单可进入查看；业务列表仍按「仅本人/本部门」过滤（资料管理员看全库）",
            SystemAdmin: true, ArchiveRoomAdmin: true, DepartmentAdmin: true, NetworkManager: true, Others: true),
        new PermissionMatrixRow(
            "年度资料·申请办理", "建档/借出/归还申请的新增与提交（部门资料员）",
            SystemAdmin: false, ArchiveRoomAdmin: false, DepartmentAdmin: true, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "年度资料·审批办理", "申请审批、立档、直办立档、台账维护、出库/归还审批、迁档、盘库、离库处置等写入",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "跨域出入网·申请办理", "入网/出网申请的新增与提交（部门资料员）",
            SystemAdmin: false, ArchiveRoomAdmin: false, DepartmentAdmin: true, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "跨域出入网·审批办理", "入网/出网审批、在网数据处置写入",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "硬盘·申请办理", "硬盘出库/归还申请的新增与提交（部门资料员）",
            SystemAdmin: false, ArchiveRoomAdmin: false, DepartmentAdmin: true, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "硬盘·审批办理", "硬盘初始登记、出库/归还审批、盘库、离库处置写入",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "历史存档·维护", "地形图、航摄、其他图件的导入、编辑、删除；历史离库处置办理",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "档案柜·维护", "档案柜登记、开柜改档口用途/迁档/摆放",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "项目信息·维护", "新增、编辑、删除项目",
            SystemAdmin: false, ArchiveRoomAdmin: true, DepartmentAdmin: false, NetworkManager: false, Others: false),
        new PermissionMatrixRow(
            "服务器路径·维护", "维护服务器路径（新增、编辑、删除）",
            SystemAdmin: false, ArchiveRoomAdmin: false, DepartmentAdmin: false, NetworkManager: true, Others: false),
        new PermissionMatrixRow(
            "系统运维·维护", "用户/部门/审核审批/逾期设置/库日志清除与启停、高级数据高危维护",
            SystemAdmin: true, ArchiveRoomAdmin: false, DepartmentAdmin: false, NetworkManager: false, Others: false),
    };
}
