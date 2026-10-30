using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;

namespace DocMgr.Config;

/// <summary>
/// 从 appsettings.json 加载 SQL Server 连接字符串。
/// SQL 登录密码若以明文写在连接串中，加载时会改写为仅本机可解密的 <c>Database:ProtectedPassword</c>。
/// </summary>
public static class DocMgrDatabaseConfiguration
{
    private const string AppSettingsFileName = "appsettings.json";
    private const string LanExampleFileName = "appsettings.lan.example.json";
    private const string ProtectedPasswordPropertyName = "ProtectedPassword";

    private static readonly JsonSerializerOptions SettingsWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 本机默认实例（MSSQLSERVER）+ Windows 身份 + 信任证书。
    /// 混合模式安装下仍可先用 Windows 登录；若需 SQL 登录，在连接串中写一次 User Id 与 Password，启动后密码会改为本机密文。
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

        string settingsText = File.ReadAllText(settingsPath);
        JsonObject root = ReadSettingsRoot(settingsPath, settingsText);
        JsonObject database = RequireDatabaseObject(root);
        string configured = RequireConnectionString(database);
        string? protectedPassword = ReadOptionalString(database, ProtectedPasswordPropertyName);

        string? passwordProtectionWarning = null;
        string liveConnectionString;
        if (DatabaseConnectionStringSecret.TryTakePlaintextPassword(
                configured,
                out string plaintextPassword,
                out string connectionStringWithoutPassword))
        {
            liveConnectionString = DatabaseConnectionStringSecret.AppendPassword(
                connectionStringWithoutPassword,
                plaintextPassword);
            passwordProtectionWarning = TryPersistProtectedPassword(
                settingsPath,
                settingsText,
                root,
                database,
                connectionStringWithoutPassword,
                plaintextPassword);
        }
        else if (!string.IsNullOrWhiteSpace(protectedPassword))
        {
            string password = DatabasePasswordProtection.Unprotect(protectedPassword);
            liveConnectionString = DatabaseConnectionStringSecret.AppendPassword(configured, password);
        }
        else
        {
            liveConnectionString = configured;
        }

        ValidateConnectionString(liveConnectionString);
        return new DocMgrDatabaseOptions(liveConnectionString, passwordProtectionWarning);
    }

    private static string? TryPersistProtectedPassword(
        string settingsPath,
        string originalText,
        JsonObject root,
        JsonObject database,
        string connectionStringWithoutPassword,
        string plaintextPassword)
    {
        string protectedPassword;
        try
        {
            protectedPassword = DatabasePasswordProtection.Protect(plaintextPassword);
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException or NotSupportedException)
        {
            return "无法用 Windows 数据保护接口加密 SQL 密码，appsettings.json 中仍是明文。请在 Windows 上运行本程序。";
        }

        database["ConnectionString"] = connectionStringWithoutPassword;
        database[ProtectedPasswordPropertyName] = protectedPassword;

        try
        {
            WriteSettingsAtomically(settingsPath, originalText, root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "SQL 密码已用于本次连接，但无法写回 appsettings.json（文件只读或目录没有修改权限），磁盘上仍是明文。"
                + Environment.NewLine
                + "请授予本程序对安装目录的修改权限后重新启动。在此之前不要把该配置文件外传。";
        }

        return null;
    }

    private static void WriteSettingsAtomically(string settingsPath, string originalText, JsonObject root)
    {
        string newline = originalText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string json = root.ToJsonString(SettingsWriteOptions).Replace("\n", newline, StringComparison.Ordinal);
        if (!json.EndsWith(newline, StringComparison.Ordinal))
        {
            json += newline;
        }

        string tempPath = settingsPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Replace(tempPath, settingsPath, destinationBackupFileName: null);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static JsonObject ReadSettingsRoot(string settingsPath, string settingsText)
    {
        try
        {
            if (JsonNode.Parse(settingsText) is JsonObject root)
            {
                return root;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"无法解析配置文件 {settingsPath}：{ex.Message}", ex);
        }

        throw new InvalidOperationException($"无法解析配置文件 {settingsPath}：根节点必须是对象。");
    }

    private static JsonObject RequireDatabaseObject(JsonObject root)
    {
        if (root["Database"] is JsonObject database)
        {
            return database;
        }

        throw new InvalidOperationException(
            $"{AppSettingsFileName} 中缺少 Database 节。" +
            $"局域网部署可参考同目录 {LanExampleFileName}。");
    }

    private static string RequireConnectionString(JsonObject database)
    {
        string? configured = ReadOptionalString(database, "ConnectionString");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"{AppSettingsFileName} 中缺少 Database:ConnectionString。" +
                $"局域网部署可参考同目录 {LanExampleFileName}，将改好的连接串写入 {AppSettingsFileName}。");
        }

        return configured.Trim();
    }

    private static string? ReadOptionalString(JsonObject database, string propertyName)
    {
        JsonNode? node = database[propertyName];
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out string? text))
        {
            return text;
        }

        throw new InvalidOperationException($"{AppSettingsFileName} 中 Database:{propertyName} 必须是字符串。");
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
}
