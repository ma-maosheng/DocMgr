using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HistoryArchive;

public class OtherMapRepository : IOtherMapRepository
{
    private readonly AppDbContext _dbContext;

    public OtherMapRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool ExistsByCategory(string categoryName)
    {
        return _dbContext.OtherMaps.Any(item => item.Category == categoryName);
    }

    public List<string> GetDistinctCategories()
    {
        return _dbContext.OtherMaps
            .Select(item => item.Category)
            .Distinct()
            .ToList();
    }

    public List<OtherMap> GetByCategory(string categoryName)
    {
        return _dbContext.OtherMaps
            .Where(item => item.Category == categoryName)
            .OrderBy(item => item.Id)
            .ToList();
    }

    public void Import(string categoryName, List<OtherMap> items, bool isRecreate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        ArgumentNullException.ThrowIfNull(items);

        using var transaction = _dbContext.Database.BeginTransaction();
        try
        {
            if (isRecreate)
            {
                _dbContext.OtherMaps
                    .Where(item => item.Category == categoryName)
                    .ExecuteDelete();
            }

            foreach (var item in items)
            {
                item.Category = categoryName;
            }

            _dbContext.OtherMaps.AddRange(items);
            _dbContext.SaveChanges();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void DeleteByCategory(string categoryName)
    {
        _dbContext.OtherMaps
            .Where(item => item.Category == categoryName)
            .ExecuteDelete();
    }

    public void Update(OtherMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _dbContext.OtherMaps.Update(map);
        _dbContext.SaveChanges();
    }
}
