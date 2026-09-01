using DocMgr.Repositories.Interfaces;
using DocMgr.Services.SystemSettings;

namespace DocMgr.Infrastructure.Seeding;

/// <summary>
/// Release/空库启动时补建默认系统管理员。仅在用户表为空时写入，不覆盖已有账号。
/// </summary>
public static class DefaultAdministratorBootstrap
{
    /// <summary>默认管理员登录名。</summary>
    public const string LoginName = "admin";

    /// <summary>空库初始口令；首次登录须修改。</summary>
    public const string InitialPassword = "123456";

    /// <summary>
    /// 用户表为空时创建 admin。已有任意用户则跳过。
    /// </summary>
    /// <returns>本次是否新建了管理员。</returns>
    public static bool EnsureIfEmpty(IUserRepository userRepository)
    {
        ArgumentNullException.ThrowIfNull(userRepository);

        if (userRepository.HasAnyUsers())
        {
            return false;
        }

        userRepository.AddUser(new User
        {
            LoginName = LoginName,
            RealName = "系统管理员",
            Department = string.Empty,
            Role = "Administrator",
            Password = PasswordHashingSupport.Hash(InitialPassword),
            CreatedDate = DateTime.Now,
            MustChangePassword = true,
            FailedLoginCount = 0
        });
        userRepository.SaveChanges();
        return true;
    }
}
