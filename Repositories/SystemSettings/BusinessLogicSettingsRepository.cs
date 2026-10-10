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
            // 单例表：取现存第一行（约定为首次插入由 IDENTITY 得到 Id=1）。
            var settings = await _dbContext.BusinessLogicSettings
                .OrderBy(item => item.Id)
                .FirstOrDefaultAsync();
            if (settings != null)
            {
                return settings;
            }

            settings = defaultFactory();
            // Id 保持 0，由数据库 IDENTITY 生成，避免 SQL Server「IDENTITY_INSERT=OFF」错误。
            settings.Id = 0;
            settings.UpdatedAt = DateTime.Now;
            _dbContext.BusinessLogicSettings.Add(settings);

            try
            {
                await _dbContext.SaveChangesAsync();
                return settings;
            }
            catch (DbUpdateException)
            {
                // 并发首插：清空跟踪后读已有行。
                _dbContext.ChangeTracker.Clear();
                var existing = await _dbContext.BusinessLogicSettings
                    .OrderBy(item => item.Id)
                    .FirstOrDefaultAsync();
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

            if (settings.Id <= 0)
            {
                throw new InvalidOperationException("业务逻辑设置尚未初始化，无法保存。");
            }

            settings.UpdatedAt = DateTime.Now;
            _dbContext.BusinessLogicSettings.Update(settings);
            await _dbContext.SaveChangesAsync();
        }
    }
}
