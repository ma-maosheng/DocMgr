namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 硬盘介质仓储事务工作单元契约：在单个事务内完成硬盘相关数据写入。
/// </summary>
public interface IHardDiskMediaRepositoryTransaction : IAsyncDisposable
{
    Task CommitAsync();

    Task RollbackAsync();
}
