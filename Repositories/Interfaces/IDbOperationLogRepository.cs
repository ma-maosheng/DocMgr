using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces
{
    public sealed class DbOperationLogQuery
    {
        public DateTime? StartTime { get; init; }

        public DateTime? EndTime { get; init; }

        public string? TableName { get; init; }

        public string? Operation { get; init; }

        public string? Keyword { get; init; }
    }

    /// <summary>
    /// 数据库操作日志数据访问契约：审计日志的写入与查询。
    /// </summary>
    public interface IDbOperationLogRepository
    {
        Task<IReadOnlyList<DbOperationLog>> SearchAsync(DbOperationLogQuery query, int limit = 500);

        Task<DbOperationLog?> GetByIdAsync(long id);

        Task<IReadOnlyList<string>> GetDistinctTableNamesAsync(int take = 200);

        Task<int> ClearAllAsync();
    }
}
