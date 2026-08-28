using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocMgr.Data;
using DocMgr.Services.Interfaces;
using Microsoft.Data.Sqlite;

namespace DocMgr.Services.SystemSettings;

/// <summary>
/// 用 SQLite 在线备份接口复制当前库，避免直接拷贝正在写入的 <c>.db</c> / WAL 文件。
/// </summary>
public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private readonly DocMgrDatabaseSettings _databaseSettings;

    public DatabaseBackupService(DocMgrDatabaseSettings databaseSettings)
    {
        _databaseSettings = databaseSettings;
    }

    /// <inheritdoc />
    public string DatabasePath => _databaseSettings.DbPath;

    /// <inheritdoc />
    public bool IsNetworkPath => _databaseSettings.IsNetworkPath;

    /// <inheritdoc />
    public Task BackupToFileAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        string destination = Path.GetFullPath(destinationPath.Trim());
        EnsureNotSameFile(DatabasePath, destination, "不能把当前库备份到它自己的路径。");
        EnsureParentDirectoryExists(destination);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyDatabase(_databaseSettings.ConnectionString, BuildFileConnectionString(destination));
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RestoreFromFileAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        string source = Path.GetFullPath(sourcePath.Trim());
        if (!File.Exists(source))
        {
            throw new InvalidOperationException($"找不到备份文件：{source}");
        }

        EnsureNotSameFile(source, DatabasePath, "所选文件就是当前正在使用的数据库，无需还原。");

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyDatabase(BuildFileConnectionString(source), _databaseSettings.ConnectionString);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public PreMigrateBackupResult TryCreatePreMigrateBackup()
    {
        const int keepCount = 3;
        if (!File.Exists(DatabasePath))
        {
            return new PreMigrateBackupResult(Skipped: true, Succeeded: true, Message: "尚无数据库文件，跳过升级前备份。");
        }

        string? directory = Path.GetDirectoryName(DatabasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return new PreMigrateBackupResult(Skipped: false, Succeeded: false, Message: $"无法解析数据库目录：{DatabasePath}");
        }

        string stem = Path.GetFileNameWithoutExtension(DatabasePath);
        string destination = Path.Combine(directory, $"{stem}.pre-migrate-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        try
        {
            CopyDatabase(_databaseSettings.ConnectionString, BuildFileConnectionString(destination));
            PrunePreMigrateBackups(directory, stem, keepCount);
            return new PreMigrateBackupResult(Skipped: false, Succeeded: true, Message: destination);
        }
        catch (Exception ex)
        {
            return new PreMigrateBackupResult(
                Skipped: false,
                Succeeded: false,
                Message: $"升级前备份未成功（将继续升级）：{ex.Message}");
        }
    }

    private static void PrunePreMigrateBackups(string directory, string stem, int keepCount)
    {
        string pattern = $"{stem}.pre-migrate-*.db";
        foreach (string stalePath in Directory.GetFiles(directory, pattern)
                     .OrderByDescending(File.GetCreationTimeUtc)
                     .Skip(keepCount))
        {
            try
            {
                File.Delete(stalePath);
            }
            catch
            {
                // 旧备份删不掉不影响本次升级。
            }
        }
    }

    private static void CopyDatabase(string sourceConnectionString, string destinationConnectionString)
    {
        using var source = new SqliteConnection(sourceConnectionString);
        using var destination = new SqliteConnection(destinationConnectionString);
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private string BuildFileConnectionString(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = _databaseSettings.BusyTimeoutSeconds
        };
        return builder.ConnectionString;
    }

    private static void EnsureParentDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"备份路径无效：{filePath}");
        }

        Directory.CreateDirectory(directory);
    }

    private static void EnsureNotSameFile(string leftPath, string rightPath, string message)
    {
        if (string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(message);
        }
    }
}
