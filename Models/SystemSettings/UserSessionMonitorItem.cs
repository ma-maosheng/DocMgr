namespace DocMgr.Models.SystemSettings;

/// <summary>在线用户 / 登录记录列表行。</summary>
public sealed class UserSessionMonitorItem
{
    public int Id { get; init; }

    public string LoginName { get; init; } = string.Empty;

    public string RealName { get; init; } = string.Empty;

    public string Department { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string TerminalName { get; init; } = string.Empty;

    public DateTime LoginTime { get; init; }

    public DateTime LastHeartbeatTime { get; init; }

    public DateTime? LogoutTime { get; init; }

    public bool IsActive { get; init; }

    /// <summary>在线 / 已结束 / 心跳超时。</summary>
    public string StatusDisplay { get; init; } = string.Empty;
}
