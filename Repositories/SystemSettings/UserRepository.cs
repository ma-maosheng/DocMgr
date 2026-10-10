using DocMgr.Data;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocMgr.Repositories.SystemSettings;

public class UserRepository : IUserRepository
{
    private sealed class UserRepositoryTransaction : IUserRepositoryTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public UserRepositoryTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public void Commit()
        {
            _transaction.Commit();
        }

        public void Rollback()
        {
            _transaction.Rollback();
        }

        public void Dispose()
        {
            _transaction.Dispose();
        }
    }

    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public User? GetByLoginName(string loginName)
    {
        string normalized = (loginName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return _dbContext.Users.FirstOrDefault(user => user.LoginName == normalized);
    }

    public bool HasAnyUsers()
    {
        return _dbContext.Users.Any();
    }

    public User? GetById(int userId)
    {
        return _dbContext.Users.FirstOrDefault(user => user.Id == userId);
    }

    public List<User> GetAllUsers()
    {
        return _dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .ToList();
    }

    public List<UserSession> GetActiveSessions(int userId)
    {
        return _dbContext.UserSessions
            .Where(session => session.UserId == userId && session.IsActive)
            .OrderByDescending(session => session.LastHeartbeatTime)
            .ToList();
    }

    public List<UserSession> GetExpiredSessions(int userId, DateTime expiredBefore)
    {
        return _dbContext.UserSessions
            .Where(session => session.UserId == userId && session.IsActive && session.LastHeartbeatTime < expiredBefore)
            .ToList();
    }

    public IReadOnlyList<UserSession> GetOnlineSessions(DateTime heartbeatNotBefore)
    {
        return _dbContext.UserSessions
            .AsNoTracking()
            .Include(session => session.User)
            .Where(session => session.IsActive && session.LastHeartbeatTime >= heartbeatNotBefore)
            .OrderByDescending(session => session.LastHeartbeatTime)
            .ToList();
    }

    public int DeactivateExpiredSessions(DateTime expiredBefore, DateTime logoutTime)
    {
        var expiredSessions = _dbContext.UserSessions
            .Where(session => session.IsActive && session.LastHeartbeatTime < expiredBefore)
            .ToList();

        if (expiredSessions.Count == 0)
        {
            return 0;
        }

        foreach (var session in expiredSessions)
        {
            session.IsActive = false;
            session.LogoutTime = logoutTime;
        }

        return expiredSessions.Count;
    }

    public IReadOnlyList<UserSession> SearchSessions(
        DateTime? loginFromInclusive,
        DateTime? loginToExclusive,
        string? keyword,
        int limit)
    {
        int take = limit <= 0 ? 500 : Math.Min(limit, 2000);
        IQueryable<UserSession> query = _dbContext.UserSessions
            .AsNoTracking()
            .Include(session => session.User);

        if (loginFromInclusive.HasValue)
        {
            DateTime from = loginFromInclusive.Value;
            query = query.Where(session => session.LoginTime >= from);
        }

        if (loginToExclusive.HasValue)
        {
            DateTime to = loginToExclusive.Value;
            query = query.Where(session => session.LoginTime < to);
        }

        string key = (keyword ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(key))
        {
            query = query.Where(session =>
                session.TerminalName.Contains(key)
                || (session.User != null
                    && (session.User.LoginName.Contains(key)
                        || session.User.RealName.Contains(key)
                        || session.User.Department.Contains(key)
                        || session.User.Role.Contains(key))));
        }

        return query
            .OrderByDescending(session => session.LoginTime)
            .Take(take)
            .ToList();
    }

    public UserSession? GetSessionBySessionId(string sessionId, bool asNoTracking = false)
    {
        IQueryable<UserSession> query = _dbContext.UserSessions;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefault(session => session.SessionId == sessionId);
    }

    public bool HasOtherActiveSession(int userId, string sessionId)
    {
        return _dbContext.UserSessions.Any(session =>
            session.UserId == userId &&
            session.IsActive &&
            session.SessionId != sessionId);
    }

    public void AddSession(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _dbContext.UserSessions.Add(session);
    }

    public void AddUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _dbContext.Users.Add(user);
    }

    public void RemoveUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _dbContext.Users.Remove(user);
    }

    public IUserRepositoryTransaction BeginTransaction()
    {
        return new UserRepositoryTransaction(_dbContext.Database.BeginTransaction());
    }

    public void ClearChangeTracker()
    {
        _dbContext.ChangeTracker.Clear();
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
