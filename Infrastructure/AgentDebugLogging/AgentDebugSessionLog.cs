using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DocMgr.Infrastructure.AgentDebugLogging
{
    /// <summary>
    /// Debug-mode NDJSON logger for session 726c1d. Do not use for production diagnostics.
    /// </summary>
    internal static class AgentDebugSessionLog
    {
        private const string SessionId = "726c1d";
        private static readonly object Sync = new();
        private static readonly string[] LogPaths =
        [
            @"F:\2026\资料室管理系统\DocMgr\debug-726c1d.log",
            Path.Combine(Path.GetTempPath(), "docmgr-debug-726c1d.log")
        ];

        public static string PrimaryLogPath => LogPaths[0];

        public static void Write(string hypothesisId, string location, string message, object? data = null)
        {
            try
            {
                var payload = new
                {
                    sessionId = SessionId,
                    hypothesisId,
                    location,
                    message,
                    data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                string line = JsonSerializer.Serialize(payload) + Environment.NewLine;
                lock (Sync)
                {
                    foreach (string path in LogPaths)
                    {
                        try
                        {
                            File.AppendAllText(path, line, Encoding.UTF8);
                        }
                        catch
                        {
                            // try next path
                        }
                    }
                }
            }
            catch
            {
                // Swallow logging failures so debug probes never affect app behavior.
            }
        }

        public static void WriteException(string hypothesisId, string location, string message, Exception ex)
        {
            Write(hypothesisId, location, message, new
            {
                type = ex.GetType().FullName,
                msg = ex.Message,
                baseType = ex.GetBaseException().GetType().FullName,
                baseMsg = ex.GetBaseException().Message,
                stack = ex.ToString()
            });
        }
    }
}
