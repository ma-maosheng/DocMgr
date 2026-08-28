namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 本机高影仪 / USB 摄像头设备摘要，供直拍窗口选择。
    /// </summary>
    public sealed class DocumentCameraDeviceInfo
    {
        /// <summary>会话打开时使用的设备标识。</summary>
        public string Identity { get; init; } = string.Empty;

        /// <summary>设备名称（驱动或系统枚举名）。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>下拉框展示文本。</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>采集后端类型，如 DirectShow。</summary>
        public string DeviceType { get; init; } = string.Empty;

        /// <summary>该设备支持的像素分辨率（去重后按像素从大到小）。</summary>
        public IReadOnlyList<DocumentCameraResolutionOption> Resolutions { get; init; } =
            Array.Empty<DocumentCameraResolutionOption>();

        /// <summary>最大分辨率宽，用于设备排序。</summary>
        public int Width { get; init; }

        /// <summary>最大分辨率高，用于设备排序。</summary>
        public int Height { get; init; }

        /// <summary>像素面积，用于优先选择主摄像头。</summary>
        public int PixelCount => Width * Height;

        /// <summary>名称是否匹配方正 / Q1300 / 高拍仪等关键词。</summary>
        public bool IsPreferred { get; init; }

        public override string ToString() => DisplayName;
    }
}
