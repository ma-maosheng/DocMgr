namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 高影仪直拍确认结果：JPEG 图像字节。
    /// </summary>
    public sealed class DocumentCameraCaptureResult
    {
        /// <summary>JPEG 文件内容。</summary>
        public byte[] JpegContent { get; init; } = Array.Empty<byte>();
    }
}
