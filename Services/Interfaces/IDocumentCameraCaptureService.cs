using DocMgr.Models.Shared;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 本机高影仪 / USB 摄像头枚举与预览会话。
    /// </summary>
    public interface IDocumentCameraCaptureService
    {
        /// <summary>
        /// 枚举可用视频采集设备。查询在后台线程执行。
        /// </summary>
        Task<IReadOnlyList<DocumentCameraDeviceInfo>> EnumerateDevicesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 打开指定设备的预览会话。宽高为设备支持的像素；任一为 0 时按设备默认分辨率。
        /// </summary>
        Task<IDocumentCameraPreviewSession> OpenPreviewAsync(
            string deviceIdentity,
            int width = 0,
            int height = 0,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 高影仪预览会话：持续出帧，可抓取当前画面为 JPEG。
    /// </summary>
    public interface IDocumentCameraPreviewSession : IAsyncDisposable
    {
        /// <summary>最新一帧原始图像（JPEG 或 BMP）。在采集线程触发。</summary>
        event EventHandler<byte[]>? PreviewFrameArrived;

        /// <summary>是否已收到至少一帧原始画面。</summary>
        bool HasFrame { get; }

        /// <summary>
        /// 将当前预览帧编码为 JPEG。无可用帧时返回 <see langword="null"/>。
        /// 须在 STA 线程调用。
        /// </summary>
        byte[]? CaptureJpeg();
    }
}
