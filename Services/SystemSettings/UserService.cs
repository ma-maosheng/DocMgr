using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.SystemSettings
{
    public class UserService : IUserService
    {
        private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(2);
        private const int MaxFailedLoginAttempts = 5;
        private static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(5);

        private readonly IUserRepository _userRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IRoleRepository _roleRepository;

        public UserService(
            IUserRepository userRepository,
            IDepartmentRepository departmentRepository,
            IRoleRepository roleRepository)
        {
            _userRepository = userRepository;
            _departmentRepository = departmentRepository;
            _roleRepository = roleRepository;
        }

        // === 登录/会话 ===
        public UserLoginResult Login(string loginName, string password, bool forceReplaceExistingSession = false)
        {
            if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrEmpty(password))
            {
                return new UserLoginResult(
                    UserLoginStatus.InvalidCredentials,
                    null,
                    string.Empty,
                    string.Empty,
                    null,
                    "用户名或密码不能为空。");
            }

            var normalizedLoginName = loginName.Trim();
            var user = _userRepository.GetByLoginName(normalizedLoginName);
            if (user == null)
            {
                return CreateInvalidCredentialsResult();
            }

            var now = DateTime.Now;
            if (user.LockoutUntil is DateTime lockoutUntil && lockoutUntil > now)
            {
                return CreateLockedOutResult(lockoutUntil);
            }

            if (user.LockoutUntil != null && user.LockoutUntil <= now)
            {
                user.LockoutUntil = null;
                user.FailedLoginCount = 0;
            }

            if (!PasswordHashingSupport.Verify(password, user.Password))
            {
                RecordFailedLogin(user, now);
                _userRepository.SaveChanges();

                if (user.LockoutUntil is DateTime newlyLocked && newlyLocked > now)
                {
                    return CreateLockedOutResult(newlyLocked);
                }

                return CreateInvalidCredentialsResult();
            }

            ResetLoginFailures(user);
            if (PasswordHashingSupport.NeedsRehash(user.Password))
            {
                user.Password = PasswordHashingSupport.Hash(password);
            }

            using var transaction = _userRepository.BeginTransaction();

            CleanupExpiredSessions(user.Id);

            var activeSession = _userRepository.GetActiveSessions(user.Id).FirstOrDefault();

            if (activeSession != null && !forceReplaceExistingSession)
            {
                try
                {
                    _userRepository.SaveChanges();
                    transaction.Commit();
                }
                catch (DbUpdateException)
                {
                    transaction.Rollback();
                    _userRepository.ClearChangeTracker();
                }

                return BuildAlreadyLoggedInResult(user, activeSession);
            }

            if (activeSession != null)
            {
                DeactivateSessions(user.Id, now);
            }

            var newSession = new UserSession
            {
                UserId = user.Id,
                User = user,
                SessionId = Guid.NewGuid().ToString("N"),
                TerminalName = Environment.MachineName,
                LoginTime = now,
                LastHeartbeatTime = now,
                IsActive = true
            };

            _userRepository.AddSession(newSession);

            try
            {
                _userRepository.SaveChanges();
                transaction.Commit();
            }
            catch (DbUpdateException)
            {
                transaction.Rollback();
                _userRepository.ClearChangeTracker();

                var latestActiveSession = _userRepository.GetActiveSessions(user.Id).FirstOrDefault();

                if (latestActiveSession != null)
                {
                    return BuildAlreadyLoggedInResult(user, latestActiveSession);
                }

                throw;
            }

            return new UserLoginResult(
                UserLoginStatus.Success,
                CreatePublicUser(user),
                newSession.SessionId,
                string.Empty,
                null,
                "登录成功。");
        }

        /// <inheritdoc />
        public PasswordChangeResult ChangeOwnPassword(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                return new PasswordChangeResult(false, "当前密码和新密码都不能为空。");
            }

            var user = _userRepository.GetById(userId);
            if (user == null)
            {
                return new PasswordChangeResult(false, "当前用户不存在，请重新登录。");
            }

            if (!PasswordHashingSupport.Verify(currentPassword, user.Password))
            {
                return new PasswordChangeResult(false, "当前密码不正确。");
            }

            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            {
                return new PasswordChangeResult(false, "新密码不能与当前密码相同。");
            }

            string? policyError = PasswordHashingSupport.ValidatePolicy(newPassword, user.LoginName);
            if (policyError != null)
            {
                return new PasswordChangeResult(false, policyError);
            }

            user.Password = PasswordHashingSupport.Hash(newPassword);
            user.MustChangePassword = false;
            ResetLoginFailures(user);
            _userRepository.SaveChanges();

            return new PasswordChangeResult(true, "密码已修改。");
        }

        public UserSessionHeartbeatResult RefreshSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return new UserSessionHeartbeatResult(UserSessionState.NotFound, "当前登录会话不存在，请重新登录。");
            }

            var session = _userRepository.GetSessionBySessionId(sessionId, asNoTracking: true);

            if (session == null)
            {
                return new UserSessionHeartbeatResult(UserSessionState.NotFound, "当前登录会话不存在，请重新登录。");
            }

            var now = DateTime.Now;
            if (!session.IsActive)
            {
                return BuildInactiveSessionResult(session);
            }

            if (IsExpired(session.LastHeartbeatTime, now))
            {
                var expiredSession = _userRepository.GetSessionBySessionId(session.SessionId);
                if (expiredSession != null)
                {
                    expiredSession.IsActive = false;
                    expiredSession.LogoutTime = now;
                    _userRepository.SaveChanges();
                }

                return new UserSessionHeartbeatResult(UserSessionState.Expired, "当前登录已超时，请重新登录。");
            }

            var activeSession = _userRepository.GetSessionBySessionId(session.SessionId);
            if (activeSession == null || !activeSession.IsActive)
            {
                return BuildInactiveSessionResult(session);
            }

            activeSession.LastHeartbeatTime = now;
            _userRepository.SaveChanges();

            return new UserSessionHeartbeatResult(UserSessionState.Valid, "会话有效。");
        }

        public void Logout(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            var session = _userRepository.GetSessionBySessionId(sessionId);
            if (session == null || !session.IsActive)
            {
                return;
            }

            session.IsActive = false;
            session.LogoutTime = DateTime.Now;
            _userRepository.SaveChanges();
        }

        // === 用户 CRUD ===
        public List<User> GetAllUsers()
        {
            var users = _userRepository.GetAllUsers();
            foreach (var user in users)
            {
                user.Password = string.Empty;
            }

            return users;
        }

        public void AddUser(User user, string password)
        {
            ArgumentNullException.ThrowIfNull(user);

            string? policyError = PasswordHashingSupport.ValidatePolicy(password, user.LoginName);
            if (policyError != null)
            {
                throw new ArgumentException(policyError, nameof(password));
            }

            user.LoginName = user.LoginName?.Trim() ?? string.Empty;
            user.RealName = user.RealName?.Trim() ?? string.Empty;
            user.Department = user.Department?.Trim() ?? string.Empty;
            user.Role = user.Role?.Trim() ?? string.Empty;
            user.Password = PasswordHashingSupport.Hash(password);
            user.MustChangePassword = true;
            user.FailedLoginCount = 0;
            user.LockoutUntil = null;
            _userRepository.AddUser(user);
            _userRepository.SaveChanges();
        }

        public void UpdateUser(User user, string? newPassword = null)
        {
            ArgumentNullException.ThrowIfNull(user);

            var existing = _userRepository.GetById(user.Id);
            if (existing == null)
            {
                return;
            }

            existing.LoginName = user.LoginName?.Trim() ?? string.Empty;
            existing.RealName = user.RealName?.Trim() ?? string.Empty;
            existing.Department = user.Department?.Trim() ?? string.Empty;
            existing.Role = user.Role?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(newPassword))
            {
                string? policyError = PasswordHashingSupport.ValidatePolicy(newPassword, existing.LoginName);
                if (policyError != null)
                {
                    throw new ArgumentException(policyError, nameof(newPassword));
                }

                existing.Password = PasswordHashingSupport.Hash(newPassword);
                existing.MustChangePassword = true;
                ResetLoginFailures(existing);
            }

            _userRepository.SaveChanges();
        }

        public void DeleteUser(int userId)
        {
            var user = _userRepository.GetById(userId);
            if (user != null)
            {
                _userRepository.RemoveUser(user);
                _userRepository.SaveChanges();
            }
        }

        // === 部门 CRUD ===
        public List<Department> GetAllDepartments()
        {
            return _departmentRepository.GetAll();
        }

        public void AddDepartment(Department dept)
        {
            ArgumentNullException.ThrowIfNull(dept);

            _departmentRepository.Add(dept);
            _departmentRepository.SaveChanges();
        }

        public void UpdateDepartment(Department dept)
        {
            ArgumentNullException.ThrowIfNull(dept);

            var existing = _departmentRepository.GetById(dept.Id);
            if (existing == null)
            {
                return;
            }

            existing.Name = dept.Name;
            existing.Description = dept.Description;

            _departmentRepository.SaveChanges();
        }

        public void DeleteDepartment(int deptId)
        {
            var dept = _departmentRepository.GetById(deptId);
            if (dept != null)
            {
                _departmentRepository.Remove(dept);
                _departmentRepository.SaveChanges();
            }
        }

        // === 角色 CRUD ===
        public List<Role> GetAllRoles()
        {
            return _roleRepository.GetAll();
        }

        public void AddRole(Role role)
        {
            ArgumentNullException.ThrowIfNull(role);

            _roleRepository.Add(role);
            _roleRepository.SaveChanges();
        }

        public void UpdateRole(Role role)
        {
            ArgumentNullException.ThrowIfNull(role);

            var existing = _roleRepository.GetById(role.Id);
            if (existing == null)
            {
                return;
            }

            existing.Name = role.Name;
            existing.Description = role.Description;

            _roleRepository.SaveChanges();
        }

        public void DeleteRole(int roleId)
        {
            var role = _roleRepository.GetById(roleId);
            if (role != null)
            {
                _roleRepository.Remove(role);
                _roleRepository.SaveChanges();
            }
        }

        private UserLoginResult BuildAlreadyLoggedInResult(User user, UserSession activeSession)
        {
            return new UserLoginResult(
                UserLoginStatus.AlreadyLoggedIn,
                CreatePublicUser(user),
                string.Empty,
                activeSession.TerminalName,
                activeSession.LoginTime,
                $"该账号已在终端【{activeSession.TerminalName}】登录。");
        }

        private UserSessionHeartbeatResult BuildInactiveSessionResult(UserSession session)
        {
            bool hasOtherActiveSession = _userRepository.HasOtherActiveSession(session.UserId, session.SessionId);

            return hasOtherActiveSession
                ? new UserSessionHeartbeatResult(UserSessionState.Replaced, "当前账号已在其他终端重新登录，本终端已被强制下线。")
                : new UserSessionHeartbeatResult(UserSessionState.LoggedOut, "当前登录已失效，请重新登录。");
        }

        private void CleanupExpiredSessions(int userId)
        {
            var now = DateTime.Now;
            var expiredBefore = now - SessionTimeout;
            var expiredSessions = _userRepository.GetExpiredSessions(userId, expiredBefore);

            if (expiredSessions.Count == 0)
            {
                return;
            }

            foreach (var session in expiredSessions)
            {
                session.IsActive = false;
                session.LogoutTime = now;
            }

            _userRepository.SaveChanges();
        }

        private void DeactivateSessions(int userId, DateTime logoutTime)
        {
            var activeSessions = _userRepository.GetActiveSessions(userId);

            if (activeSessions.Count == 0)
            {
                return;
            }

            foreach (var session in activeSessions)
            {
                session.IsActive = false;
                session.LogoutTime = logoutTime;
            }

            _userRepository.SaveChanges();
        }

        private static bool IsExpired(DateTime lastHeartbeatTime, DateTime now)
        {
            return lastHeartbeatTime < now - SessionTimeout;
        }

        private static void RecordFailedLogin(User user, DateTime now)
        {
            user.FailedLoginCount += 1;
            if (user.FailedLoginCount < MaxFailedLoginAttempts)
            {
                return;
            }

            user.LockoutUntil = now.Add(LoginLockoutDuration);
            user.FailedLoginCount = 0;
        }

        private static void ResetLoginFailures(User user)
        {
            user.FailedLoginCount = 0;
            user.LockoutUntil = null;
        }

        private static UserLoginResult CreateInvalidCredentialsResult()
        {
            return new UserLoginResult(
                UserLoginStatus.InvalidCredentials,
                null,
                string.Empty,
                string.Empty,
                null,
                "用户名或密码错误。");
        }

        private static UserLoginResult CreateLockedOutResult(DateTime lockoutUntil)
        {
            return new UserLoginResult(
                UserLoginStatus.LockedOut,
                null,
                string.Empty,
                string.Empty,
                null,
                $"登录失败次数过多，请于 {lockoutUntil:HH:mm} 后再试。");
        }

        private static User CreatePublicUser(User user)
        {
            return new User
            {
                Id = user.Id,
                LoginName = user.LoginName,
                RealName = user.RealName,
                Department = user.Department,
                Role = user.Role,
                Password = string.Empty,
                CreatedDate = user.CreatedDate,
                MustChangePassword = user.MustChangePassword,
                FailedLoginCount = 0,
                LockoutUntil = null
            };
        }
    }
}
