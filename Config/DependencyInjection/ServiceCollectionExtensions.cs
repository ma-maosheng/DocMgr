using DocMgr.Config;
using DocMgr.Data;
using DocMgr.Data.Interceptors;
using DocMgr.Data.Sqlite;
using DocMgr.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Config.DependencyInjection;

/// <summary>
/// DocMgr 应用服务注册入口。
/// 通过拆分 partial 文件，将仓储、领域服务和 ViewModel 注册分层组织，便于维护。
/// </summary>
public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocMgrCore(this IServiceCollection services, DocMgrDatabaseOptions databaseOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(databaseOptions);

        var databaseSettings = new DocMgrDatabaseSettings(databaseOptions);
        string connectionString = databaseSettings.ConnectionString;

        services.AddSingleton(databaseSettings);
        services.AddSingleton<AppInitializationState>();
        services.AddScoped<DbOperationLogWriter>();
        services.AddScoped<DbOperationLogInterceptor>();
        services.AddSingleton<SqliteConnectionPragmaInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                serviceProvider.GetRequiredService<DbOperationLogInterceptor>(),
                serviceProvider.GetRequiredService<SqliteConnectionPragmaInterceptor>());
        });

        services.AddRepositories();
        services.AddDomainServices();
        services.AddViewModels();
        services.AddTransient<Func<string, DocMgr.ViewModels.YearlyArchive.ArchiveFilingSearchViewModel>>(sp =>
            mediaKind => ActivatorUtilities.CreateInstance<DocMgr.ViewModels.YearlyArchive.ArchiveFilingSearchViewModel>(
                sp, mediaKind));
        services.AddTransient<Func<string, DocMgr.ViewModels.YearlyArchive.ArchiveFilingSearchPoolViewModel>>(sp =>
            mediaKind => ActivatorUtilities.CreateInstance<DocMgr.ViewModels.YearlyArchive.ArchiveFilingSearchPoolViewModel>(
                sp, mediaKind));
        return services;
    }
}
