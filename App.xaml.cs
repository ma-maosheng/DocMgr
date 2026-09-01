using DocMgr.Config;
using DocMgr.Config.DependencyInjection;
using DocMgr.Data;
using DocMgr.Data.Sqlite;
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
        /// 应用 EF Core 迁移以建立/升级数据库结构（含视图），随后在后台执行种子与数据同步。
        /// 数据库结构完全由迁移描述，不再在启动期手写建表/补列。
        /// </summary>
        private static void InitializeDatabaseAsync(AppInitializationState initializationState)
        {
            try
            {
                initializationState.ReportProgress("正在连接并升级数据库（首次启动可能较慢）…");

                using var scope = CurrentProvider.CreateScope();
                var databaseSettings = scope.ServiceProvider.GetRequiredService<DocMgrDatabaseSettings>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var databaseOptions = new DocMgrDatabaseOptions(
                    databaseSettings.DbPath,
                    databaseSettings.BusyTimeoutSeconds,
                    databaseSettings.IsNetworkPath);

                try
                {
                    if (File.Exists(databaseSettings.DbPath))
                    {
                        var pendingMigrations = db.Database.GetPendingMigrations().ToList();
                        if (pendingMigrations.Count > 0)
                        {
                            initializationState.ReportProgress(
                                $"检测到 {pendingMigrations.Count} 项数据库升级，正在备份当前库…");
                            var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
                            PreMigrateBackupResult backupResult = backupService.TryCreatePreMigrateBackup();
                            if (!backupResult.Skipped && backupResult.Succeeded)
                            {
                                initializationState.ReportProgress($"升级前备份已写入：{backupResult.Message}");
                            }
                            else if (!backupResult.Succeeded)
                            {
                                initializationState.ReportProgress(backupResult.Message);
                            }
                        }
                    }

                    SqliteNetworkAccessSupport.MigrateWithRetry(db, databaseOptions, initializationState);
                }
                catch (Exception ex) when (SqliteNetworkAccessSupport.IsSqliteLockException(ex))
                {
                    throw SqliteNetworkAccessSupport.CreateSharedDatabaseUnavailableException(ex);
                }

                initializationState.ReportProgress("正在检查系统基础数据…");
                var devSeedRepository = scope.ServiceProvider.GetRequiredService<IDevSystemSettingsSeedRepository>();
                var seedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings", "system-settings.seed.json");
                DevSystemSettingsSeeder.SeedFromExternalFileIfEmpty(devSeedRepository, seedPath);

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
                initializationState.MarkFailed(ex);
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
                System.Diagnostics.Debug.WriteLine(ex.Message);
                initializationState.ReportProgress("后台数据同步未完成，部分功能可能受限。可重启应用后重试。");
            }
        }

        /// <summary>
        /// 登录不依赖的耗时维护：种子数据、历史档案盒同步等，在后台执行。
        /// </summary>
        private static void RunDeferredDatabaseMaintenance(AppInitializationState initializationState)
        {
            using var scope = CurrentProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var devSeedRepository = scope.ServiceProvider.GetRequiredService<IDevSystemSettingsSeedRepository>();
            var cabinetSpecificationSeedRepository = scope.ServiceProvider.GetRequiredService<ICabinetSpecificationSeedRepository>();
            var cabinetArchiveBoxPlacementSyncRepository = scope.ServiceProvider.GetRequiredService<ICabinetArchiveBoxPlacementSyncRepository>();
            var fieldDomainSeedRepository = scope.ServiceProvider.GetRequiredService<IFieldDomainSeedRepository>();

            initializationState.ReportProgress("正在同步基础数据…");
            var seedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings", "system-settings.seed.json");
            DevSystemSettingsSeeder.SeedFromExternalFile(devSeedRepository, seedPath);
            CabinetSpecificationSeedService.SeedDefaults(cabinetSpecificationSeedRepository);

            initializationState.ReportProgress("正在整理档案柜数据…");
            NormalizeCabinetNameStorage(db);

            initializationState.ReportProgress("正在归一化硬盘申请单状态…");
            NormalizeHardDiskApplicationStatusStorage(db);

            initializationState.ReportProgress("正在归一化硬盘台账状态文案…");
            NormalizeHardDiskLedgerStatusStorage(db);

            initializationState.ReportProgress("正在归一化资料归还单状态…");
            NormalizeYearlyArchiveReturnStatusStorage(db);

            initializationState.ReportProgress("正在补全防磁磁盘柜未配置档口用途…");
            scope.ServiceProvider.GetRequiredService<ICabinetService>()
                .EnsureAllMagneticDiskSlotsUseBlankCategoryOnStartup();

            initializationState.ReportProgress("正在补全标准滑道式档案柜未配置档口用途…");
            scope.ServiceProvider.GetRequiredService<ICabinetService>()
                .EnsureAllStandardArchiveSlotsUseUnsetCategoryOnStartup();

            initializationState.ReportProgress("正在同步历史档案盒位置…");
            CabinetArchiveBoxPlacementSyncService.SyncHistoryArchivePlacements(cabinetArchiveBoxPlacementSyncRepository);

            initializationState.ReportProgress("正在同步字段字典…");
            FieldDomainSeedService.SeedDefaults(fieldDomainSeedRepository);

            initializationState.ReportProgress("正在回填立档事实台账…");
            var filingFactRepository = scope.ServiceProvider.GetRequiredService<IArchiveFilingFactRepository>();
            filingFactRepository.BackfillFromExistingLinksAsync().GetAwaiter().GetResult();

            initializationState.ReportProgress("正在纠偏空盒/空袋残留在库状态…");
            int repairedEmptyContainers = scope.ServiceProvider
                .GetRequiredService<IArchiveEmptiedContainerLegacyRepairService>()
                .RepairAsync()
                .GetAwaiter()
                .GetResult();
            if (repairedEmptyContainers > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"空盒/空袋历史纠偏：已对齐 {repairedEmptyContainers} 条立档事实生命周期。");
            }

            var outboundService = scope.ServiceProvider.GetRequiredService<IArchiveOutboundService>();
            int voidedCount = outboundService.ProcessOverdueAutoForceVoidAsync(DateTime.Now).GetAwaiter().GetResult();
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

        /// <summary>
        /// 归一化数据库中既有的柜号存储（去除遗留的非标准写法），保证档口比较一致。
        /// </summary>
        private static void NormalizeCabinetNameStorage(AppDbContext db)
        {
            ArgumentNullException.ThrowIfNull(db);

            bool changed = false;

            foreach (var cabinet in db.Cabinets)
            {
                string normalizedName = CabinetNameNormalizer.Normalize(cabinet.Name);
                if (!string.Equals(cabinet.Name, normalizedName, StringComparison.Ordinal))
                {
                    cabinet.Name = normalizedName;
                    changed = true;
                }
            }

            foreach (var rule in db.CabinetSlotSpecialRules)
            {
                string normalizedName = CabinetNameNormalizer.Normalize(rule.CabinetName);
                if (!string.Equals(rule.CabinetName, normalizedName, StringComparison.Ordinal))
                {
                    rule.CabinetName = normalizedName;
                    changed = true;
                }
            }

            foreach (var archiveBox in db.YearlyArchiveBoxes)
            {
                string normalizedName = CabinetNameNormalizer.Normalize(archiveBox.CabinetName);
                if (!string.Equals(archiveBox.CabinetName, normalizedName, StringComparison.Ordinal))
                {
                    archiveBox.CabinetName = normalizedName;
                    changed = true;
                }
            }

            foreach (var placement in db.CabinetArchiveBoxPlacements)
            {
                string normalizedName = CabinetNameNormalizer.Normalize(placement.CabinetName);
                if (!string.Equals(placement.CabinetName, normalizedName, StringComparison.Ordinal))
                {
                    placement.CabinetName = normalizedName;
                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }

        /// <summary>
        /// 硬盘申请单状态历史文案 → int 的转换已由 EF Migration（ConvertHardDiskApplicationStatusToInt）
        /// 在库结构升级阶段一次性完成，此处保留空实现以维持启动流程调用点不变。
        /// </summary>
        private static void NormalizeHardDiskApplicationStatusStorage(AppDbContext db)
        {
            ArgumentNullException.ThrowIfNull(db);
        }

        /// <summary>
        /// 硬盘台账/流水状态文案迁移：出库(销毁)→离库(处置)；
        /// 已办结离库处置「盘失」对应台账若仍为出库(挂失)则改为在库(盘失)。
        /// </summary>
        private static void NormalizeHardDiskLedgerStatusStorage(AppDbContext db)
        {
            ArgumentNullException.ThrowIfNull(db);

            bool changed = false;

            foreach (var ledger in db.HardDiskLedgers)
            {
                string normalized = HardDiskMediaStatusNormalizer.Normalize(ledger.MediaStatus);
                if (!string.Equals(ledger.MediaStatus, normalized, StringComparison.Ordinal))
                {
                    ledger.MediaStatus = normalized;
                    changed = true;
                }
            }

            foreach (var transaction in db.HardDiskMediaTransactions)
            {
                string before = HardDiskMediaStatusNormalizer.Normalize(transaction.BeforeStatus);
                string after = HardDiskMediaStatusNormalizer.Normalize(transaction.AfterStatus);
                string type = HardDiskMediaStatusNormalizer.NormalizeTransactionType(transaction.TransactionType);

                if (!string.Equals(transaction.BeforeStatus, before, StringComparison.Ordinal)
                    || !string.Equals(transaction.AfterStatus, after, StringComparison.Ordinal)
                    || !string.Equals(transaction.TransactionType, type, StringComparison.Ordinal))
                {
                    transaction.BeforeStatus = before;
                    transaction.AfterStatus = after;
                    transaction.TransactionType = type;
                    changed = true;
                }
            }

            // SQLite 不支持对导航集合 SelectMany/过滤 产生的 APPLY；改为显式 join 明细表。
            string reasonLost = HardDiskDisposalDomainValues.ReasonLost;
            int completedStatus = HardDiskDisposalRecord.StatusCompleted;
            var disposalLostMediumIds = (
                    from item in db.HardDiskDisposalItems
                    join record in db.HardDiskDisposalRecords on item.DisposalRecordId equals record.Id
                    where record.Status == completedStatus
                        && (item.DisposalReason == reasonLost
                            || (string.IsNullOrEmpty(item.DisposalReason) && record.DisposalReason == reasonLost))
                    select item.MediumId)
                .Distinct()
                .ToList();

            if (disposalLostMediumIds.Count > 0)
            {
                var lostLedgers = db.HardDiskLedgers
                    .Where(ledger => disposalLostMediumIds.Contains(ledger.MediumId))
                    .ToList();

                foreach (var ledger in lostLedgers)
                {
                    string current = HardDiskMediaStatusNormalizer.Normalize(ledger.MediaStatus);
                    if (string.Equals(current, HardDiskMedium.StatusOutLost, StringComparison.Ordinal)
                        || string.Equals(current, HardDiskMediaStatusNormalizer.LegacyStatusOutDestroyed, StringComparison.Ordinal))
                    {
                        ledger.MediaStatus = HardDiskMedium.StatusInStockLost;
                        changed = true;
                    }
                }

                var disposalNos = (
                        from item in db.HardDiskDisposalItems
                        join record in db.HardDiskDisposalRecords on item.DisposalRecordId equals record.Id
                        where record.Status == completedStatus
                            && (item.DisposalReason == reasonLost
                                || (string.IsNullOrEmpty(item.DisposalReason) && record.DisposalReason == reasonLost))
                        select record.DisposalNo)
                    .Distinct()
                    .ToList();

                var relatedTransactions = db.HardDiskMediaTransactions
                    .Where(item => disposalNos.Contains(item.RelatedBatch)
                        && disposalLostMediumIds.Contains(item.MediumId))
                    .ToList();

                foreach (var transaction in relatedTransactions)
                {
                    string after = HardDiskMediaStatusNormalizer.Normalize(transaction.AfterStatus);
                    if (string.Equals(after, HardDiskMedium.StatusOutLost, StringComparison.Ordinal)
                        || string.Equals(after, HardDiskMedium.StatusDisposed, StringComparison.Ordinal)
                        || string.Equals(after, HardDiskMediaStatusNormalizer.LegacyStatusOutDestroyed, StringComparison.Ordinal))
                    {
                        transaction.AfterStatus = HardDiskMedium.StatusInStockLost;
                        changed = true;
                    }

                    string type = HardDiskMediaStatusNormalizer.NormalizeTransactionType(transaction.TransactionType);
                    if (string.Equals(type, HardDiskMediaTransaction.TypeLossRegistration, StringComparison.Ordinal)
                        || string.Equals(type, HardDiskMediaTransaction.TypeDisposal, StringComparison.Ordinal)
                        || string.Equals(type, HardDiskMediaStatusNormalizer.LegacyStatusOutDestroyed, StringComparison.Ordinal))
                    {
                        transaction.TransactionType = HardDiskMediaTransaction.TypeInventoryLost;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }

        /// <summary>
        /// 资料归还旧 4 态（0草稿/1已登记/2已办结/3已作废）迁移为统一 7 态。
        /// 仅当 CompletedAt/VoidedAt 表明仍为旧语义时改写，避免与新「已审批」态冲突。
        /// </summary>
        private static void NormalizeYearlyArchiveReturnStatusStorage(AppDbContext db)
        {
            ArgumentNullException.ThrowIfNull(db);

            bool changed = false;

            foreach (var record in db.YearlyArchiveReturnRecords)
            {
                int original = record.Status;
                int normalized = original;

                // 旧 Completed=2 且已办结时间存在 → 新 Completed=4
                if (original == 2 && record.CompletedAt.HasValue)
                {
                    normalized = ApplicationWorkflowStatus.Completed;
                }
                // 旧 Voided=3 → 新 Withdrawn=5
                else if (original == 3)
                {
                    normalized = ApplicationWorkflowStatus.Withdrawn;
                }

                if (normalized != original)
                {
                    record.Status = normalized;
                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
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
    }
}
