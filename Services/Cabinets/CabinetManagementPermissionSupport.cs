namespace DocMgr.Services.Cabinets;

/// <summary>
/// 档案柜管理权限：登记仅资料室资料管理员；检索时其他人仅可浏览、检索与查看。
/// </summary>
public static class CabinetManagementPermissionSupport
{
    public const string RegisterDeniedMessage = "仅资料室资料管理员可办理档案柜登记。";
    public const string MaintainDeniedMessage = "仅资料室资料管理员可维护档案柜档口、摆放与柜体。";

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
