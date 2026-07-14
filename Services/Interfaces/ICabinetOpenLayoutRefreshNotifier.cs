using DocMgr.Models.Cabinets;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 开柜界面布局刷新通知：多个开柜/快照窗体打开时同步档口内容变更。
    /// </summary>
    public interface ICabinetOpenLayoutRefreshNotifier
    {
        event Action<CabinetOpenLayoutRefreshScope>? LayoutRefreshRequested;

        void RequestRefresh(CabinetOpenLayoutRefreshScope scope);
    }
}
