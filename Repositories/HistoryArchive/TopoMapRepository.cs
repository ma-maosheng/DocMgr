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
        var list = _dbContext.TopoMaps
            .AsNoTracking()
            .Where(item => item.Category == categoryName)
            .OrderBy(item => item.Id)
            .ToList();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, MaterialKind, list);
        return list;
    }

    public List<TopoMap> GetAll()
    {
        var list = _dbContext.TopoMaps
            .AsNoTracking()
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Scale)
            .ThenBy(item => item.Id)
            .ToList();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, MaterialKind, list);
        // 盒号投影非映射列，SQL 侧排序后须在内存按投影重排
        return list
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.BoxNumber, StringComparer.Ordinal)
            .ThenBy(item => item.MapNumber, StringComparer.Ordinal)
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
        // 盒号/规格为投影属性：编辑路径不再同步盒与链接（权威源不随台账字段变化）
        _dbContext.TopoMaps.Update(map);
        _dbContext.SaveChanges();
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }
}
