using System.Collections.Generic;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 用户账户管理服务契约：用户、角色、部门及登录凭据的维护。
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// 执行登录，并按单点登录规则创建或接管会话。
        /// </summary>
        UserLoginResult Login(string loginName, string password, bool forceReplaceExistingSession = false);

        /// <summary>
        /// 当前用户修改本人密码。
        /// </summary>
        PasswordChangeResult ChangeOwnPassword(int userId, string currentPassword, string newPassword);

        /// <summary>
        /// 刷新当前会话心跳。
        /// </summary>
        UserSessionHeartbeatResult RefreshSession(string sessionId);

        /// <summary>
        /// 注销指定会话。
        /// </summary>
        void Logout(string sessionId);

        /// <summary>
        /// 查询当前在线用户（系统管理员 / 资料管理员）。
        /// </summary>
        IReadOnlyList<UserSessionMonitorItem> GetOnlineUsers(User operatorUser);

        /// <summary>
        /// 查询用户登录记录（系统管理员 / 资料管理员）。
        /// </summary>
        IReadOnlyList<UserSessionMonitorItem> SearchLoginRecords(
            User operatorUser,
            DateTime? loginFromInclusive,
            DateTime? loginToInclusive,
            string? keyword,
            int limit = 500);

        // 用户管理
        List<User> GetAllUsers();
        void AddUser(User user, string password);
        void UpdateUser(User user, string? newPassword = null);
        void DeleteUser(int userId);

        // 部门管理
        List<Department> GetAllDepartments();
        void AddDepartment(Department dept);
        void UpdateDepartment(Department dept);
        void DeleteDepartment(int deptId);

        // 角色管理
        List<Role> GetAllRoles();
        void AddRole(Role role);
        void UpdateRole(Role role);
        void DeleteRole(int roleId);
    }
}
