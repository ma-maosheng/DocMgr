using DocMgr.Models.Shared;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 高影仪直拍处理偏好的本机读写（按登录用户分文件）。
    /// </summary>
    public interface IDocumentCameraCaptureSettingsStore
    {
        /// <summary>读取指定用户的偏好；无文件时返回默认值。</summary>
        DocumentCameraCaptureSettings Load(int? userId);

        /// <summary>保存指定用户的偏好。</summary>
        void Save(int? userId, DocumentCameraCaptureSettings settings);
    }
}
