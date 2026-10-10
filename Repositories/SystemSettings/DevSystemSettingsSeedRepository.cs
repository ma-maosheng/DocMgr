using DocMgr.Data;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.SystemSettings;

public class DevSystemSettingsSeedRepository : IDevSystemSettingsSeedRepository
{
    private readonly AppDbContext _dbContext;

    public DevSystemSettingsSeedRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool HasAnyDepartments()
    {
        return _dbContext.Departments.Any();
    }

    public bool HasAnyRoles()
    {
        return _dbContext.Roles.Any();
    }

    public bool HasAnyUsers()
    {
        return _dbContext.Users.Any();
    }

    public Department? GetDepartmentByName(string name)
    {
        return _dbContext.Departments.FirstOrDefault(item => item.Name == name);
    }

    public void AddDepartment(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        department.Id = 0;
        _dbContext.Departments.Add(department);
    }

    public Role? GetRoleByName(string name)
    {
        return _dbContext.Roles.FirstOrDefault(item => item.Name == name);
    }

    public void AddRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        role.Id = 0;
        _dbContext.Roles.Add(role);
    }

    public User? GetUserByLoginName(string loginName)
    {
        return _dbContext.Users.FirstOrDefault(item => item.LoginName == loginName);
    }

    public void AddUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.Id = 0;
        _dbContext.Users.Add(user);
    }

    public ProjectInfo? GetProjectInfoById(int id)
    {
        return _dbContext.ProjectInfos.FirstOrDefault(item => item.Id == id);
    }

    public ProjectInfo? GetProjectInfoByCode(string projectCode)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
        {
            return null;
        }

        string code = projectCode.Trim();
        return _dbContext.ProjectInfos.FirstOrDefault(item => item.ProjectCode == code);
    }

    public ProjectInfo? GetProjectInfoByNameAndYear(string projectName, string implementYear)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return null;
        }

        string name = projectName.Trim();
        string year = implementYear?.Trim() ?? string.Empty;
        return _dbContext.ProjectInfos.FirstOrDefault(item =>
            item.ProjectName == name
            && item.ImplementYear == year);
    }

    public void AddProjectInfo(ProjectInfo projectInfo)
    {
        ArgumentNullException.ThrowIfNull(projectInfo);
        // 禁止种子/调用方写入显式 Id；交由 SQL Server IDENTITY 生成。
        projectInfo.Id = 0;
        _dbContext.ProjectInfos.Add(projectInfo);
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }

    public void RejectPendingChanges()
    {
        _dbContext.ChangeTracker.Clear();
    }
}
