using System;

namespace DocMgr.Models.SystemSettings
{
    public class UserSession
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string SessionId { get; set; } = string.Empty;

        public string TerminalName { get; set; } = string.Empty;

        public DateTime LoginTime { get; set; }

        public DateTime LastHeartbeatTime { get; set; }

        public DateTime? LogoutTime { get; set; }

        public bool IsActive { get; set; }

        public User? User { get; set; }
    }
}
