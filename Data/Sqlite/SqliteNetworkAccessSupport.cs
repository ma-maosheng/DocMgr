using System;
using System.Threading;
using System.Threading.Tasks;
using DocMgr.Config;
using DocMgr.Data.Interceptors;
using DocMgr.Infrastructure.Startup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Data.Sqlite;

/// <summary>
/// 共享文件夹场景下 SQLite 连接串构建、迁移重试与锁异常识别。
/// </summary>
public static class SqliteNetworkAccessSupport
{
    /// <summary>
    /// 构建适用于局域网共享库的 SQLite 连接字符串。
    /// </summary>
    public static string BuildConnectionString(DocMgrDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = (int)TimeSpan.FromSeconds(options.BusyTimeoutSeconds).TotalSeconds
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// 为独立创建的 <see cref="DbContext"/> 应用与主库一致的 SQLite 配置。
    /// </summary>
    public static DbContextOptions<AppDbContext> CreateDbContextOptions(
        string connectionString,
        SqliteConnectionPragmaInterceptor pragmaInterceptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(pragmaInterceptor);

        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(pragmaInterceptor)
            .Options;
    }

    /// <summary>
    /// 在共享库上执行迁移，遇到数据库锁时自动重试。
    /// </summary>
    public static void MigrateWithRetry(
        AppDbContext dbContext,
        DocMgrDatabaseOptions options,
        AppInitializationState initializationState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(initializationState);

        int maxAttempts = options.IsNetworkPath ? 15 : 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (attempt > 1)
                {
                    initializationState.ReportProgress(
                        $"共享数据库正被其他终端使用，正在等待可用（{attempt}/{maxAttempts}）…");
                }

                dbContext.Database.Migrate();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsSqliteLockException(ex))
            {
                Thread.Sleep(CalculateRetryDelay(attempt));
            }
        }
    }

    /// <summary>
    /// 判断异常是否由 SQLite 文件锁/忙等待引起。
    /// </summary>
    public static bool IsSqliteLockException(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SqliteException sqliteException
                && (sqliteException.SqliteErrorCode == 5 || sqliteException.SqliteErrorCode == 6))
            {
                return true;
            }

            if (current.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("database table is locked", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 将锁相关异常包装为面向用户的提示信息。
    /// </summary>
    public static InvalidOperationException CreateSharedDatabaseUnavailableException(Exception innerException)
    {
        return new InvalidOperationException(
            "共享数据库暂时无法获取访问权限（可能被其他终端占用，或网络盘锁等待超时）。" +
            "请稍后重试；若持续失败，请暂时关闭其他已打开的客户端后，再删除共享目录中的 DocMgr.db-wal / DocMgr.db-shm 残留文件并重启。",
            innerException);
    }

    private static TimeSpan CalculateRetryDelay(int attempt)
    {
        int seconds = Math.Min(attempt * 2, 10);
        return TimeSpan.FromSeconds(seconds);
    }
}
