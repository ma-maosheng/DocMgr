using System;
using System.IO;
using System.Text.Json;

namespace DocMgr.Config;

/// <summary>
/// 从 appsettings.json 加载 SQLite 数据库路径与网络访问参数。
/// </summary>
public static class DocMgrDatabaseConfiguration
{
    private const string DefaultDatabaseFileName = "DocMgr.db";
    private const string AppSettingsFileName = "appsettings.json";
    private const int DefaultLocalBusyTimeoutSeconds = 30;
    private const int DefaultNetworkBusyTimeoutSeconds = 120;

    /// <summary>
    /// 加载当前应用应使用的数据库配置。
    /// </summary>
    public static DocMgrDatabaseOptions Load(string? baseDirectory = null)
    {
        string appBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;

        var settings = TryReadSettings(Path.Combine(appBaseDirectory, AppSettingsFileName));
        string databasePath = ResolveDatabasePath(appBaseDirectory, settings?.Database?.Path);
        bool isNetworkPath = IsNetworkDatabasePath(databasePath);
        int busyTimeoutSeconds = ResolveBusyTimeoutSeconds(settings?.Database?.BusyTimeoutSeconds, isNetworkPath);

        ValidateDatabasePath(databasePath);
        return new DocMgrDatabaseOptions(databasePath, busyTimeoutSeconds, isNetworkPath);
    }

    private static string ResolveDatabasePath(string appBaseDirectory, string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.Combine(appBaseDirectory, DefaultDatabaseFileName);
        }

        return NormalizeConfiguredPath(configuredPath.Trim(), appBaseDirectory);
    }

    private static int ResolveBusyTimeoutSeconds(int? configuredValue, bool isNetworkPath)
    {
        if (configuredValue is > 0)
        {
            return configuredValue.Value;
        }

        return isNetworkPath ? DefaultNetworkBusyTimeoutSeconds : DefaultLocalBusyTimeoutSeconds;
    }

    internal static bool IsNetworkDatabasePath(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            return false;
        }

        if (databasePath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            string root = Path.GetPathRoot(databasePath) ?? string.Empty;
            if (root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return true;
            }

            DriveInfo drive = new(root);
            return drive.DriveType == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeConfiguredPath(string configuredPath, string appBaseDirectory)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(appBaseDirectory, configuredPath));
    }

    private static void ValidateDatabasePath(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"数据库路径无效：{databasePath}");
        }

        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException(
                $"数据库目录不存在或无法访问：{directory}。请确认 appsettings.json 中 Database:Path 配置正确，且当前用户对该共享目录有读写权限。");
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
        public string? Path { get; set; }

        public int? BusyTimeoutSeconds { get; set; }
    }
}
