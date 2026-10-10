using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace DocMgr.Config;

/// <summary>
/// 从 appsettings.json 加载 SQL Server 连接字符串。
/// </summary>
public static class DocMgrDatabaseConfiguration
{
    private const string AppSettingsFileName = "appsettings.json";

    /// <summary>
    /// 本机默认实例（MSSQLSERVER）+ Windows 身份 + 信任证书。
    /// 混合模式安装下仍可先用 Windows 登录；若需 sa，请在 appsettings 中改为 User Id/Password。
    /// </summary>
    public const string DefaultConnectionString =
        "Server=localhost;Database=DocMgr;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    /// <summary>
    /// 加载当前应用应使用的数据库配置。
    /// </summary>
    public static DocMgrDatabaseOptions Load(string? baseDirectory = null)
    {
        string appBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;

        var settings = TryReadSettings(Path.Combine(appBaseDirectory, AppSettingsFileName));
        string connectionString = ResolveConnectionString(settings?.Database?.ConnectionString);
        ValidateConnectionString(connectionString);
        return new DocMgrDatabaseOptions(connectionString);
    }

    private static string ResolveConnectionString(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultConnectionString;
        }

        return configured.Trim();
    }

    private static void ValidateConnectionString(string connectionString)
    {
        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"无法解析数据库连接字符串：{ex.Message}",
                ex);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException(
                "数据库连接字符串缺少 Server/Data Source。默认实例请使用 Server=localhost。");
        }

        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException(
                "数据库连接字符串缺少 Database/Initial Catalog。请指定业务库名（如 DocMgr）。");
        }
    }

    private static DocMgrAppSettings? TryReadSettings(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(settingsFilePath);
            return JsonSerializer.Deserialize<DocMgrAppSettings>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"无法解析配置文件 {settingsFilePath}：{ex.Message}", ex);
        }
    }

    private sealed class DocMgrAppSettings
    {
        public DatabaseSettings? Database { get; set; }
    }

    private sealed class DatabaseSettings
    {
        public string? ConnectionString { get; set; }
    }
}
