namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 高影仪直拍影像处理偏好。按登录用户保存在本机，重开直拍窗与重新登录后仍生效。
    /// </summary>
    public sealed class DocumentCameraCaptureSettings
    {
        public const string ColorModeColor = "Color";
        public const string ColorModeGray = "Gray";
        public const string ColorModeBinary = "Binary";

        /// <summary>自动检测纸张外轮廓并裁掉台面。</summary>
        public bool AutoCrop { get; set; } = true;

        /// <summary>按纸张四角做透视校正（形变校正）。</summary>
        public bool PerspectiveCorrection { get; set; } = true;

        /// <summary>裁切后再去掉残余黑边。</summary>
        public bool RemoveBlackBorder { get; set; } = true;

        /// <summary>提亮纸面、压暗背景，适合签批单。</summary>
        public bool WhiteDocument { get; set; } = true;

        /// <summary>减轻侧光阴影；可能发灰，默认关闭。</summary>
        public bool RemoveShadow { get; set; }

        /// <summary>检测小角度倾斜并扶正。已做透视校正时通常不再需要。</summary>
        public bool AutoDeskew { get; set; } = true;

        /// <summary>水平镜像。</summary>
        public bool Mirror { get; set; }

        /// <summary>轻度锐化文字边缘。</summary>
        public bool Sharpen { get; set; }

        /// <summary>顺时针旋转角度：0 / 90 / 180 / 270。</summary>
        public int RotationDegrees { get; set; }

        /// <summary>亮度偏移，范围 -50～50。</summary>
        public int Brightness { get; set; }

        /// <summary>对比度偏移，范围 -50～50。</summary>
        public int Contrast { get; set; }

        /// <summary>JPEG 质量，范围 60～95。</summary>
        public int JpegQuality { get; set; } = 88;

        /// <summary><see cref="ColorModeColor"/> / <see cref="ColorModeGray"/> / <see cref="ColorModeBinary"/>。</summary>
        public string ColorMode { get; set; } = ColorModeColor;

        /// <summary>上次选用的拍摄设备标识。</summary>
        public string? LastDeviceIdentity { get; set; }

        /// <summary>上次选用的采集像素宽；0 表示按设备默认（优先 4160×3120）。</summary>
        public int CaptureWidth { get; set; }

        /// <summary>上次选用的采集像素高；0 表示按设备默认。</summary>
        public int CaptureHeight { get; set; }

        /// <summary>生成面向签批单/资料照片的默认值。</summary>
        public static DocumentCameraCaptureSettings CreateDefault() => new();

        /// <summary>色彩模式选项（值 + 显示名）。</summary>
        public static IReadOnlyList<DocumentCameraColorModeOption> ColorModeOptions { get; } =
        [
            new(ColorModeColor, "彩色"),
            new(ColorModeGray, "灰度"),
            new(ColorModeBinary, "黑白")
        ];

        /// <summary>校正越界取值。</summary>
        public void Normalize()
        {
            RotationDegrees = NormalizeRotation(RotationDegrees);
            Brightness = Math.Clamp(Brightness, -50, 50);
            Contrast = Math.Clamp(Contrast, -50, 50);
            JpegQuality = Math.Clamp(JpegQuality, 60, 95);
            string mode = ColorMode?.Trim() ?? string.Empty;
            ColorMode = string.Equals(mode, ColorModeGray, StringComparison.Ordinal)
                || string.Equals(mode, ColorModeBinary, StringComparison.Ordinal)
                ? mode
                : ColorModeColor;
            LastDeviceIdentity = string.IsNullOrWhiteSpace(LastDeviceIdentity)
                ? null
                : LastDeviceIdentity.Trim();
            CaptureWidth = Math.Max(0, CaptureWidth);
            CaptureHeight = Math.Max(0, CaptureHeight);
        }

        /// <summary>复制一份，避免缓存被界面直接改写。</summary>
        public DocumentCameraCaptureSettings Clone()
        {
            return new DocumentCameraCaptureSettings
            {
                AutoCrop = AutoCrop,
                PerspectiveCorrection = PerspectiveCorrection,
                RemoveBlackBorder = RemoveBlackBorder,
                WhiteDocument = WhiteDocument,
                RemoveShadow = RemoveShadow,
                AutoDeskew = AutoDeskew,
                Mirror = Mirror,
                Sharpen = Sharpen,
                RotationDegrees = RotationDegrees,
                Brightness = Brightness,
                Contrast = Contrast,
                JpegQuality = JpegQuality,
                ColorMode = ColorMode,
                LastDeviceIdentity = LastDeviceIdentity,
                CaptureWidth = CaptureWidth,
                CaptureHeight = CaptureHeight
            };
        }

        /// <summary>将任意角度归到 0/90/180/270。</summary>
        public static int NormalizeRotation(int degrees)
        {
            int value = degrees % 360;
            if (value < 0)
            {
                value += 360;
            }

            return ((value + 45) / 90) * 90 % 360;
        }
    }

    /// <summary>直拍色彩模式下拉项。</summary>
    public sealed class DocumentCameraColorModeOption
    {
        public DocumentCameraColorModeOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public string Value { get; }

        public string DisplayName { get; }
    }
}
