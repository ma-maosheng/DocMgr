using System;
using System.Threading.Tasks;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.SystemSettings
{
    public class UserPreferenceService : IUserPreferenceService
    {
        private readonly IUserPreferenceRepository _userPreferenceRepository;

        private const int MinRefreshSeconds = 5;
        private const int MaxRefreshSeconds = 3600;
        private const int MinTopN = 1;
        private const int MaxTopN = 200;

        public UserPreferenceService(IUserPreferenceRepository userPreferenceRepository)
        {
            _userPreferenceRepository = userPreferenceRepository;
        }

        public async Task<UserPreference> GetOrCreateAsync(int userId)
        {
            return await _userPreferenceRepository.GetOrCreateAsync(userId, CreateDefaultTemplate);
        }

        public async Task SaveAsync(UserPreference preference)
        {
            await _userPreferenceRepository.SaveAsync(preference);
        }

        public UserPreference CreateDefaultTemplate()
        {
            return new UserPreference
            {
                EnableToDoPopup = true,
                EnableToDoBadge = true,
                ToDoRefreshSeconds = 15,
                ToDoTopN = 20,
                MarkAllAsReadOnAcknowledge = true
            };
        }

        public bool TryValidate(UserPreference preference, out string errorMessage)
        {
            if (preference.ToDoRefreshSeconds < MinRefreshSeconds || preference.ToDoRefreshSeconds > MaxRefreshSeconds)
            {
                errorMessage = $"刷新频率请设置在 {MinRefreshSeconds}~{MaxRefreshSeconds} 秒之间。";
                return false;
            }

            if (preference.ToDoTopN < MinTopN || preference.ToDoTopN > MaxTopN)
            {
                errorMessage = $"待办条数请设置在 {MinTopN}~{MaxTopN} 之间。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}