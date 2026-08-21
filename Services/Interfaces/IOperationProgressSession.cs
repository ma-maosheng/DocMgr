using System;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 桌面操作进度会话：用于 Excel 导入等耗时任务的状态与进度条提示。
    /// </summary>
    public interface IOperationProgressSession : IDisposable
    {
        /// <summary>仅更新说明文字，不改变当前进度条模式。</summary>
        void SetStatus(string status);

        /// <summary>切换为不确定进度（滚动条），并可选更新说明。</summary>
        void SetIndeterminate(string? status = null);

        /// <summary>按当前/总数更新确定进度；总数小于等于 0 时视为不确定进度。</summary>
        void Report(int current, int total, string? status = null);
    }
}
