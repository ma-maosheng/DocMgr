namespace DocMgr.Services.HistoryArchive;

/// <summary>
/// 历史存档「资料录入与编辑」权限：导入、编辑、删除仅资料室资料管理员；
/// 其他部门资料管理员仅可浏览与检索。
/// </summary>
public static class HistoryArchiveLedgerPermissionSupport
{
    public const string MaintainDeniedMessage = "仅资料室资料管理员可录入或编辑历史存档资料。";

    /// <summary>
        /// 资料室资料管理员可维护台账。系统管理员不替代本角色。
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
