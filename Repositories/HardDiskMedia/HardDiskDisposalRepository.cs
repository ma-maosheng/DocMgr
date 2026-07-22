using DocMgr.Data;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HardDiskMedia;

/// <summary>
/// 硬盘离库处置仓储。
/// </summary>
public sealed class HardDiskDisposalRepository : IHardDiskDisposalRepository
{
    private static readonly int[] ActiveStatuses =
    [
        HardDiskDisposalRecord.StatusDraft,
        HardDiskDisposalRecord.StatusSubmitted,
        HardDiskDisposalRecord.StatusApproved,
        HardDiskDisposalRecord.StatusSignedUploaded
    ];

    private readonly AppDbContext _dbContext;

    public HardDiskDisposalRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<HardDiskDisposalRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear)
    {
        IQueryable<HardDiskDisposalRecord> query = _dbContext.HardDiskDisposalRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (applyYear.HasValue)
        {
            int year = applyYear.Value;
            query = query.Where(item => item.ApplyTime.Year == year);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string trimmed = keyword.Trim();
            query = query.Where(item =>
                item.DisposalNo.Contains(trimmed)
                || item.ApplicantName.Contains(trimmed)
                || item.DisposalReason.Contains(trimmed)
                || item.DispositionMethod.Contains(trimmed)
                || item.Items.Any(detail => detail.DiskCode.Contains(trimmed) || detail.SerialNumber.Contains(trimmed)));
        }

        return await query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<HardDiskDisposalRecord?> GetRecordByIdAsync(int recordId)
    {
        return _dbContext.HardDiskDisposalRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public Task<HardDiskDisposalRecord?> GetRecordByIdForUpdateAsync(int recordId)
    {
        return _dbContext.HardDiskDisposalRecords
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public async Task<string?> GetLastDisposalNoByPrefixAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        string trimmed = prefix.Trim();
        return await _dbContext.HardDiskDisposalRecords
            .AsNoTracking()
            .Where(item => item.DisposalNo.StartsWith(trimmed))
            .OrderByDescending(item => item.DisposalNo)
            .Select(item => item.DisposalNo)
            .FirstOrDefaultAsync();
    }

    public async Task<List<HardDiskMedium>> GetSelectableInStockMediaAsync(IReadOnlyCollection<int>? excludeMediumIds = null)
    {
        HashSet<int> excluded = excludeMediumIds == null
            ? new HashSet<int>()
            : excludeMediumIds.Where(id => id > 0).ToHashSet();

        var activeDisposalMediumIds = await _dbContext.HardDiskDisposalItems
            .AsNoTracking()
            .Where(item => ActiveStatuses.Contains(item.DisposalRecord!.Status))
            .Select(item => item.MediumId)
            .Distinct()
            .ToListAsync();

        HashSet<int> busyMediumIds = activeDisposalMediumIds.ToHashSet();
        busyMediumIds.ExceptWith(excluded);

        return await _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted)
            .Where(item => item.Ledger != null
                           && (item.Ledger.MediaStatus == HardDiskMedium.StatusInStockBlank
                               || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged))
            .Where(item => !busyMediumIds.Contains(item.Id))
            .Where(item => item.RegisterLock == null
                           || (excluded.Contains(item.Id)
                               && item.RegisterLock.BusinessType == HardDiskRegisterLock.BusinessTypeDisposal))
            .OrderBy(item => item.DiskCode)
            .ToListAsync();
    }

    public async Task<List<HardDiskMedium>> GetMediaWithLedgerByIdsAsync(IReadOnlyCollection<int> mediumIds)
    {
        if (mediumIds == null || mediumIds.Count == 0)
        {
            return new List<HardDiskMedium>();
        }

        List<int> ids = mediumIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<HardDiskMedium>();
        }

        return await _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted && ids.Contains(item.Id))
            .ToListAsync();
    }

    public Task<bool> ExistsActiveDisposalForMediumAsync(int mediumId, int? excludeRecordId = null)
    {
        IQueryable<HardDiskDisposalItem> query = _dbContext.HardDiskDisposalItems
            .AsNoTracking()
            .Where(item => item.MediumId == mediumId
                           && ActiveStatuses.Contains(item.DisposalRecord!.Status));

        if (excludeRecordId.HasValue && excludeRecordId.Value > 0)
        {
            int excludeId = excludeRecordId.Value;
            query = query.Where(item => item.DisposalRecordId != excludeId);
        }

        return query.AnyAsync();
    }

    public Task<List<SystemAttachment>> GetAttachmentsAsync(string disposalNo)
    {
        if (string.IsNullOrWhiteSpace(disposalNo))
        {
            return Task.FromResult(new List<SystemAttachment>());
        }

        string trimmed = disposalNo.Trim();
        return _dbContext.SystemAttachments
            .AsNoTracking()
            .Where(item => item.BusinessType == HardDiskDisposalDomainValues.AttachmentBusinessType
                           && item.BusinessNo == trimmed)
            .OrderByDescending(item => item.UploadTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
    {
        return _dbContext.SystemAttachments.FirstOrDefaultAsync(item => item.Id == attachmentId);
    }

    public void AddRecord(HardDiskDisposalRecord record)
    {
        _dbContext.HardDiskDisposalRecords.Add(record);
    }

    public void RemoveItems(IEnumerable<HardDiskDisposalItem> items)
    {
        _dbContext.HardDiskDisposalItems.RemoveRange(items);
    }

    public void AddTransaction(HardDiskMediaTransaction transaction)
    {
        _dbContext.HardDiskMediaTransactions.Add(transaction);
    }

    public void AddAttachment(SystemAttachment attachment)
    {
        _dbContext.SystemAttachments.Add(attachment);
    }

    public void RemoveAttachment(SystemAttachment attachment)
    {
        _dbContext.SystemAttachments.Remove(attachment);
    }

    public void RemoveRegisterLock(HardDiskRegisterLock lockItem)
    {
        _dbContext.HardDiskRegisterLocks.Remove(lockItem);
    }

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
}
