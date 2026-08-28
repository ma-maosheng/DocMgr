using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 系统附件查看、临时文件写入与图像识别辅助逻辑。
    /// </summary>
    public static class SystemAttachmentViewSupport
    {
        private static readonly string[] ImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff"
        };

        /// <summary>
        /// 解析用于展示与保存的完整文件名（补齐缺失后缀）。
        /// </summary>
        public static string ResolveDisplayFileName(SystemAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);

            string fileName = string.IsNullOrWhiteSpace(attachment.FileName)
                ? "attachment"
                : attachment.FileName.Trim();

            string extension = NormalizeExtension(attachment.Extension);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = InferExtensionFromContent(attachment.FileContent);
            }

            if (!string.IsNullOrWhiteSpace(extension) && !HasExtension(fileName, extension))
            {
                return fileName + extension;
            }

            return fileName;
        }

        /// <summary>
        /// 判断附件是否应按图像方式预览。
        /// </summary>
        public static bool IsImageAttachment(SystemAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);

            if (IsImageContent(attachment.FileContent))
            {
                return true;
            }

            string extension = GetEffectiveExtension(attachment);
            return IsImageExtension(extension);
        }

        /// <summary>
        /// 尝试从附件内容创建可绑定的预览图像源。
        /// 会校正异常 DPI，并将超大图缩到显卡纹理上限内，避免预览窗打开后空白或只看到一角。
        /// </summary>
        public static bool TryCreateImageSource(SystemAttachment attachment, out BitmapSource? imageSource)
        {
            imageSource = null;
            ArgumentNullException.ThrowIfNull(attachment);

            if (attachment.FileContent == null || attachment.FileContent.Length == 0 || !IsImageAttachment(attachment))
            {
                return false;
            }

            try
            {
                imageSource = CreateDisplayBitmapSource(attachment.FileContent);
                return imageSource != null;
            }
            catch
            {
                imageSource = null;
                return false;
            }
        }

        /// <summary>
        /// WPF 硬件加速纹理常见上限。高影仪 4160×3120 等超过此值时 Image 会空白。
        /// </summary>
        private const int MaxDisplayEdgePx = 4096;

        /// <summary>
        /// 解码附件字节为适合界面展示的位图（96 DPI，最长边不超过纹理上限）。
        /// </summary>
        private static BitmapSource? CreateDisplayBitmapSource(byte[] content)
        {
            BitmapImage loaded = LoadBitmapImage(content, decodePixelWidth: null, decodePixelHeight: null);
            if (loaded.PixelWidth <= 0 || loaded.PixelHeight <= 0)
            {
                return null;
            }

            if (loaded.PixelWidth > MaxDisplayEdgePx || loaded.PixelHeight > MaxDisplayEdgePx)
            {
                loaded = loaded.PixelWidth >= loaded.PixelHeight
                    ? LoadBitmapImage(content, decodePixelWidth: MaxDisplayEdgePx, decodePixelHeight: null)
                    : LoadBitmapImage(content, decodePixelWidth: null, decodePixelHeight: MaxDisplayEdgePx);
            }

            return NormalizeDpi(loaded);
        }

        private static BitmapImage LoadBitmapImage(byte[] content, int? decodePixelWidth, int? decodePixelHeight)
        {
            var stream = new MemoryStream(content, 0, content.Length, writable: false, publiclyVisible: true);
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
                bitmap.StreamSource = stream;
                if (decodePixelWidth is int width && width > 0)
                {
                    bitmap.DecodePixelWidth = width;
                }

                if (decodePixelHeight is int height && height > 0)
                {
                    bitmap.DecodePixelHeight = height;
                }

                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                stream.Dispose();
            }
        }

        /// <summary>
        /// 高影仪/OpenCV 写出的 JPEG 常带 JFIF 密度 1（无单位），WPF 会当成 1 DPI，
        /// 预览尺寸被放大数百倍，ScrollViewer 里只看到黑边或一角。
        /// </summary>
        private static BitmapSource NormalizeDpi(BitmapSource source)
        {
            if (Math.Abs(source.DpiX - 96) < 0.5 && Math.Abs(source.DpiY - 96) < 0.5)
            {
                return source;
            }

            BitmapSource converted = source;
            if (source.Format != PixelFormats.Bgr32
                && source.Format != PixelFormats.Bgra32
                && source.Format != PixelFormats.Pbgra32
                && source.Format != PixelFormats.Bgr24)
            {
                converted = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
                converted.Freeze();
            }

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = (width * converted.Format.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[checked(stride * height)];
            converted.CopyPixels(pixels, stride, 0);
            var normalized = BitmapSource.Create(
                width,
                height,
                96,
                96,
                converted.Format,
                converted.Palette,
                pixels,
                stride);
            normalized.Freeze();
            return normalized;
        }

        /// <summary>
        /// 将附件写入临时目录并返回完整路径。
        /// </summary>
        public static string WriteTempFile(SystemAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);

            if (attachment.FileContent == null || attachment.FileContent.Length == 0)
            {
                throw new InvalidOperationException("附件内容为空，无法打开。");
            }

            string displayFileName = ResolveDisplayFileName(attachment);
            string safeFileName = SanitizeFileName(displayFileName);
            string tempDirectory = Path.Combine(Path.GetTempPath(), "DocMgr", "attachments", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            string tempPath = Path.Combine(tempDirectory, safeFileName);
            File.WriteAllBytes(tempPath, attachment.FileContent);
            return tempPath;
        }

        /// <summary>
        /// 打开附件。图像不走系统默认关联（本机默认是百度网盘「智能看图」），改用 Windows 照片查看器等。
        /// </summary>
        public static void OpenWithDefaultApplication(SystemAttachment attachment)
        {
            if (IsImageAttachment(attachment))
            {
                OpenImageWithPreferredViewer(attachment);
                return;
            }

            string tempPath = WriteTempFile(attachment);
            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
        }

        /// <summary>
        /// 用 Windows 自带看图程序打开图像；不调用被屏蔽的默认关联（百度网盘「智能看图」）。
        /// </summary>
        private static void OpenImageWithPreferredViewer(SystemAttachment attachment)
        {
            string imagePath = WriteImageTempFile(attachment);
            if (TryOpenWithWindowsPhotoViewer(imagePath))
            {
                return;
            }

            if (TryOpenWithPaint(imagePath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "rundll32.exe"),
                Arguments = $"shell32.dll,OpenAs_RunDLL {imagePath}",
                UseShellExecute = false
            });
        }

        /// <summary>
        /// 写出无空格路径的临时图像，避免 Windows 照片查看器无法解析带空格路径。
        /// </summary>
        private static string WriteImageTempFile(SystemAttachment attachment)
        {
            string displayName = ResolveDisplayFileName(attachment);
            string extension = Path.GetExtension(displayName);
            if (string.IsNullOrWhiteSpace(extension) || !IsImageExtension(extension))
            {
                extension = InferExtensionFromContent(attachment.FileContent);
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            string directory = Path.Combine(Path.GetTempPath(), "DocMgr", "attachments");
            Directory.CreateDirectory(directory);
            string imagePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + extension.ToLowerInvariant());
            File.WriteAllBytes(imagePath, attachment.FileContent!);
            return imagePath;
        }

        private static bool TryOpenWithWindowsPhotoViewer(string imagePath)
        {
            string? dll = ResolveWindowsPhotoViewerDll();
            if (dll == null)
            {
                return false;
            }

            string rundll = Path.Combine(Environment.SystemDirectory, "rundll32.exe");
            if (!File.Exists(rundll))
            {
                return false;
            }

            try
            {
                Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = rundll,
                    Arguments = $"\"{dll}\", ImageView_Fullscreen {imagePath}",
                    UseShellExecute = false
                });
                return process != null;
            }
            catch
            {
                return false;
            }
        }

        private static string? ResolveWindowsPhotoViewerDll()
        {
            string[] candidates =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Photo Viewer", "PhotoViewer.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Photo Viewer", "PhotoViewer.dll")
            ];

            return candidates.FirstOrDefault(File.Exists);
        }

        private static bool TryOpenWithPaint(string imagePath)
        {
            string paintPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsApps",
                "mspaint.exe");
            if (!File.Exists(paintPath))
            {
                return false;
            }

            try
            {
                Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = paintPath,
                    Arguments = $"\"{imagePath}\"",
                    UseShellExecute = true
                });
                return process != null;
            }
            catch
            {
                return false;
            }
        }

        private static string GetEffectiveExtension(SystemAttachment attachment)
        {
            string extension = NormalizeExtension(attachment.Extension);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension;
            }

            extension = InferExtensionFromContent(attachment.FileContent);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension;
            }

            string fileName = attachment.FileName?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(fileName)
                ? string.Empty
                : Path.GetExtension(fileName);
        }

        private static string NormalizeExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            string trimmed = extension.Trim();
            if (trimmed.StartsWith('.'))
            {
                return trimmed.ToLowerInvariant();
            }

            return "." + trimmed.ToLowerInvariant();
        }

        private static bool HasExtension(string fileName, string extension)
        {
            return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImageExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            return ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsImageContent(byte[]? content)
        {
            if (content == null || content.Length < 4)
            {
                return false;
            }

            if (content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            {
                return true;
            }

            if (content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
            {
                return true;
            }

            if (content[0] == 0x47 && content[1] == 0x49 && content[2] == 0x46)
            {
                return true;
            }

            if (content[0] == 0x42 && content[1] == 0x4D)
            {
                return true;
            }

            if (content.Length >= 12 &&
                content[0] == 0x52 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x46 &&
                content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50)
            {
                return true;
            }

            return IsTiffContent(content);
        }

        private static string InferExtensionFromContent(byte[]? content)
        {
            if (content == null || content.Length < 4)
            {
                return string.Empty;
            }

            if (content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            {
                return ".jpg";
            }

            if (content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
            {
                return ".png";
            }

            if (content[0] == 0x47 && content[1] == 0x49 && content[2] == 0x46)
            {
                return ".gif";
            }

            if (content[0] == 0x42 && content[1] == 0x4D)
            {
                return ".bmp";
            }

            if (content.Length >= 12 &&
                content[0] == 0x52 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x46 &&
                content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50)
            {
                return ".webp";
            }

            return IsTiffContent(content) ? ".tif" : string.Empty;
        }

        private static bool IsTiffContent(byte[] content)
        {
            if (content.Length < 4)
            {
                return false;
            }

            return (content[0] == 0x49 && content[1] == 0x49 && content[2] == 0x2A && content[3] == 0x00)
                || (content[0] == 0x4D && content[1] == 0x4D && content[2] == 0x00 && content[3] == 0x2A);
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "attachment.dat";
            }

            string sanitized = string.Concat(fileName.Trim().Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            return string.IsNullOrWhiteSpace(sanitized) ? "attachment.dat" : sanitized;
        }
    }
}
