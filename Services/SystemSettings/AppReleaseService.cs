using System.IO;
using DocMgr.Data;
using DocMgr.Infrastructure;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.SystemSettings;

/// <summary>
/// 将安装包存入 SQL Server，并在登录时把更高版本写到临时目录后静默安装。
/// </summary>
public sealed class AppReleaseService : IAppReleaseService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _dbContext;

    public AppReleaseService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<AppReleasePackageSummary?> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppReleasePackages
            .AsNoTracking()
            .OrderByDescending(item => item.Id)
            .Select(item => new AppReleasePackageSummary
            {
                Id = item.Id,
                Version = item.Version,
                FileName = item.FileName,
                FileSize = item.FileSize,
                UploadedAt = item.UploadedAt,
                UploaderName = item.UploaderName
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AppReleaseUploadResult> UploadAsync(
        string filePath,
        User user,
        CancellationToken cancellationToken = default)
    {
        if (!ArchiveRegisterBusinessRules.IsSystemAdministrator(user))
        {
            return AppReleaseUploadResult.Fail("仅系统管理员可上传安装包。");
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return AppReleaseUploadResult.Fail("未找到要上传的安装包。");
        }

        string extension = Path.GetExtension(filePath);
        if (!string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return AppReleaseUploadResult.Fail("请选择本系统打出的 DocMgr-Setup 安装包（.exe）。");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < AppReleaseVersionSupport.MinPackageBytes
            || fileInfo.Length > AppReleaseVersionSupport.MaxPackageBytes)
        {
            return AppReleaseUploadResult.Fail("安装包大小应在 1 MB 到 300 MB 之间。");
        }

        if (!await IsWindowsExecutableAsync(filePath, cancellationToken))
        {
            return AppReleaseUploadResult.Fail("所选文件不是可执行的 Windows 安装包。");
        }

        if (!AppReleaseVersionSupport.TryReadPackageVersion(filePath, out string version))
        {
            return AppReleaseUploadResult.Fail("无法从安装包读取版本号。请使用本系统打出的 DocMgr-Setup 安装包。");
        }

        AppReleasePackageSummary? published = await GetPublishedAsync(cancellationToken);
        if (published != null
            && AppReleaseVersionSupport.TryNormalize(published.Version, out string publishedVersion)
            && Version.Parse(version) < Version.Parse(publishedVersion))
        {
            return AppReleaseUploadResult.Fail($"库内已发布 {publishedVersion}，不能改成更低的 {version}。");
        }

        byte[] content = await File.ReadAllBytesAsync(filePath, cancellationToken);
        if (content.LongLength != fileInfo.Length)
        {
            return AppReleaseUploadResult.Fail("读取安装包失败，请重新选择文件。");
        }

        string uploaderName = string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName : user.RealName.Trim();
        var package = new AppReleasePackage
        {
            Version = version,
            FileName = Path.GetFileName(filePath),
            FileSize = content.LongLength,
            FileContent = content,
            UploadedAt = DateTime.Now,
            UploaderName = uploaderName
        };

        int? previousTimeout = _dbContext.Database.GetCommandTimeout();
        _dbContext.Database.SetCommandTimeout(CommandTimeout);
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await _dbContext.AppReleasePackages.ExecuteDeleteAsync(cancellationToken);
            _dbContext.AppReleasePackages.Add(package);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _dbContext.Database.SetCommandTimeout(previousTimeout);
        }

        string sameVersionNote = published != null
            && AppReleaseVersionSupport.TryNormalize(published.Version, out string current)
            && string.Equals(current, version, StringComparison.Ordinal)
            ? "已替换同版本安装包。"
            : "已发布新版本。";
        return AppReleaseUploadResult.Ok($"{sameVersionNote}版本 {version}。其他终端下次登录时，若低于该版本将自动安装。");
    }

    /// <inheritdoc />
    public async Task<AppReleaseUpgradeStartResult> DownloadAndStartInstallerAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        int? previousTimeout = _dbContext.Database.GetCommandTimeout();
        _dbContext.Database.SetCommandTimeout(CommandTimeout);
        try
        {
            var row = await _dbContext.AppReleasePackages
                .AsNoTracking()
                .Where(item => item.Id == packageId)
                .Select(item => new { item.Version, item.FileSize, item.FileContent })
                .FirstOrDefaultAsync(cancellationToken);
            if (row?.FileContent == null || row.FileContent.Length == 0)
            {
                return AppReleaseUpgradeStartResult.Fail("库内安装包内容为空，请联系系统管理员重新上传。");
            }

            if (row.FileContent.LongLength != row.FileSize)
            {
                return AppReleaseUpgradeStartResult.Fail("库内安装包大小与记录不一致，请联系系统管理员重新上传。");
            }

            string directory = Path.Combine(Path.GetTempPath(), "DocMgrUpgrade");
            Directory.CreateDirectory(directory);
            string version = AppReleaseVersionSupport.TryNormalize(row.Version, out string canonical)
                ? canonical
                : "package";
            string setupPath = Path.Combine(directory, "DocMgr-Setup-" + version + ".exe");
            await File.WriteAllBytesAsync(setupPath, row.FileContent, cancellationToken);
            AppReleaseVersionSupport.StartSilentUpgrade(setupPath);
            return AppReleaseUpgradeStartResult.Ok();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return AppReleaseUpgradeStartResult.Fail("准备安装失败：" + ex.Message);
        }
        finally
        {
            _dbContext.Database.SetCommandTimeout(previousTimeout);
        }
    }

    private static async Task<bool> IsWindowsExecutableAsync(string filePath, CancellationToken cancellationToken)
    {
        byte[] header = new byte[2];
        await using var stream = File.OpenRead(filePath);
        int read = await stream.ReadAsync(header.AsMemory(0, 2), cancellationToken);
        return read == 2 && header[0] == (byte)'M' && header[1] == (byte)'Z';
    }
}
