using DocMgr.ViewModels;
using DocMgr.ViewModels.Cabinets;
using DocMgr.ViewModels.HardDiskMedia;
using DocMgr.ViewModels.HistoryArchive;
using DocMgr.ViewModels.Projects;
using DocMgr.ViewModels.SystemSettings;
using DocMgr.ViewModels.YearlyArchive;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Config.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<CabinetLayoutViewModel>();
        services.AddTransient<CabinetSearchViewModel>();
        services.AddTransient<HardDiskMediumLedgerViewModel>();
        services.AddTransient<OpticalDiscMediumLedgerViewModel>();
        services.AddTransient<HardDiskMediaOutboundApplicationPageViewModel>();
        services.AddTransient<Func<HardDiskReturnWorkspaceMode, HardDiskMediaReturnRegistrationPageViewModel>>(sp =>
            mode => ActivatorUtilities.CreateInstance<HardDiskMediaReturnRegistrationPageViewModel>(sp, mode));
        services.AddTransient<HardDiskMediaApprovalPageViewModel>();
        services.AddTransient<HardDiskMediaTransactionPageViewModel>();
        services.AddTransient<HardDiskMediaPageViewModel>();
        services.AddTransient<AerialPhotoViewModel>();
        services.AddTransient<OtherMapViewModel>();
        services.AddTransient<TopoMapViewModel>();
        services.AddTransient<DeptSettingViewModel>();
        services.AddTransient<RoleSettingViewModel>();
        services.AddTransient<TestPreparationPageViewModel>();
        services.AddTransient<ArchiveFilingViewModel>();
        services.AddTransient<ArchiveFilingLedgerViewModel>();
        services.AddTransient<ArchiveRelocationLedgerViewModel>();
        services.AddTransient<ArchiveCirculationLedgerViewModel>();
        services.AddTransient<ArchiveSimulatedRelocationViewModel>();
        services.AddTransient<Func<ArchiveReturnWorkspaceMode, ArchiveReturnWorkbenchViewModel>>(sp =>
            mode => ActivatorUtilities.CreateInstance<ArchiveReturnWorkbenchViewModel>(sp, mode));
        services.AddTransient<ArchiveElectronicRelocationViewModel>();
        services.AddTransient<ProjectSettingViewModel>();
        services.AddTransient<ArchiveSearchViewModel>();
        services.AddTransient<ArchiveDetailViewModel>();
        services.AddTransient<ArchiveRegisterViewModel>();
        services.AddTransient<Func<ArchiveRegisterWorkspaceMode, int, ArchiveRegisterWorkbenchPageViewModel>>(sp =>
            (mode, initialRecordId) => ActivatorUtilities.CreateInstance<ArchiveRegisterWorkbenchPageViewModel>(
                sp, mode, initialRecordId));
        services.AddTransient<ArchiveOutboundViewModel>();
        services.AddTransient<Func<ArchiveOutboundWorkspaceMode, int, ArchiveOutboundWorkbenchPageViewModel>>(sp =>
            (mode, initialRecordId) => ActivatorUtilities.CreateInstance<ArchiveOutboundWorkbenchPageViewModel>(
                sp, mode, initialRecordId));
        services.AddTransient<ArchiveRegisterSimulationViewModel>();
        services.AddTransient<AdvancedDataPageViewModel>();
        services.AddTransient<LoginWindowViewModel>();
        services.AddTransient<UserPreferenceViewModel>();
        services.AddTransient<BusinessLogicSettingsViewModel>();
        services.AddTransient<DbOperationLogPageViewModel>();
        return services;
    }
}
