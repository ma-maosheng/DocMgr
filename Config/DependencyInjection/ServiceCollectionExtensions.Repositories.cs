using DocMgr.Repositories.Cabinets;
using DocMgr.Repositories.HardDiskMedia;
using DocMgr.Repositories.HistoryArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Repositories.NetworkTransfer;
using DocMgr.Repositories.Projects;
using DocMgr.Repositories.SystemSettings;
using DocMgr.Repositories.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Config.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAdvancedDataRepository, AdvancedDataRepository>();
        services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        services.AddScoped<IBusinessLogicSettingsRepository, BusinessLogicSettingsRepository>();
        services.AddScoped<IFieldDomainSeedRepository, FieldDomainSeedRepository>();
        services.AddScoped<IDevSystemSettingsSeedRepository, DevSystemSettingsSeedRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ICabinetRepository, CabinetRepository>();
        services.AddScoped<ICabinetArchiveBoxPlacementRepository, CabinetArchiveBoxPlacementRepository>();
        services.AddScoped<ICabinetSpecificationSeedRepository, CabinetSpecificationSeedRepository>();
        services.AddScoped<ICabinetArchiveBoxPlacementSyncRepository, CabinetArchiveBoxPlacementSyncRepository>();
        services.AddScoped<ICabinetOpenLayoutRepository, CabinetOpenLayoutRepository>();
        services.AddScoped<ITopoMapRepository, TopoMapRepository>();
        services.AddScoped<IAerialPhotoRepository, AerialPhotoRepository>();
        services.AddScoped<IOtherMapRepository, OtherMapRepository>();
        services.AddScoped<IArchiveRegisterRepository, ArchiveRegisterRepository>();
        services.AddScoped<IArchiveRegisterSimulationRepository, ArchiveRegisterSimulationRepository>();
        services.AddScoped<IArchiveFilingRepository, ArchiveFilingRepository>();
        services.AddScoped<IArchiveFilingFactRepository, ArchiveFilingFactRepository>();
        services.AddScoped<IArchiveRelocationRepository, ArchiveRelocationRepository>();
        services.AddScoped<IArchiveOutboundRepository, ArchiveOutboundRepository>();
        services.AddScoped<IArchiveReturnRepository, ArchiveReturnRepository>();
        services.AddScoped<IArchiveMaterialTransactionRepository, ArchiveMaterialTransactionRepository>();
        services.AddScoped<IHardDiskMediaRepository, HardDiskMediaRepository>();
        services.AddScoped<IHardDiskDisposalRepository, HardDiskDisposalRepository>();
        services.AddScoped<IHardDiskInventoryRegisterRepository, HardDiskInventoryRegisterRepository>();
        services.AddScoped<IArchiveInventoryRegisterRepository, ArchiveInventoryRegisterRepository>();
        services.AddScoped<IArchiveDisposalRepository, ArchiveDisposalRepository>();
        services.AddScoped<INetworkTransferRepository, NetworkTransferRepository>();
        services.AddScoped<IDbOperationLogRepository, DbOperationLogRepository>();
        return services;
    }
}
