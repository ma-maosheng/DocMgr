using DocMgr.Config;
using DocMgr.Config.DependencyInjection;
using DocMgr.Data;
using DocMgr.Infrastructure.DbOperationLog;
using DocMgr.Infrastructure.Seeding;
using DocMgr.Infrastructure.Startup;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services;
using DocMgr.Services.Interfaces;
using DocMgr.Views;
using DocMgr.Views.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace DocMgr
{
    public partial class App : Application
    {
        public static ServiceProvider CurrentProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // #region agent log
            DispatcherUnhandledException += (_, args) =>
            {
                DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.WriteException(
                    "E",
                    "App.DispatcherUnhandledException",
                    "ui unhandled — preventing silent exit",
                    args.Exception);
                try
                {
                    MessageBox.Show(
                        "未处理异常（已写入调试日志，程序暂不退出）：\n\n"
                        + args.Exception.GetType().FullName + "\n"
                        + args.Exception.GetBaseException().Message,
                        "调试捕获",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // ignore
                }

                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.WriteException(
                        "E",
                        "App.UnhandledException",
                        "domain unhandled isTerminating=" + args.IsTerminating,
                        ex);
                }
            };
            DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.Write(
                "E",
                "App.OnStartup",
                "startup begin post-fix",
                new { logPath = DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.PrimaryLogPath, runId = "post-fix" });
            // #endregion

            DocMgrWindowBranding.Register();

#if DEBUG
            DocMgr.Infrastructure.DebugUi.UiDebugIdBadgeSupport.Register();
#endif

            ScrollViewerWheelRoutingSupport.Register();

            DocMgrDatabaseOptions databaseOptions;
            try
            {
                databaseOptions = DocMgrDatabaseConfiguration.Load();
            }
            catch (Exception ex)
            {
                // #region agent log
                DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.WriteException(
                    "E",
                    "App.OnStartup",
                    "database config failed",
                    ex);
                // #endregion
                MessageBox.Show(
                    ex.Message,
                    "数据库配置错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            if (!string.IsNullOrWhiteSpace(databaseOptions.PasswordProtectionWarning))
            {
                MessageBox.Show(
                    databaseOptions.PasswordProtectionWarning,
                    "数据库密码仍为明文",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            IServiceCollection services = new ServiceCollection();
            ConfigureServices(services, databaseOptions);

            CurrentProvider = BuildServiceProvider(services);

            // #region agent log
            AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
            {
                try
                {
                    Exception ex = args.Exception;
                    string text = ex.ToString();
                    if (text.Contains("ArchiveFiling", StringComparison.Ordinal)
                        || text.Contains("HandleSelectedRecords", StringComparison.Ordinal)
                        || text.Contains("CalculateBoxIndex", StringComparison.Ordinal)
                        || text.Contains("RebuildSimulated", StringComparison.Ordinal)
                        || text.Contains("InvalidOperationException", StringComparison.Ordinal)
                        || text.Contains("NullReferenceException", StringComparison.Ordinal))
                    {
                        DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.WriteException(
                            "E",
                            "App.FirstChanceException",
                            "first-chance",
                            ex);
                    }
                }
                catch
                {
                    // ignore
                }
            };
            DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.Write(
                "E",
                "App.OnStartup",
                "provider ready, showing login");
            // #endregion

            var logContextService = CurrentProvider.GetRequiredService<IDbOperationLogContextService>();
            DbOperationLogUiCapture.Register(logContextService);

            var loginWindow = new LoginWindow();
            loginWindow.Show();

            var initializationState = CurrentProvider.GetRequiredService<AppInitializationState>();
            _ = Task.Run(() => InitializeDatabaseAsync(initializationState));
        }

        /// <summary>
        /// 应用 EF Core 迁移以建立数据库结构（含视图），随后在后台执行种子与数据同步。
        /// 开发期数据库结构改造一律删库重建，此处不处理存量数据的升级兼容。
        /// </summary>
        private static void InitializeDatabaseAsync(AppInitializationState initializationState)
        {
            try
            {
                initializationState.ReportProgress("正在连接并初始化数据库（首次启动可能较慢）…");

                using var scope = CurrentProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();

                initializationState.ReportProgress("正在检查系统基础数据…");
                var devSeedRepository = scope.ServiceProvider.GetRequiredService<IDevSystemSettingsSeedRepository>();
                DevSystemSettingsSeeder.SeedIfEmpty(devSeedRepository);

                initializationState.ReportProgress("正在检查默认管理员账号…");
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                DefaultAdministratorBootstrap.EnsureIfEmpty(userRepository);

                initializationState.MarkLoginReady();
                _ = Task.Run(() => RunDeferredDatabaseMaintenanceSafe(initializationState));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.WriteException(
                    "E",
                    "App.InitializeDatabaseAsync",
                    "database migrate failed",
                    ex);
                initializationState.MarkFailed(
                    WrapDatabaseInitializationFailure(ex));
            }
        }

        /// <summary>
        /// 后台执行登录后不阻塞的耗时维护：种子数据、字段字典、台账回填等。
        /// </summary>
        private static void RunDeferredDatabaseMaintenanceSafe(AppInitializationState initializationState)
        {
            try
            {
                RunDeferredDatabaseMaintenance(initializationState);
                initializationState.MarkFullyReady();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                DocMgr.Infrastructure.AgentDebugLogging.AgentDebugSessionLog.WriteException(
                    "E",
                    "App.RunDeferredDatabaseMaintenanceSafe",
                    "deferred maintenance failed",
                    ex);

                string detail = ex.GetBaseException().Message;
                if (string.IsNullOrWhiteSpace(detail))
                {
                    detail = ex.Message;
                }

                initializationState.ReportProgress(
                    "后台数据同步未完成，部分功能可能受限。可重启应用后重试。"
                    + Environment.NewLine
                    + detail);
            }
        }

        /// <summary>
        /// 登录不依赖的耗时维护：种子数据等，在后台执行。
        /// </summary>
        private static void RunDeferredDatabaseMaintenance(AppInitializationState initializationState)
        {
            using var scope = CurrentProvider.CreateScope();
            var devSeedRepository = scope.ServiceProvider.GetRequiredService<IDevSystemSettingsSeedRepository>();
            var cabinetSpecificationSeedRepository = scope.ServiceProvider.GetRequiredService<ICabinetSpecificationSeedRepository>();
            var fieldDomainSeedRepository = scope.ServiceProvider.GetRequiredService<IFieldDomainSeedRepository>();

            initializationState.ReportProgress("正在同步基础数据…");
            DevSystemSettingsSeeder.SeedSyncForDebug(devSeedRepository);
            CabinetSpecificationSeedService.SeedDefaults(cabinetSpecificationSeedRepository);

            initializationState.ReportProgress("正在补全防磁磁盘柜未配置档口用途…");
            scope.ServiceProvider.GetRequiredService<ICabinetService>()
                .EnsureAllMagneticDiskSlotsUseBlankCategoryOnStartup();

            initializationState.ReportProgress("正在补全标准滑道式档案柜未配置档口用途…");
            scope.ServiceProvider.GetRequiredService<ICabinetService>()
                .EnsureAllStandardArchiveSlotsUseUnsetCategoryOnStartup();

            initializationState.ReportProgress("正在同步字段字典…");
            FieldDomainSeedService.SeedDefaults(fieldDomainSeedRepository);

            var outboundService = scope.ServiceProvider.GetRequiredService<IArchiveOutboundService>();
            // 本方法已在 Task.Run 线程池执行；ConfigureAwait(false) 避免误捕获同步上下文。
            int voidedCount = outboundService
                .ProcessOverdueAutoForceVoidAsync(DateTime.Now)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            if (voidedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"资料借出：已自动强制作废 {voidedCount} 条逾期未审批申请。");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (CurrentProvider != null)
            {
                var userContextService = CurrentProvider.GetService<IUserContextService>();
                string? sessionId = userContextService?.CurrentSessionId;
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    using var scope = CurrentProvider.CreateScope();
                    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                    userService.Logout(sessionId);
                    userContextService?.Clear();
                }
            }

            CurrentProvider?.Dispose();
            base.OnExit(e);
        }

        private static ServiceProvider BuildServiceProvider(IServiceCollection services)
        {
#if DEBUG
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
#else
            return services.BuildServiceProvider();
#endif
        }

        private static void ConfigureServices(IServiceCollection services, DocMgrDatabaseOptions databaseOptions)
        {
            services.AddDocMgrCore(databaseOptions);
        }

        /// <summary>
        /// 把「系统找不到指定的文件」等含混错误包装成可操作的部署提示。
        /// </summary>
        private static Exception WrapDatabaseInitializationFailure(Exception exception)
        {
            string detail = exception.GetBaseException().Message;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = exception.Message;
            }

            bool looksLikeMissingFile =
                detail.Contains("找不到指定的文件", StringComparison.Ordinal)
                || detail.Contains("cannot find the file", StringComparison.OrdinalIgnoreCase)
                || exception is FileNotFoundException
                || exception.GetBaseException() is FileNotFoundException;

            if (!looksLikeMissingFile)
            {
                return exception;
            }

            string hint =
                "常见原因：" + Environment.NewLine
                + "1）程序目录缺少正式配置 appsettings.json（仅改 appsettings.lan.example.json 无效，程序不会读取 example）；" + Environment.NewLine
                + "2）ConnectionString 中的 Server 指向了错误的主机/实例，或本机访问不到该 SQL Server；" + Environment.NewLine
                + "3）请尽量安装到本机磁盘再运行，避免直接从网络共享路径（\\\\服务器\\共享\\…）启动。" + Environment.NewLine
                + Environment.NewLine
                + "原始错误：" + detail;

            return new InvalidOperationException(hint, exception);
        }
    }
}
