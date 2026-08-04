using DocMgr.Data;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Services.YearlyArchive;
using DocMgr.Models.YearlyArchive;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.HardDiskMedia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocMgr.Repositories.HardDiskMedia;

public partial class HardDiskMediaRepository : IHardDiskMediaRepository
{
    private readonly AppDbContext _dbContext;

    public HardDiskMediaRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<HardDiskMedium>> GetOverviewMediaAsync()
    {
        return _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted)
            .ToListAsync();
    }

    public Task<List<HardDiskMediaApplication>> GetOverviewApplicationsAsync()
    {
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<HardDiskMediaTransaction>> GetOverviewTransactionsAsync()
    {
        return _dbContext.HardDiskMediaTransactions
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<HardDiskDisposalRecord>> GetOverviewDisposalRecordsAsync()
    {
        return _dbContext.HardDiskDisposalRecords
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<HardDiskInventoryRegisterRecord>> GetOverviewInventoryRegisterRecordsAsync()
    {
        return _dbContext.HardDiskInventoryRegisterRecords
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<HardDiskMedium>> GetSelectableMediaAsync()
    {
        return _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.DiskCode)
            .ToListAsync();
    }

    public async Task<List<HardDiskMediaApplication>> GetCompletedOutboundApplicationsForReturnCandidatesAsync()
    {
        // 已有办结归还/挂失且 SourceApplicationId 指向本出库单的，视为该借出周期已关闭。
        var closedOutboundApplicationIds = await _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Where(item => item.SourceApplicationId != null)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            .Select(item => item.SourceApplicationId!.Value)
            .Distinct()
            .ToListAsync();

        return await _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Include(item => item.Medium)
                .ThenInclude(medium => medium!.Ledger)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                           item.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm)
            .Where(item => !closedOutboundApplicationIds.Contains(item.Id))
            .Where(item => item.Medium != null &&
                           !item.Medium.IsDeleted &&
                           item.Medium.Ledger != null &&
                           item.Medium.Ledger.NeedReturn &&
                           (item.Medium.Ledger.MediaStatus == HardDiskMedium.StatusOutTemporary ||
                            item.Medium.Ledger.MediaStatus == HardDiskMedium.StatusOutLongTerm))
            .OrderByDescending(item => item.ExecutedTime ?? item.UpdatedTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<HardDiskMediaApplication?> GetActiveReturnRegistrationByMediumIdAsync(int mediumId)
    {
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Include(item => item.Medium)
            .Where(item => item.MediumId == mediumId)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            .Where(item => item.ApplicationStatus != HardDiskMediaApplication.StatusCompleted
                           && item.ApplicationStatus != HardDiskMediaApplication.StatusWithdrawn
                           && item.ApplicationStatus != HardDiskMediaApplication.StatusForceWithdrawn)
            .OrderByDescending(item => item.UpdatedTime)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync();
    }

    public Task<HardDiskMediaApplication?> GetActiveReturnRegistrationByMediumIdForUpdateAsync(int mediumId)
    {
        return _dbContext.HardDiskMediaApplications
            .Where(item => item.MediumId == mediumId)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            .Where(item => item.ApplicationStatus != HardDiskMediaApplication.StatusCompleted
                           && item.ApplicationStatus != HardDiskMediaApplication.StatusWithdrawn
                           && item.ApplicationStatus != HardDiskMediaApplication.StatusForceWithdrawn)
            .OrderByDescending(item => item.UpdatedTime)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync();
    }

    public Task<List<int>> GetMediumIdsWithActiveReturnRegistrationAsync()
    {
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            .Where(item => item.ApplicationStatus != HardDiskMediaApplication.StatusCompleted
                           && item.ApplicationStatus != HardDiskMediaApplication.StatusWithdrawn
                           && item.ApplicationStatus != HardDiskMediaApplication.StatusForceWithdrawn)
            .Select(item => item.MediumId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<HashSet<int>> GetMediumIdsWithRegisterLockAsync(IReadOnlyCollection<int> mediumIds)
    {
        if (mediumIds == null || mediumIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var lockedIds = await _dbContext.HardDiskRegisterLocks
            .AsNoTracking()
            .Where(item => mediumIds.Contains(item.MediumId))
            .Select(item => item.MediumId)
            .ToListAsync();

        return lockedIds.ToHashSet();
    }

    public Task<HardDiskMediaApplication?> GetLatestCompletedOutboundApplicationByDiskCodeAsync(string diskCode)
    {
        if (string.IsNullOrWhiteSpace(diskCode))
        {
            return Task.FromResult<HardDiskMediaApplication?>(null);
        }

        string normalizedDiskCode = diskCode.Trim();
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Include(item => item.Medium)
                .ThenInclude(medium => medium!.Ledger)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                           item.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm)
            .Where(item => item.Medium != null &&
                           !item.Medium.IsDeleted &&
                           item.Medium.DiskCode == normalizedDiskCode &&
                           item.Medium.Ledger != null)
            .OrderByDescending(item => item.ExecutedTime ?? item.UpdatedTime)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync();
    }

    public Task<List<HardDiskMedium>> SearchMediaAsync(string? keyword, string? status, string? nature)
    {
        IQueryable<HardDiskMedium> query = _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string trimmedKeyword = keyword.Trim();
            query = query.Where(item =>
                item.DiskCode.Contains(trimmedKeyword) ||
                item.SerialNumber.Contains(trimmedKeyword) ||
                item.Brand.Contains(trimmedKeyword) ||
                item.RegistrationMethod.Contains(trimmedKeyword) ||
                ((item.Ledger != null) && item.Ledger.StorageLocation.Contains(trimmedKeyword)) ||
                item.RegisterPerson.Contains(trimmedKeyword) ||
                item.Remark.Contains(trimmedKeyword));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Ledger != null && item.Ledger.MediaStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(nature))
        {
            query = query.Where(item => item.Ledger != null && item.Ledger.MediaNature == nature);
        }

        return query
            .OrderByDescending(item => item.RegisterDate)
            .ThenBy(item => item.DiskCode)
            .ToListAsync();
    }

    public Task<List<HardDiskMedium>> GetArchiveFilingCandidateBlankHardDisksAsync(string? keyword)
    {
        IQueryable<HardDiskMedium> query = _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Where(item => !item.IsDeleted)
            .Where(item => item.Ledger != null)
            .Where(item => item.Ledger!.MediaStatus == HardDiskMedium.StatusInStockBlank)
            .Where(item => item.RegisterLock == null);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string trimmedKeyword = keyword.Trim();
            query = query.Where(item =>
                item.DiskCode.Contains(trimmedKeyword) ||
                item.SerialNumber.Contains(trimmedKeyword) ||
                item.Brand.Contains(trimmedKeyword) ||
                item.InterfaceType.Contains(trimmedKeyword) ||
                item.Capacity.Contains(trimmedKeyword) ||
                ((item.Ledger != null) && item.Ledger.StorageLocation.Contains(trimmedKeyword)));
        }

        return query
            .OrderBy(item => item.DiskCode)
            .ToListAsync();
    }

    public Task<List<OpticalDiscMedium>> SearchOpticalDiscMediaAsync(string? keyword, string? status)
    {
        IQueryable<OpticalDiscMedium> query = _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string trimmedKeyword = keyword.Trim();
            query = query.Where(item =>
                item.DiscCode.Contains(trimmedKeyword) ||
                item.DiscType.Contains(trimmedKeyword) ||
                (item.Ledger != null && item.Ledger.StorageLocation.Contains(trimmedKeyword)) ||
                (item.Ledger != null && item.Ledger.HolderOrOrganization.Contains(trimmedKeyword)) ||
                item.SourceType.Contains(trimmedKeyword) ||
                item.SourceRecordKey.Contains(trimmedKeyword) ||
                item.Remarks.Contains(trimmedKeyword));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Ledger != null && item.Ledger.MediaStatus == status);
        }

        return query
            .OrderByDescending(item => item.UpdatedTime)
            .ThenBy(item => item.DiscCode)
            .ToListAsync();
    }

    public Task<List<OpticalDiscMedium>> GetOpticalDiscMediaForExportAsync()
    {
        return _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.DiscCode)
            .ToListAsync();
    }

    public Task<List<OpticalDiscMedium>> GetOpticalDiscOverviewMediaAsync()
    {
        return _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Where(item => !item.IsDeleted)
            .ToListAsync();
    }

    public Task<List<OpticalDiscMediaTransaction>> GetOpticalDiscOverviewTransactionsAsync()
    {
        return _dbContext.OpticalDiscMediaTransactions
            .AsNoTracking()
            .Include(item => item.Medium)
            .Where(item => item.Medium != null && !item.Medium.IsDeleted)
            .ToListAsync();
    }

    public Task<List<OpticalDiscMediumTransactionRecord>> SearchOpticalDiscTransactionsAsync(
        string? discCodeKeyword,
        string? businessNoKeyword,
        int? mediumId = null,
        string? transactionType = null)
    {
        IQueryable<OpticalDiscMediaTransaction> query = _dbContext.OpticalDiscMediaTransactions
            .AsNoTracking()
            .Include(item => item.Medium)
            .Where(item => item.Medium != null && !item.Medium.IsDeleted);

        if (mediumId.HasValue && mediumId.Value > 0)
        {
            query = query.Where(item => item.MediumId == mediumId.Value);
        }

        if (!string.IsNullOrWhiteSpace(discCodeKeyword))
        {
            string trimmedKeyword = discCodeKeyword.Trim();
            query = query.Where(item => item.Medium!.DiscCode.Contains(trimmedKeyword));
        }

        if (!string.IsNullOrWhiteSpace(businessNoKeyword))
        {
            string trimmedBusinessNoKeyword = businessNoKeyword.Trim();
            query = query.Where(item =>
                item.BusinessNo.Contains(trimmedBusinessNoKeyword)
                || item.RelatedBatch.Contains(trimmedBusinessNoKeyword)
                || item.RelatedArchiveTitle.Contains(trimmedBusinessNoKeyword));
        }

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            string trimmedType = transactionType.Trim();
            query = query.Where(item => item.TransactionType == trimmedType);
        }

        return query
            .OrderByDescending(item => item.OperateTime)
            .ThenBy(item => item.Medium!.DiscCode)
            .Select(item => new OpticalDiscMediumTransactionRecord
            {
                Id = item.Id,
                MediumId = item.MediumId,
                DiscCode = item.Medium!.DiscCode,
                TransactionType = item.TransactionType,
                BusinessNo = item.BusinessNo,
                BeforeStatus = item.BeforeStatus,
                AfterStatus = item.AfterStatus,
                BeforeLocation = item.BeforeLocation,
                AfterLocation = item.AfterLocation,
                OperatorName = item.OperatorName,
                OperateTime = item.OperateTime,
                Description = item.Description,
                Remark = item.Remark
            })
            .ToListAsync();
    }

    public Task<List<HardDiskMediaTransaction>> SearchTransactionsAsync(string? keyword, string? transactionType)
    {
        IQueryable<HardDiskMediaTransaction> query = _dbContext.HardDiskMediaTransactions
            .AsNoTracking()
            .Include(item => item.Medium)
            .Include(item => item.Application);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string trimmedKeyword = keyword.Trim();
            query = query.Where(item =>
                item.TransactionType.Contains(trimmedKeyword) ||
                item.OperatorName.Contains(trimmedKeyword) ||
                item.RelatedPerson.Contains(trimmedKeyword) ||
                item.TargetOrganization.Contains(trimmedKeyword) ||
                item.RelatedBatch.Contains(trimmedKeyword) ||
                item.RelatedArchiveTitle.Contains(trimmedKeyword) ||
                item.Description.Contains(trimmedKeyword) ||
                item.Remark.Contains(trimmedKeyword) ||
                (item.Medium != null &&
                 (item.Medium.DiskCode.Contains(trimmedKeyword) ||
                  item.Medium.SerialNumber.Contains(trimmedKeyword))) ||
                (item.Application != null && item.Application.ApplicationNo.Contains(trimmedKeyword)));
        }

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            query = query.Where(item => item.TransactionType == transactionType);
        }

        return query
            .OrderByDescending(item => item.OperateTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<List<HardDiskMediaApplication>> SearchApplicationsAsync(string? keyword, int? status, string? applicationType)
    {
        IQueryable<HardDiskMediaApplication> query = _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Include(item => item.Medium);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string trimmedKeyword = keyword.Trim();
            query = query.Where(item =>
                item.ApplicationNo.Contains(trimmedKeyword) ||
                item.ApplicantName.Contains(trimmedKeyword) ||
                item.TargetPersonOrUnit.Contains(trimmedKeyword) ||
                item.RelatedBatch.Contains(trimmedKeyword) ||
                item.RelatedArchiveTitle.Contains(trimmedKeyword) ||
                item.Reason.Contains(trimmedKeyword) ||
                (item.Medium != null && (item.Medium.DiskCode.Contains(trimmedKeyword) || item.Medium.SerialNumber.Contains(trimmedKeyword))));
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.ApplicationStatus == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(applicationType))
        {
            query = query.Where(item => item.ApplicationType == applicationType);
        }

        return query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<List<HardDiskMediaApplication>> GetSubmittedApplicationsForToDoAsync(int takeCount)
    {
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Include(item => item.Medium)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted
                || item.ApplicationStatus == HardDiskMediaApplication.StatusApproved
                || item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded)
            .Where(item => item.ApplicationType != HardDiskMediaApplication.TypeReturnBlankRegistration &&
                           item.ApplicationType != HardDiskMediaApplication.TypeReturnDataRegistration &&
                           item.ApplicationType != HardDiskMediaApplication.TypeReturnDamagedRegistration &&
                           item.ApplicationType != HardDiskMediaApplication.TypeLossRegistration)
            .OrderByDescending(item => item.ApplyTime)
            .Take(takeCount)
            .ToListAsync();
    }

    public Task<List<HardDiskMediaApplication>> GetPendingReturnRegistrationsForToDoAsync(int takeCount)
    {
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Include(item => item.Medium)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted
                || item.ApplicationStatus == HardDiskMediaApplication.StatusApproved
                || item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration ||
                           item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration ||
                           item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                           item.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            .OrderByDescending(item => item.ApplyTime)
            .Take(takeCount)
            .ToListAsync();
    }

    public async Task<List<HardDiskMediaApplication>> GetOverdueOutboundApplicationsForToDoAsync(DateTime asOf, int takeCount)
    {
        var activeReturnSourceApplicationIds = await _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Where(item => item.SourceApplicationId != null)
            .Where(item => item.ApplicationStatus != HardDiskMediaApplication.StatusWithdrawn &&
                           item.ApplicationStatus != HardDiskMediaApplication.StatusForceWithdrawn)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration ||
                           item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration ||
                           item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                           item.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            .Select(item => item.SourceApplicationId!.Value)
            .Distinct()
            .ToListAsync();

        return await _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Include(item => item.Medium)
                .ThenInclude(medium => medium!.Ledger)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                           item.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm)
            .Where(item => item.ExpectedReturnDate != null && item.ExpectedReturnDate < asOf)
            .Where(item => item.Medium != null &&
                           !item.Medium.IsDeleted &&
                           item.Medium.Ledger != null &&
                           item.Medium.Ledger.NeedReturn &&
                           (item.Medium.Ledger.MediaStatus == HardDiskMedium.StatusOutTemporary ||
                            item.Medium.Ledger.MediaStatus == HardDiskMedium.StatusOutLongTerm))
            .Where(item => !activeReturnSourceApplicationIds.Contains(item.Id))
            .OrderBy(item => item.ExpectedReturnDate)
            .Take(takeCount)
            .ToListAsync();
    }

    public Task<List<SystemAttachment>> GetApplicationAttachmentsAsync(string businessType, string applicationNo)
    {
        return _dbContext.SystemAttachments
            .AsNoTracking()
            .Where(item => item.BusinessType == businessType && item.BusinessNo == applicationNo)
            .OrderByDescending(item => item.UploadTime)
            .ToListAsync();
    }

    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
    {
        return _dbContext.SystemAttachments
            .FirstOrDefaultAsync(item => item.Id == attachmentId);
    }

    public Task<HardDiskMedium?> GetActiveMediumByIdAsync(int mediumId)
    {
        return _dbContext.HardDiskMedia
            .FirstOrDefaultAsync(item => item.Id == mediumId && !item.IsDeleted);
    }

    public Task<HardDiskMedium?> GetActiveMediumWithLedgerByIdAsync(int mediumId)
    {
        return _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .FirstOrDefaultAsync(item => item.Id == mediumId && !item.IsDeleted);
    }

    public Task<HardDiskMedium?> GetActiveMediumWithLedgerByIdForUpdateAsync(int mediumId)
    {
        return _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .FirstOrDefaultAsync(item => item.Id == mediumId && !item.IsDeleted);
    }

    public Task<HardDiskMediaApplication?> GetApplicationByIdAsync(int applicationId)
    {
        return _dbContext.HardDiskMediaApplications
            .FirstOrDefaultAsync(item => item.Id == applicationId);
    }

    public Task<HardDiskMediaApplication?> GetApplicationWithMediumLedgerByIdAsync(int applicationId)
    {
        return _dbContext.HardDiskMediaApplications
            .Include(item => item.Medium)
                .ThenInclude(item => item!.Ledger)
            .Include(item => item.Medium)
                .ThenInclude(item => item!.RegisterLock)
            .FirstOrDefaultAsync(item => item.Id == applicationId);
    }

    public Task<HardDiskMediaApplication?> GetApplicationWithMediumLedgerByIdAsNoTrackingAsync(int applicationId)
    {
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Include(item => item.Medium)
                .ThenInclude(item => item!.Ledger)
            .Include(item => item.Medium)
                .ThenInclude(item => item!.RegisterLock)
            .FirstOrDefaultAsync(item => item.Id == applicationId);
    }

    public Task<bool> ExistsOtherActiveOutboundApplicationAsync(int mediumId, int? excludedApplicationId)
    {
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Where(item => item.MediumId == mediumId)
            .Where(item => !excludedApplicationId.HasValue || item.Id != excludedApplicationId.Value)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                           item.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm ||
                           item.ApplicationType == HardDiskMediaApplication.TypeOutboundPermanent)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusDraft ||
                           item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted ||
                           item.ApplicationStatus == HardDiskMediaApplication.StatusApproved ||
                           item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded ||
                           item.ApplicationStatus == HardDiskMediaApplication.StatusPendingUpload ||
                           item.ApplicationStatus == HardDiskMediaApplication.StatusPendingProcess)
            .AnyAsync();
    }

    public Task<string?> GetApplicationNoByIdAsync(int applicationId)
    {
        return _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Where(item => item.Id == applicationId)
            .Select(item => item.ApplicationNo)
            .FirstOrDefaultAsync();
    }

    public Task<string?> GetOutboundNoByRecordIdAsync(int outboundRecordId)
    {
        return _dbContext.YearlyArchiveOutboundRecords
            .AsNoTracking()
            .Where(record => record.Id == outboundRecordId)
            .Select(record => record.OutboundNo)
            .FirstOrDefaultAsync();
    }

    public Task<HardDiskMediaBorrowApprovalSnapshot?> GetOutboundApprovalSnapshotAsync(int outboundRecordId)
    {
        return _dbContext.YearlyArchiveOutboundRecords
            .AsNoTracking()
            .Where(record => record.Id == outboundRecordId)
            .Select(record => new HardDiskMediaBorrowApprovalSnapshot
            {
                DeptAuditor = record.DeptAuditor,
                DeptAuditDate = record.DeptAuditDate,
                ArchiveRoomHead = record.ArchiveRoomHead,
                ArchiveRoomHeadDate = record.ArchiveRoomHeadDate
            })
            .FirstOrDefaultAsync();
    }

    public Task<bool> HasDuplicateApplicationNoAsync(int currentId, string applicationNo)
    {
        return _dbContext.HardDiskMediaApplications
            .AnyAsync(item => item.Id != currentId && item.ApplicationNo == applicationNo);
    }

    public Task<bool> HasDuplicateDiskCodeAsync(int currentId, string diskCode)
    {
        return _dbContext.HardDiskMedia
            .AnyAsync(item => !item.IsDeleted && item.Id != currentId && item.DiskCode == diskCode);
    }

    public Task<bool> HasDuplicateSerialNumberAsync(int currentId, string serialNumber)
    {
        return _dbContext.HardDiskMedia
            .AnyAsync(item => !item.IsDeleted && item.Id != currentId && item.SerialNumber == serialNumber);
    }

    public void AddApplication(HardDiskMediaApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _dbContext.HardDiskMediaApplications.Add(application);
    }

    public void AddMedium(HardDiskMedium medium)
    {
        ArgumentNullException.ThrowIfNull(medium);
        _dbContext.HardDiskMedia.Add(medium);
    }

    public void AddTransaction(HardDiskMediaTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _dbContext.HardDiskMediaTransactions.Add(transaction);
    }

    public Task<SystemAttachment?> GetSystemAttachmentByIdAsync(int attachmentId)
    {
        return _dbContext.SystemAttachments
            .FirstOrDefaultAsync(item => item.Id == attachmentId);
    }

    public void AddSystemAttachment(SystemAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _dbContext.SystemAttachments.Add(attachment);
    }

    public void RemoveSystemAttachment(SystemAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _dbContext.SystemAttachments.Remove(attachment);
    }

    public Task<bool> HasOtherSignedAttachmentsAsync(string businessType, int businessId, int excludedAttachmentId, string fileCategory)
    {
        return _dbContext.SystemAttachments
            .AnyAsync(item =>
                item.Id != excludedAttachmentId
                && item.BusinessType == businessType
                && item.BusinessId == businessId
                && item.FileCategory == fileCategory);
    }

    public Task<List<string>> GetDomainOptionLabelsAsync(string entityName, string fieldName)
    {
        return _dbContext.FieldDomainDefinitions
            .Where(definition => definition.EntityName == entityName && definition.FieldName == fieldName)
            .SelectMany(definition => definition.Options)
            .Where(option => option.IsEnabled)
            .OrderBy(option => option.SortOrder)
            .Select(option => option.OptionLabel)
            .ToListAsync();
    }

    public async Task<List<CabinetHardDiskSlotCategoryAssignment>> GetDedicatedMagneticSlotsByCategoryAsync(string categoryName)
    {
        var items = await _dbContext.CabinetHardDiskSlotCategoryAssignments
            .AsNoTracking()
            .Include(item => item.Cabinet)
            .Where(item => item.Cabinet != null && item.Cabinet.Type == CabinetType.MagneticDisk)
            .Where(item => item.CategoryName == categoryName)
            .ToListAsync();

        return items
            .OrderBy(item => CabinetSelectionSupport.GetTraditionalCabinetNameOrder(item.Cabinet!.Name))
            .ThenBy(item => item.FaceCode)
            .ThenBy(item => item.SlotCode)
            .ToList();
    }

    public Task<List<HardDiskMedium>> GetBlankInStockMediaNeedingLocationAssignmentAsync()
    {
        return _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Where(item => !item.IsDeleted)
            .Where(item => item.Ledger != null)
            .Where(item => item.Ledger!.MediaStatus == HardDiskMedium.StatusInStockBlank)
            .Where(item => string.IsNullOrWhiteSpace(item.Ledger!.StorageLocation))
            .OrderBy(item => item.DiskCode)
            .ToListAsync();
    }

    public Task<CabinetHardDiskSlotCategoryAssignment?> GetFirstDedicatedMagneticSlotByCategoryAsync(string categoryName)
    {
        return _dbContext.CabinetHardDiskSlotCategoryAssignments
            .AsNoTracking()
            .Include(item => item.Cabinet)
            .Where(item => item.Cabinet != null && item.Cabinet.Type == CabinetType.MagneticDisk)
            .Where(item => item.CategoryName == categoryName)
            .OrderBy(item => item.CabinetId)
            .ThenBy(item => item.FaceCode)
            .ThenBy(item => item.SlotCode)
            .FirstOrDefaultAsync();
    }

    public Task<Dictionary<string, int>> GetInStockLedgerCountsByLocationsAsync(IReadOnlyCollection<string> locations)
    {
        return _dbContext.HardDiskLedgers
            .AsNoTracking()
            .Where(item => item.MediaStatus == HardDiskMedium.StatusInStockBlank ||
                           item.MediaStatus == HardDiskMedium.StatusInStockData ||
                           item.MediaStatus == HardDiskMedium.StatusInStockDamaged)
            .Where(item => locations.Contains(item.StorageLocation))
            .GroupBy(item => item.StorageLocation)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, int>> GetInStockBlankLedgerCountsBySlotCodesAsync(IReadOnlyCollection<string> slotCodes)
    {
        if (slotCodes == null || slotCodes.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var counts = slotCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(code => code.Trim(), _ => 0, StringComparer.OrdinalIgnoreCase);

        var locations = await _dbContext.HardDiskLedgers
            .AsNoTracking()
            .Where(item => item.MediaStatus == HardDiskMedium.StatusInStockBlank)
            .Select(item => item.StorageLocation)
            .ToListAsync();

        foreach (string location in locations)
        {
            string slotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location);
            if (counts.ContainsKey(slotCode))
            {
                counts[slotCode]++;
            }
        }

        return counts;
    }

    public async Task<List<int>> GetInStockHardDiskSequenceIndexesInSlotAsync(string slotCode)
    {
        var locations = await GetInStockHardDiskStorageLocationsInSlotAsync(slotCode);
        return MagneticDedicatedSlotOccupancySupport.CollectOccupiedSequenceIndexes(slotCode, locations);
    }

    public async Task<List<string>> GetInStockHardDiskStorageLocationsInSlotAsync(string slotCode)
    {
        if (string.IsNullOrWhiteSpace(slotCode))
        {
            return [];
        }

        string normalizedSlotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotCode);
        var locations = await _dbContext.HardDiskLedgers
            .AsNoTracking()
            .Where(item => item.MediaStatus == HardDiskMedium.StatusInStockBlank
                || item.MediaStatus == HardDiskMedium.StatusInStockData
                || item.MediaStatus == HardDiskMedium.StatusInStockDamaged)
            .Select(item => item.StorageLocation)
            .ToListAsync();

        return locations
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Where(location => string.Equals(
                HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location),
                normalizedSlotCode,
                StringComparison.OrdinalIgnoreCase))
            .Select(location => location!.Trim())
            .ToList();
    }

    public async Task<List<HardDiskMedium>> GetInStockBlankHardDisksInSlotAsync(string slotKey, bool unlockedOnly = true)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return [];
        }

        string normalizedSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotKey);
        var query = _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted)
            .Where(item => item.Ledger != null && item.Ledger.MediaStatus == HardDiskMedium.StatusInStockBlank);

        if (unlockedOnly)
        {
            query = query.Where(item => item.RegisterLock == null);
        }

        var media = await query.ToListAsync();

        return media
            .Where(item => string.Equals(
                HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.Ledger!.StorageLocation),
                normalizedSlotKey,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<HardDiskMedium>> GetInStockDamagedHardDisksInSlotAsync(string slotKey, bool unlockedOnly = true)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return [];
        }

        string normalizedSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotKey);
        var query = _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted)
            .Where(item => item.Ledger != null && item.Ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged);

        if (unlockedOnly)
        {
            query = query.Where(item => item.RegisterLock == null);
        }

        var media = await query.ToListAsync();

        return media
            .Where(item => string.Equals(
                HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.Ledger!.StorageLocation),
                normalizedSlotKey,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<OpticalDiscMedium>> GetInStockDamagedOpticalDiscsInSlotAsync(string slotKey)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return [];
        }

        string normalizedSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotKey);
        var media = await _dbContext.OpticalDiscMedia
            .Include(item => item.Ledger)
            .Include(item => item.Transactions)
            .Where(item => !item.IsDeleted)
            .Where(item => item.Ledger != null && item.Ledger.MediaStatus == OpticalDiscMedium.StatusDamaged)
            .ToListAsync();

        return media
            .Where(item => string.Equals(
                HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.Ledger!.StorageLocation),
                normalizedSlotKey,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.DiscCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<int> CountPendingReturnBlankHardDisksInSlotAsync(string slotKey)
        => CountPendingReturnBlankHardDisksInSlotInternalAsync(slotKey);

    public async Task<List<HardDiskMedium>> LoadPendingReturnBlankHardDisksInSlotForRelocationAsync(string slotKey)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return [];
        }

        string normalizedSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotKey);
        var media = await _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.Transactions)
            .Where(item => !item.IsDeleted)
            .Where(item => item.Ledger != null && item.Ledger.NeedReturn)
            .Where(item => item.Ledger!.MediaStatus == HardDiskMedium.StatusOutTemporary
                || item.Ledger!.MediaStatus == HardDiskMedium.StatusOutLongTerm)
            .ToListAsync();

        return FilterPendingReturnBlankHardDisksInSlot(media, normalizedSlotKey);
    }

    public Task<List<HardDiskMediaApplication>> GetCompletedOutboundApplicationsByMediumIdsAsync(IReadOnlyCollection<int> mediumIds)
    {
        if (mediumIds == null || mediumIds.Count == 0)
        {
            return Task.FromResult<List<HardDiskMediaApplication>>([]);
        }

        var targetIds = mediumIds.Where(id => id > 0).Distinct().ToList();
        if (targetIds.Count == 0)
        {
            return Task.FromResult<List<HardDiskMediaApplication>>([]);
        }

        return _dbContext.HardDiskMediaApplications
            .Where(item => targetIds.Contains(item.MediumId))
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeOutboundTemporary
                || item.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm)
            .ToListAsync();
    }

    private async Task<int> CountPendingReturnBlankHardDisksInSlotInternalAsync(string slotKey)
    {
        var media = await LoadPendingReturnBlankHardDisksInSlotForRelocationAsync(slotKey);
        return media.Count;
    }

    private static List<HardDiskMedium> FilterPendingReturnBlankHardDisksInSlot(
        List<HardDiskMedium> media,
        string normalizedSlotKey)
    {
        return media
            .Where(item => MatchesPendingReturnBlankHardDiskHomeSlot(item, normalizedSlotKey))
            .OrderBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesPendingReturnBlankHardDiskHomeSlot(HardDiskMedium medium, string normalizedSlotKey)
    {
        var latestTransaction = medium.Transactions
            .OrderByDescending(item => item.OperateTime)
            .ThenByDescending(item => item.Id)
            .FirstOrDefault();
        if (latestTransaction == null)
        {
            return false;
        }

        if (!string.Equals(latestTransaction.BeforeStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(
            HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(latestTransaction.BeforeLocation),
            normalizedSlotKey,
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<string>> GetInStockOpticalDiscStorageLocationsInSlotAsync(string slotCode)
    {
        if (string.IsNullOrWhiteSpace(slotCode))
        {
            return [];
        }

        string normalizedSlotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotCode);
        var locations = await _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Where(item => item.Ledger != null && item.Ledger.MediaStatus == OpticalDiscMedium.StatusInStock)
            .Select(item => item.Ledger!.StorageLocation)
            .ToListAsync();

        return locations
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Where(location => string.Equals(
                HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location),
                normalizedSlotCode,
                StringComparison.OrdinalIgnoreCase))
            .Select(location => location!.Trim())
            .ToList();
    }

    public Task<int> GetCurrentInStockMediumCountAsync(string location)
    {
        return _dbContext.HardDiskMedia
            .Join(
                _dbContext.HardDiskLedgers.AsNoTracking(),
                medium => medium.Id,
                ledger => ledger.MediumId,
                (medium, ledger) => new { medium, ledger })
            .Where(item => !item.medium.IsDeleted)
            .Where(item => item.ledger.StorageLocation == location)
            .Where(item => item.ledger.MediaStatus == HardDiskMedium.StatusInStockBlank ||
                           item.ledger.MediaStatus == HardDiskMedium.StatusInStockData ||
                           item.ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged)
            .CountAsync();
    }

    public Task<List<string>> GetActiveDiskCodesAsync()
    {
        return _dbContext.HardDiskMedia
            .AsNoTracking()
            .Where(item => !item.IsDeleted && !string.IsNullOrWhiteSpace(item.DiskCode))
            .Select(item => item.DiskCode)
            .ToListAsync();
    }

    public Task<string?> FindFirstDuplicateDiskCodeAsync(IReadOnlyCollection<string> diskCodes)
    {
        return _dbContext.HardDiskMedia
            .AsNoTracking()
            .Where(item => diskCodes.Contains(item.DiskCode))
            .Select(item => item.DiskCode)
            .FirstOrDefaultAsync();
    }

    public Task<string?> FindFirstDuplicateSerialNumberAsync(IReadOnlyCollection<string> serialNumbers)
    {
        return _dbContext.HardDiskMedia
            .AsNoTracking()
            .Where(item => serialNumbers.Contains(item.SerialNumber))
            .Select(item => item.SerialNumber)
            .FirstOrDefaultAsync();
    }

    public void RemoveApplication(HardDiskMediaApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _dbContext.HardDiskMediaApplications.Remove(application);
    }

    public Task<string?> GetLastApplicationNoByPrefixAsync(string prefix)
    {
        return _dbContext.HardDiskMediaApplications
            .Where(item => item.ApplicationNo.StartsWith(prefix))
            .OrderByDescending(item => item.ApplicationNo)
            .Select(item => item.ApplicationNo)
            .FirstOrDefaultAsync();
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    public Task<bool> HasMediaRecordsAsync()
    {
        return _dbContext.HardDiskMedia
            .AnyAsync(item => !item.IsDeleted);
    }

    public Task<bool> HasAnyApplicationsAsync()
    {
        return _dbContext.HardDiskMediaApplications.AnyAsync();
    }

    public Task<bool> HasAnyTransactionsAsync()
    {
        return _dbContext.HardDiskMediaTransactions.AnyAsync();
    }

    public Task<int> GetMediaCountAsync()
    {
        return _dbContext.HardDiskMedia.CountAsync();
    }

    public Task DeleteAllMediaAsync()
    {
        return _dbContext.HardDiskMedia.ExecuteDeleteAsync();
    }

    public Task AddMediaRangeAsync(IReadOnlyCollection<HardDiskMedium> media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return _dbContext.HardDiskMedia.AddRangeAsync(media);
    }

    public async Task<IHardDiskMediaRepositoryTransaction> BeginTransactionAsync()
    {
        IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync();
        return new HardDiskMediaRepositoryTransaction(transaction);
    }

    private sealed class HardDiskMediaRepositoryTransaction : IHardDiskMediaRepositoryTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private bool _completed;

        public HardDiskMediaRepositoryTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync()
        {
            if (_completed)
            {
                return;
            }

            await _transaction.CommitAsync();
            _completed = true;
        }

        public async Task RollbackAsync()
        {
            if (_completed)
            {
                return;
            }

            await _transaction.RollbackAsync();
            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            await _transaction.DisposeAsync();
        }
    }
}
