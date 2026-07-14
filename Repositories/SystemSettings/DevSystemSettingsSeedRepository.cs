using DocMgr.Data;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Repositories.SystemSettings;

public class DevSystemSettingsSeedRepository : IDevSystemSettingsSeedRepository
{
    private readonly AppDbContext _dbContext;

    public DevSystemSettingsSeedRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Department? GetDepartmentByName(string name)
    {
        return _dbContext.Departments.FirstOrDefault(item => item.Name == name);
    }

    public void AddDepartment(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        _dbContext.Departments.Add(department);
    }

    public Role? GetRoleByName(string name)
    {
        return _dbContext.Roles.FirstOrDefault(item => item.Name == name);
    }

    public void AddRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _dbContext.Roles.Add(role);
    }

    public User? GetUserByLoginName(string loginName)
    {
        return _dbContext.Users.FirstOrDefault(item => item.LoginName == loginName);
    }

    public void AddUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _dbContext.Users.Add(user);
    }

    public ProjectInfo? GetProjectInfoById(int id)
    {
        return _dbContext.ProjectInfos.FirstOrDefault(item => item.Id == id);
    }

    public void AddProjectInfo(ProjectInfo projectInfo)
    {
        ArgumentNullException.ThrowIfNull(projectInfo);
        _dbContext.ProjectInfos.Add(projectInfo);
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
