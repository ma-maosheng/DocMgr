using DocMgr.Data;
using DocMgr.Models.Shared;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.SystemSettings;

public class UserPreferenceRepository : IUserPreferenceRepository
{
    private readonly AppDbContext _dbContext;

    public UserPreferenceRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserPreference> GetOrCreateAsync(int userId, Func<UserPreference> defaultFactory)
    {
        var preference = await _dbContext.Set<UserPreference>()
            .FirstOrDefaultAsync(item => item.UserId == userId);
        if (preference != null)
        {
            return preference;
        }

        preference = defaultFactory();
        preference.UserId = userId;
        preference.UpdatedAt = DateTime.Now;
        _dbContext.Set<UserPreference>().Add(preference);

        try
        {
            await _dbContext.SaveChangesAsync();
            return preference;
        }
        catch (DbUpdateException)
        {
            var existing = await _dbContext.Set<UserPreference>()
                .FirstOrDefaultAsync(item => item.UserId == userId);
            if (existing != null)
            {
                return existing;
            }

            throw;
        }
    }

    public async Task SaveAsync(UserPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        preference.UpdatedAt = DateTime.Now;
        _dbContext.Set<UserPreference>().Update(preference);
        await _dbContext.SaveChangesAsync();
    }
}
