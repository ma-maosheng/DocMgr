using System;
using System.IO;
using DocMgr.Config;
using DocMgr.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocMgr.Data
{
    /// <summary>
    /// 为 EF Core 设计时命令提供 <see cref="AppDbContext"/> 实例。
    /// </summary>
    public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        /// <summary>
        /// 创建设计时使用的 <see cref="AppDbContext"/>。
        /// </summary>
        /// <param name="args">设计时命令参数。</param>
        /// <returns>配置完成的 <see cref="AppDbContext"/>。</returns>
        public AppDbContext CreateDbContext(string[] args)
        {
            string basePath = Directory.GetCurrentDirectory();
            var databaseOptions = DocMgrDatabaseConfiguration.Load(basePath);
            var databaseSettings = new DocMgrDatabaseSettings(databaseOptions);

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite(databaseSettings.ConnectionString);
            optionsBuilder.AddInterceptors(new SqliteConnectionPragmaInterceptor(databaseSettings));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
