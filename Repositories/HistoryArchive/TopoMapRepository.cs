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

    public bool ExistsByScale(string scale)
    {
        return _dbContext.TopoMaps.Any(item => item.Scale == scale);
    }

    public List<string> GetDistinctScales()
    {
        return _dbContext.TopoMaps
            .Select(item => item.Scale)
            .Distinct()
            .ToList();
    }

    public List<TopoMap> GetByScale(string scale)
    {
        return _dbContext.TopoMaps
            .Where(item => item.Scale == scale)
            .OrderBy(item => item.Id)
            .ToList();
    }

    public void Import(List<TopoMap> maps, bool isRecreate)
    {
        ArgumentNullException.ThrowIfNull(maps);

        using var transaction = _dbContext.Database.BeginTransaction();
        try
        {
            var groupedMaps = maps.GroupBy(item => item.Scale);
            foreach (var group in groupedMaps)
            {
                string scale = group.Key;
                if (string.IsNullOrWhiteSpace(scale))
                {
                    continue;
                }

                if (isRecreate)
                {
                    _dbContext.TopoMaps.Where(item => item.Scale == scale).ExecuteDelete();
                }

                _dbContext.TopoMaps.AddRange(group);
            }

            _dbContext.SaveChanges();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void DeleteByScale(string scale)
    {
        _dbContext.TopoMaps.Where(item => item.Scale == scale).ExecuteDelete();
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
}
