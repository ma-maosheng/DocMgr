using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 待办通知展示契约：将待办提醒呈现给用户。
    /// </summary>
    public interface IToDoNotificationPresenter
    {
        void Show(
            Window owner,
            IEnumerable<ToDoItem> items,
            Func<ToDoItem, Task>? openAction = null,
            Func<IEnumerable<ToDoItem>, Task>? ackAction = null);
    }
}