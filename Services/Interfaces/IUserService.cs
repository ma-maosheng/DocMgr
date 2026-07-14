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
        /// 刷新当前会话心跳。
        /// </summary>
        UserSessionHeartbeatResult RefreshSession(string sessionId);

        /// <summary>
        /// 注销指定会话。
        /// </summary>
        void Logout(string sessionId);

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
