using System.Threading.Tasks;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 用户偏好设置服务契约：读取与保存用户个性化配置。
    /// </summary>
    /// <summary>
    /// 用户个性化偏好服务契约：读取与保存用户界面/操作偏好设置。
    /// </summary>
    public interface IUserPreferenceService
    {
        Task<UserPreference> GetOrCreateAsync(int userId);
        Task SaveAsync(UserPreference preference);

        UserPreference CreateDefaultTemplate();
        bool TryValidate(UserPreference preference, out string errorMessage);
    }
}