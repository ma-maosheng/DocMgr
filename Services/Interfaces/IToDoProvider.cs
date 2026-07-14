using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 待办事项数据来源契约：由各业务模块实现以向待办中心提供待办项。
    /// </summary>
    /// <summary>
    /// 待办事项数据来源契约：向待办中心提供某一业务域的待办项。
    /// </summary>
    public interface IToDoProvider
    {
        Task<List<ToDoItem>> GetToDosAsync(User currentUser);
    }
}