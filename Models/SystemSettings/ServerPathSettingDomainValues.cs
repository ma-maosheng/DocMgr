namespace DocMgr.Models.SystemSettings
{
    /// <summary>
    /// 服务器路径设置域值：部门范围与访问权限。
    /// </summary>
    public static class ServerPathSettingDomainValues
    {
        /// <summary>所有部门可用的部门标识。</summary>
        public const string PublicDepartment = "公用";

        public const string PermissionRead = "读";
        public const string PermissionWrite = "写";
        public const string PermissionReadWrite = "读写";

        public static IReadOnlyList<string> PermissionOptions { get; } =
        [
            PermissionRead,
            PermissionWrite,
            PermissionReadWrite
        ];

        /// <summary>是否为可写入权限（写 / 读写）。</summary>
        public static bool IsWritablePermission(string? permission)
        {
            string trimmed = permission?.Trim() ?? string.Empty;
            return string.Equals(trimmed, PermissionWrite, StringComparison.Ordinal)
                   || string.Equals(trimmed, PermissionReadWrite, StringComparison.Ordinal);
        }
    }
}
