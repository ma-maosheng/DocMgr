namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 高影仪采集分辨率（像素数），如 4160×3120。
    /// </summary>
    public sealed class DocumentCameraResolutionOption : IEquatable<DocumentCameraResolutionOption>
    {
        public DocumentCameraResolutionOption(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>像素宽。</summary>
        public int Width { get; }

        /// <summary>像素高。</summary>
        public int Height { get; }

        /// <summary>下拉展示，如 4160×3120。</summary>
        public string DisplayName => $"{Width}×{Height}";

        /// <summary>像素面积，用于排序与默认挑选。</summary>
        public int PixelCount => Width * Height;

        /// <summary>
        /// 在设备支持的分辨率中挑选：优先已记住的宽高，其次 4160×3120，否则取最大像素。
        /// </summary>
        public static DocumentCameraResolutionOption? ResolvePreferred(
            IReadOnlyList<DocumentCameraResolutionOption> options,
            int preferredWidth,
            int preferredHeight)
        {
            if (options == null || options.Count == 0)
            {
                return null;
            }

            if (preferredWidth > 0 && preferredHeight > 0)
            {
                DocumentCameraResolutionOption? remembered = options.FirstOrDefault(item =>
                    item.Width == preferredWidth && item.Height == preferredHeight);
                if (remembered != null)
                {
                    return remembered;
                }
            }

            DocumentCameraResolutionOption? native13M = options.FirstOrDefault(item =>
                item.Width == 4160 && item.Height == 3120);
            if (native13M != null)
            {
                return native13M;
            }

            return options.OrderByDescending(item => item.PixelCount).First();
        }

        public bool Equals(DocumentCameraResolutionOption? other) =>
            other != null && Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) => Equals(obj as DocumentCameraResolutionOption);

        public override int GetHashCode() => HashCode.Combine(Width, Height);

        public override string ToString() => DisplayName;
    }
}
