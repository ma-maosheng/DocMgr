using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocMgr.Data;
using DocMgr.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace DocMgr.Services.SystemSettings;

/// <summary>
/// 使用 SQL Server BACKUP / RESTORE / DROP 备份、还原与删除当前业务库。
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

    /// <inheritdoc />
    public Task DropCurrentDatabaseAsync(CancellationToken cancellationToken = default)
    {
        string catalogName = _databaseSettings.InitialCatalog.Trim();
        EnsureDroppableCatalog(catalogName);
        string catalog = QuoteIdentifier(catalogName);

        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 先清掉本程序连接池，避免自己的连接占着库导致无法删除。
            SqlConnection.ClearAllPools();

            await using var connection = new SqlConnection(_databaseSettings.BuildMasterConnectionString());
            await connection.OpenAsync(cancellationToken);

            if (!await DatabaseExistsAsync(connection, catalogName, cancellationToken))
            {
                throw new InvalidOperationException($"当前数据库不存在：{catalogName}");
            }

            await ExecuteNonQueryAsync(
                connection,
                $"ALTER DATABASE {catalog} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;",
                cancellationToken);

            try
            {
                await ExecuteNonQueryAsync(
                    connection,
                    $"DROP DATABASE {catalog};",
                    cancellationToken);
            }
            catch
            {
                try
                {
                    await ExecuteNonQueryAsync(
                        connection,
                        $"ALTER DATABASE {catalog} SET MULTI_USER;",
                        CancellationToken.None);
                }
                catch (Exception recoverEx)
                {
                    System.Diagnostics.Debug.WriteLine(recoverEx);
                }

                throw;
            }
        }, cancellationToken);
    }

    private static async Task<bool> DatabaseExistsAsync(
        SqlConnection connection,
        string catalogName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = "SELECT DB_ID(@name);";
        command.Parameters.Add(new SqlParameter("@name", catalogName));
        object? id = await command.ExecuteScalarAsync(cancellationToken);
        return id is not null and not DBNull;
    }

    private static void EnsureDroppableCatalog(string catalogName)
    {
        if (string.IsNullOrWhiteSpace(catalogName))
        {
            throw new InvalidOperationException("数据库名无效。");
        }

        if (string.Equals(catalogName, "master", StringComparison.OrdinalIgnoreCase)
            || string.Equals(catalogName, "model", StringComparison.OrdinalIgnoreCase)
            || string.Equals(catalogName, "msdb", StringComparison.OrdinalIgnoreCase)
            || string.Equals(catalogName, "tempdb", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"不允许删除系统数据库：{catalogName}");
        }
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
