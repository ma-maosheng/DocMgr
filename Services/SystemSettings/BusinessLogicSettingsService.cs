using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.SystemSettings
{
    public class BusinessLogicSettingsService : IBusinessLogicSettingsService
    {
        private readonly IBusinessLogicSettingsRepository _repository;

        public BusinessLogicSettingsService(IBusinessLogicSettingsRepository repository)
        {
            _repository = repository;
        }

        public async Task<string> GetApplicationOverdueSettingCodeAsync()
        {
            var settings = await _repository.GetOrCreateAsync(CreateDefaultTemplate);
            return ApplicationOverdueSettingSupport.Normalize(settings.ApplicationOverdueSetting);
        }

        public async Task SaveApplicationOverdueSettingCodeAsync(string settingCode, User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);

            if (!IsSystemAdministrator(operatorUser))
            {
                throw new InvalidOperationException("仅系统管理员可修改业务逻辑设置。");
            }

            if (!TryValidateApplicationOverdueSetting(settingCode, out string errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(settingCode));
            }

            var settings = await _repository.GetOrCreateAsync(CreateDefaultTemplate);
            settings.ApplicationOverdueSetting = ApplicationOverdueSettingSupport.Normalize(settingCode);
            await _repository.SaveAsync(settings);
        }

        public bool IsEligibleForAdminForceVoid(DateTime applyDate, string? settingCode, DateTime? asOf = null)
        {
            return ApplicationOverdueSettingSupport.IsEligibleForAdminForceVoid(applyDate, settingCode, asOf);
        }

        public string BuildNotEligibleMessage(string? settingCode)
        {
            return ApplicationOverdueSettingSupport.BuildNotEligibleMessage(settingCode);
        }

        public IReadOnlyList<ApplicationOverdueOption> GetApplicationOverdueOptions()
        {
            return ApplicationOverdueDomainValues.AllOptions;
        }

        public BusinessLogicSettings CreateDefaultTemplate()
        {
            return new BusinessLogicSettings
            {
                ApplicationOverdueSetting = ApplicationOverdueDomainValues.Default
            };
        }

        public bool TryValidateApplicationOverdueSetting(string? settingCode, out string errorMessage)
        {
            string normalized = ApplicationOverdueSettingSupport.Normalize(settingCode);
            bool isValid = ApplicationOverdueDomainValues.AllOptions
                .Any(option => string.Equals(option.Code, normalized, StringComparison.Ordinal));

            if (!isValid)
            {
                errorMessage = "申请单逾期设置无效，请选择当天、7天或30天。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool IsSystemAdministrator(User user)
        {
            string role = user.Role?.Trim() ?? string.Empty;
            return string.Equals(role, "Administrator", StringComparison.Ordinal)
                   || string.Equals(role, "管理员", StringComparison.Ordinal);
        }
    }
}
