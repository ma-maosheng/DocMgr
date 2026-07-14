using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HistoryArchive;

public class AerialPhotoRepository : IAerialPhotoRepository
{
    private readonly AppDbContext _dbContext;

    public AerialPhotoRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool ExistsByCategory(string categoryName)
    {
        return _dbContext.AerialPhotos.Any(item => item.Category == categoryName);
    }

    public List<string> GetDistinctCategories()
    {
        return _dbContext.AerialPhotos
            .Select(item => item.Category)
            .Distinct()
            .ToList();
    }

    public List<AerialPhoto> GetByCategory(string categoryName)
    {
        return _dbContext.AerialPhotos
            .Where(item => item.Category == categoryName)
            .OrderBy(item => item.Id)
            .ToList();
    }

    public void Import(string categoryName, List<AerialPhoto> items, bool isRecreate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        ArgumentNullException.ThrowIfNull(items);

        using var transaction = _dbContext.Database.BeginTransaction();
        try
        {
            if (isRecreate)
            {
                _dbContext.AerialPhotos
                    .Where(item => item.Category == categoryName)
                    .ExecuteDelete();
            }

            foreach (var item in items)
            {
                item.Category = categoryName;
            }

            _dbContext.AerialPhotos.AddRange(items);
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
        _dbContext.AerialPhotos
            .Where(item => item.Category == categoryName)
            .ExecuteDelete();
    }

    public void Update(AerialPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        _dbContext.AerialPhotos.Update(photo);
        _dbContext.SaveChanges();
    }
}
