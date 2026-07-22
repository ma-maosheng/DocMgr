using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive;

public sealed class ArchiveFilingFactRepository : IArchiveFilingFactRepository
{
    private readonly AppDbContext _dbContext;

    public ArchiveFilingFactRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<string?> GetLastFilingFactNoByPrefixAsync(string prefix)
    {
        return _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.FilingFactNo.StartsWith(prefix))
            .OrderByDescending(fact => fact.FilingFactNo)
            .Select(fact => fact.FilingFactNo)
            .FirstOrDefaultAsync();
    }

    public void AddFilingFacts(IEnumerable<YearlyArchiveFilingFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        _dbContext.YearlyArchiveFilingFacts.AddRange(facts);
    }

    public Task<bool> ExistsBySourceLinkAsync(string sourceLinkType, int sourceLinkId)
    {
        return _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .AnyAsync(fact => fact.SourceLinkType == sourceLinkType && fact.SourceLinkId == sourceLinkId);
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    public async Task BackfillFromExistingLinksAsync()
    {
        var existingKeys = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Select(fact => new { fact.SourceLinkType, fact.SourceLinkId })
            .ToListAsync();
        var existingSet = existingKeys
            .Select(key => $"{key.SourceLinkType}:{key.SourceLinkId}")
            .ToHashSet(StringComparer.Ordinal);

        var boxLinks = await _dbContext.YearlyArchiveBoxMediaItemLinks
            .AsNoTracking()
            .Include(link => link.ArchiveBox)
            .Include(link => link.MediaItem)
                .ThenInclude(item => item!.MediaEntry)
                    .ThenInclude(media => media!.RegisterRecord)
            .ToListAsync();

        foreach (var link in boxLinks)
        {
            string key = $"{FilingFactSourceLinkType.BoxMediaItemLink}:{link.Id}";
            if (existingSet.Contains(key))
            {
                continue;
            }

            _dbContext.YearlyArchiveFilingFacts.Add(BuildSimulatedFactFromLink(link, link.ArchiveBox, link.MediaItem));
            existingSet.Add(key);
        }

        var electronicLinks = await _dbContext.YearlyElectronicArchiveUnitMediaItemLinks
            .AsNoTracking()
            .Include(link => link.ElectronicArchiveUnit)
            .Include(link => link.MediaItem)
                .ThenInclude(item => item!.MediaEntry)
                    .ThenInclude(media => media!.RegisterRecord)
            .ToListAsync();

        foreach (var link in electronicLinks)
        {
            string key = $"{FilingFactSourceLinkType.ElectronicMediaItemLink}:{link.Id}";
            if (existingSet.Contains(key))
            {
                continue;
            }

            _dbContext.YearlyArchiveFilingFacts.Add(
                BuildElectronicFactFromLink(link, link.ElectronicArchiveUnit, link.MediaItem));
            existingSet.Add(key);
        }

        await AssignMissingFilingFactNumbersAsync();
        await _dbContext.SaveChangesAsync();
    }

    public Task<List<YearlyArchiveFilingFact>> SearchByRegisterCriteriaAsync(
        string mediaKind,
        RegisterDirectionSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        IQueryable<YearlyArchiveFilingFact> query = _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.MediaKind == mediaKind);

        if (!string.IsNullOrWhiteSpace(criteria.Year))
        {
            string year = criteria.Year.Trim();
            query = query.Where(fact => fact.FiledAt.Year.ToString() == year
                                        || fact.ContainerCode.Contains(year));
        }

        if (criteria.ProjectId.HasValue)
        {
            query = query.Where(fact => fact.ProjectId == criteria.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.LifecycleStatus))
        {
            string lifecycleStatus = criteria.LifecycleStatus.Trim();
            query = query.Where(fact => fact.LifecycleStatus == lifecycleStatus);

            // 「在库」兜底：所属容器已非 InUse（已清空/销号等）时不作为在库命中。
            if (string.Equals(lifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal))
            {
                if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
                {
                    query = query.Where(fact =>
                        fact.ContainerId <= 0
                        || !_dbContext.YearlyArchiveBoxes.Any(box =>
                            box.Id == fact.ContainerId
                            && box.ContainerLifecycleStatus != ArchiveContainerLifecycleStatus.InUse));
                }
                else if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
                {
                    query = query.Where(fact =>
                        fact.ContainerId <= 0
                        || !_dbContext.YearlyElectronicArchiveUnits.Any(unit =>
                            unit.Id == fact.ContainerId
                            && unit.UnitLifecycleStatus != ArchiveContainerLifecycleStatus.InUse));
                }
            }
        }

        if (criteria.FiledFrom.HasValue)
        {
            query = query.Where(fact => fact.FiledAt >= criteria.FiledFrom.Value);
        }

        if (criteria.FiledTo.HasValue)
        {
            DateTime end = criteria.FiledTo.Value.Date.AddDays(1);
            query = query.Where(fact => fact.FiledAt < end);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            string keyword = criteria.Keyword.Trim();
            query = query.Where(fact =>
                fact.FormNo.Contains(keyword) ||
                fact.MaterialName.Contains(keyword) ||
                fact.ItemName.Contains(keyword) ||
                fact.ConfidentialLevel.Contains(keyword) ||
                fact.ProjectName.Contains(keyword) ||
                fact.ProvideUnit.Contains(keyword) ||
                fact.ApplicantName.Contains(keyword) ||
                fact.ContainerCode.Contains(keyword) ||
                fact.StorageLocation.Contains(keyword) ||
                fact.MediumCode.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(criteria.ConfidentialLevel))
        {
            string confidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(criteria.ConfidentialLevel);
            query = query.Where(fact => fact.ConfidentialLevel == confidentialLevel);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ContentEntryKeyword)
            && string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
        {
            string likePattern = SearchWildcardPatternSupport.ToSqlLikePattern(criteria.ContentEntryKeyword);
            string entryKindFilter = criteria.ContentEntryKindFilter?.Trim() ?? string.Empty;

            query = query.Where(fact =>
                _dbContext.YearlyArchiveRegisterElectronicMediaItemEntries.Any(entry =>
                    entry.ElectronicMediaItemDetailId == fact.MediaItemId
                    && (entryKindFilter == string.Empty || entry.EntryKind == entryKindFilter)
                    && EF.Functions.Like(
                        entry.EntryName,
                        likePattern,
                        SearchWildcardPatternSupport.EscapeCharacterString)));
        }

        return query
            .OrderByDescending(fact => fact.FiledAt)
            .ThenByDescending(fact => fact.Id)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveFilingFact>> SearchLedgerAsync(FilingLedgerSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        IQueryable<YearlyArchiveFilingFact> query = _dbContext.YearlyArchiveFilingFacts.AsNoTracking();

        if (criteria.FilingFactId is int filingFactId and > 0)
        {
            return query
                .Where(fact => fact.Id == filingFactId)
                .OrderByDescending(fact => fact.FiledAt)
                .ThenByDescending(fact => fact.Id)
                .ToListAsync();
        }

        if (!string.IsNullOrWhiteSpace(criteria.MediaKind))
        {
            string mediaKind = criteria.MediaKind.Trim();
            query = query.Where(fact => fact.MediaKind == mediaKind);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Year))
        {
            string year = criteria.Year.Trim();
            if (int.TryParse(year, out int filingYear))
            {
                query = query.Where(fact => fact.FiledAt.Year == filingYear);
            }
        }

        if (criteria.ProjectId.HasValue)
        {
            query = query.Where(fact => fact.ProjectId == criteria.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.LifecycleStatus))
        {
            query = query.Where(fact => fact.LifecycleStatus == criteria.LifecycleStatus);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ArchiveCopyRole))
        {
            query = query.Where(fact => fact.ArchiveCopyRole == criteria.ArchiveCopyRole);
        }

        if (criteria.FiledFrom.HasValue)
        {
            query = query.Where(fact => fact.FiledAt >= criteria.FiledFrom.Value);
        }

        if (criteria.FiledTo.HasValue)
        {
            DateTime end = criteria.FiledTo.Value.Date.AddDays(1);
            query = query.Where(fact => fact.FiledAt < end);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            string keyword = criteria.Keyword.Trim();
            query = query.Where(fact =>
                fact.FilingFactNo.Contains(keyword) ||
                fact.FormNo.Contains(keyword) ||
                fact.MaterialName.Contains(keyword) ||
                fact.ItemName.Contains(keyword) ||
                fact.ConfidentialLevel.Contains(keyword) ||
                fact.ProjectName.Contains(keyword) ||
                fact.ProvideUnit.Contains(keyword) ||
                fact.ApplicantName.Contains(keyword) ||
                fact.ContainerCode.Contains(keyword) ||
                fact.CurrentContainerCode.Contains(keyword) ||
                fact.StorageLocation.Contains(keyword) ||
                fact.CurrentStorageLocation.Contains(keyword) ||
                fact.MediumCode.Contains(keyword) ||
                fact.FilingStoragePath.Contains(keyword) ||
                fact.CabinetName.Contains(keyword) ||
                fact.BoxLocationCode.Contains(keyword));
        }

        return query
            .OrderByDescending(fact => fact.FiledAt)
            .ThenByDescending(fact => fact.Id)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterMediaItem>> GetRegisterMediaItemsWithSupplementsAsync(
        IReadOnlyCollection<int> mediaItemIds)
    {
        if (mediaItemIds == null || mediaItemIds.Count == 0)
        {
            return Task.FromResult(new List<YearlyArchiveRegisterMediaItem>());
        }

        return _dbContext.YearlyArchiveRegisterMediaItems
            .AsNoTracking()
            .Where(item => mediaItemIds.Contains(item.Id))
            .Include(item => item.MediaEntry)
            .Include(item => item.ElectronicDetail)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterMedia>> GetRegisterMediasByIdsAsync(
        IReadOnlyCollection<int> registerMediaIds)
    {
        if (registerMediaIds == null || registerMediaIds.Count == 0)
        {
            return Task.FromResult(new List<YearlyArchiveRegisterMedia>());
        }

        return _dbContext.YearlyArchiveRegisterMedias
            .AsNoTracking()
            .Where(media => registerMediaIds.Contains(media.Id))
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetArchivePurposesByRegisterRecordIdsAsync(
        IReadOnlyCollection<int> registerRecordIds)
    {
        if (registerRecordIds == null || registerRecordIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var rows = await _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Where(record => registerRecordIds.Contains(record.Id))
            .Select(record => new { record.Id, record.ArchivePurpose })
            .ToListAsync();

        return rows.ToDictionary(
            record => record.Id,
            record => record.ArchivePurpose?.Trim() ?? string.Empty);
    }

    public Task<List<YearlyArchiveRegisterElectronicMediaItemEntry>> GetElectronicContentEntriesByMediaItemIdsAsync(
        IReadOnlyCollection<int> mediaItemIds)
    {
        if (mediaItemIds == null || mediaItemIds.Count == 0)
        {
            return Task.FromResult(new List<YearlyArchiveRegisterElectronicMediaItemEntry>());
        }

        return _dbContext.YearlyArchiveRegisterElectronicMediaItemEntries
            .AsNoTracking()
            .Where(entry => mediaItemIds.Contains(entry.ElectronicMediaItemDetailId))
            .OrderBy(entry => entry.ElectronicMediaItemDetailId)
            .ThenBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.EntryName)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterElectronicMediaItemEntry>> GetElectronicContentEntriesByIdsAsync(
        IReadOnlyCollection<int> entryIds)
    {
        if (entryIds == null || entryIds.Count == 0)
        {
            return Task.FromResult(new List<YearlyArchiveRegisterElectronicMediaItemEntry>());
        }

        return _dbContext.YearlyArchiveRegisterElectronicMediaItemEntries
            .AsNoTracking()
            .Where(entry => entryIds.Contains(entry.Id))
            .OrderBy(entry => entry.ElectronicMediaItemDetailId)
            .ThenBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.EntryName)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveFilingFact>> SearchByContainerCriteriaAsync(
        string mediaKind,
        ContainerDirectionSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        IQueryable<YearlyArchiveFilingFact> query = _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.MediaKind == mediaKind);

        if (!string.IsNullOrWhiteSpace(criteria.Year))
        {
            string year = criteria.Year.Trim();
            query = query.Where(fact => fact.FiledAt.Year.ToString() == year
                                        || fact.ContainerCode.Contains(year));
        }

        if (!string.IsNullOrWhiteSpace(criteria.ContainerCode))
        {
            string code = criteria.ContainerCode.Trim();
            query = criteria.SearchCurrentLocation
                ? query.Where(fact =>
                    fact.CurrentContainerCode.Contains(code) || fact.ContainerCode.Contains(code))
                : query.Where(fact => fact.ContainerCode.Contains(code));
        }

        if (!string.IsNullOrWhiteSpace(criteria.StorageLocation))
        {
            string location = criteria.StorageLocation.Trim();
            query = criteria.SearchCurrentLocation
                ? query.Where(fact =>
                    fact.CurrentStorageLocation.Contains(location) || fact.StorageLocation.Contains(location))
                : query.Where(fact =>
                    fact.StorageLocation.Contains(location) || fact.BoxLocationCode.Contains(location));
        }

        if (!string.IsNullOrWhiteSpace(criteria.MediumCode))
        {
            string mediumCode = criteria.MediumCode.Trim();
            query = query.Where(fact => fact.MediumCode.Contains(mediumCode));
        }

        if (!string.IsNullOrWhiteSpace(criteria.StorageCarrierType))
        {
            string carrier = criteria.StorageCarrierType.Trim();
            query = query.Where(fact => fact.StorageCarrierType == carrier);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            string keyword = criteria.Keyword.Trim();
            query = query.Where(fact =>
                fact.ItemName.Contains(keyword) ||
                fact.ContainerCode.Contains(keyword) ||
                fact.StorageLocation.Contains(keyword) ||
                fact.MediumCode.Contains(keyword) ||
                fact.FilingStoragePath.Contains(keyword));
        }

        return query
            .OrderByDescending(fact => fact.FiledAt)
            .ThenByDescending(fact => fact.Id)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveFilingFact>> GetFactsByMediaItemIdsAsync(IReadOnlyCollection<int> mediaItemIds)
    {
        if (mediaItemIds == null || mediaItemIds.Count == 0)
        {
            return Task.FromResult(new List<YearlyArchiveFilingFact>());
        }

        return _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => mediaItemIds.Contains(fact.MediaItemId))
            .OrderByDescending(fact => fact.FiledAt)
            .ThenByDescending(fact => fact.Id)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveFilingFact>> GetFactsByIdsAsync(IReadOnlyCollection<int> filingFactIds)
    {
        if (filingFactIds == null || filingFactIds.Count == 0)
        {
            return Task.FromResult(new List<YearlyArchiveFilingFact>());
        }

        return _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => filingFactIds.Contains(fact.Id))
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<int, int>> GetRegisterMediaStockCountsByIdsAsync(
        IReadOnlyCollection<int> registerMediaIds)
    {
        if (registerMediaIds == null || registerMediaIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var rows = await _dbContext.YearlyArchiveRegisterMedias
            .AsNoTracking()
            .Where(media => registerMediaIds.Contains(media.Id))
            .Select(media => new { media.Id, media.MediaCount })
            .ToListAsync();

        return rows.ToDictionary(row => row.Id, row => row.MediaCount);
    }

    public Task<List<YearlyArchiveFilingFact>> GetBackupFactsByPrimaryIdsAsync(IReadOnlyCollection<int> primaryFilingFactIds)
    {
        if (primaryFilingFactIds == null || primaryFilingFactIds.Count == 0)
        {
            return Task.FromResult(new List<YearlyArchiveFilingFact>());
        }

        return _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.PrimaryFilingFactId != null
                           && primaryFilingFactIds.Contains(fact.PrimaryFilingFactId.Value))
            .OrderBy(fact => fact.PrimaryFilingFactId)
            .ThenBy(fact => fact.FiledAt)
            .ThenBy(fact => fact.Id)
            .ToListAsync();
    }

    public Task<string?> GetLastResultSetNoByPrefixAsync(string prefix)
    {
        return _dbContext.YearlyArchiveSearchResultSets
            .AsNoTracking()
            .Where(set => set.ResultSetNo.StartsWith(prefix))
            .OrderByDescending(set => set.ResultSetNo)
            .Select(set => set.ResultSetNo)
            .FirstOrDefaultAsync();
    }

    public void AddResultSet(YearlyArchiveSearchResultSet resultSet)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        _dbContext.YearlyArchiveSearchResultSets.Add(resultSet);
    }

    public async Task<List<SearchPoolListItem>> SearchResultSetsAsync(
        string mediaKind,
        SearchPoolListCriteria criteria,
        int currentUserId,
        bool isArchiveAdmin)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        IQueryable<YearlyArchiveSearchResultSet> query = _dbContext.YearlyArchiveSearchResultSets
            .AsNoTracking()
            .Where(set => set.MediaKind == mediaKind);

        if (!isArchiveAdmin || criteria.OnlyMine)
        {
            query = query.Where(set => set.CreatedByUserId == currentUserId);
        }

        string keyword = criteria.Keyword?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(set =>
                set.Name.Contains(keyword)
                || set.ResultSetNo.Contains(keyword)
                || set.Remarks.Contains(keyword)
                || set.CreatedByName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Status))
        {
            query = query.Where(set => set.Status == criteria.Status);
        }

        var rows = await query
            .OrderByDescending(set => set.UpdatedAt ?? set.CreatedAt)
            .ThenByDescending(set => set.Id)
            .Select(set => new SearchPoolListItem
            {
                Id = set.Id,
                ResultSetNo = set.ResultSetNo,
                Name = set.Name,
                MediaKind = set.MediaKind,
                Status = set.Status,
                StatusDisplay = MapResultSetStatus(set.Status),
                CreatedByName = set.CreatedByName,
                CreatedAt = set.CreatedAt,
                UpdatedAt = set.UpdatedAt,
                Remarks = set.Remarks,
                ItemCount = set.Items.Count
            })
            .ToListAsync();

        return rows;
    }

    public Task<YearlyArchiveSearchResultSet?> GetResultSetWithItemsAsync(int resultSetId)
    {
        return _dbContext.YearlyArchiveSearchResultSets
            .Include(set => set.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
            .FirstOrDefaultAsync(set => set.Id == resultSetId);
    }

    public async Task<bool> DeleteResultSetAsync(int resultSetId)
    {
        var resultSet = await _dbContext.YearlyArchiveSearchResultSets
            .FirstOrDefaultAsync(set => set.Id == resultSetId);

        if (resultSet == null)
        {
            return false;
        }

        _dbContext.YearlyArchiveSearchResultSets.Remove(resultSet);
        return true;
    }

    public Task SaveResultSetChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    public Task<int> CountUserResultSetsByMediaKindAsync(int userId, string mediaKind)
    {
        return _dbContext.YearlyArchiveSearchResultSets
            .AsNoTracking()
            .CountAsync(set => set.CreatedByUserId == userId && set.MediaKind == mediaKind);
    }

    public Task<List<YearlyArchiveSearchResultSet>> GetOldestUserResultSetsByMediaKindAsync(
        int userId,
        string mediaKind,
        int count)
    {
        if (count <= 0)
        {
            return Task.FromResult(new List<YearlyArchiveSearchResultSet>());
        }

        return _dbContext.YearlyArchiveSearchResultSets
            .Where(set => set.CreatedByUserId == userId && set.MediaKind == mediaKind)
            .OrderBy(set => set.CreatedAt)
            .ThenBy(set => set.Id)
            .Take(count)
            .ToListAsync();
    }

    public async Task UpdateFilingFactLifecycleAsync(
        int filingFactId,
        string lifecycleStatus,
        string borrowHintLevel,
        string borrowHintText,
        string operatedBy)
    {
        var fact = await _dbContext.YearlyArchiveFilingFacts
            .FirstOrDefaultAsync(item => item.Id == filingFactId);

        if (fact == null)
        {
            return;
        }

        fact.LifecycleStatus = lifecycleStatus;
        fact.BorrowHintLevel = borrowHintLevel;
        fact.BorrowHintText = borrowHintText;
        fact.BorrowHintUpdatedAt = DateTime.Now;
        fact.LifecycleUpdatedAt = DateTime.Now;
        fact.LifecycleRemark = $"资料出库：{operatedBy}";
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateFilingFactLifecyclesAsync(
        IReadOnlyList<FilingFactLifecycleUpdate> updates,
        string operatedBy,
        string? businessLabel = null)
    {
        if (updates.Count == 0)
        {
            return;
        }

        var ids = updates.Select(update => update.FilingFactId).Distinct().ToList();
        var facts = await _dbContext.YearlyArchiveFilingFacts
            .Where(fact => ids.Contains(fact.Id))
            .ToDictionaryAsync(fact => fact.Id);

        DateTime now = DateTime.Now;
        string label = string.IsNullOrWhiteSpace(businessLabel) ? "资料出库" : businessLabel.Trim();
        string defaultLifecycleRemark = $"{label}：{operatedBy}";

        foreach (var update in updates)
        {
            if (!facts.TryGetValue(update.FilingFactId, out var fact))
            {
                continue;
            }

            fact.LifecycleStatus = update.LifecycleStatus;
            fact.BorrowHintLevel = update.BorrowHintLevel;
            fact.BorrowHintText = update.BorrowHintText;
            fact.BorrowHintUpdatedAt = now;
            fact.LifecycleUpdatedAt = now;
            fact.LifecycleRemark = string.IsNullOrWhiteSpace(update.LifecycleRemark)
                ? defaultLifecycleRemark
                : $"{update.LifecycleRemark.Trim()}（{operatedBy}）";
        }

        await _dbContext.SaveChangesAsync();
    }

    private static string MapResultSetStatus(string status) => status switch
    {
        ArchiveSearchResultSetStatus.Draft => "草稿",
        ArchiveSearchResultSetStatus.Confirmed => "已确认",
        ArchiveSearchResultSetStatus.Referenced => "已引用",
        _ => status
    };

    private async Task AssignMissingFilingFactNumbersAsync()
    {
        var factsWithoutNo = await _dbContext.YearlyArchiveFilingFacts
            .Where(fact => fact.FilingFactNo == string.Empty)
            .OrderBy(fact => fact.FiledAt)
            .ThenBy(fact => fact.Id)
            .ToListAsync();

        foreach (var group in factsWithoutNo.GroupBy(fact => new { fact.MediaKind, Year = fact.FiledAt.Year }))
        {
            string prefix = $"立档-{group.Key.MediaKind}-{group.Key.Year}-";
            string? lastNo = await GetLastFilingFactNoByPrefixAsync(prefix);
            int nextSequence = ParseSequence(lastNo, prefix);

            foreach (var fact in group)
            {
                fact.FilingFactNo = $"{prefix}{nextSequence:D6}";
                nextSequence++;
            }
        }
    }

    private static int ParseSequence(string? lastNo, string prefix)
    {
        if (string.IsNullOrWhiteSpace(lastNo) || lastNo.Length <= prefix.Length)
        {
            return 1;
        }

        return int.TryParse(lastNo[prefix.Length..], out int parsed) && parsed > 0
            ? parsed + 1
            : 1;
    }

    internal static YearlyArchiveFilingFact BuildSimulatedFactFromLink(
        YearlyArchiveBoxMediaItemLink link,
        YearlyArchiveBox box,
        YearlyArchiveRegisterMediaItem mediaItem)
    {
        var record = mediaItem.MediaEntry?.RegisterRecord;
        var mediaEntry = mediaItem.MediaEntry;
        DateTime filedAt = link.CreatedAt == default ? box.ArchivedDate : link.CreatedAt;

        return new YearlyArchiveFilingFact
        {
            FilingFactNo = string.Empty,
            MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
            RegisterRecordId = record?.Id ?? 0,
            RegisterMediaId = mediaEntry?.Id ?? mediaItem.YearlyArchiveRegisterMediaId,
            MediaItemId = mediaItem.Id,
            FormNo = record?.FormNo?.Trim() ?? string.Empty,
            MaterialName = record?.MaterialName?.Trim() ?? string.Empty,
            ProjectId = record?.ProjectId,
            ProjectName = record?.ProjectName?.Trim() ?? box.ProjectName?.Trim() ?? string.Empty,
            ProvideUnit = record?.ProvideUnit?.Trim() ?? string.Empty,
            ApplicantName = record?.ApplicantName?.Trim() ?? string.Empty,
            ItemType = mediaItem.ItemType?.Trim() ?? string.Empty,
            ItemName = mediaItem.ContentDesc?.Trim() ?? string.Empty,
            ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(mediaItem.ConfidentialLevel),
            ContentCount = mediaItem.ContentCount,
            ContainerKind = ArchiveContainerKind.ArchiveBox,
            ContainerId = box.Id,
            ContainerCode = box.ArchiveSequenceNo?.Trim() ?? string.Empty,
            StorageLocation = box.BoxLocationCode?.Trim() ?? string.Empty,
            CabinetName = box.CabinetName?.Trim() ?? string.Empty,
            BoxLocationCode = box.BoxLocationCode?.Trim() ?? string.Empty,
            BoxSpecs = box.Specs?.Trim() ?? string.Empty,
            FiledAt = filedAt,
            FiledBy = box.ArchivedBy?.Trim() ?? string.Empty,
            SourceLinkType = FilingFactSourceLinkType.BoxMediaItemLink,
            SourceLinkId = link.Id,
            LifecycleStatus = FilingFactLifecycleStatus.InArchive,
            CurrentContainerCode = box.ArchiveSequenceNo?.Trim() ?? string.Empty,
            CurrentStorageLocation = box.BoxLocationCode?.Trim() ?? string.Empty,
            BorrowHintLevel = FilingFactBorrowHintLevel.Unknown,
            ArchiveCopyRole = FilingFactArchiveCopyRole.Original,
            CreatedAt = DateTime.Now
        };
    }

    internal static YearlyArchiveFilingFact BuildElectronicFactFromLink(
        YearlyElectronicArchiveUnitMediaItemLink link,
        YearlyElectronicArchiveUnit unit,
        YearlyArchiveRegisterMediaItem mediaItem)
    {
        var record = mediaItem.MediaEntry?.RegisterRecord;
        var mediaEntry = mediaItem.MediaEntry;
        DateTime filedAt = link.CreatedAt == default ? unit.ArchivedDate : link.CreatedAt;

        return new YearlyArchiveFilingFact
        {
            FilingFactNo = string.Empty,
            MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
            RegisterRecordId = record?.Id ?? 0,
            RegisterMediaId = mediaEntry?.Id ?? mediaItem.YearlyArchiveRegisterMediaId,
            MediaItemId = mediaItem.Id,
            FormNo = link.FormNo?.Trim() ?? record?.FormNo?.Trim() ?? string.Empty,
            MaterialName = link.MaterialName?.Trim() ?? record?.MaterialName?.Trim() ?? string.Empty,
            ProjectId = record?.ProjectId,
            ProjectName = record?.ProjectName?.Trim() ?? unit.ProjectName?.Trim() ?? string.Empty,
            ProvideUnit = record?.ProvideUnit?.Trim() ?? string.Empty,
            ApplicantName = record?.ApplicantName?.Trim() ?? string.Empty,
            ItemType = mediaItem.ItemType?.Trim() ?? string.Empty,
            ItemName = link.ItemName?.Trim() ?? mediaItem.ContentDesc?.Trim() ?? string.Empty,
            ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(mediaItem.ConfidentialLevel),
            ContentCount = mediaItem.ContentCount,
            ContainerKind = ArchiveContainerKind.ElectronicBag,
            ContainerId = unit.Id,
            ContainerCode = unit.ElectronicArchiveNo?.Trim() ?? string.Empty,
            StorageLocation = unit.StorageLocation?.Trim() ?? string.Empty,
            StorageCarrierType = unit.StorageCarrierType?.Trim() ?? string.Empty,
            Disposition = unit.Disposition?.Trim() ?? mediaEntry?.Disposition?.Trim() ?? string.Empty,
            MediumCode = link.MediumCode?.Trim() ?? string.Empty,
            FilingStoragePath = link.FilingStoragePath?.Trim() ?? string.Empty,
            DataSizeMb = link.DataSizeMb,
            FiledAt = filedAt,
            FiledBy = unit.ArchivedBy?.Trim() ?? string.Empty,
            SourceLinkType = FilingFactSourceLinkType.ElectronicMediaItemLink,
            SourceLinkId = link.Id,
            LifecycleStatus = FilingFactLifecycleStatus.InArchive,
            CurrentContainerCode = unit.ElectronicArchiveNo?.Trim() ?? string.Empty,
            CurrentStorageLocation = unit.StorageLocation?.Trim() ?? string.Empty,
            BorrowHintLevel = FilingFactBorrowHintLevel.Unknown,
            ArchiveCopyRole = FilingFactArchiveCopyRole.Original,
            CreatedAt = DateTime.Now
        };
    }
}
