using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces
{
    /// <summary>
    /// 系统业务逻辑设置仓储契约。
    /// </summary>
    public interface IBusinessLogicSettingsRepository
    {
        Task<BusinessLogicSettings> GetOrCreateAsync(Func<BusinessLogicSettings> defaultFactory);

        Task SaveAsync(BusinessLogicSettings settings);
    }
}
