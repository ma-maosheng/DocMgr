using DocMgr.Config;
using Microsoft.Data.SqlClient;

namespace DocMgr.Data
{
    /// <summary>
    /// 应用数据库连接配置（供日志写入、备份还原等独立连接使用）。
    /// </summary>
    public sealed class DocMgrDatabaseSettings
    {
        public DocMgrDatabaseSettings(DocMgrDatabaseOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            ConnectionString = options.ConnectionString;
            var builder = new SqlConnectionStringBuilder(ConnectionString);
            DataSource = builder.DataSource;
            InitialCatalog = builder.InitialCatalog;
        }

        public string ConnectionString { get; }

        /// <summary>服务器/实例（如 localhost）。</summary>
        public string DataSource { get; }

        /// <summary>业务库名（如 DocMgr）。</summary>
        public string InitialCatalog { get; }

        /// <summary>界面展示用：服务器 / 库名。</summary>
        public string TargetDisplay => $"{DataSource} / {InitialCatalog}";

        /// <summary>
        /// 指向 master 的连接串（备份/还原切换数据库模式时使用）。
        /// </summary>
        public string BuildMasterConnectionString()
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            };
            return builder.ConnectionString;
        }
    }
}
