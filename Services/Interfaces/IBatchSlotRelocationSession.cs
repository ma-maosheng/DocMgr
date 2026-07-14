using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 档口批量移库会话契约：维护一次批量换位/迁移操作的中间状态。
    /// </summary>
    public interface IBatchSlotRelocationSession
    {
        BatchSlotRelocationEndpoint? Source { get; }

        event Action? SourceChanged;

        void SetSource(BatchSlotRelocationEndpoint source);

        void ClearSource();
    }

}
