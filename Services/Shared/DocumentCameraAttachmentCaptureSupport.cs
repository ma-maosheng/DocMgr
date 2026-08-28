using System.IO;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 高影仪直拍结果转附件文件名的共用逻辑。
    /// </summary>
    public static class DocumentCameraAttachmentCaptureSupport
    {
        /// <summary>
        /// 打开高影仪直拍窗口；取消或未拍到有效画面时返回 <see langword="null"/>。
        /// </summary>
        public static DocumentCameraCaptureResult? Capture(IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(dialogService);
            DocumentCameraCaptureResult? captured = dialogService.ShowDocumentCameraCaptureDialog();
            if (captured == null || captured.JpegContent == null || captured.JpegContent.Length == 0)
            {
                return null;
            }

            return captured;
        }

        /// <summary>
        /// 生成直拍附件文件名：业务编号_分类_时间.jpg。
        /// </summary>
        public static string BuildFileName(string? businessNo, string? category, string fallbackPrefix = "附件")
        {
            string no = string.IsNullOrWhiteSpace(businessNo)
                ? (string.IsNullOrWhiteSpace(fallbackPrefix) ? "附件" : fallbackPrefix.Trim())
                : businessNo.Trim();
            string cat = string.IsNullOrWhiteSpace(category) ? "附件" : category.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                no = no.Replace(invalid, '_');
                cat = cat.Replace(invalid, '_');
            }

            return $"{no}_{cat}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
        }
    }
}
