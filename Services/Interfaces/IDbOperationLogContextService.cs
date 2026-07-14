using System.ComponentModel;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 记录 UI 触发来源，并控制是否写入数据库操作日志。
    /// </summary>
    public interface IDbOperationLogContextService : INotifyPropertyChanged
    {
        bool IsRecordingEnabled { get; }

        string? CurrentPageName { get; }

        string? CurrentButtonName { get; }

        void SetRecordingEnabled(bool enabled);

        void SetCurrentPage(string? pageName);

        void CaptureButtonAction(string? buttonName);

        void ClearCurrentButtonName();
    }
}
