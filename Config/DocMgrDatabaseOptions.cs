namespace DocMgr.Config;

/// <summary>
/// 应用数据库连接选项（由 appsettings.json 解析）。
/// </summary>
public sealed class DocMgrDatabaseOptions
{
    public DocMgrDatabaseOptions(string databasePath, int busyTimeoutSeconds, bool isNetworkPath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("数据库路径不能为空。", nameof(databasePath));
        }

        if (busyTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(busyTimeoutSeconds), "BusyTimeoutSeconds 必须大于 0。");
        }

        DatabasePath = databasePath;
        BusyTimeoutSeconds = busyTimeoutSeconds;
        IsNetworkPath = isNetworkPath;
    }

    /// <summary>SQLite 数据库文件完整路径。</summary>
    public string DatabasePath { get; }

    /// <summary>数据库忙等待超时（秒）。共享库场景建议 120 及以上。</summary>
    public int BusyTimeoutSeconds { get; }

    /// <summary>是否为 UNC 等网络路径。</summary>
    public bool IsNetworkPath { get; }
}
