using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 数据库操作日志服务契约：记录与查询关键数据变更的审计日志。
    /// </summary>
    public interface IDbOperationLogService
    {
        Task<IReadOnlyList<DbOperationLog>> SearchAsync(DbOperationLogQuery query, int limit = 500);

        Task<DbOperationLog?> GetByIdAsync(long id);

        Task<IReadOnlyList<string>> GetDistinctTableNamesAsync();

        Task<int> ClearAllAsync();
    }
}
