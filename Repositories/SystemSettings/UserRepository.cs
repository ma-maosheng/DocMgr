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

    public User? GetByLogin(string loginName, string hashedPassword)
    {
        return _dbContext.Users.FirstOrDefault(user =>
            user.LoginName == loginName &&
            user.Password == hashedPassword);
    }

    public User? GetById(int userId)
    {
        return _dbContext.Users.FirstOrDefault(user => user.Id == userId);
    }

    public List<User> GetAllUsers()
    {
        return _dbContext.Users.OrderBy(user => user.Id).ToList();
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
