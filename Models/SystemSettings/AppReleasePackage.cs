using System;

namespace DocMgr.Models.SystemSettings;

/// <summary>
/// 软件升级包。库内只保留最新一包，供各终端登录时按版本号自动安装。
/// </summary>
public class AppReleasePackage
{
    public int Id { get; set; }

    /// <summary>安装包版本，规范为 <c>主.次.修订</c>，例如 <c>1.1.5</c>。</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>原始文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>文件字节数。</summary>
    public long FileSize { get; set; }

    /// <summary>安装包内容。</summary>
    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    /// <summary>上传时间。</summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>上传人姓名。</summary>
    public string UploaderName { get; set; } = string.Empty;
}
