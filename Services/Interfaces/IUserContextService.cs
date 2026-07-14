using System.ComponentModel;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 当前登录用户上下文服务契约：提供当前用户身份与权限状态并通知变更。
    /// </summary>
    /// <summary>
    /// 当前登录用户上下文服务契约：维护并通知当前登录用户及其权限信息。
    /// </summary>
    public interface IUserContextService : INotifyPropertyChanged
    {
        User? CurrentUser { get; }

        string? CurrentSessionId { get; }

        /// <summary>
        /// 写入当前登录用户与会话标识。
        /// </summary>
        void SetCurrentSession(User user, string sessionId);

        /// <summary>
        /// 清空当前登录上下文。
        /// </summary>
        void Clear();
    }
}
