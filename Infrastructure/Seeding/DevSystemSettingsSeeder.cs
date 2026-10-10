using DocMgr.Models.Projects;
using DocMgr.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DocMgr.Infrastructure.Seeding;

public static class DevSystemSettingsSeeder
{
    /// <summary>
    /// Release/空库启动：部门、角色、用户表各自为空时，从外部 JSON 一次性写入初始数据；不更新已有记录，不写入项目。
    /// </summary>
    public static void SeedFromExternalFileIfEmpty(IDevSystemSettingsSeedRepository repository, string filePath)
    {
        ArgumentNullException.ThrowIfNull(repository);

        if (!TryLoadSeedFile(filePath, out var seed))
        {
            return;
        }

        var changed = false;

        if (!repository.HasAnyDepartments())
        {
            foreach (var departmentSeed in seed.Departments ?? [])
            {
                if (string.IsNullOrWhiteSpace(departmentSeed.Name))
                {
                    continue;
                }

                repository.AddDepartment(new Department
                {
                    Name = departmentSeed.Name.Trim(),
                    Description = departmentSeed.Description ?? string.Empty
                });
                changed = true;
            }
        }

        if (!repository.HasAnyRoles())
        {
            foreach (var roleSeed in seed.Roles ?? [])
            {
                if (string.IsNullOrWhiteSpace(roleSeed.Name))
                {
                    continue;
                }

                repository.AddRole(new Role
                {
                    Name = roleSeed.Name.Trim(),
                    Description = roleSeed.Description ?? string.Empty
                });
                changed = true;
            }
        }

        if (!repository.HasAnyUsers())
        {
            foreach (var userSeed in seed.Users ?? [])
            {
                if (string.IsNullOrWhiteSpace(userSeed.LoginName))
                {
                    continue;
                }

                var loginName = userSeed.LoginName.Trim();
                var passwordHash = ResolvePasswordHash(userSeed);

                repository.AddUser(new User
                {
                    LoginName = loginName,
                    RealName = string.IsNullOrWhiteSpace(userSeed.RealName) ? loginName : userSeed.RealName,
                    Department = userSeed.Department ?? string.Empty,
                    Role = userSeed.Role ?? string.Empty,
                    Password = passwordHash ?? PasswordHashingSupport.Hash("123456"),
                    CreatedDate = userSeed.CreatedDate ?? DateTime.Now,
#if DEBUG
                    MustChangePassword = false,
#else
                    MustChangePassword = true,
#endif
                    FailedLoginCount = 0
                });
                changed = true;
            }
        }

        if (changed)
        {
            repository.SaveChanges();
            Debug.WriteLine("[SystemSeed] 空库已从外部文件写入部门/角色/用户。");
        }
    }

