using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocMgr.Data;
using DocMgr.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace DocMgr.Services.SystemSettings;

/// <summary>
/// 使用 SQL Server BACKUP / RESTORE 备份与还原当前业务库。
/// </summary>
public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private readonly DocMgrDatabaseSettings _databaseSettings;

    public DatabaseBackupService(DocMgrDatabaseSettings databaseSettings)
    {
        _databaseSettings = databaseSettings;
    }

    /// <inheritdoc />
    public string DatabasePath => _databaseSettings.TargetDisplay;

    /// <inheritdoc />
    public bool IsNetworkPath => !IsLocalDataSource(_databaseSettings.DataSource);

    /// <inheritdoc />
    public Task BackupToFileAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        string destination = Path.GetFullPath(destinationPath.Trim());
        EnsureParentDirectoryExists(destination);

        string catalog = QuoteIdentifier(_databaseSettings.InitialCatalog);
        string diskPath = EscapeSqlLiteral(destination);

        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = new SqlConnection(_databaseSettings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 0;
            command.CommandText =
                $"BACKUP DATABASE {catalog} TO DISK = N'{diskPath}' WITH COPY_ONLY, INIT, FORMAT, STATS = 10;";
            await command.ExecuteNonQueryAsync(cancellationToken);
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

        string catalog = QuoteIdentifier(_databaseSettings.InitialCatalog);
        string diskPath = EscapeSqlLiteral(source);

        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = new SqlConnection(_databaseSettings.BuildMasterConnectionString());
            await connection.OpenAsync(cancellationToken);

            await ExecuteNonQueryAsync(
                connection,
                $"ALTER DATABASE {catalog} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;",
                cancellationToken);

            try
            {
                await ExecuteNonQueryAsync(
                    connection,
                    $"RESTORE DATABASE {catalog} FROM DISK = N'{diskPath}' WITH REPLACE, STATS = 10;",
                    cancellationToken);
            }
            finally
            {
                await ExecuteNonQueryAsync(
                    connection,
                    $"ALTER DATABASE {catalog} SET MULTI_USER;",
                    cancellationToken);
            }
        }, cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        SqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool IsLocalDataSource(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return true;
        }

        string normalized = dataSource.Trim();
        if (normalized.StartsWith("(local)", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".", StringComparison.Ordinal)
            || string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // localhost\INSTANCE 或 .\INSTANCE
        int slash = normalized.IndexOf('\\');
        if (slash > 0)
        {
            string host = normalized[..slash];
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, ".", StringComparison.Ordinal)
                || string.Equals(host, "(local)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            return string.Equals(normalized, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string QuoteIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("数据库名无效。");
        }

        return "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    private static string EscapeSqlLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static void EnsureParentDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"备份路径无效：{filePath}");
        }

        Directory.CreateDirectory(directory);
    }
}
