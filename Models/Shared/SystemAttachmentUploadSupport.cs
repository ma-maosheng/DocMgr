using System;
using System.IO;
using System.Linq;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 系统附件上传时的格式校验与文件选择对话框辅助。
    /// </summary>
    public static class SystemAttachmentUploadSupport
    {
        /// <summary>
        /// 允许上传的附件扩展名（常见图像格式与 PDF）。
        /// </summary>
        public static readonly string[] AllowedUploadExtensions =
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff"
        };

        /// <summary>
        /// 附件选择对话框过滤器。
        /// </summary>
        public const string OpenFileDialogFilter =
            "图像/PDF|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.tif;*.tiff;*.pdf|所有文件|*.*";

        /// <summary>
        /// 允许上传格式的用户提示文本。
        /// </summary>
        public const string AllowedFormatsDescription =
            "PDF 或常见图像格式（JPG、PNG、GIF、BMP、WEBP、TIFF）";

        /// <summary>
        /// 校验上传附件的文件格式；通过时返回 <see langword="null"/>，否则返回错误说明。
        /// </summary>
        public static string? ValidateUploadFormat(string? fileName, string? extension, byte[]? fileContent)
        {
            if (fileContent == null || fileContent.Length == 0)
            {
                return "附件内容为空，无法上传。";
            }

            string resolvedExtension = ResolveExtension(fileName, extension, fileContent);
            if (string.IsNullOrWhiteSpace(resolvedExtension))
            {
                return $"无法识别附件格式，仅支持{AllowedFormatsDescription}。";
            }

            if (!IsAllowedUploadExtension(resolvedExtension))
            {
                return $"不支持的附件格式「{resolvedExtension}」，仅支持{AllowedFormatsDescription}。";
            }

            string? contentExtension = DetectFormatFromContent(fileContent);
            if (string.IsNullOrWhiteSpace(contentExtension))
            {
                return "附件内容与常见图像或 PDF 格式不匹配，无法上传。";
            }

            if (!ExtensionsEquivalent(resolvedExtension, contentExtension))
            {
                return $"附件扩展名为「{resolvedExtension}」，但文件内容与该格式不一致，请检查后重试。";
            }

            return null;
        }

        /// <summary>
        /// 判断扩展名是否在允许上传范围内。
        /// </summary>
        public static bool IsAllowedUploadExtension(string? extension)
        {
            string normalized = NormalizeExtension(extension);
            return !string.IsNullOrWhiteSpace(normalized)
                && AllowedUploadExtensions.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveExtension(string? fileName, string? extension, byte[] fileContent)
        {
            string normalizedExtension = NormalizeExtension(extension);
            if (!string.IsNullOrWhiteSpace(normalizedExtension))
            {
                return normalizedExtension;
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                normalizedExtension = NormalizeExtension(Path.GetExtension(fileName.Trim()));
                if (!string.IsNullOrWhiteSpace(normalizedExtension))
                {
                    return normalizedExtension;
                }
            }

            return DetectFormatFromContent(fileContent) ?? string.Empty;
        }

        private static string? DetectFormatFromContent(byte[] content)
        {
            if (content.Length >= 4 && content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46)
            {
                return ".pdf";
            }

            if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            {
                return ".jpg";
            }

            if (content.Length >= 4 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
            {
                return ".png";
            }

            if (content.Length >= 3 && content[0] == 0x47 && content[1] == 0x49 && content[2] == 0x46)
            {
                return ".gif";
            }

            if (content.Length >= 2 && content[0] == 0x42 && content[1] == 0x4D)
            {
                return ".bmp";
            }

            if (content.Length >= 12 &&
                content[0] == 0x52 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x46 &&
                content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50)
            {
                return ".webp";
            }

            if (content.Length >= 4 &&
                ((content[0] == 0x49 && content[1] == 0x49 && content[2] == 0x2A && content[3] == 0x00) ||
                 (content[0] == 0x4D && content[1] == 0x4D && content[2] == 0x00 && content[3] == 0x2A)))
            {
                return ".tif";
            }

            return null;
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

        private static bool ExtensionsEquivalent(string left, string right)
        {
            string normalizedLeft = NormalizeExtension(left);
            string normalizedRight = NormalizeExtension(right);

            if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
            {
                return true;
            }

            if (IsJpegExtension(normalizedLeft) && IsJpegExtension(normalizedRight))
            {
                return true;
            }

            if (IsTiffExtension(normalizedLeft) && IsTiffExtension(normalizedRight))
            {
                return true;
            }

            return false;
        }

        private static bool IsJpegExtension(string extension) =>
            string.Equals(extension, ".jpg", StringComparison.Ordinal) ||
            string.Equals(extension, ".jpeg", StringComparison.Ordinal);

        private static bool IsTiffExtension(string extension) =>
            string.Equals(extension, ".tif", StringComparison.Ordinal) ||
            string.Equals(extension, ".tiff", StringComparison.Ordinal);
    }
}
