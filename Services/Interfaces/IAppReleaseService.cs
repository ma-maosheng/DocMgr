using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 软件升级包：系统管理员上传安装包，登录时按版本号下载并静默安装。
/// </summary>
public interface IAppReleaseService
{
    /// <summary>读取当前已发布安装包的版本信息，不加载文件内容。</summary>
    Task<AppReleasePackageSummary?> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>用所选安装包替换库内已发布的一包。仅系统管理员可调用。</summary>
    Task<AppReleaseUploadResult> UploadAsync(string filePath, User user, CancellationToken cancellationToken = default);

    /// <summary>把指定安装包写到临时目录并启动静默升级。调用方随后应退出本进程。</summary>
    Task<AppReleaseUpgradeStartResult> DownloadAndStartInstallerAsync(int packageId, CancellationToken cancellationToken = default);
}

/// <summary>已发布安装包的版本信息，不含文件内容。</summary>
public sealed class AppReleasePackageSummary
{
    public int Id { get; init; }

    public string Version { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public DateTime UploadedAt { get; init; }

    public string UploaderName { get; init; } = string.Empty;

    /// <summary>供管理页展示的一行说明。</summary>
    public string DisplayText =>
        $"已发布 {Version}，{FileName}，{FileSize / 1024d / 1024d:F1} MB，{UploaderName} 于 {UploadedAt:yyyy-MM-dd HH:mm} 上传。";
}

/// <summary>上传安装包的结果。</summary>
public sealed class AppReleaseUploadResult
{
    public bool Succeeded { get; init; }

    public string Message { get; init; } = string.Empty;

    public static AppReleaseUploadResult Ok(string message) =>
        new() { Succeeded = true, Message = message };

    public static AppReleaseUploadResult Fail(string message) =>
        new() { Succeeded = false, Message = message };
}

/// <summary>登录时启动升级的结果。</summary>
public sealed class AppReleaseUpgradeStartResult
{
    public bool Started { get; init; }

    public string Message { get; init; } = string.Empty;

    public static AppReleaseUpgradeStartResult Ok() =>
        new() { Started = true, Message = string.Empty };

    public static AppReleaseUpgradeStartResult Fail(string message) =>
        new() { Started = false, Message = message };
}
