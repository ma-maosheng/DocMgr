using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.HistoryArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HistoryArchive;

/// <summary>
/// 历史存档离库处置数据访问。
/// </summary>
public sealed class HistoryArchiveDisposalRepository : IHistoryArchiveDisposalRepository
{
    private static readonly int[] ActiveStatuses =
    [
        HistoryArchiveDisposalRecord.StatusDraft,
        HistoryArchiveDisposalRecord.StatusSubmitted,
        HistoryArchiveDisposalRecord.StatusApproved,
        HistoryArchiveDisposalRecord.StatusSignedUploaded
    ];

    private readonly AppDbContext _dbContext;

    public HistoryArchiveDisposalRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<HistoryArchiveDisposalRecord>> SearchRecordsAsync(string? keyword, int? status, int? applyYear)
    {
        IQueryable<HistoryArchiveDisposalRecord> query = _dbContext.HistoryArchiveDisposalRecords
            .AsNoTracking()
            .Include(item => item.Items);

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (applyYear.HasValue)
        {
            int year = applyYear.Value;
            query = query.Where(item => item.ApplyTime.Year == year);
        }

        string? trimmed = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            query = query.Where(item =>
                item.DisposalNo.Contains(trimmed)
                || item.Reason.Contains(trimmed)
                || item.DispositionMethod.Contains(trimmed)
                || item.ApplicantName.Contains(trimmed)
                || item.Items.Any(row => row.BoxCode.Contains(trimmed) || row.ContentSummary.Contains(trimmed)));
        }

