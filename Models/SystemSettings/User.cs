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
        public string Password { get; set; } = string.Empty;    // 密码哈希
        public DateTime CreatedDate { get; set; }               // 开通日期

        /// <summary>为 true 时，下次登录必须先修改密码。</summary>
        public bool MustChangePassword { get; set; }

        /// <summary>连续登录失败次数（锁定后清零）。</summary>
        public int FailedLoginCount { get; set; }

        /// <summary>登录锁定截止时间；空表示未锁定。</summary>
        public DateTime? LockoutUntil { get; set; }
    }
}