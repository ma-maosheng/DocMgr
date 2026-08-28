using System.Threading;
using System.Threading.Tasks;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 当前 SQLite 库的备份与还原。使用 SQLite Backup API，可正确处理 WAL。
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>当前正在使用的数据库文件完整路径。</summary>
    string DatabasePath { get; }

    /// <summary>库文件是否位于网络路径（共享盘）。</summary>
    bool IsNetworkPath { get; }

    /// <summary>将当前库完整备份到指定文件。</summary>
    Task BackupToFileAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>用指定备份文件覆盖当前库内容。还原后应立即重启程序。</summary>
    Task RestoreFromFileAsync(string sourcePath, CancellationToken cancellationToken = default);
}
