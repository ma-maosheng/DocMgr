using DocMgr.Models.Projects;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.SystemSettings;
using System.Diagnostics;

namespace DocMgr.Infrastructure.Seeding;

/// <summary>
/// 从 <see cref="SystemIdentitySeedCatalog"/> 写入部门/角色/用户/项目（不再读取外部 JSON）。
/// </summary>
public static class DevSystemSettingsSeeder
{
    /// <summary>
    /// 空库启动：部门、角色、用户表各自为空时一次性写入目录种子；不更新已有记录；不写入项目。
    /// </summary>
    public static void SeedIfEmpty(IDevSystemSettingsSeedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var changed = false;

        if (!repository.HasAnyDepartments())
        {
            foreach (var departmentSeed in SystemIdentitySeedCatalog.Departments)
            {
                repository.AddDepartment(new Department
                {
                    Name = departmentSeed.Name,
                    Description = departmentSeed.Description
                });
                changed = true;
            }
        }

        if (!repository.HasAnyRoles())
        {
            foreach (var roleSeed in SystemIdentitySeedCatalog.Roles)
            {
                repository.AddRole(new Role
                {
                    Name = roleSeed.Name,
                    Description = roleSeed.Description
                });
                changed = true;
            }
        }

        if (!repository.HasAnyUsers())
        {
            foreach (var userSeed in SystemIdentitySeedCatalog.Users)
            {
                repository.AddUser(CreateUserFromSeed(userSeed, forceMustChangePasswordInRelease: true));
                changed = true;
            }
        }

        if (changed)
        {
            repository.SaveChanges();
            Debug.WriteLine("[SystemSeed] 空库已从代码目录写入部门/角色/用户。");
        }
    }

    /// <summary>
    /// 开发期全量同步：补缺失项并更新描述等字段，含项目信息。Release 构建不执行。
    /// </summary>
    public static void SeedSyncForDebug(IDevSystemSettingsSeedRepository repository)
    {
#if DEBUG
        ArgumentNullException.ThrowIfNull(repository);

        try
        {
            var changed = false;

            foreach (var departmentSeed in SystemIdentitySeedCatalog.Departments)
            {
                var existingDepartment = repository.GetDepartmentByName(departmentSeed.Name);
                if (existingDepartment == null)
                {
                    repository.AddDepartment(new Department
                    {
                        Name = departmentSeed.Name,
                        Description = departmentSeed.Description
                    });
                    changed = true;
                    continue;
                }

                if ((existingDepartment.Description ?? string.Empty) != departmentSeed.Description)
                {
                    existingDepartment.Description = departmentSeed.Description;
                    changed = true;
                }
            }

            foreach (var roleSeed in SystemIdentitySeedCatalog.Roles)
            {
                var existingRole = repository.GetRoleByName(roleSeed.Name);
                if (existingRole == null)
                {
                    repository.AddRole(new Role
                    {
                        Name = roleSeed.Name,
                        Description = roleSeed.Description
                    });
                    changed = true;
                    continue;
                }

                if ((existingRole.Description ?? string.Empty) != roleSeed.Description)
                {
                    existingRole.Description = roleSeed.Description;
                    changed = true;
                }
            }

            foreach (var userSeed in SystemIdentitySeedCatalog.Users)
            {
                var existingUser = repository.GetUserByLoginName(userSeed.LoginName);
                if (existingUser == null)
                {
                    repository.AddUser(CreateUserFromSeed(userSeed, forceMustChangePasswordInRelease: false));
                    changed = true;
                    continue;
                }

                if (existingUser.RealName != userSeed.RealName)
                {
                    existingUser.RealName = userSeed.RealName;
                    changed = true;
                }

                if (existingUser.Department != userSeed.Department)
                {
                    existingUser.Department = userSeed.Department;
                    changed = true;
                }

                if (existingUser.Role != userSeed.Role)
                {
                    existingUser.Role = userSeed.Role;
                    changed = true;
                }
            }

            foreach (var projectSeed in SystemIdentitySeedCatalog.ProjectInfos)
            {
                var existingProject = ResolveExistingProject(repository, projectSeed);
                if (existingProject == null)
                {
                    repository.AddProjectInfo(new ProjectInfo
                    {
                        ProjectName = projectSeed.ProjectName,
                        ProjectCode = projectSeed.ProjectCode,
                        ImplementYear = projectSeed.ImplementYear,
                        CapitalMgrDept = projectSeed.CapitalMgrDept,
                        Remark = projectSeed.Remark
                    });
                    changed = true;
                    continue;
                }

                if (existingProject.ProjectName != projectSeed.ProjectName)
                {
                    existingProject.ProjectName = projectSeed.ProjectName;
                    changed = true;
                }

                if (existingProject.ProjectCode != projectSeed.ProjectCode)
                {
                    existingProject.ProjectCode = projectSeed.ProjectCode;
                    changed = true;
                }

                if (existingProject.ImplementYear != projectSeed.ImplementYear)
                {
                    existingProject.ImplementYear = projectSeed.ImplementYear;
                    changed = true;
                }

                if (existingProject.CapitalMgrDept != projectSeed.CapitalMgrDept)
                {
                    existingProject.CapitalMgrDept = projectSeed.CapitalMgrDept;
                    changed = true;
                }

                if (existingProject.Remark != projectSeed.Remark)
                {
                    existingProject.Remark = projectSeed.Remark;
                    changed = true;
                }
            }

            if (changed)
            {
                repository.SaveChanges();
                Debug.WriteLine("[DevSeed] 部门/角色/用户/项目已从代码目录同步到数据库。");
            }
            else
            {
                Debug.WriteLine("[DevSeed] 无需更新。");
            }
        }
        catch (Exception ex)
        {
            try
            {
                repository.RejectPendingChanges();
            }
            catch
            {
                // ignore
            }

            Debug.WriteLine("[DevSeed] 导入失败: " + ex);
            throw;
        }
#else
        _ = repository;
#endif
    }

    private static User CreateUserFromSeed(
        SystemIdentitySeedCatalog.UserSeed userSeed,
        bool forceMustChangePasswordInRelease)
    {
        bool mustChangePassword;
#if DEBUG
        mustChangePassword = false;
#else
        mustChangePassword = forceMustChangePasswordInRelease;
#endif

        return new User
        {
            LoginName = userSeed.LoginName,
            RealName = string.IsNullOrWhiteSpace(userSeed.RealName) ? userSeed.LoginName : userSeed.RealName,
            Department = userSeed.Department,
            Role = userSeed.Role,
            Password = PasswordHashingSupport.Hash(
                string.IsNullOrWhiteSpace(userSeed.Password)
                    ? DefaultAdministratorBootstrap.InitialPassword
                    : userSeed.Password),
            CreatedDate = DateTime.Now,
            MustChangePassword = mustChangePassword,
            FailedLoginCount = 0
        };
    }

    private static ProjectInfo? ResolveExistingProject(
        IDevSystemSettingsSeedRepository repository,
        SystemIdentitySeedCatalog.ProjectInfoSeed projectSeed)
    {
        ProjectInfo? existing = null;
        if (!string.IsNullOrWhiteSpace(projectSeed.ProjectCode))
        {
            existing = repository.GetProjectInfoByCode(projectSeed.ProjectCode);
        }

        if (existing == null && !string.IsNullOrWhiteSpace(projectSeed.ProjectName))
        {
            existing = repository.GetProjectInfoByNameAndYear(
                projectSeed.ProjectName,
                projectSeed.ImplementYear);
        }

        return existing;
    }
}
