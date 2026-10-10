namespace DocMgr.Config;

/// <summary>
/// 应用数据库连接选项（由 appsettings.json 解析）。
/// </summary>
public sealed class DocMgrDatabaseOptions
{
    public DocMgrDatabaseOptions(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("数据库连接字符串不能为空。", nameof(connectionString));
        }

        ConnectionString = connectionString.Trim();
    }

    /// <summary>SQL Server 连接字符串。</summary>
    public string ConnectionString { get; }
}
