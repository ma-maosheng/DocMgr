using DocMgr.Data;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.SystemSettings
{
    public class DbOperationLogRepository : IDbOperationLogRepository
    {
        private readonly AppDbContext _dbContext;

        public DbOperationLogRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<DbOperationLog>> SearchAsync(DbOperationLogQuery query, int limit = 500)
        {
            ArgumentNullException.ThrowIfNull(query);

            int safeLimit = Math.Clamp(limit, 1, 5000);
            IQueryable<DbOperationLog> dbQuery = _dbContext.DbOperationLogs.AsNoTracking();

            if (query.StartTime.HasValue)
            {
                dbQuery = dbQuery.Where(item => item.OperationTime >= query.StartTime.Value);
            }

            if (query.EndTime.HasValue)
            {
                dbQuery = dbQuery.Where(item => item.OperationTime <= query.EndTime.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.TableName))
            {
                string tableName = query.TableName.Trim();
                dbQuery = dbQuery.Where(item => item.TableName == tableName);
            }

            if (!string.IsNullOrWhiteSpace(query.Operation))
            {
                string operation = query.Operation.Trim();
                dbQuery = dbQuery.Where(item => item.Operation == operation);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                string keyword = query.Keyword.Trim();
                dbQuery = dbQuery.Where(item =>
                    item.Summary.Contains(keyword)
                    || item.EntityKey.Contains(keyword)
                    || item.UserName.Contains(keyword)
                    || item.SourcePage.Contains(keyword)
                    || item.SourceButton.Contains(keyword)
                    || item.ChangedColumns.Contains(keyword));
            }

            return await dbQuery
                .OrderByDescending(item => item.OperationTime)
                .ThenByDescending(item => item.Id)
                .Take(safeLimit)
                .ToListAsync();
        }

        public Task<DbOperationLog?> GetByIdAsync(long id)
        {
            return _dbContext.DbOperationLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<IReadOnlyList<string>> GetDistinctTableNamesAsync(int take = 200)
        {
            int safeTake = Math.Clamp(take, 1, 1000);

            return await _dbContext.DbOperationLogs
                .AsNoTracking()
                .Select(item => item.TableName)
                .Distinct()
                .OrderBy(item => item)
                .Take(safeTake)
                .ToListAsync();
        }

        public Task<int> ClearAllAsync()
        {
            return _dbContext.DbOperationLogs.ExecuteDeleteAsync();
        }
    }
}
