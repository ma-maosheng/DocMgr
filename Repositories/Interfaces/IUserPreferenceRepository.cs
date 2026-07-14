using DocMgr.Models.Shared;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 用户偏好数据访问契约：用户偏好设置数据读写。
/// </summary>
public interface IUserPreferenceRepository
{
    Task<UserPreference> GetOrCreateAsync(int userId, Func<UserPreference> defaultFactory);

    Task SaveAsync(UserPreference preference);
}
