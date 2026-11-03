using System.Threading;
using System.Threading.Tasks;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 在库服务器上完成局域网接入所需的 SQL Server 设置：TCP/IP、防火墙、专用登录。
/// </summary>
public interface ISqlServerLanSetupService
{
    /// <summary>当前连接是否指向本机 SQL Server。</summary>
    bool IsLocalServer { get; }

    /// <summary>当前实例说明，供界面展示。</summary>
    string InstanceDescription { get; }

    /// <summary>
    /// 启用本机实例 TCP/IP，将 IPAll 固定为 1433 并清空动态端口。配置有变化时重启 SQL Server 服务。
    /// </summary>
    Task<string> EnableTcpAsync(CancellationToken cancellationToken = default);

    /// <summary>为本机入站 TCP 1433 添加或启用防火墙规则「SQL Server 1433」。</summary>
    Task<string> AllowFirewallAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取当前实例的登录模式。1 表示仅 Windows 身份验证，2 表示混合验证。读不到时返回 null。
    /// </summary>
    Task<int?> ReadAuthenticationModeAsync(CancellationToken cancellationToken = default);

    /// <summary>将本机实例改为混合验证并重启 SQL Server 服务。</summary>
    Task EnableMixedModeAsync(CancellationToken cancellationToken = default);

    /// <summary>服务器上是否已有登录 docmgr_app。</summary>
    Task<bool> DocMgrAppLoginExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建 SQL 登录 docmgr_app，并确保当前业务库中有对应用户且属于 db_owner。
    /// 登录尚不存在时 <paramref name="password"/> 必填。登录已存在且传入密码时，改为该新密码。
    /// </summary>
    Task<string> EnsureDocMgrAppLoginAsync(string? password, CancellationToken cancellationToken = default);
}
