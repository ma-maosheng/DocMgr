using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 用户数据访问契约：用户、角色、部门关联数据读写。
/// </summary>
public interface IUserRepository
{
    /// <summary>按登录名查询用户（不含口令比对）。</summary>
    User? GetByLoginName(string loginName);

    /// <summary>用户表是否已有任意账号。</summary>
    bool HasAnyUsers();

    User? GetById(int userId);

    List<User> GetAllUsers();

    List<UserSession> GetActiveSessions(int userId);

    List<UserSession> GetExpiredSessions(int userId, DateTime expiredBefore);

    /// <summary>查询心跳未过期的活跃会话（含用户），按最近心跳倒序。</summary>
    IReadOnlyList<UserSession> GetOnlineSessions(DateTime heartbeatNotBefore);

    /// <summary>将心跳早于阈值的活跃会话标记为结束。</summary>
    int DeactivateExpiredSessions(DateTime expiredBefore, DateTime logoutTime);

    /// <summary>按登录时间与关键字检索会话履历（含用户），按登录时间倒序。</summary>
    IReadOnlyList<UserSession> SearchSessions(
        DateTime? loginFromInclusive,
        DateTime? loginToExclusive,
        string? keyword,
        int limit);

    UserSession? GetSessionBySessionId(string sessionId, bool asNoTracking = false);

    bool HasOtherActiveSession(int userId, string sessionId);

    void AddSession(UserSession session);

    void AddUser(User user);

    void RemoveUser(User user);

    IUserRepositoryTransaction BeginTransaction();

    void ClearChangeTracker();

    int SaveChanges();
}
