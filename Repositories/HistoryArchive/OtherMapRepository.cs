using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.HistoryArchive;
using DocMgr.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HistoryArchive;

public class OtherMapRepository : IOtherMapRepository
{
    private const string MaterialKind = HistoryArchiveDisposalDomainValues.MaterialKindOtherMap;
    private readonly AppDbContext _dbContext;
    private readonly IUserContextService? _userContextService;

    public OtherMapRepository(AppDbContext dbContext, IUserContextService? userContextService = null)
    {
        _dbContext = dbContext;
        _userContextService = userContextService;
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
        var list = _dbContext.OtherMaps
            .Where(item => item.Category == categoryName)
            .OrderBy(item => item.Id)
            .ToList();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, MaterialKind, list);
        return list;
    }

    public List<OtherMap> GetAll()
    {
        return _dbContext.OtherMaps
            .OrderBy(item => item.Category)
            .ThenBy(item => item.SequenceNumber)
            .ThenBy(item => item.Id)
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
                var oldRecordIds = _dbContext.OtherMaps
                    .Where(item => item.Category == categoryName)
                    .Select(item => item.Id)
                    .ToList();
                HistoryArchiveBoxLedgerMaintenanceSupport.RemoveLinksForRecords(
                    _dbContext, MaterialKind, oldRecordIds);
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

            HistoryArchiveBoxLedgerMaintenanceSupport.SyncBoxesAndLinksForRows(
                _dbContext,
                MaterialKind,
                items.Select(item => new HistoryArchiveBoxLedgerMaintenanceSupport.LedgerRowSnapshot(
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
            var recordIds = _dbContext.OtherMaps
                .Where(item => item.Category == categoryName)
                .Select(item => item.Id)
                .ToList();
            HistoryArchiveBoxLedgerMaintenanceSupport.RemoveLinksForRecords(
                _dbContext, MaterialKind, recordIds);
            _dbContext.OtherMaps
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
            _dbContext.OtherMaps.Where(item => item.Id == id).ExecuteDelete();
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

    public void Update(OtherMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        // 盒号/规格为投影属性：编辑路径不再同步盒与链接（权威源不随台账字段变化）
        _dbContext.OtherMaps.Update(map);
        _dbContext.SaveChanges();
    }
}
