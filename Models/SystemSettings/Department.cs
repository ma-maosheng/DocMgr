namespace DocMgr.Models.SystemSettings
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;        // 部门名称
        public string Description { get; set; } = string.Empty; // 备注/描述
    }
}