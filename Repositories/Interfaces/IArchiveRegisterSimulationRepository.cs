using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 仿真测试数据访问契约：登记/立档模拟数据的读写。
/// </summary>
public interface IArchiveRegisterSimulationRepository
{
    Task<List<ProjectInfo>> GetProjectsAsync();

    Task<IDbContextTransaction> BeginTransactionAsync();

    void AddHardDiskMedium(HardDiskMedium medium);

    void AddHardDiskMediaApplication(HardDiskMediaApplication application);

    void AddHardDiskMediaTransaction(HardDiskMediaTransaction transaction);

    Task<int> SaveChangesAsync();

    Task<List<YearlyArchiveRegisterRecord>> GetSimulatedRegisterRecordsAsync(string simulationMarker, string applicantLoginName, string legacyMaterialPrefix);

    Task<List<HardDiskMedium>> GetSimulatedHardDiskMediaAsync(string marker);

    Task<List<HardDiskMediaApplication>> GetSimulatedHardDiskApplicationsAsync(string marker);

    Task<List<HardDiskMediaTransaction>> GetSimulatedHardDiskTransactionsAsync(string marker);

    Task<List<YearlyArchiveRegisterRecord>> GetSubmittedRegisterRecordsAsync();

    void RemoveHardDiskMediaRange(IEnumerable<HardDiskMedium> media);

    void RemoveHardDiskMediaApplicationsRange(IEnumerable<HardDiskMediaApplication> applications);

    void RemoveHardDiskMediaTransactionsRange(IEnumerable<HardDiskMediaTransaction> transactions);

    Task<User?> GetUserByLoginAsync(string loginName);

}
