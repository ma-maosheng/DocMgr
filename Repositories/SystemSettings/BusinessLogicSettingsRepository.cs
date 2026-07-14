using DocMgr.Data;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.SystemSettings
{
    public class BusinessLogicSettingsRepository : IBusinessLogicSettingsRepository
    {
        private readonly AppDbContext _dbContext;

        public BusinessLogicSettingsRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<BusinessLogicSettings> GetOrCreateAsync(Func<BusinessLogicSettings> defaultFactory)
        {
            var settings = await _dbContext.BusinessLogicSettings
                .FirstOrDefaultAsync(item => item.Id == BusinessLogicSettings.SingletonId);
            if (settings != null)
            {
                return settings;
            }

            settings = defaultFactory();
            settings.Id = BusinessLogicSettings.SingletonId;
            settings.UpdatedAt = DateTime.Now;
            _dbContext.BusinessLogicSettings.Add(settings);

            try
            {
                await _dbContext.SaveChangesAsync();
                return settings;
            }
            catch (DbUpdateException)
            {
                var existing = await _dbContext.BusinessLogicSettings
                    .FirstOrDefaultAsync(item => item.Id == BusinessLogicSettings.SingletonId);
                if (existing != null)
                {
                    return existing;
                }

                throw;
            }
        }

        public async Task SaveAsync(BusinessLogicSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            settings.Id = BusinessLogicSettings.SingletonId;
            settings.UpdatedAt = DateTime.Now;
            _dbContext.BusinessLogicSettings.Update(settings);
            await _dbContext.SaveChangesAsync();
        }
    }
}
