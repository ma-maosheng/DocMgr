using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        /// 尝试从附件内容创建可绑定的图像源。
        /// </summary>
        public static bool TryCreateImageSource(SystemAttachment attachment, out BitmapImage? imageSource)
        {
            imageSource = null;
            ArgumentNullException.ThrowIfNull(attachment);

            if (attachment.FileContent == null || attachment.FileContent.Length == 0 || !IsImageAttachment(attachment))
            {
                return false;
            }

            try
            {
                using var stream = new MemoryStream(attachment.FileContent);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                imageSource = bitmap;
                return true;
            }
            catch
            {
                return false;
            }
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
        /// 使用系统默认程序打开附件。
        /// </summary>
        public static void OpenWithDefaultApplication(SystemAttachment attachment)
        {
            string tempPath = WriteTempFile(attachment);
            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
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

            return false;
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

            return string.Empty;
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
