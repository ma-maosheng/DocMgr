using DocMgr.Models.Cabinets;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Cabinets
{
    /// <summary>
    /// 开柜界面布局刷新通知（Singleton，跨开柜窗体共享）。
    /// </summary>
    public sealed class CabinetOpenLayoutRefreshNotifier : ICabinetOpenLayoutRefreshNotifier
    {
        public event Action<CabinetOpenLayoutRefreshScope>? LayoutRefreshRequested;

        public void RequestRefresh(CabinetOpenLayoutRefreshScope scope)
        {
            ArgumentNullException.ThrowIfNull(scope);
            LayoutRefreshRequested?.Invoke(scope);
        }
    }
}
