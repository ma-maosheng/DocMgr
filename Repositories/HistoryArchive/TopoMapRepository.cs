using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.HistoryArchive;
using DocMgr.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HistoryArchive;

public class TopoMapRepository : ITopoMapRepository
{
    private const string MaterialKind = HistoryArchiveDisposalDomainValues.MaterialKindTopoMap;
    private readonly AppDbContext _dbContext;
    private readonly IUserContextService? _userContextService;

    public TopoMapRepository(AppDbContext dbContext, IUserContextService? userContextService = null)
    {
        _dbContext = dbContext;
        _userContextService = userContextService;
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
                var oldRecordIds = _dbContext.TopoMaps
                    .Where(item => item.Category == categoryName)
                    .Select(item => item.Id)
                    .ToList();
                HistoryArchiveBoxLedgerMaintenanceSupport.RemoveLinksForRecords(
                    _dbContext, MaterialKind, oldRecordIds);
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

            HistoryArchiveBoxLedgerMaintenanceSupport.SyncBoxesAndLinksForRows(
                _dbContext,
                MaterialKind,
                maps.Select(item => new HistoryArchiveBoxLedgerMaintenanceSupport.LedgerRowSnapshot(
                    item.Id, item.BoxNumber, item.BoxSpecification)).ToList(),
                _userContextService?.CurrentUser?.RealName);
            HistoryArchiveBoxLedgerMaintenanceSupport.CleanupOrphanBoxes(_dbContext);
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
        using var transaction = _dbContext.Database.BeginTransaction();
        try
        {
            var recordIds = _dbContext.TopoMaps
                .Where(item => item.Category == categoryName)
                .Select(item => item.Id)
                .ToList();
            HistoryArchiveBoxLedgerMaintenanceSupport.RemoveLinksForRecords(
                _dbContext, MaterialKind, recordIds);
            _dbContext.TopoMaps
                .Where(item => item.Category == categoryName)
                .ExecuteDelete();
            HistoryArchiveBoxLedgerMaintenanceSupport.CleanupOrphanBoxes(_dbContext);
            _dbContext.SaveChanges();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void DeleteById(int id)
    {
        using var transaction = _dbContext.Database.BeginTransaction();
        try
        {
            HistoryArchiveBoxLedgerMaintenanceSupport.RemoveLinksForRecords(
                _dbContext, MaterialKind, [id]);
            _dbContext.TopoMaps.Where(item => item.Id == id).ExecuteDelete();
            HistoryArchiveBoxLedgerMaintenanceSupport.CleanupOrphanBoxes(_dbContext);
            _dbContext.SaveChanges();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void Update(TopoMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        using var transaction = _dbContext.Database.BeginTransaction();
        try
        {
            _dbContext.TopoMaps.Update(map);
            _dbContext.SaveChanges();
            HistoryArchiveBoxLedgerMaintenanceSupport.SyncBoxesAndLinksForRows(
                _dbContext,
                MaterialKind,
                [new HistoryArchiveBoxLedgerMaintenanceSupport.LedgerRowSnapshot(
                    map.Id, map.BoxNumber, map.BoxSpecification)],
                _userContextService?.CurrentUser?.RealName);
            HistoryArchiveBoxLedgerMaintenanceSupport.CleanupOrphanBoxes(_dbContext);
            _dbContext.SaveChanges();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }
}
