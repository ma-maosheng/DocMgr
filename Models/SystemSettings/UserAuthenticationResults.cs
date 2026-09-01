using System;

namespace DocMgr.Models.SystemSettings
{
    public enum UserLoginStatus
    {
        Success = 0,
        InvalidCredentials = 1,
        AlreadyLoggedIn = 2,
        LockedOut = 3
    }

    /// <summary>本人修改密码的结果。</summary>
    public sealed record PasswordChangeResult(bool Succeeded, string Message)
    {
        public bool IsSuccess => Succeeded;
    }

    public sealed record UserLoginResult(
        UserLoginStatus Status,
        User? User,
        string SessionId,
        string ExistingTerminalName,
        DateTime? ExistingLoginTime,
        string Message)
    {
        public bool IsSuccess =>
            Status == UserLoginStatus.Success
            && User != null
            && !string.IsNullOrWhiteSpace(SessionId);
    }

    public enum UserSessionState
    {
        Valid = 0,
        Expired = 1,
        Replaced = 2,
        LoggedOut = 3,
        NotFound = 4
    }

    public sealed record UserSessionHeartbeatResult(UserSessionState State, string Message)
    {
        public bool IsValid => State == UserSessionState.Valid;
    }
}
