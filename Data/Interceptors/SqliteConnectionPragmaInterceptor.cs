using System.Data.Common;
using System.Globalization;
using DocMgr.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DocMgr.Data.Interceptors;

/// <summary>
/// 打开 SQLite 连接时统一设置外键、日志模式与忙等待，兼容局域网共享库多客户端访问。
/// </summary>
public sealed class SqliteConnectionPragmaInterceptor : DbConnectionInterceptor
{
    private readonly int _busyTimeoutMilliseconds;

    public SqliteConnectionPragmaInterceptor(DocMgrDatabaseSettings databaseSettings)
    {
        ArgumentNullException.ThrowIfNull(databaseSettings);
        _busyTimeoutMilliseconds = databaseSettings.BusyTimeoutSeconds * 1000;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyPragmas(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void ApplyPragmas(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            return;
        }

        using var command = sqliteConnection.CreateCommand();
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            PRAGMA busy_timeout = {_busyTimeoutMilliseconds};
            PRAGMA journal_mode = DELETE;
            PRAGMA foreign_keys = ON;
            """);
        command.ExecuteNonQuery();
    }
}
