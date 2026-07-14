namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 立档仓储事务工作单元契约：在单个事务内完成立档相关数据写入。
/// </summary>
public interface IArchiveFilingRepositoryTransaction : IAsyncDisposable
{
    Task CommitAsync();

    Task RollbackAsync();
}
