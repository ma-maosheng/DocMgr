namespace DocMgr.Models.SystemSettings
{
    /// <summary>
    /// 服务器路径预设：按部门配置可访问的服务器路径、权限与容量。
    /// </summary>
    public class ServerPathSetting
    {
        public int Id { get; set; }

        /// <summary>
        /// 所属部门名称；取值为部门表名称或 <see cref="ServerPathSettingDomainValues.PublicDepartment"/>。
        /// </summary>
        public string DepartmentName { get; set; } = string.Empty;

        /// <summary>路径名称。</summary>
        public string PathName { get; set; } = string.Empty;

        /// <summary>物理地址。</summary>
        public string PhysicalPath { get; set; } = string.Empty;

        /// <summary>
        /// 访问权限，取值见 <see cref="ServerPathSettingDomainValues"/>。
        /// </summary>
        public string Permission { get; set; } = ServerPathSettingDomainValues.PermissionReadWrite;

        /// <summary>容量上限（TB）。</summary>
        public decimal CapacityTb { get; set; }
    }
}
