using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 系统业务逻辑设置服务契约。
    /// </summary>
    public interface IBusinessLogicSettingsService
    {
        Task<string> GetApplicationOverdueSettingCodeAsync();

        Task SaveApplicationOverdueSettingCodeAsync(string settingCode, User operatorUser);

        bool IsEligibleForAdminForceVoid(DateTime applyDate, string? settingCode, DateTime? asOf = null);

        string BuildNotEligibleMessage(string? settingCode);

        IReadOnlyList<ApplicationOverdueOption> GetApplicationOverdueOptions();

        BusinessLogicSettings CreateDefaultTemplate();

        bool TryValidateApplicationOverdueSetting(string? settingCode, out string errorMessage);
    }
}
