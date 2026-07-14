using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.SystemSettings
{
    public class DbOperationLogService : IDbOperationLogService
    {
        private readonly IDbOperationLogRepository _repository;

        public DbOperationLogService(IDbOperationLogRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<DbOperationLog>> SearchAsync(DbOperationLogQuery query, int limit = 500)
        {
            return _repository.SearchAsync(query, limit);
        }

        public Task<DbOperationLog?> GetByIdAsync(long id)
        {
            return _repository.GetByIdAsync(id);
        }

        public Task<IReadOnlyList<string>> GetDistinctTableNamesAsync()
        {
            return _repository.GetDistinctTableNamesAsync();
        }

        public Task<int> ClearAllAsync()
        {
            return _repository.ClearAllAsync();
        }
    }
}
