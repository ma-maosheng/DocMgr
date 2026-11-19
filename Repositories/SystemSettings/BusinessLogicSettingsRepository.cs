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
                await UpgradeLegacySameDayDefaultAsync(settings);
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

        /// <summary>
        /// 产品默认由「当天」改为「7天」。2026-11-20 之前写入的「当天」视为旧默认，读到时升为 7 天。
        /// 该时间之后管理员再保存的「当天」保持不变。
        /// </summary>
        private async Task UpgradeLegacySameDayDefaultAsync(BusinessLogicSettings settings)
        {
            if (!string.Equals(
                    settings.ApplicationOverdueSetting,
                    ApplicationOverdueDomainValues.SameDay,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (settings.UpdatedAt >= new DateTime(2026, 11, 20))
            {
                return;
            }

            settings.ApplicationOverdueSetting = ApplicationOverdueDomainValues.Days7;
            settings.UpdatedAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();
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
