using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 待办事项业务服务契约：待办项的查询、处理与状态更新。
    /// </summary>
    public interface IToDoService
    {
        Task<List<ToDoItem>> GetMyToDosAsync(User currentUser, int topN = 20);

        Task MarkAsReadAsync(User currentUser, string toDoId);

        Task MarkAsReadBatchAsync(User currentUser, IEnumerable<string> toDoIds);
    }
}