using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
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

    public Task<List<CabinetArchiveBoxPlacement>> GetHistoryPlacementsAsync()
    {
        return _dbContext.CabinetArchiveBoxPlacements
            .AsNoTracking()
            .Where(item =>
                item.SourceType == HistoryArchiveDisposalDomainValues.PlacementSourceTopoMap
                || item.SourceType == HistoryArchiveDisposalDomainValues.PlacementSourceAerialPhoto
                || item.SourceType == HistoryArchiveDisposalDomainValues.PlacementSourceOtherMap
                || item.SourceType == HistoryArchiveDisposalDomainValues.PlacementSourceMixed)
            .ToListAsync();
    }

    public Task<List<TopoMap>> GetTopoMapsAsync() =>
        _dbContext.TopoMaps.AsNoTracking().ToListAsync();

    public Task<List<AerialPhoto>> GetAerialPhotosAsync() =>
        _dbContext.AerialPhotos.AsNoTracking().ToListAsync();

    public Task<List<OtherMap>> GetOtherMapsAsync() =>
        _dbContext.OtherMaps.AsNoTracking().ToListAsync();

    public Task<List<TopoMap>> GetTopoMapsForUpdateAsync() =>
        _dbContext.TopoMaps.ToListAsync();

    public Task<List<AerialPhoto>> GetAerialPhotosForUpdateAsync() =>
        _dbContext.AerialPhotos.ToListAsync();

    public Task<List<OtherMap>> GetOtherMapsForUpdateAsync() =>
        _dbContext.OtherMaps.ToListAsync();

    public Task<List<TopoMap>> GetTopoMapsByIdsAsync(IReadOnlyCollection<int> ids, bool tracking)
    {
        IQueryable<TopoMap> query = tracking ? _dbContext.TopoMaps : _dbContext.TopoMaps.AsNoTracking();
        return query.Where(item => ids.Contains(item.Id)).ToListAsync();
    }

    public Task<List<AerialPhoto>> GetAerialPhotosByIdsAsync(IReadOnlyCollection<int> ids, bool tracking)
    {
        IQueryable<AerialPhoto> query = tracking ? _dbContext.AerialPhotos : _dbContext.AerialPhotos.AsNoTracking();
        return query.Where(item => ids.Contains(item.Id)).ToListAsync();
    }

    public Task<List<OtherMap>> GetOtherMapsByIdsAsync(IReadOnlyCollection<int> ids, bool tracking)
    {
        IQueryable<OtherMap> query = tracking ? _dbContext.OtherMaps : _dbContext.OtherMaps.AsNoTracking();
        return query.Where(item => ids.Contains(item.Id)).ToListAsync();
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

    public void RemoveArchiveBoxPlacementByBoxCode(string boxCode)
    {
        string trimmed = boxCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var placement = _dbContext.CabinetArchiveBoxPlacements
            .FirstOrDefault(item => item.BoxCode == trimmed);
        if (placement != null)
        {
            _dbContext.CabinetArchiveBoxPlacements.Remove(placement);
        }
    }

    public void AddAttachment(SystemAttachment attachment) =>
        _dbContext.SystemAttachments.Add(attachment);

    public void RemoveAttachment(SystemAttachment attachment) =>
        _dbContext.SystemAttachments.Remove(attachment);

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
}
