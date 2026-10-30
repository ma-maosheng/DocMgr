namespace DocMgr.Config;

/// <summary>
/// 应用数据库连接选项（由 appsettings.json 解析）。
/// </summary>
public sealed class DocMgrDatabaseOptions
{
    public DocMgrDatabaseOptions(string connectionString, string? passwordProtectionWarning = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("数据库连接字符串不能为空。", nameof(connectionString));
        }

        ConnectionString = connectionString.Trim();
        PasswordProtectionWarning = string.IsNullOrWhiteSpace(passwordProtectionWarning)
            ? null
            : passwordProtectionWarning.Trim();
    }

    /// <summary>SQL Server 连接字符串（仅存在于内存；磁盘上的 SQL 密码以本机密文保存）。</summary>
    public string ConnectionString { get; }

    /// <summary>
    /// 磁盘上仍留有明文 SQL 密码时的提示。为空表示配置文件里没有明文密码。
    /// </summary>
    public string? PasswordProtectionWarning { get; }
}
