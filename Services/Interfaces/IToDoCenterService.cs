using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 待办中心聚合服务契约：汇总各业务域待办项并对外通知变化。
    /// </summary>
    public interface IToDoCenterService : INotifyPropertyChanged, IDisposable
    {
        int PendingCount { get; }
        IReadOnlyList<ToDoItem> Items { get; }

        bool EnableToDoPopup { get; }
        bool EnableToDoBadge { get; }
        bool MarkAllAsReadOnAcknowledge { get; }

        Task InitializeAsync(int topN = 20);
        Task RefreshAsync(int topN = 20);
        Task ApplyPreferenceAsync(UserPreference preference);

        Task MarkAsReadAsync(ToDoItem item);
        Task MarkAsReadAsync(IEnumerable<ToDoItem> items);

        void StartAutoRefresh(TimeSpan? interval = null);
        void StopAutoRefresh();
    }
}