using DocMgr.Data;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.NetworkTransfer;

/// <summary>
/// 年度资料出入网管理仓储。
/// </summary>
public sealed class NetworkTransferRepository : INetworkTransferRepository
{
    private static readonly int[] ActiveStatuses =
    [
        ApplicationWorkflowStatus.Draft,
        ApplicationWorkflowStatus.Submitted,
        ApplicationWorkflowStatus.Approved,
        ApplicationWorkflowStatus.SignedUploaded
    ];

    private readonly AppDbContext _dbContext;

    public NetworkTransferRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<NetworkInboundRecord>> SearchInboundRecordsAsync(string? keyword, int? status, int? applyYear)
    {
        IQueryable<NetworkInboundRecord> query = _dbContext.NetworkInboundRecords
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
                item.InboundNo.Contains(trimmed)
                || item.ApplicantName.Contains(trimmed)
                || item.ProjectName.Contains(trimmed)
                || item.Items.Any(detail =>
                    detail.AssetName.Contains(trimmed)
                    || detail.MaterialName.Contains(trimmed)
                    || detail.FormNo.Contains(trimmed)));
        }

        return await query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<NetworkInboundRecord?> GetInboundByIdAsync(int recordId, bool tracking = false)
    {
        IQueryable<NetworkInboundRecord> query = tracking
            ? _dbContext.NetworkInboundRecords.Include(item => item.Items)
            : _dbContext.NetworkInboundRecords.AsNoTracking().Include(item => item.Items);
        return query.FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public void AddInbound(NetworkInboundRecord record) => _dbContext.NetworkInboundRecords.Add(record);

    public void RemoveInboundItems(IEnumerable<NetworkInboundItem> items) =>
        _dbContext.NetworkInboundItems.RemoveRange(items);

    public async Task<string?> GetLastInboundNoByPrefixAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        string trimmed = prefix.Trim();
        return await _dbContext.NetworkInboundRecords
            .AsNoTracking()
            .Where(item => item.InboundNo.StartsWith(trimmed))
            .OrderByDescending(item => item.InboundNo)
            .Select(item => item.InboundNo)
            .FirstOrDefaultAsync();
    }

    public async Task<List<NetworkOutboundRecord>> SearchOutboundRecordsAsync(string? keyword, int? status, int? applyYear)
    {
        IQueryable<NetworkOutboundRecord> query = _dbContext.NetworkOutboundRecords
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
                item.OutboundNo.Contains(trimmed)
                || item.ApplicantName.Contains(trimmed)
                || item.ProjectName.Contains(trimmed)
                || item.Items.Any(detail =>
                    detail.AssetName.Contains(trimmed) || detail.AssetNo.Contains(trimmed)));
        }

        return await query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<NetworkOutboundRecord?> GetOutboundByIdAsync(int recordId, bool tracking = false)
    {
        IQueryable<NetworkOutboundRecord> query = tracking
            ? _dbContext.NetworkOutboundRecords.Include(item => item.Items)
            : _dbContext.NetworkOutboundRecords.AsNoTracking().Include(item => item.Items);
        return query.FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public void AddOutbound(NetworkOutboundRecord record) => _dbContext.NetworkOutboundRecords.Add(record);

    public void RemoveOutboundItems(IEnumerable<NetworkOutboundItem> items) =>
        _dbContext.NetworkOutboundItems.RemoveRange(items);

    public async Task<string?> GetLastOutboundNoByPrefixAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        string trimmed = prefix.Trim();
        return await _dbContext.NetworkOutboundRecords
            .AsNoTracking()
            .Where(item => item.OutboundNo.StartsWith(trimmed))
            .OrderByDescending(item => item.OutboundNo)
            .Select(item => item.OutboundNo)
            .FirstOrDefaultAsync();
    }

    public async Task<List<NetworkOnNetDisposalRecord>> SearchDisposalRecordsAsync(string? keyword, int? status, int? applyYear)
    {
        IQueryable<NetworkOnNetDisposalRecord> query = _dbContext.NetworkOnNetDisposalRecords
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
                || item.Items.Any(detail =>
                    detail.AssetName.Contains(trimmed) || detail.AssetNo.Contains(trimmed)));
        }

        return await query
            .OrderByDescending(item => item.ApplyTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<NetworkOnNetDisposalRecord?> GetDisposalByIdAsync(int recordId, bool tracking = false)
    {
        IQueryable<NetworkOnNetDisposalRecord> query = tracking
            ? _dbContext.NetworkOnNetDisposalRecords.Include(item => item.Items)
            : _dbContext.NetworkOnNetDisposalRecords.AsNoTracking().Include(item => item.Items);
        return query.FirstOrDefaultAsync(item => item.Id == recordId);
    }

    public void AddDisposal(NetworkOnNetDisposalRecord record) => _dbContext.NetworkOnNetDisposalRecords.Add(record);

    public void RemoveDisposalItems(IEnumerable<NetworkOnNetDisposalItem> items) =>
        _dbContext.NetworkOnNetDisposalItems.RemoveRange(items);

    public async Task<string?> GetLastDisposalNoByPrefixAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        string trimmed = prefix.Trim();
        return await _dbContext.NetworkOnNetDisposalRecords
            .AsNoTracking()
            .Where(item => item.DisposalNo.StartsWith(trimmed))
            .OrderByDescending(item => item.DisposalNo)
            .Select(item => item.DisposalNo)
            .FirstOrDefaultAsync();
    }

    public async Task<List<NetworkOnNetAsset>> SearchOnNetAssetsAsync(
        string? keyword,
        string? originKind,
        string? lifecycleStatus)
    {
        IQueryable<NetworkOnNetAsset> query = _dbContext.NetworkOnNetAssets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(originKind))
        {
            string origin = originKind.Trim();
            query = query.Where(item => item.OriginKind == origin);
        }

        if (!string.IsNullOrWhiteSpace(lifecycleStatus))
        {
            string status = lifecycleStatus.Trim();
            query = query.Where(item => item.LifecycleStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string trimmed = keyword.Trim();
            query = query.Where(item =>
                item.AssetNo.Contains(trimmed)
                || item.AssetName.Contains(trimmed)
                || item.ProjectName.Contains(trimmed)
                || item.ServerPath.Contains(trimmed));
        }

        return await query
            .OrderByDescending(item => item.RegisteredAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<NetworkOnNetAsset?> GetOnNetAssetByIdAsync(int assetId, bool tracking = false)
    {
        IQueryable<NetworkOnNetAsset> query = tracking
            ? _dbContext.NetworkOnNetAssets
            : _dbContext.NetworkOnNetAssets.AsNoTracking();
        return query.FirstOrDefaultAsync(item => item.Id == assetId);
    }

    public async Task<List<NetworkOnNetAsset>> GetOnNetAssetsByIdsAsync(
        IReadOnlyCollection<int> assetIds,
        bool tracking = false)
    {
        if (assetIds == null || assetIds.Count == 0)
        {
            return [];
        }

        HashSet<int> ids = assetIds.Where(id => id > 0).ToHashSet();
        IQueryable<NetworkOnNetAsset> query = tracking
            ? _dbContext.NetworkOnNetAssets
            : _dbContext.NetworkOnNetAssets.AsNoTracking();
        return await query.Where(item => ids.Contains(item.Id)).ToListAsync();
    }

    public async Task<List<NetworkOnNetAsset>> GetSelectableOutboundAssetsAsync(int? currentOutboundRecordId = null)
    {
        HashSet<int> lockedIds = await GetLockedAssetIdsAsync(
            excludeOutboundRecordId: currentOutboundRecordId,
            excludeDisposalRecordId: null);

        return await _dbContext.NetworkOnNetAssets
            .AsNoTracking()
            .Where(item =>
                item.OriginKind == NetworkTransferDomainValues.OriginKindProcessedOutput
                && item.LifecycleStatus == NetworkTransferDomainValues.LifecycleOnNet
                && !lockedIds.Contains(item.Id))
            .OrderByDescending(item => item.RegisteredAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public async Task<List<NetworkOnNetAsset>> GetSelectableDisposalAssetsAsync(int? currentDisposalRecordId = null)
    {
        HashSet<int> lockedIds = await GetLockedAssetIdsAsync(
            excludeOutboundRecordId: null,
            excludeDisposalRecordId: currentDisposalRecordId);

        return await _dbContext.NetworkOnNetAssets
            .AsNoTracking()
            .Where(item =>
                item.LifecycleStatus == NetworkTransferDomainValues.LifecycleOnNet
                && !lockedIds.Contains(item.Id))
            .OrderByDescending(item => item.RegisteredAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public void AddOnNetAsset(NetworkOnNetAsset asset) => _dbContext.NetworkOnNetAssets.Add(asset);

    public async Task<string?> GetLastOnNetAssetNoByPrefixAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        string trimmed = prefix.Trim();
        return await _dbContext.NetworkOnNetAssets
            .AsNoTracking()
            .Where(item => item.AssetNo.StartsWith(trimmed))
            .OrderByDescending(item => item.AssetNo)
            .Select(item => item.AssetNo)
            .FirstOrDefaultAsync();
    }

    public Task<YearlyArchiveSearchResultSet?> GetElectronicSearchResultSetAsync(int resultSetId)
    {
        return _dbContext.YearlyArchiveSearchResultSets
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item =>
                item.Id == resultSetId
                && item.MediaKind == ArchiveRegisterDomainValues.MediaKindElectronic);
    }

    public async Task<Dictionary<int, YearlyArchiveFilingFact>> GetFilingFactsByIdsAsync(IReadOnlyCollection<int> filingFactIds)
    {
        if (filingFactIds == null || filingFactIds.Count == 0)
        {
            return new Dictionary<int, YearlyArchiveFilingFact>();
        }

        HashSet<int> ids = filingFactIds.Where(id => id > 0).ToHashSet();
        if (ids.Count == 0)
        {
            return new Dictionary<int, YearlyArchiveFilingFact>();
        }

        List<YearlyArchiveFilingFact> facts = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => ids.Contains(fact.Id))
            .ToListAsync();

        return facts.ToDictionary(fact => fact.Id);
    }

    public Task<List<SystemAttachment>> GetAttachmentsAsync(string businessType, string businessNo)
    {
        string type = businessType?.Trim() ?? string.Empty;
        string no = businessNo?.Trim() ?? string.Empty;
        return _dbContext.SystemAttachments
            .AsNoTracking()
            .Where(item => item.BusinessType == type && item.BusinessNo == no)
            .OrderByDescending(item => item.UploadTime)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
    }

    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId) =>
        _dbContext.SystemAttachments.FirstOrDefaultAsync(item => item.Id == attachmentId);

    public void AddAttachment(SystemAttachment attachment) => _dbContext.SystemAttachments.Add(attachment);

    public void RemoveAttachment(SystemAttachment attachment) => _dbContext.SystemAttachments.Remove(attachment);

    public void AddRegisterRecord(YearlyArchiveRegisterRecord record) =>
        _dbContext.YearlyArchiveRegisterRecords.Add(record);

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();

    private async Task<HashSet<int>> GetLockedAssetIdsAsync(
        int? excludeOutboundRecordId,
        int? excludeDisposalRecordId)
    {
        var outboundLocked = await _dbContext.NetworkOutboundItems
            .AsNoTracking()
            .Where(item =>
                ActiveStatuses.Contains(item.OutboundRecord!.Status)
                && (!excludeOutboundRecordId.HasValue || item.OutboundRecordId != excludeOutboundRecordId.Value))
            .Select(item => item.OnNetAssetId)
            .ToListAsync();

        var disposalLocked = await _dbContext.NetworkOnNetDisposalItems
            .AsNoTracking()
            .Where(item =>
                ActiveStatuses.Contains(item.DisposalRecord!.Status)
                && (!excludeDisposalRecordId.HasValue || item.DisposalRecordId != excludeDisposalRecordId.Value))
            .Select(item => item.OnNetAssetId)
            .ToListAsync();

        return outboundLocked.Concat(disposalLocked).ToHashSet();
    }
}
