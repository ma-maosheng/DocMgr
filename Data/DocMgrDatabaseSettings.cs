using DocMgr.Config;

namespace DocMgr.Data
{
    /// <summary>
    /// 应用数据库连接配置（供日志写入等独立 DbContext 使用）。
    /// </summary>
    public sealed class DocMgrDatabaseSettings
    {
        public DocMgrDatabaseSettings(DocMgrDatabaseOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            DbPath = options.DatabasePath;
            BusyTimeoutSeconds = options.BusyTimeoutSeconds;
            IsNetworkPath = options.IsNetworkPath;
            ConnectionString = Sqlite.SqliteNetworkAccessSupport.BuildConnectionString(options);
        }

        public string DbPath { get; }

        public string ConnectionString { get; }

        public int BusyTimeoutSeconds { get; }

        public bool IsNetworkPath { get; }
    }
}
