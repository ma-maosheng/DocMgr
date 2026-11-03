using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocMgr.Data;
using DocMgr.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace DocMgr.Services.SystemSettings;

/// <summary>
/// 通过本机注册表、防火墙和当前 SQL 连接，完成局域网库服务器的三项设置。
/// TCP/IP 与防火墙改注册表和服务，需管理员权限；登录创建沿用当前数据库连接。
/// </summary>
public sealed class SqlServerLanSetupService : ISqlServerLanSetupService
{
    private const string FirewallRuleName = "SQL Server 1433";

    private readonly DocMgrDatabaseSettings _databaseSettings;

    public SqlServerLanSetupService(DocMgrDatabaseSettings databaseSettings)
    {
        _databaseSettings = databaseSettings;
    }

    /// <inheritdoc />
    public bool IsLocalServer => IsLocalDataSource(_databaseSettings.DataSource);

    /// <inheritdoc />
    public string InstanceDescription
    {
        get
        {
            if (!IsLocalServer)
            {
                return "远程 " + _databaseSettings.DataSource;
            }

            string? instanceName = TryGetInstanceName(_databaseSettings.DataSource);
            return instanceName == null
                ? "本机 " + _databaseSettings.DataSource
                : "本机实例 " + instanceName + "（" + _databaseSettings.DataSource + "）";
        }
    }

    /// <inheritdoc />
    public async Task<string> EnableTcpAsync(CancellationToken cancellationToken = default)
    {
        string instanceName = RequireLocalInstanceName();
        if (IsTcpAlreadyConfigured(instanceName))
        {
            return "TCP/IP 已启用，IPAll 端口已是 1433，动态端口已留空。未重启 SQL Server 服务。";
        }

        string message = await RunElevatedAsync(BuildTcpScript(instanceName), cancellationToken).ConfigureAwait(false);
        await WaitUntilOnlineAsync(cancellationToken).ConfigureAwait(false);
        return message + " SQL Server 服务已按新设置启动。若随后打开数据失败，请重新打开程序。";
    }

