namespace DocMgr.Models.SystemSettings
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;        // 角色名称
        public string Description { get; set; } = string.Empty; // 备注
    }
}