    /// <summary>
    /// 开发期全量同步：补缺失项并更新描述等字段，含项目信息。Release 构建不执行。
    /// </summary>
    public static void SeedFromExternalFile(IDevSystemSettingsSeedRepository repository, string filePath)
    {
#if DEBUG
        ArgumentNullException.ThrowIfNull(repository);

        if (!TryLoadSeedFile(filePath, out var seed))
        {
            return;
        }

        try
        {
            var changed = false;

            // 1) 部门
            foreach (var departmentSeed in seed.Departments ?? [])
            {
                if (string.IsNullOrWhiteSpace(departmentSeed.Name))
                {
                    continue;
                }

                var name = departmentSeed.Name.Trim();
                var description = departmentSeed.Description ?? string.Empty;
                var existingDepartment = repository.GetDepartmentByName(name);

                if (existingDepartment == null)
                {
                    repository.AddDepartment(new Department
                    {
                        Name = name,
                        Description = description
                    });
                    changed = true;
                    continue;
                }

                if ((existingDepartment.Description ?? string.Empty) != description)
                {
                    existingDepartment.Description = description;
                    changed = true;
                }
            }

            // 2) 角色
            foreach (var roleSeed in seed.Roles ?? [])
            {
                if (string.IsNullOrWhiteSpace(roleSeed.Name))
                {
                    continue;
                }

                var name = roleSeed.Name.Trim();
                var description = roleSeed.Description ?? string.Empty;
                var existingRole = repository.GetRoleByName(name);

                if (existingRole == null)
                {
                    repository.AddRole(new Role
                    {
                        Name = name,
                        Description = description
                    });
                    changed = true;
                    continue;
                }

                if ((existingRole.Description ?? string.Empty) != description)
                {
                    existingRole.Description = description;
                    changed = true;
                }
            }

            // 3) 用户
            foreach (var userSeed in seed.Users ?? [])
            {
                if (string.IsNullOrWhiteSpace(userSeed.LoginName))
                {
                    continue;
                }

                var loginName = userSeed.LoginName.Trim();
                var existingUser = repository.GetUserByLoginName(loginName);
                var passwordHash = ResolvePasswordHash(userSeed);

                if (existingUser == null)
                {
                    repository.AddUser(new User
                    {
                        LoginName = loginName,
                        RealName = string.IsNullOrWhiteSpace(userSeed.RealName) ? loginName : userSeed.RealName,
                        Department = userSeed.Department ?? string.Empty,
                        Role = userSeed.Role ?? string.Empty,
                        Password = passwordHash ?? PasswordHashingSupport.Hash("123456"),
                        CreatedDate = userSeed.CreatedDate ?? DateTime.Now,
                        // 开发期全量同步仅 DEBUG 执行，不强制首次改密
                        MustChangePassword = false,
                        FailedLoginCount = 0
                    });
                    changed = true;
                    continue;
                }

                var newRealName = string.IsNullOrWhiteSpace(userSeed.RealName) ? existingUser.RealName : userSeed.RealName;
                var newDepartment = userSeed.Department ?? existingUser.Department;
                var newRole = userSeed.Role ?? existingUser.Role;

                if (existingUser.RealName != newRealName)
                {
                    existingUser.RealName = newRealName;
                    changed = true;
                }

                if (existingUser.Department != newDepartment)
                {
                    existingUser.Department = newDepartment;
                    changed = true;
                }

                if (existingUser.Role != newRole)
                {
                    existingUser.Role = newRole;
                    changed = true;
                }

                if (userSeed.CreatedDate.HasValue && existingUser.CreatedDate != userSeed.CreatedDate.Value)
                {
                    existingUser.CreatedDate = userSeed.CreatedDate.Value;
                    changed = true;
                }
            }

            // 4) 项目：按业务键匹配，禁止写入显式 IDENTITY Id（兼容 SQL Server）。
            foreach (var projectSeed in seed.ProjectInfos ?? [])
            {
                var existingProject = ResolveExistingProject(repository, projectSeed);

                if (existingProject == null)
                {
                    repository.AddProjectInfo(new ProjectInfo
                    {
                        ProjectName = projectSeed.ProjectName ?? string.Empty,
                        ProjectCode = projectSeed.ProjectCode ?? string.Empty,
                        ImplementYear = projectSeed.ImplementYear ?? string.Empty,
                        CapitalMgrDept = projectSeed.CapitalMgrDept ?? string.Empty,
                        Remark = projectSeed.Remark ?? string.Empty
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
                Debug.WriteLine("[DevSeed] 部门/角色/用户/项目已从外部文件同步到数据库。");
            }
            else
            {
                Debug.WriteLine("[DevSeed] 无需更新。");
            }
        }
        catch (Exception ex)
        {
            // 必须清空 ChangeTracker：否则 ProjectInfos 显式 Id 写入失败后，
            // 同作用域后续档口规格/字段字典 SaveChanges 会连带失败。
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
#endif
    }

    private static ProjectInfo? ResolveExistingProject(
        IDevSystemSettingsSeedRepository repository,
        ProjectInfoSeed projectSeed)
    {
        ProjectInfo? existing = null;
        if (!string.IsNullOrWhiteSpace(projectSeed.ProjectCode))
        {
            existing = repository.GetProjectInfoByCode(projectSeed.ProjectCode);
        }

        if (existing == null
            && !string.IsNullOrWhiteSpace(projectSeed.ProjectName))
        {
            existing = repository.GetProjectInfoByNameAndYear(
                projectSeed.ProjectName,
                projectSeed.ImplementYear ?? string.Empty);
        }

        // 兼容旧种子仍带 Id：仅作回退匹配，插入时不再写显式 Id。
        if (existing == null && projectSeed.Id > 0)
        {
            existing = repository.GetProjectInfoById(projectSeed.Id);
        }

        return existing;
    }

    private static bool TryLoadSeedFile(string filePath, out SystemSettingsSeedFile seed)
    {
        seed = null!;

        try
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"[SystemSeed] 未找到配置文件: {filePath}");
                return false;
            }

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<SystemSettingsSeedFile>(json, options);
            if (parsed == null || !parsed.Enabled)
            {
                Debug.WriteLine("[SystemSeed] 配置为空或未启用。");
                return false;
            }

            seed = parsed;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[SystemSeed] 读取配置失败: " + ex.Message);
            return false;
        }
    }

    private static string? ResolvePasswordHash(UserSeed userSeed)
    {
        if (!string.IsNullOrWhiteSpace(userSeed.PasswordHash))
        {
            return userSeed.PasswordHash;
        }

        return string.IsNullOrWhiteSpace(userSeed.Password)
            ? null
            : PasswordHashingSupport.Hash(userSeed.Password);
    }

    private sealed class SystemSettingsSeedFile
    {
        public bool Enabled { get; set; } = true;

        public List<DepartmentSeed>? Departments { get; set; }

        public List<RoleSeed>? Roles { get; set; }

        public List<UserSeed>? Users { get; set; }

        public List<ProjectInfoSeed>? ProjectInfos { get; set; }
    }

    private sealed class DepartmentSeed
    {
        public string? Name { get; set; }

        public string? Description { get; set; }
    }

    private sealed class RoleSeed
    {
        public string? Name { get; set; }

        public string? Description { get; set; }
    }

    private sealed class UserSeed
    {
        public string? LoginName { get; set; }

        public string? RealName { get; set; }

        public string? Department { get; set; }

        public string? Role { get; set; }

        // 二选一：PasswordHash 优先，其次 Password（明文仅用于首次建账号，不会覆盖已有用户口令）
        public string? PasswordHash { get; set; }

        public string? Password { get; set; }

        public DateTime? CreatedDate { get; set; }
    }

    private sealed class ProjectInfoSeed
    {
        public int Id { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public string ProjectCode { get; set; } = string.Empty;

        public string ImplementYear { get; set; } = string.Empty;

        public string CapitalMgrDept { get; set; } = string.Empty; // 厅资金管理部门

        public string Remark { get; set; } = string.Empty;
    }
}