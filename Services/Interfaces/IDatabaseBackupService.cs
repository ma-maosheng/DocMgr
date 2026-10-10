using System.Threading;
using System.Threading.Tasks;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 当前 SQL Server 业务库的备份与还原（BACKUP / RESTORE DATABASE）。
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>当前库目标展示文本（服务器 / 库名）。</summary>
    string DatabasePath { get; }

    /// <summary>是否为远程服务器（非本机）。</summary>
    bool IsNetworkPath { get; }

    /// <summary>将当前库完整备份到指定 <c>.bak</c> 文件。</summary>
    Task BackupToFileAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>用指定备份文件覆盖当前库。还原后应立即重启程序。</summary>
    Task RestoreFromFileAsync(string sourcePath, CancellationToken cancellationToken = default);
}
