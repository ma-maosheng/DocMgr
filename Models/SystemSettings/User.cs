using System;

namespace DocMgr.Models.SystemSettings
{
    public class User
    {
        public int Id { get; set; }
        public string LoginName { get; set; } = string.Empty;   // 用户登录名称
        public string RealName { get; set; } = string.Empty;    // 真实姓名
        public string Department { get; set; } = string.Empty;  // 所属部门
        public string Role { get; set; } = string.Empty;        // 用户角色
        public string Password { get; set; } = string.Empty;    // 密码 (加密存储)
        public DateTime CreatedDate { get; set; }               // 开通日期
    }
}