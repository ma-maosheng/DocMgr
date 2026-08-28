using System;
using System.IO;
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
