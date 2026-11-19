using System.IO;
using System.Text;

namespace DocMgr.Infrastructure;

/// <summary>
/// 本机运行日志。写在当前用户的本地应用数据目录，单文件超过 2MB 时保留一份备份后重新开始。
/// </summary>
internal static class AppRuntimeLog
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Sync = new();

    /// <summary>日志文件路径：%LocalAppData%\DocMgr\logs\docmgr.log。</summary>
    public static string LogFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DocMgr",
        "logs",
        "docmgr.log");

    /// <summary>
    /// 追加一行运行记录。写失败时不影响业务。
    /// </summary>
    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            string? directory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.Append(' ');
            builder.Append(message);
            if (exception != null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            builder.AppendLine();
            string line = builder.ToString();

            lock (Sync)
            {
                var info = new FileInfo(LogFilePath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Copy(LogFilePath, LogFilePath + ".1", overwrite: true);
                    File.WriteAllText(LogFilePath, string.Empty, Encoding.UTF8);
                }

                File.AppendAllText(LogFilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败不能反过来影响业务操作。
        }
    }
}