        return query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<HistoryArchiveDisposalRecord?> GetRecordByIdAsync(int recordId, bool tracking = false)
    {
        IQueryable<HistoryArchiveDisposalRecord> query = tracking
            ? _dbContext.HistoryArchiveDisposalRecords.Include(item => item.Items)
            : _dbContext.HistoryArchiveDisposalRecords.AsNoTracking().Include(item => item.Items);
        return query.FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public async Task<string?> GetLastDisposalNoByPrefixAsync(string prefix)
    {
        string trimmed = prefix?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        List<string> numbers = await _dbContext.HistoryArchiveDisposalRecords
            .AsNoTracking()
            .Where(item => item.DisposalNo.StartsWith(trimmed))
            .Select(item => item.DisposalNo)
            .ToListAsync();
        return numbers
            .OrderByDescending(item => item, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>跨类混放盒号集合：同一盒号的台账关联存在多种资料类别（链接表聚合）。</summary>
    public async Task<List<string>> GetCrossTypeMixedBoxCodesAsync()
    {
        // EF Core 无法翻译 GroupBy 内嵌 Distinct 计数，拉平后在内存聚合。
        var rows = await _dbContext.HistoryArchiveBoxLedgerLinks
            .AsNoTracking()
            .Select(link => new { link.HistoryArchiveBoxId, link.MaterialKind })
            .ToListAsync();
        if (rows.Count == 0)
        {
            return [];
        }

        var crossTypeBoxIds = rows
            .GroupBy(row => row.HistoryArchiveBoxId)
            .Where(group => group.Select(row => row.MaterialKind).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (crossTypeBoxIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.HistoryArchiveBoxes
            .AsNoTracking()
            .Where(box => crossTypeBoxIds.Contains(box.Id))
            .Select(box => box.BoxCode)
            .ToListAsync();
    }

    public Task<List<HistoryArchiveBox>> GetInStockHistoryArchiveBoxesAsync()
    {
        return _dbContext.HistoryArchiveBoxes
            .AsNoTracking()
            .Where(box => box.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
            .ToListAsync();
    }

    public async Task<List<TopoMap>> GetTopoMapsAsync()
    {
        var list = await _dbContext.TopoMaps.AsNoTracking().ToListAsync();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, HistoryArchiveDisposalDomainValues.MaterialKindTopoMap, list);
        return list;
    }

    public async Task<List<AerialPhoto>> GetAerialPhotosAsync()
    {
        var list = await _dbContext.AerialPhotos.AsNoTracking().ToListAsync();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto, list);
        return list;
    }

    public async Task<List<OtherMap>> GetOtherMapsAsync()
    {
        var list = await _dbContext.OtherMaps.AsNoTracking().ToListAsync();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, HistoryArchiveDisposalDomainValues.MaterialKindOtherMap, list);
        return list;
    }

    public async Task<List<TopoMap>> GetTopoMapsByIdsAsync(IReadOnlyCollection<int> ids, bool tracking)
    {
        IQueryable<TopoMap> query = tracking ? _dbContext.TopoMaps : _dbContext.TopoMaps.AsNoTracking();
        var list = await query.Where(item => ids.Contains(item.Id)).ToListAsync();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, HistoryArchiveDisposalDomainValues.MaterialKindTopoMap, list);
        return list;
    }

    public async Task<List<AerialPhoto>> GetAerialPhotosByIdsAsync(IReadOnlyCollection<int> ids, bool tracking)
    {
        IQueryable<AerialPhoto> query = tracking ? _dbContext.AerialPhotos : _dbContext.AerialPhotos.AsNoTracking();
        var list = await query.Where(item => ids.Contains(item.Id)).ToListAsync();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto, list);
        return list;
    }

    public async Task<List<OtherMap>> GetOtherMapsByIdsAsync(IReadOnlyCollection<int> ids, bool tracking)
    {
        IQueryable<OtherMap> query = tracking ? _dbContext.OtherMaps : _dbContext.OtherMaps.AsNoTracking();
        var list = await query.Where(item => ids.Contains(item.Id)).ToListAsync();
        HistoryArchiveBoxProjectionSupport.Hydrate(_dbContext, HistoryArchiveDisposalDomainValues.MaterialKindOtherMap, list);
        return list;
    }

    public async Task<HashSet<string>> GetLockedBoxCodesAsync(int? excludeRecordId)
    {
        IQueryable<HistoryArchiveDisposalItem> query = _dbContext.HistoryArchiveDisposalItems
            .AsNoTracking()
            .Where(item => ActiveStatuses.Contains(item.DisposalRecord!.Status));
        if (excludeRecordId.HasValue && excludeRecordId.Value > 0)
        {
            int excludeId = excludeRecordId.Value;
            query = query.Where(item => item.DisposalRecordId != excludeId);
        }

        List<string> codes = await query.Select(item => item.BoxCode).ToListAsync();
        return codes
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Task<List<HistoryArchiveDisposalRecord>> GetPendingRecordsForToDoAsync(int takeCount)
    {
        return _dbContext.HistoryArchiveDisposalRecords
            .AsNoTracking()
            .Include(item => item.Items)
            .Where(item =>
                item.Status == HistoryArchiveDisposalRecord.StatusSubmitted
                || item.Status == HistoryArchiveDisposalRecord.StatusApproved
                || item.Status == HistoryArchiveDisposalRecord.StatusSignedUploaded)
            .OrderBy(item => item.SubmittedAt ?? item.ApplyTime)
            .Take(takeCount)
            .ToListAsync();
    }

    public async Task<List<HistoryArchiveBox>> GetHistoryArchiveBoxesByCodesAsync(IReadOnlyCollection<string> boxCodes)
    {
        if (boxCodes == null || boxCodes.Count == 0)
        {
            return [];
        }

        var normalized = boxCodes
            .Select(code => code?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToList();
        if (normalized.Count == 0)
        {
            return [];
        }

        return await _dbContext.HistoryArchiveBoxes
            .Where(box => normalized.Contains(box.BoxCode))
            .ToListAsync();
    }

    public async Task RemoveHistoryArchiveBoxLinksByBoxCodesAsync(IReadOnlyCollection<string> boxCodes)
    {
        if (boxCodes == null || boxCodes.Count == 0)
        {
            return;
        }

        var normalized = boxCodes
            .Select(code => code?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToList();
        if (normalized.Count == 0)
        {
            return;
        }

        var boxIds = await _dbContext.HistoryArchiveBoxes
            .Where(box => normalized.Contains(box.BoxCode))
            .Select(box => box.Id)
            .ToListAsync();
        if (boxIds.Count == 0)
        {
            return;
        }

        await _dbContext.HistoryArchiveBoxLedgerLinks
            .Where(link => boxIds.Contains(link.HistoryArchiveBoxId))
            .ExecuteDeleteAsync();
    }

    public Task<List<SystemAttachment>> GetAttachmentsAsync(string disposalNo)
    {
        string trimmed = disposalNo?.Trim() ?? string.Empty;
        return _dbContext.SystemAttachments
            .AsNoTracking()
            .Where(item =>
                item.BusinessType == HistoryArchiveDisposalDomainValues.AttachmentBusinessType
                && item.BusinessNo == trimmed)
            .OrderByDescending(item => item.UploadTime)
            .ToListAsync();
    }

    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId) =>
        _dbContext.SystemAttachments.FirstOrDefaultAsync(item => item.Id == attachmentId);

    public void AddRecord(HistoryArchiveDisposalRecord record) =>
        _dbContext.HistoryArchiveDisposalRecords.Add(record);

    public void RemoveItems(IEnumerable<HistoryArchiveDisposalItem> items) =>
        _dbContext.HistoryArchiveDisposalItems.RemoveRange(items);

    public void AddAttachment(SystemAttachment attachment) =>
        _dbContext.SystemAttachments.Add(attachment);

    public void RemoveAttachment(SystemAttachment attachment) =>
        _dbContext.SystemAttachments.Remove(attachment);

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
}
