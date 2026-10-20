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
    private const string LanExampleFileName = "appsettings.lan.example.json";

    /// <summary>
    /// 本机默认实例（MSSQLSERVER）+ Windows 身份 + 信任证书。
    /// 混合模式安装下仍可先用 Windows 登录；若需 sa，请在 appsettings 中改为 User Id/Password。
    /// </summary>
    public const string DefaultConnectionString =
        "Server=localhost;Database=DocMgr;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    /// <summary>
    /// 加载当前应用应使用的数据库配置。
    /// 仅读取程序目录下的 <c>appsettings.json</c>；<c>appsettings.lan.example.json</c> 只是样例，不会被读取。
    /// </summary>
    public static DocMgrDatabaseOptions Load(string? baseDirectory = null)
    {
        string appBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;

        string settingsPath = Path.Combine(appBaseDirectory, AppSettingsFileName);
        if (!File.Exists(settingsPath))
        {
            string lanExamplePath = Path.Combine(appBaseDirectory, LanExampleFileName);
            string hint = File.Exists(lanExamplePath)
                ? $"请将同目录下的 {LanExampleFileName} 复制为 {AppSettingsFileName}，把其中的 Server=… 改成实际 SQL Server 后保存，再启动。"
                : $"请在程序目录创建 {AppSettingsFileName}，写入 Database:ConnectionString（可参考安装包内的局域网示例）。";

            throw new InvalidOperationException(
                $"未找到正式配置文件：{settingsPath}{Environment.NewLine}" +
                $"程序不会读取 {LanExampleFileName}。{Environment.NewLine}" +
                hint);
        }

        var settings = TryReadSettings(settingsPath);
        string connectionString = ResolveConnectionString(settings?.Database?.ConnectionString);
        ValidateConnectionString(connectionString);
        return new DocMgrDatabaseOptions(connectionString);
    }

    private static string ResolveConnectionString(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"{AppSettingsFileName} 中缺少 Database:ConnectionString。" +
                $"局域网部署可参考同目录 {LanExampleFileName}，将改好的连接串写入 {AppSettingsFileName}。");
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
