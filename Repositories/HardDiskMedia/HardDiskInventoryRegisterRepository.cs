using DocMgr.Data;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HardDiskMedia;

/// <summary>
/// 硬盘盘库登记仓储。
/// </summary>
public sealed class HardDiskInventoryRegisterRepository : IHardDiskInventoryRegisterRepository
{
    private static readonly int[] ActiveRegisterStatuses =
    [
        HardDiskInventoryRegisterRecord.StatusDraft
    ];

    private static readonly int[] ActiveDisposalStatuses =
    [
        HardDiskDisposalRecord.StatusDraft,
        HardDiskDisposalRecord.StatusSubmitted,
        HardDiskDisposalRecord.StatusApproved,
        HardDiskDisposalRecord.StatusSignedUploaded
    ];

    private readonly AppDbContext _dbContext;

    public HardDiskInventoryRegisterRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<HardDiskInventoryRegisterRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear)
    {
        IQueryable<HardDiskInventoryRegisterRecord> query = _dbContext.HardDiskInventoryRegisterRecords
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
                item.RegisterNo.Contains(trimmed)
                || item.ApplicantName.Contains(trimmed)
                || item.RegisterKind.Contains(trimmed)
                || item.Reason.Contains(trimmed)
                || item.Items.Any(detail => detail.DiskCode.Contains(trimmed) || detail.SerialNumber.Contains(trimmed)));
        }

        return await query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<HardDiskInventoryRegisterRecord?> GetRecordByIdAsync(int recordId)
    {
        return _dbContext.HardDiskInventoryRegisterRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public Task<HardDiskInventoryRegisterRecord?> GetRecordByIdForUpdateAsync(int recordId)
    {
        return _dbContext.HardDiskInventoryRegisterRecords
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public async Task<string?> GetLastRegisterNoByPrefixAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        string trimmed = prefix.Trim();
        return await _dbContext.HardDiskInventoryRegisterRecords
            .AsNoTracking()
            .Where(item => item.RegisterNo.StartsWith(trimmed))
            .OrderByDescending(item => item.RegisterNo)
            .Select(item => item.RegisterNo)
            .FirstOrDefaultAsync();
    }

    public async Task<List<HardDiskMedium>> GetSelectableInStockMediaAsync(IReadOnlyCollection<int>? excludeMediumIds = null)
    {
        HashSet<int> excluded = excludeMediumIds == null
            ? new HashSet<int>()
            : excludeMediumIds.Where(id => id > 0).ToHashSet();

        var activeRegisterMediumIds = await _dbContext.HardDiskInventoryRegisterItems
            .AsNoTracking()
            .Where(item => ActiveRegisterStatuses.Contains(item.RegisterRecord!.Status))
            .Select(item => item.MediumId)
            .Distinct()
            .ToListAsync();

        var activeDisposalMediumIds = await _dbContext.HardDiskDisposalItems
            .AsNoTracking()
            .Where(item => ActiveDisposalStatuses.Contains(item.DisposalRecord!.Status))
            .Select(item => item.MediumId)
            .Distinct()
            .ToListAsync();

        HashSet<int> busyMediumIds = activeRegisterMediumIds.Concat(activeDisposalMediumIds).ToHashSet();
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
                               && item.RegisterLock.BusinessType == HardDiskRegisterLock.BusinessTypeInventoryRegister))
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

    public Task<bool> ExistsActiveRegisterForMediumAsync(int mediumId, int? excludeRecordId = null)
    {
        IQueryable<HardDiskInventoryRegisterItem> query = _dbContext.HardDiskInventoryRegisterItems
            .AsNoTracking()
            .Where(item => item.MediumId == mediumId
                           && ActiveRegisterStatuses.Contains(item.RegisterRecord!.Status));

        if (excludeRecordId.HasValue && excludeRecordId.Value > 0)
        {
            int excludeId = excludeRecordId.Value;
            query = query.Where(item => item.RegisterRecordId != excludeId);
        }

        return query.AnyAsync();
    }

    public Task<bool> ExistsActiveDisposalForMediumAsync(int mediumId)
    {
        return _dbContext.HardDiskDisposalItems
            .AsNoTracking()
            .AnyAsync(item => item.MediumId == mediumId
                              && ActiveDisposalStatuses.Contains(item.DisposalRecord!.Status));
    }

    public void AddRecord(HardDiskInventoryRegisterRecord record)
    {
        _dbContext.HardDiskInventoryRegisterRecords.Add(record);
    }

    public void RemoveItems(IEnumerable<HardDiskInventoryRegisterItem> items)
    {
        _dbContext.HardDiskInventoryRegisterItems.RemoveRange(items);
    }

    public void AddTransaction(HardDiskMediaTransaction transaction)
    {
        _dbContext.HardDiskMediaTransactions.Add(transaction);
    }

    public void RemoveRegisterLock(HardDiskRegisterLock lockItem)
    {
        _dbContext.HardDiskRegisterLocks.Remove(lockItem);
    }

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
}
