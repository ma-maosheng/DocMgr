namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 用户仓储事务工作单元契约：在单个事务内完成用户相关数据写入。
/// </summary>
public interface IUserRepositoryTransaction : IDisposable
{
    void Commit();

    void Rollback();
}
