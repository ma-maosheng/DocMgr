using DocMgr.Infrastructure.Schema;
using DocMgr.Services.Cabinets;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.HistoryArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.NetworkTransfer;
using DocMgr.Services.Projects;
using DocMgr.Services.Shared;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Config.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<IUserContextService, UserContextService>();
        services.AddSingleton<IDbOperationLogContextService, DbOperationLogContextService>();
        services.AddSingleton<IToDoCenterService, ToDoCenterService>();
        services.AddSingleton<IToDoNotificationPresenter, ToDoNotificationPresenter>();

        services.AddScoped<IAdvancedDataService, AdvancedDataService>();
        services.AddScoped<ISchemaDictionaryMaintenanceService, SchemaDictionaryMaintenanceService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IServerPathSettingService, ServerPathSettingService>();
        services.AddScoped<ICabinetService, CabinetService>();
        services.AddScoped<ICabinetArchiveBoxPlacementService, CabinetArchiveBoxPlacementService>();
        services.AddScoped<ICabinetOpenLayoutService, CabinetOpenLayoutService>();
        services.AddScoped<ICabinetArchiveBoxContentService, CabinetArchiveBoxContentService>();
        services.AddScoped<IHardDiskMediaService, HardDiskMediaService>();
        services.AddScoped<ILocalPhysicalDiskHardwareService, LocalPhysicalDiskHardwareService>();
        services.AddScoped<IHardDiskDisposalService, HardDiskDisposalService>();
        services.AddScoped<IHardDiskInventoryRegisterService, HardDiskInventoryRegisterService>();
        services.AddScoped<IArchiveInventoryRegisterService, ArchiveInventoryRegisterService>();
        services.AddScoped<IArchiveDisposalService, ArchiveDisposalService>();
        services.AddScoped<INetworkTransferService, NetworkTransferService>();
        services.AddScoped<HistoryArchiveImportSlotGuard>();
        services.AddScoped<IAerialPhotoService, AerialPhotoService>();
        services.AddScoped<IOtherMapService, OtherMapService>();
        services.AddScoped<ITopoMapService, TopoMapService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IArchiveRegisterService, ArchiveRegisterService>();
        services.AddScoped<IArchiveRegisterWordExportService, ArchiveRegisterWordExportService>();
        services.AddScoped<IArchiveOutboundWordExportService, ArchiveOutboundWordExportService>();
        services.AddScoped<IElectronicMediaContentScanService, ElectronicMediaContentScanService>();
        services.AddScoped<IArchiveFilingService, ArchiveFilingService>();
        services.AddScoped<IStockHardDiskDirectFilingService, StockHardDiskDirectFilingService>();
        services.AddScoped<IStockTextArchiveDirectFilingService, StockTextArchiveDirectFilingService>();
        services.AddScoped<IStockDirectFilingYearProjectCatalog, StockDirectFilingYearProjectCatalog>();
        services.AddScoped<IFilingFactWriter, FilingFactWriter>();
        services.AddScoped<IArchiveFilingSearchService, ArchiveFilingSearchService>();
        services.AddScoped<IArchiveFilingLedgerService, ArchiveFilingLedgerService>();
        services.AddScoped<IArchiveRelocationLedgerService, ArchiveRelocationLedgerService>();
        services.AddScoped<IArchiveCirculationLedgerService, ArchiveCirculationLedgerService>();
        services.AddScoped<IArchiveCrossDomainTransferLedgerService, ArchiveCrossDomainTransferLedgerService>();
        services.AddScoped<IArchiveMaterialTransactionService, ArchiveMaterialTransactionService>();
        services.AddScoped<IArchiveMaterialTransactionWriter, ArchiveMaterialTransactionWriter>();
        services.AddScoped<IArchiveSimulatedBoxSlotSyncService, ArchiveSimulatedBoxSlotSyncService>();
        services.AddScoped<IArchiveElectronicBagSlotSyncService, ArchiveElectronicBagSlotSyncService>();
        services.AddScoped<IArchiveEmptiedContainerLegacyRepairService, ArchiveEmptiedContainerLegacyRepairService>();
        services.AddScoped<IArchiveOutboundPendingReturnContainerService, ArchiveOutboundPendingReturnContainerService>();
        services.AddSingleton<IArchiveFilingSearchPoolSession, ArchiveFilingSearchPoolSession>();
        services.AddScoped<IArchiveRelocationService, ArchiveRelocationService>();
        services.AddScoped<IArchiveOutboundService, ArchiveOutboundService>();
        services.AddScoped<IArchiveReturnService, ArchiveReturnService>();
        services.AddSingleton<IBatchSlotRelocationSession, BatchSlotRelocationSession>();
        services.AddSingleton<IInteractiveItemRelocationSession, InteractiveItemRelocationSession>();
        services.AddSingleton<ICabinetOpenLayoutRefreshNotifier, CabinetOpenLayoutRefreshNotifier>();
        services.AddScoped<IToDoProvider, YearlyArchiveToDoProvider>();
        services.AddScoped<IToDoProvider, HardDiskMediaToDoProvider>();
        services.AddScoped<IToDoProvider, NetworkTransferToDoProvider>();
        services.AddScoped<IToDoService, ToDoAggregationService>();
        services.AddScoped<IUserPreferenceService, UserPreferenceService>();
        services.AddScoped<IBusinessLogicSettingsService, BusinessLogicSettingsService>();
        services.AddScoped<IDbOperationLogService, DbOperationLogService>();
        services.AddScoped<IBusinessNoGenerator, DefaultBusinessNoGenerator>();
        services.AddScoped<IBusinessPolicyProvider, DefaultBusinessPolicyProvider>();
        services.AddScoped<IBusinessRuleService, BusinessRuleService>();
        services.AddTransient<IDialogService, DialogService>();
        return services;
    }
}
