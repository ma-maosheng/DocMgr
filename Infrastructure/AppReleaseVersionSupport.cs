using System.Diagnostics;
using System.IO;
using System.Text;

namespace DocMgr.Infrastructure;

/// <summary>
/// 安装包版本比较，以及退出当前进程后的静默安装。
/// </summary>
public static class AppReleaseVersionSupport
{
    /// <summary>与 <c>tools/DocMgr.iss</c> 的 AppId 一致，用于安装后定位程序目录。</summary>
    public const string InnoAppId = "{8F3C1A6B-4D2E-4B91-9C7A-2E5D8F0A1B3C}";

    public const long MinPackageBytes = 1L * 1024 * 1024;

    public const long MaxPackageBytes = 300L * 1024 * 1024;

    /// <summary>
    /// 将文件版本规范为 <c>主.次.修订</c>。无法解析时返回 false。
    /// </summary>
    public static bool TryNormalize(string? text, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        int spaceIndex = trimmed.IndexOf(' ', StringComparison.Ordinal);
        if (spaceIndex > 0)
        {
            trimmed = trimmed[..spaceIndex];
        }

        if (!Version.TryParse(trimmed, out Version? version))
        {
            return false;
        }

        int build = version.Build < 0 ? 0 : version.Build;
        canonical = $"{version.Major}.{version.Minor}.{build}";
        return true;
    }

    /// <summary>从安装包文件版本读取规范版本号。</summary>
    public static bool TryReadPackageVersion(string filePath, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        FileVersionInfo info = FileVersionInfo.GetVersionInfo(filePath);
        if (TryNormalize(info.FileVersion, out canonical))
        {
            return true;
        }

        return TryNormalize(info.ProductVersion, out canonical);
    }

    /// <summary>已发布版本是否高于当前程序版本。</summary>
    public static bool IsNewer(string? publishedVersion, string? localVersion)
    {
        if (!TryNormalize(publishedVersion, out string published))
        {
            return false;
        }

        if (!TryNormalize(localVersion, out string local))
        {
            return false;
        }

        return Version.Parse(published) > Version.Parse(local);
    }

    /// <summary>
    /// 启动独立 PowerShell：等待本进程退出后静默安装，再打开安装目录中的程序。
    /// </summary>
    public static void StartSilentUpgrade(string setupFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setupFilePath);
        if (!File.Exists(setupFilePath))
        {
            throw new InvalidOperationException("安装包文件不存在，无法启动升级。");
        }

        string directory = Path.Combine(Path.GetTempPath(), "DocMgrUpgrade");
        Directory.CreateDirectory(directory);
        string scriptPath = Path.Combine(directory, "upgrade.ps1");
        string logPath = Path.Combine(directory, "upgrade.log");
        int processId = Environment.ProcessId;

        string script = BuildUpgradeScript(processId, setupFilePath, logPath);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static string BuildUpgradeScript(int processId, string setupFilePath, string logPath)
    {
        string setup = QuotePowerShell(setupFilePath);
        string log = QuotePowerShell(logPath);
        string appId = QuotePowerShell(InnoAppId + "_is1");
        return """
            $ErrorActionPreference = 'Continue'
            $log = __LOG__
            function Write-Log([string] $message) {
                Add-Content -LiteralPath $log -Value ((Get-Date).ToString('s') + ' ' + $message)
            }
            Write-Log 'wait process __PID__'
            try { Wait-Process -Id __PID__ -Timeout 180 -ErrorAction SilentlyContinue } catch { }
            Start-Sleep -Seconds 1
            Write-Log 'start setup'
            $proc = Start-Process -FilePath __SETUP__ -ArgumentList '/VERYSILENT','/NORESTART','/SP-','/SUPPRESSMSGBOXES' -Wait -PassThru
            Write-Log ('setup exit ' + $proc.ExitCode)
            $keys = @(
                ('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + __APPID__),
                ('HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + __APPID__),
                ('HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\' + __APPID__)
            )
            foreach ($key in $keys) {
                if (Test-Path -LiteralPath $key) {
                    $loc = (Get-ItemProperty -LiteralPath $key).InstallLocation
                    if ($loc) {
                        $exe = Join-Path $loc 'DocMgr.exe'
                        if (Test-Path -LiteralPath $exe) {
                            Write-Log ('start ' + $exe)
                            Start-Process -FilePath $exe
                            exit 0
                        }
                    }
                }
            }
            Write-Log 'installed exe not found'
            """
            .Replace("__LOG__", log, StringComparison.Ordinal)
            .Replace("__PID__", processId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("__SETUP__", setup, StringComparison.Ordinal)
            .Replace("__APPID__", appId, StringComparison.Ordinal);
    }

    private static string QuotePowerShell(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
