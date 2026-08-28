using System.Reflection;

namespace DocMgr.Infrastructure;

/// <summary>
/// 读取程序集版本，供登录窗与主窗口展示。发版时改 <c>DocMgr.csproj</c> 的 <c>Version</c>。
/// </summary>
public static class AppVersionInfo
{
    /// <summary>
    /// 面向用户的版本号，例如 <c>1.0.0</c>。
    /// </summary>
    public static string DisplayVersion
    {
        get
        {
            string? informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?.Trim();
            if (!string.IsNullOrWhiteSpace(informational))
            {
                int plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
                return plusIndex > 0 ? informational[..plusIndex] : informational;
            }

            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
            {
                return "未知";
            }

            return version.ToString(3);
        }
    }
}
