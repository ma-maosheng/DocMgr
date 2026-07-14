using DocMgr.Data;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocMgr.Repositories.YearlyArchive;

public class ArchiveRegisterSimulationRepository : IArchiveRegisterSimulationRepository
{
    private readonly AppDbContext _dbContext;

    public ArchiveRegisterSimulationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<ProjectInfo>> GetProjectsAsync()
    {
        return _dbContext.ProjectInfos
            .AsNoTracking()
            .OrderBy(project => project.ImplementYear)
            .ThenBy(project => project.ProjectName)
            .ToListAsync();
    }

    public Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return _dbContext.Database.BeginTransactionAsync();
    }

    public void AddHardDiskMedium(HardDiskMedium medium)
    {
        ArgumentNullException.ThrowIfNull(medium);
        _dbContext.HardDiskMedia.Add(medium);
    }

    public void AddHardDiskMediaApplication(HardDiskMediaApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _dbContext.HardDiskMediaApplications.Add(application);
    }

    public void AddHardDiskMediaTransaction(HardDiskMediaTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _dbContext.HardDiskMediaTransactions.Add(transaction);
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetSimulatedRegisterRecordsAsync(string simulationMarker, string applicantLoginName, string legacyMaterialPrefix)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Where(record =>
                (!string.IsNullOrWhiteSpace(record.OtherRequests) && EF.Functions.Like(record.OtherRequests, $"%{simulationMarker}%"))
                || (record.ApplicantName == applicantLoginName && record.MaterialName.StartsWith(legacyMaterialPrefix)))
            .OrderByDescending(record => record.CreatedDate)
            .ToListAsync();
    }

    public Task<List<HardDiskMedium>> GetSimulatedHardDiskMediaAsync(string marker)
    {
        return _dbContext.HardDiskMedia
            .Where(item => !string.IsNullOrWhiteSpace(item.Remark) && EF.Functions.Like(item.Remark, $"%{marker}%"))
            .ToListAsync();
    }

    public Task<List<HardDiskMediaApplication>> GetSimulatedHardDiskApplicationsAsync(string marker)
    {
        return _dbContext.HardDiskMediaApplications
            .Where(item => (!string.IsNullOrWhiteSpace(item.Remark) && EF.Functions.Like(item.Remark, $"%{marker}%"))
                || (!string.IsNullOrWhiteSpace(item.RelatedBatch) && EF.Functions.Like(item.RelatedBatch, $"%{marker}%")))
            .ToListAsync();
    }

    public Task<List<HardDiskMediaTransaction>> GetSimulatedHardDiskTransactionsAsync(string marker)
    {
        return _dbContext.HardDiskMediaTransactions
            .Where(item => (!string.IsNullOrWhiteSpace(item.Remark) && EF.Functions.Like(item.Remark, $"%{marker}%"))
                || (!string.IsNullOrWhiteSpace(item.RelatedBatch) && EF.Functions.Like(item.RelatedBatch, $"%{marker}%")))
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetSubmittedRegisterRecordsAsync()
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Where(record => record.Status == YearlyArchiveRegisterRecord.Submitted)
            .OrderBy(record => record.CreatedDate)
            .ToListAsync();
    }

    public void RemoveHardDiskMediaRange(IEnumerable<HardDiskMedium> media)
    {
        ArgumentNullException.ThrowIfNull(media);
        _dbContext.HardDiskMedia.RemoveRange(media);
    }

    public void RemoveHardDiskMediaApplicationsRange(IEnumerable<HardDiskMediaApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);
        _dbContext.HardDiskMediaApplications.RemoveRange(applications);
    }

    public void RemoveHardDiskMediaTransactionsRange(IEnumerable<HardDiskMediaTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        _dbContext.HardDiskMediaTransactions.RemoveRange(transactions);
    }

    public Task<User?> GetUserByLoginAsync(string loginName)
    {
        return _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.LoginName == loginName);
    }

}
