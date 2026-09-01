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

    UserSession? GetSessionBySessionId(string sessionId, bool asNoTracking = false);

    bool HasOtherActiveSession(int userId, string sessionId);

    void AddSession(UserSession session);

    void AddUser(User user);

    void RemoveUser(User user);

    IUserRepositoryTransaction BeginTransaction();

    void ClearChangeTracker();

    int SaveChanges();
}
