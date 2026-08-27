using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HistoryArchive;

public class TopoMapRepository : ITopoMapRepository
{
    private readonly AppDbContext _dbContext;

    public TopoMapRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool ExistsByCategory(string categoryName)
    {
        return _dbContext.TopoMaps.Any(item => item.Category == categoryName);
    }

    public List<string> GetDistinctCategories()
    {
        return _dbContext.TopoMaps
            .Select(item => item.Category)
            .Distinct()
            .ToList();
    }

    public List<TopoMap> GetByCategory(string categoryName)
    {
        return _dbContext.TopoMaps
            .AsNoTracking()
            .Where(item => item.Category == categoryName)
            .OrderBy(item => item.Id)
            .ToList();
    }

    public List<TopoMap> GetAll()
    {
        return _dbContext.TopoMaps
            .AsNoTracking()
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Scale)
            .ThenBy(item => item.BoxNumber)
            .ThenBy(item => item.MapNumber)
            .ThenBy(item => item.Id)
            .ToList();
    }

    public void Import(string categoryName, List<TopoMap> maps, bool isRecreate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        ArgumentNullException.ThrowIfNull(maps);

        using var transaction = _dbContext.Database.BeginTransaction();
        try
        {
            if (isRecreate)
            {
                _dbContext.TopoMaps
                    .Where(item => item.Category == categoryName)
                    .ExecuteDelete();
            }

            foreach (var item in maps)
            {
                item.Category = categoryName;
            }

            _dbContext.TopoMaps.AddRange(maps);
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
        _dbContext.TopoMaps.Where(item => item.Category == categoryName).ExecuteDelete();
    }

    public void DeleteById(int id)
    {
        _dbContext.TopoMaps.Where(item => item.Id == id).ExecuteDelete();
    }

    public void Update(TopoMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _dbContext.TopoMaps.Update(map);
        _dbContext.SaveChanges();
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }
}
