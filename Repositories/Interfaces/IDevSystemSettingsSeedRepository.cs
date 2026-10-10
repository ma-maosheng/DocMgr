namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 开发期系统设置种子数据访问契约：开发/测试初始化数据读写。
/// </summary>
public interface IDevSystemSettingsSeedRepository
{
    bool HasAnyDepartments();

    bool HasAnyRoles();

    bool HasAnyUsers();

    Department? GetDepartmentByName(string name);

    void AddDepartment(Department department);

    Role? GetRoleByName(string name);

    void AddRole(Role role);

    User? GetUserByLoginName(string loginName);

    void AddUser(User user);

    ProjectInfo? GetProjectInfoById(int id);

    ProjectInfo? GetProjectInfoByCode(string projectCode);

    ProjectInfo? GetProjectInfoByNameAndYear(string projectName, string implementYear);

    void AddProjectInfo(ProjectInfo projectInfo);

    int SaveChanges();

    /// <summary>
    /// 丢弃当前上下文未提交变更（种子失败时避免污染同作用域后续写入）。
    /// </summary>
    void RejectPendingChanges();
}
