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

    void AddProjectInfo(ProjectInfo projectInfo);

    int SaveChanges();
}