    /// <inheritdoc />
    public Task<string> AllowFirewallAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLocalServer)
        {
            throw new InvalidOperationException("防火墙规则只能在库服务器本机添加。请在装有 SQL Server 的那台电脑上打开本系统后再执行。");
        }

        return RunElevatedAsync(BuildFirewallScript(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int?> ReadAuthenticationModeAsync(CancellationToken cancellationToken = default)
    {
        int? fromServer = await TryReadLoginModeFromServerAsync(cancellationToken).ConfigureAwait(false);
        if (fromServer is 1 or 2)
        {
            return fromServer;
        }

        if (!IsLocalServer)
        {
            return fromServer;
        }

        string? instanceName = TryGetInstanceName(_databaseSettings.DataSource);
        return instanceName == null ? fromServer : TryReadLoginModeFromRegistry(instanceName) ?? fromServer;
    }

    /// <inheritdoc />
    public async Task EnableMixedModeAsync(CancellationToken cancellationToken = default)
    {
        string instanceName = RequireLocalInstanceName();
        await RunElevatedAsync(BuildMixedModeScript(instanceName), cancellationToken).ConfigureAwait(false);
        await WaitUntilOnlineAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DocMgrAppLoginExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_databaseSettings.BuildMasterConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'docmgr_app') THEN 1 ELSE 0 END;";
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is int number && number == 1;
    }

    /// <inheritdoc />
    public async Task<string> EnsureDocMgrAppLoginAsync(string? password, CancellationToken cancellationToken = default)
    {
        bool loginExists = await DocMgrAppLoginExistsAsync(cancellationToken).ConfigureAwait(false);
        if (!loginExists && string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("登录 docmgr_app 尚不存在，请先输入密码。");
        }

        string catalog = QuoteIdentifier(_databaseSettings.InitialCatalog);
        await using var connection = new SqlConnection(_databaseSettings.BuildMasterConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.Parameters.Add(new SqlParameter("@password", SqlDbType.NVarChar, 128)
        {
            Value = (object?)password ?? DBNull.Value
        });
        command.CommandText =
            """
            DECLARE @result int = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'docmgr_app')
            BEGIN
                IF @password IS NULL OR LEN(@password) = 0
                    THROW 50001, N'登录 docmgr_app 尚不存在，请先输入密码。', 1;
                DECLARE @create nvarchar(max) =
                    N'CREATE LOGIN [docmgr_app] WITH PASSWORD = ' + QUOTENAME(@password, N'''') + N', CHECK_POLICY = ON;';
                EXEC (@create);
                SET @result = 1;
            END
            ELSE IF @password IS NOT NULL AND LEN(@password) > 0
            BEGIN
                DECLARE @alter nvarchar(max) =
                    N'ALTER LOGIN [docmgr_app] WITH PASSWORD = ' + QUOTENAME(@password, N'''') + N' UNLOCK, CHECK_POLICY = ON;';
                EXEC (@alter);
                SET @result = 2;
            END
            """
            + Environment.NewLine
            + $"USE {catalog};"
            + Environment.NewLine
            + """
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'docmgr_app')
            BEGIN
                CREATE USER [docmgr_app] FOR LOGIN [docmgr_app];
            END
            IF NOT EXISTS (
                SELECT 1
                FROM sys.database_role_members AS member
                INNER JOIN sys.database_principals AS role_principal ON member.role_principal_id = role_principal.principal_id
                INNER JOIN sys.database_principals AS member_principal ON member.member_principal_id = member_principal.principal_id
                WHERE role_principal.name = N'db_owner' AND member_principal.name = N'docmgr_app')
            BEGIN
                ALTER ROLE [db_owner] ADD MEMBER [docmgr_app];
            END
            SELECT @result;
            """;

        object? resultValue = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        int result = resultValue is int code ? code : 0;
        string catalogName = _databaseSettings.InitialCatalog;
        if (result == 1)
        {
            return $"已创建登录 docmgr_app，并授予数据库 {catalogName} 的 db_owner。客户端连接串使用 User Id=docmgr_app，密码只写入一次。";
        }

        if (result == 2)
        {
            return $"已修改登录 docmgr_app 的密码，并确认数据库 {catalogName} 中的用户属于 db_owner。请在各客户端连接串中重新写入一次 Password=。";
        }

        return $"登录 docmgr_app 已存在，未修改密码。已确认数据库 {catalogName} 中的用户属于 db_owner。";
    }

    private string RequireLocalInstanceName()
    {
        if (!IsLocalServer)
        {
            throw new InvalidOperationException("这项设置只能在库服务器本机执行。请在装有 SQL Server 的那台电脑上打开本系统。");
        }

        string dataSource = _databaseSettings.DataSource;
        if (dataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前连接的是 LocalDB，不能按局域网库服务器的方式开放 1433 端口。");
        }

        string? instanceName = TryGetInstanceName(dataSource);
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            throw new InvalidOperationException("无法从连接串识别本机 SQL Server 实例名。默认实例请使用 Server=localhost，命名实例请使用 Server=localhost\\实例名。");
        }

        return instanceName;
    }

    private static bool IsTcpAlreadyConfigured(string instanceName)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? names = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");
            if (names?.GetValue(instanceName) is not string instanceId || string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            string tcpPath = $@"SOFTWARE\Microsoft\Microsoft SQL Server\{instanceId}\MSSQLServer\SuperSocketNetLib\Tcp";
            using RegistryKey? tcp = baseKey.OpenSubKey(tcpPath);
            using RegistryKey? ipAll = baseKey.OpenSubKey(tcpPath + @"\IPAll");
            if (tcp == null || ipAll == null)
            {
                return false;
            }

            bool enabled = tcp.GetValue("Enabled") is int enabledValue && enabledValue == 1;
            string port = (ipAll.GetValue("TcpPort") as string ?? string.Empty).Trim();
            string dynamicPort = (ipAll.GetValue("TcpDynamicPorts") as string ?? string.Empty).Trim();
            return enabled && string.Equals(port, "1433", StringComparison.Ordinal) && dynamicPort.Length == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    private static int? TryReadLoginModeFromRegistry(string instanceName)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? names = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");
            if (names?.GetValue(instanceName) is not string instanceId || string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            using RegistryKey? server = baseKey.OpenSubKey($@"SOFTWARE\Microsoft\Microsoft SQL Server\{instanceId}\MSSQLServer");
            object? mode = server?.GetValue("LoginMode");
            if (mode is int number)
            {
                return number;
            }

            return mode is string text && int.TryParse(text, out int parsed) ? parsed : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    private async Task<int?> TryReadLoginModeFromServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(_databaseSettings.BuildMasterConnectionString());
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                DECLARE @mode int;
                EXEC master.dbo.xp_instance_regread
                    N'HKEY_LOCAL_MACHINE',
                    N'Software\Microsoft\MSSQLServer\MSSQLServer',
                    N'LoginMode',
                    @mode OUTPUT;
                SELECT @mode;
                """;
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (value is null || value is DBNull)
            {
                return null;
            }

            return int.TryParse(Convert.ToString(value), out int mode) ? mode : null;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task WaitUntilOnlineAsync(CancellationToken cancellationToken)
    {
        SqlConnection.ClearAllPools();
        Exception? lastError = null;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = new SqlConnection(_databaseSettings.ConnectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            "SQL Server 服务已重启，但 30 秒内仍无法重新连接。"
            + (lastError == null ? string.Empty : Environment.NewLine + lastError.Message));
    }

    private static string BuildTcpScript(string instanceName)
    {
        string serviceName = GetServiceName(instanceName);
        return
            """
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            """
            + Environment.NewLine
            + BuildScriptPreamble()
            + Environment.NewLine
            + $$"""
            try {
                $instanceName = {{PsQuote(instanceName)}}
                $namesKey = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
                $instanceId = (Get-ItemProperty -Path $namesKey -Name $instanceName).$instanceName
                if ([string]::IsNullOrWhiteSpace($instanceId)) { throw "本机注册表中找不到 SQL Server 实例 $instanceName。" }
                $tcpKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer\SuperSocketNetLib\Tcp"
                if (-not (Test-Path -LiteralPath $tcpKey)) { throw "找不到 TCP/IP 协议项。" }
                New-ItemProperty -Path $tcpKey -Name 'Enabled' -Value 1 -PropertyType DWord -Force | Out-Null
                $ipAll = Join-Path $tcpKey 'IPAll'
                if (-not (Test-Path -LiteralPath $ipAll)) { throw "找不到 IPAll 端口项。" }
                New-ItemProperty -Path $ipAll -Name 'TcpPort' -Value '1433' -PropertyType String -Force | Out-Null
                New-ItemProperty -Path $ipAll -Name 'TcpDynamicPorts' -Value '' -PropertyType String -Force | Out-Null
                $serviceName = {{PsQuote(serviceName)}}
                $service = Get-Service -Name $serviceName
                if ($service.Status -eq 'Running') { Restart-Service -Name $serviceName -Force }
                else { Start-Service -Name $serviceName }
                Write-Result 'OK' "已启用实例 $instanceName 的 TCP/IP，IPAll 端口为 1433，动态端口已留空。"
            } catch {
                Write-Result 'FAIL' $_.Exception.Message
                exit 1
            }
            """;
    }

    private static string BuildMixedModeScript(string instanceName)
    {
        string serviceName = GetServiceName(instanceName);
        return
            """
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            """
            + Environment.NewLine
            + BuildScriptPreamble()
            + Environment.NewLine
            + $$"""
            try {
                $instanceName = {{PsQuote(instanceName)}}
                $namesKey = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
                $instanceId = (Get-ItemProperty -Path $namesKey -Name $instanceName).$instanceName
                if ([string]::IsNullOrWhiteSpace($instanceId)) { throw "本机注册表中找不到 SQL Server 实例 $instanceName。" }
                $serverKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer"
                New-ItemProperty -Path $serverKey -Name 'LoginMode' -Value 2 -PropertyType DWord -Force | Out-Null
                $serviceName = {{PsQuote(serviceName)}}
                $service = Get-Service -Name $serviceName
                if ($service.Status -eq 'Running') { Restart-Service -Name $serviceName -Force }
                else { Start-Service -Name $serviceName }
                Write-Result 'OK' '已改为混合验证并重启 SQL Server 服务。'
            } catch {
                Write-Result 'FAIL' $_.Exception.Message
                exit 1
            }
            """;
    }

    private static string BuildFirewallScript()
    {
        return
            """
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            """
            + Environment.NewLine
            + BuildScriptPreamble()
            + Environment.NewLine
            + $$"""
            try {
                $displayName = {{PsQuote(FirewallRuleName)}}
                $existing = Get-NetFirewallRule -DisplayName $displayName -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($null -eq $existing) {
                    New-NetFirewallRule -DisplayName $displayName -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow | Out-Null
                    Write-Result 'OK' '已添加入站规则「SQL Server 1433」，放行 TCP 1433。'
                } elseif ($existing.Enabled -ne 'True') {
                    Enable-NetFirewallRule -Name $existing.Name
                    Write-Result 'OK' '防火墙规则「SQL Server 1433」已存在但被禁用，现已重新启用。'
                } else {
                    Write-Result 'OK' '防火墙规则「SQL Server 1433」已存在，未重复添加。'
                }
            } catch {
                Write-Result 'FAIL' $_.Exception.Message
                exit 1
            }
            """;
    }

    private static string BuildScriptPreamble()
    {
        return
            """
            function Write-Result([string]$status, [string]$message) {
                Set-Content -LiteralPath $resultPath -Value ($status + "`r`n" + $message) -Encoding UTF8
            }
            """;
    }

    private static async Task<string> RunElevatedAsync(string scriptBody, CancellationToken cancellationToken)
    {
        string directory = Path.Combine(Path.GetTempPath(), "docmgr-sql-setup");
        Directory.CreateDirectory(directory);
        string id = Guid.NewGuid().ToString("N");
        string scriptPath = Path.Combine(directory, id + ".ps1");
        string resultPath = Path.Combine(directory, id + ".txt");
        string script = "$resultPath = " + PsQuote(resultPath) + Environment.NewLine + scriptBody;

        try
        {
            await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken)
                .ConfigureAwait(false);
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolvePowerShellPath(),
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动管理员权限进程。");
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (!File.Exists(resultPath))
            {
                throw new InvalidOperationException("管理员操作没有返回结果。若已在授权窗口点了否，请重新执行并选择是。");
            }

            string text = await File.ReadAllTextAsync(resultPath, cancellationToken).ConfigureAwait(false);
            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', 2);
            string status = lines[0].Trim();
            string detail = lines.Length > 1 ? lines[1].Trim() : string.Empty;
            if (!string.Equals(status, "OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? "管理员操作失败。" : detail);
            }

            return string.IsNullOrWhiteSpace(detail) ? "已完成。" : detail;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException("已取消管理员授权。");
        }
        finally
        {
            TryDelete(scriptPath);
            TryDelete(resultPath);
        }
    }

    private static string ResolvePowerShellPath()
    {
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
        {
            string sysnative = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Sysnative",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            if (File.Exists(sysnative))
            {
                return sysnative;
            }
        }

        string system = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        return File.Exists(system) ? system : "powershell.exe";
    }

    private static string GetServiceName(string instanceName)
    {
        return string.Equals(instanceName, "MSSQLSERVER", StringComparison.OrdinalIgnoreCase)
            ? "MSSQLSERVER"
            : "MSSQL$" + instanceName;
    }

    private static string PsQuote(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static bool IsLocalDataSource(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return true;
        }

        string host = ExtractHost(dataSource);
        if (host.StartsWith("(local)", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, ".", StringComparison.Ordinal)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            return string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string ExtractHost(string dataSource)
    {
        string normalized = dataSource.Trim();
        int comma = normalized.IndexOf(',');
        if (comma >= 0)
        {
            normalized = normalized[..comma];
        }

        int slash = normalized.IndexOf('\\');
        if (slash >= 0)
        {
            normalized = normalized[..slash];
        }

        return normalized.Trim();
    }

    private static string? TryGetInstanceName(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource)
            || dataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string normalized = dataSource.Trim();
        int comma = normalized.IndexOf(',');
        if (comma >= 0)
        {
            normalized = normalized[..comma].Trim();
        }

        int slash = normalized.IndexOf('\\');
        string instanceName = slash < 0
            ? "MSSQLSERVER"
            : normalized[(slash + 1)..].Trim();
        if (instanceName.Length == 0 || instanceName.Length > 16)
        {
            return null;
        }

        foreach (char character in instanceName)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return null;
            }
        }

        return instanceName;
    }

    private static string QuoteIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("数据库名无效。");
        }

        return "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }
}
