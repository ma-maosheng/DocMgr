using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Inventory;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.Inventory;

/// <summary>
/// 库管资料总览仓储：按档案盒 / 电子介质袋 / 历史存档盒组装只读投影。
/// </summary>
public sealed class StockContainerOverviewRepository : IStockContainerOverviewRepository
{
    private const int ContentSummaryMaxItems = 5;

    private readonly AppDbContext _dbContext;

    public StockContainerOverviewRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<int>> GetDistinctYearsAsync()
    {
        var boxYears = await _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Select(box => box.Year)
            .Where(year => year != null && year != string.Empty)
            .Distinct()
            .ToListAsync();

        var bagYears = await _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .Select(unit => unit.Year)
            .Where(year => year != null && year != string.Empty)
            .Distinct()
            .ToListAsync();

        return boxYears
            .Concat(bagYears)
            .Select(ParseYear)
            .Where(year => year.HasValue)
            .Select(year => year!.Value)
            .Distinct()
            .OrderByDescending(year => year)
            .ToList();
    }

    public async Task<IReadOnlyList<StockContainerOverviewRow>> SearchContainersAsync(
        StockContainerSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var kinds = ResolveKinds(criteria.ContainerKinds);
        string keyword = criteria.Keyword?.Trim() ?? string.Empty;
        bool keywordCaseSensitive = criteria.KeywordCaseSensitive;
        string locationKeyword = criteria.StorageLocationKeyword?.Trim() ?? string.Empty;
        string? year = string.IsNullOrWhiteSpace(criteria.Year) ? null : criteria.Year.Trim();
        string lifecycleFilter = criteria.LifecycleFilter?.Trim() ?? string.Empty;
        bool includeInactive = criteria.IncludeInactive
            || string.Equals(lifecycleFilter, StockContainerLifecycleDisplay.FilterInactive, StringComparison.Ordinal);
        DateTime? archivedFrom = criteria.ArchivedFrom?.Date;
        DateTime? archivedToExclusive = criteria.ArchivedTo?.Date.AddDays(1);

        HashSet<int> yearlyBoxContentHits = new();
        HashSet<int> electronicBagContentHits = new();
        HashSet<int> historyBoxContentHits = new();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            await CollectContentHitsAsync(
                keyword,
                keywordCaseSensitive,
                criteria.IncludeElectronicEntryNames,
                kinds,
                yearlyBoxContentHits,
                electronicBagContentHits,
                historyBoxContentHits);
        }

        var rows = new List<StockContainerOverviewRow>();

        if (kinds.Contains(StockContainerKind.YearlyBox))
        {
            rows.AddRange(await SearchYearlyBoxesAsync(
                keyword,
                keywordCaseSensitive,
                locationKeyword,
                year,
                includeInactive,
                archivedFrom,
                archivedToExclusive,
                yearlyBoxContentHits));
        }

        if (kinds.Contains(StockContainerKind.ElectronicBag))
        {
            rows.AddRange(await SearchElectronicBagsAsync(
                keyword,
                keywordCaseSensitive,
                locationKeyword,
                year,
                includeInactive,
                archivedFrom,
                archivedToExclusive,
                electronicBagContentHits));
        }

        // 历史盒无容器级年度字段；选定档案年度时仅查年度盒/袋。
        if (kinds.Contains(StockContainerKind.HistoryBox) && year == null)
        {
            rows.AddRange(await SearchHistoryBoxesAsync(
                keyword,
                keywordCaseSensitive,
                locationKeyword,
                includeInactive,
                archivedFrom,
                archivedToExclusive,
                historyBoxContentHits));
        }

        IEnumerable<StockContainerOverviewRow> filtered = rows;
        if (string.Equals(lifecycleFilter, StockContainerLifecycleDisplay.FilterActive, StringComparison.Ordinal))
        {
            filtered = filtered.Where(row => !row.IsInactive);
        }
        else if (string.Equals(lifecycleFilter, StockContainerLifecycleDisplay.FilterHasBorrowed, StringComparison.Ordinal))
        {
            filtered = filtered.Where(row => row.HasBorrowedContent);
        }
        else if (string.Equals(lifecycleFilter, StockContainerLifecycleDisplay.FilterInactive, StringComparison.Ordinal))
        {
            filtered = filtered.Where(row => row.IsInactive);
        }
        else if (!includeInactive)
        {
            filtered = filtered.Where(row => !row.IsInactive);
        }

        return filtered
            .OrderByDescending(row => row.ArchivedDate)
            .ThenBy(row => row.ContainerKind, StringComparer.Ordinal)
            .ThenBy(row => row.ContainerCode, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<StockContainerContentItem>> GetContentsAsync(
        string containerKind,
        int containerId)
    {
        if (containerId <= 0 || string.IsNullOrWhiteSpace(containerKind))
        {
            return Array.Empty<StockContainerContentItem>();
        }

        string kind = containerKind.Trim();
        if (string.Equals(kind, StockContainerKind.YearlyBox, StringComparison.Ordinal))
        {
            return await GetYearlyFilingContentsAsync(ArchiveContainerKind.ArchiveBox, containerId, kind);
        }

        if (string.Equals(kind, StockContainerKind.ElectronicBag, StringComparison.Ordinal))
        {
            return await GetYearlyFilingContentsAsync(ArchiveContainerKind.ElectronicBag, containerId, kind);
        }

        if (string.Equals(kind, StockContainerKind.HistoryBox, StringComparison.Ordinal))
        {
            return await GetHistoryBoxContentsAsync(containerId);
        }

        return Array.Empty<StockContainerContentItem>();
    }

    public async Task<IReadOnlyList<YearlyArchiveRegisterElectronicMediaItemEntry>> GetElectronicContentEntriesAsync(
        int mediaItemId)
    {
        if (mediaItemId <= 0)
        {
            return Array.Empty<YearlyArchiveRegisterElectronicMediaItemEntry>();
        }

        return await _dbContext.YearlyArchiveRegisterElectronicMediaItemEntries
            .AsNoTracking()
            .Where(entry => entry.ElectronicMediaItemDetailId == mediaItemId)
            .OrderBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.Id)
            .ToListAsync();
    }

    private async Task CollectContentHitsAsync(
        string keyword,
        bool caseSensitive,
        bool includeElectronicEntryNames,
        HashSet<string> kinds,
        HashSet<int> yearlyBoxHits,
        HashSet<int> electronicBagHits,
        HashSet<int> historyBoxHits)
    {
        if (kinds.Contains(StockContainerKind.YearlyBox) || kinds.Contains(StockContainerKind.ElectronicBag))
        {
            // EF Contains 受库排序规则影响；先取候选再在内存按开关统一判定。
            var factCandidates = await _dbContext.YearlyArchiveFilingFacts
                .AsNoTracking()
                .Where(fact => fact.ContainerId > 0)
                .Select(fact => new
                {
                    fact.ContainerKind,
                    fact.ContainerId,
                    fact.MaterialName,
                    fact.ItemName,
                    fact.FormNo,
                    fact.FilingFactNo,
                    fact.MediumCode
                })
                .ToListAsync();

            foreach (var hit in factCandidates)
            {
                if (!ContainsText(hit.MaterialName, keyword, caseSensitive)
                    && !ContainsText(hit.ItemName, keyword, caseSensitive)
                    && !ContainsText(hit.FormNo, keyword, caseSensitive)
                    && !ContainsText(hit.FilingFactNo, keyword, caseSensitive)
                    && !ContainsText(hit.MediumCode, keyword, caseSensitive))
                {
                    continue;
                }

                if (hit.ContainerKind == ArchiveContainerKind.ArchiveBox)
                {
                    yearlyBoxHits.Add(hit.ContainerId);
                }
                else if (hit.ContainerKind == ArchiveContainerKind.ElectronicBag)
                {
                    electronicBagHits.Add(hit.ContainerId);
                }
            }

            if (includeElectronicEntryNames)
            {
                await CollectElectronicEntryNameHitsAsync(
                    keyword,
                    caseSensitive,
                    yearlyBoxHits,
                    electronicBagHits);
            }
        }

        if (!kinds.Contains(StockContainerKind.HistoryBox))
        {
            return;
        }

        var topoCandidates = await _dbContext.TopoMaps
            .AsNoTracking()
            .Select(row => new { row.Id, row.MapName, row.MapNumber, row.CurrentMapNumber, row.Category })
            .ToListAsync();
        var topoIds = topoCandidates
            .Where(row => ContainsText(row.MapName, keyword, caseSensitive)
                || ContainsText(row.MapNumber, keyword, caseSensitive)
                || ContainsText(row.CurrentMapNumber, keyword, caseSensitive)
                || ContainsText(row.Category, keyword, caseSensitive))
            .Select(row => row.Id)
            .ToList();

        var aerialCandidates = await _dbContext.AerialPhotos
            .AsNoTracking()
            .Select(row => new { row.Id, row.BoxContents, row.SurveyArea, row.Category })
            .ToListAsync();
        var aerialIds = aerialCandidates
            .Where(row => ContainsText(row.BoxContents, keyword, caseSensitive)
                || ContainsText(row.SurveyArea, keyword, caseSensitive)
                || ContainsText(row.Category, keyword, caseSensitive))
            .Select(row => row.Id)
            .ToList();

        var otherCandidates = await _dbContext.OtherMaps
            .AsNoTracking()
            .Select(row => new { row.Id, row.MapName, row.MaterialCategory, row.SequenceNumber, row.Category })
            .ToListAsync();
        var otherIds = otherCandidates
            .Where(row => ContainsText(row.MapName, keyword, caseSensitive)
                || ContainsText(row.MaterialCategory, keyword, caseSensitive)
                || ContainsText(row.SequenceNumber, keyword, caseSensitive)
                || ContainsText(row.Category, keyword, caseSensitive))
            .Select(row => row.Id)
            .ToList();

        if (topoIds.Count == 0 && aerialIds.Count == 0 && otherIds.Count == 0)
        {
            return;
        }

        var links = await _dbContext.HistoryArchiveBoxLedgerLinks
            .AsNoTracking()
            .Where(link =>
                (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindTopoMap
                    && topoIds.Contains(link.RecordId))
                || (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto
                    && aerialIds.Contains(link.RecordId))
                || (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindOtherMap
                    && otherIds.Contains(link.RecordId)))
            .Select(link => link.HistoryArchiveBoxId)
            .Distinct()
            .ToListAsync();

        foreach (int boxId in links)
        {
            historyBoxHits.Add(boxId);
        }
    }

    /// <summary>
    /// 按电子介质目录/文件明细名称反查容器：Entry → MediaItemId → FilingFact → 盒/袋。
    /// </summary>
    private async Task CollectElectronicEntryNameHitsAsync(
        string keyword,
        bool caseSensitive,
        HashSet<int> yearlyBoxHits,
        HashSet<int> electronicBagHits)
    {
        // SQL Contains 仅作候选缩小；大小写策略仍由内存 ContainsText 统一判定。
        var entryCandidates = await _dbContext.YearlyArchiveRegisterElectronicMediaItemEntries
            .AsNoTracking()
            .Where(entry => entry.EntryName.Contains(keyword))
            .Select(entry => new
            {
                entry.ElectronicMediaItemDetailId,
                entry.EntryName
            })
            .ToListAsync();

        var matchedMediaItemIds = entryCandidates
            .Where(entry => ContainsText(entry.EntryName, keyword, caseSensitive))
            .Select(entry => entry.ElectronicMediaItemDetailId)
            .Distinct()
            .ToList();

        if (matchedMediaItemIds.Count == 0)
        {
            return;
        }

        var factContainers = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.ContainerId > 0 && matchedMediaItemIds.Contains(fact.MediaItemId))
            .Select(fact => new { fact.ContainerKind, fact.ContainerId })
            .Distinct()
            .ToListAsync();

        foreach (var hit in factContainers)
        {
            if (hit.ContainerKind == ArchiveContainerKind.ArchiveBox)
            {
                yearlyBoxHits.Add(hit.ContainerId);
            }
            else if (hit.ContainerKind == ArchiveContainerKind.ElectronicBag)
            {
                electronicBagHits.Add(hit.ContainerId);
            }
        }
    }

    private async Task<List<StockContainerOverviewRow>> SearchYearlyBoxesAsync(
        string keyword,
        bool keywordCaseSensitive,
        string locationKeyword,
        string? year,
        bool includeInactive,
        DateTime? archivedFrom,
        DateTime? archivedToExclusive,
        HashSet<int> contentHits)
    {
        IQueryable<YearlyArchiveBox> query = _dbContext.YearlyArchiveBoxes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(year))
        {
            query = query.Where(box => box.Year == year);
        }

        if (!string.IsNullOrWhiteSpace(locationKeyword))
        {
            query = query.Where(box =>
                box.BoxLocationCode.Contains(locationKeyword)
                || box.LastStorageLocation.Contains(locationKeyword));
        }

        if (archivedFrom.HasValue)
        {
            query = query.Where(box => box.ArchivedDate >= archivedFrom.Value);
        }

        if (archivedToExclusive.HasValue)
        {
            query = query.Where(box => box.ArchivedDate < archivedToExclusive.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse);
        }

        var boxes = await query.ToListAsync();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            boxes = boxes
                .Where(box =>
                    contentHits.Contains(box.Id)
                    || ContainsText(box.ArchiveSequenceNo, keyword, keywordCaseSensitive)
                    || ContainsText(box.ProjectName, keyword, keywordCaseSensitive)
                    || ContainsText(box.BoxLocationCode, keyword, keywordCaseSensitive)
                    || ContainsText(box.LastStorageLocation, keyword, keywordCaseSensitive)
                    || ContainsText(box.Remarks, keyword, keywordCaseSensitive))
                .ToList();
        }

        if (boxes.Count == 0)
        {
            return new List<StockContainerOverviewRow>();
        }

        var boxIds = boxes.Select(box => box.Id).ToList();
        var factStats = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                && boxIds.Contains(fact.ContainerId))
            .Select(fact => new FactStat(
                fact.ContainerId,
                fact.MaterialName,
                fact.ItemName,
                fact.LifecycleStatus))
            .ToListAsync();

        var statsByBoxId = factStats
            .GroupBy(stat => stat.ContainerId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = new List<StockContainerOverviewRow>(boxes.Count);
        foreach (var box in boxes)
        {
            statsByBoxId.TryGetValue(box.Id, out var stats);
            stats ??= new List<FactStat>();
            int borrowedCount = stats.Count(stat =>
                string.Equals(stat.LifecycleStatus, FilingFactLifecycleStatus.Borrowed, StringComparison.Ordinal));
            int inArchiveCount = stats.Count(stat =>
                string.Equals(stat.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal));
            bool isInactive = !string.Equals(
                box.ContainerLifecycleStatus,
                ArchiveContainerLifecycleStatus.InUse,
                StringComparison.Ordinal);

            rows.Add(new StockContainerOverviewRow
            {
                ContainerKind = StockContainerKind.YearlyBox,
                ContainerId = box.Id,
                ContainerCode = box.ArchiveSequenceNo?.Trim() ?? string.Empty,
                ProjectOrCategory = box.ProjectName?.Trim() ?? string.Empty,
                Year = box.Year?.Trim() ?? string.Empty,
                StorageLocation = ResolveYearlyBoxLocation(box),
                LifecycleStatusDisplay = BuildYearlyLifecycleDisplay(
                    box.ContainerLifecycleStatus,
                    borrowedCount,
                    inArchiveCount),
                ItemCount = stats.Count,
                ContentSummary = BuildContentSummary(stats.Select(stat => PreferTitle(stat.MaterialName, stat.ItemName))),
                Specs = box.Specs?.Trim() ?? string.Empty,
                PlacementMode = box.PlacementMode?.Trim() ?? string.Empty,
                ArchivedBy = box.ArchivedBy?.Trim() ?? string.Empty,
                ArchivedDate = box.ArchivedDate,
                Remarks = box.Remarks?.Trim() ?? string.Empty,
                HasBorrowedContent = borrowedCount > 0,
                IsInactive = isInactive
            });
        }

        return rows;
    }

    private async Task<List<StockContainerOverviewRow>> SearchElectronicBagsAsync(
        string keyword,
        bool keywordCaseSensitive,
        string locationKeyword,
        string? year,
        bool includeInactive,
        DateTime? archivedFrom,
        DateTime? archivedToExclusive,
        HashSet<int> contentHits)
    {
        IQueryable<YearlyElectronicArchiveUnit> query = _dbContext.YearlyElectronicArchiveUnits.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(year))
        {
            query = query.Where(unit => unit.Year == year);
        }

        if (!string.IsNullOrWhiteSpace(locationKeyword))
        {
            query = query.Where(unit => unit.StorageLocation.Contains(locationKeyword));
        }

        if (archivedFrom.HasValue)
        {
            query = query.Where(unit => unit.ArchivedDate >= archivedFrom.Value);
        }

        if (archivedToExclusive.HasValue)
        {
            query = query.Where(unit => unit.ArchivedDate < archivedToExclusive.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(unit => unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse);
        }

        var units = await query.ToListAsync();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            units = units
                .Where(unit =>
                    contentHits.Contains(unit.Id)
                    || ContainsText(unit.ElectronicArchiveNo, keyword, keywordCaseSensitive)
                    || ContainsText(unit.ProjectName, keyword, keywordCaseSensitive)
                    || ContainsText(unit.StorageLocation, keyword, keywordCaseSensitive)
                    || ContainsText(unit.ContentSummary, keyword, keywordCaseSensitive)
                    || ContainsText(unit.LinkedMediumCodes, keyword, keywordCaseSensitive)
                    || ContainsText(unit.Remarks, keyword, keywordCaseSensitive))
                .ToList();
        }

        if (units.Count == 0)
        {
            return new List<StockContainerOverviewRow>();
        }

        var unitIds = units.Select(unit => unit.Id).ToList();
        var factStats = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.ContainerKind == ArchiveContainerKind.ElectronicBag
                && unitIds.Contains(fact.ContainerId))
            .Select(fact => new FactStat(
                fact.ContainerId,
                fact.MaterialName,
                fact.ItemName,
                fact.LifecycleStatus))
            .ToListAsync();

        var statsByUnitId = factStats
            .GroupBy(stat => stat.ContainerId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = new List<StockContainerOverviewRow>(units.Count);
        foreach (var unit in units)
        {
            statsByUnitId.TryGetValue(unit.Id, out var stats);
            stats ??= new List<FactStat>();
            int borrowedCount = stats.Count(stat =>
                string.Equals(stat.LifecycleStatus, FilingFactLifecycleStatus.Borrowed, StringComparison.Ordinal));
            int inArchiveCount = stats.Count(stat =>
                string.Equals(stat.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal));
            bool isInactive = !string.Equals(
                unit.UnitLifecycleStatus,
                ArchiveContainerLifecycleStatus.InUse,
                StringComparison.Ordinal);

            string summary = string.IsNullOrWhiteSpace(unit.ContentSummary)
                ? BuildContentSummary(stats.Select(stat => PreferTitle(stat.MaterialName, stat.ItemName)))
                : unit.ContentSummary.Trim();

            rows.Add(new StockContainerOverviewRow
            {
                ContainerKind = StockContainerKind.ElectronicBag,
                ContainerId = unit.Id,
                ContainerCode = unit.ElectronicArchiveNo?.Trim() ?? string.Empty,
                ProjectOrCategory = unit.ProjectName?.Trim() ?? string.Empty,
                Year = unit.Year?.Trim() ?? string.Empty,
                StorageLocation = unit.StorageLocation?.Trim() ?? string.Empty,
                LifecycleStatusDisplay = BuildYearlyLifecycleDisplay(
                    unit.UnitLifecycleStatus,
                    borrowedCount,
                    inArchiveCount),
                ItemCount = stats.Count,
                ContentSummary = summary,
                Specs = BuildElectronicBagSpecsDisplay(unit.LinkedMediumCodes),
                PlacementMode = string.Empty,
                ArchivedBy = unit.ArchivedBy?.Trim() ?? string.Empty,
                ArchivedDate = unit.ArchivedDate,
                Remarks = unit.Remarks?.Trim() ?? string.Empty,
                HasBorrowedContent = borrowedCount > 0,
                IsInactive = isInactive
            });
        }

        return rows;
    }

    private async Task<List<StockContainerOverviewRow>> SearchHistoryBoxesAsync(
        string keyword,
        bool keywordCaseSensitive,
        string locationKeyword,
        bool includeInactive,
        DateTime? archivedFrom,
        DateTime? archivedToExclusive,
        HashSet<int> contentHits)
    {
        IQueryable<HistoryArchiveBox> query = _dbContext.HistoryArchiveBoxes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(locationKeyword))
        {
            query = query.Where(box => box.BoxCode.Contains(locationKeyword));
        }

        if (archivedFrom.HasValue)
        {
            query = query.Where(box => box.ArchivedDate >= archivedFrom.Value);
        }

        if (archivedToExclusive.HasValue)
        {
            query = query.Where(box => box.ArchivedDate < archivedToExclusive.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(box =>
                box.LifecycleStatus != HistoryArchiveDisposalDomainValues.LifecycleDisposed);
        }

        var boxes = await query.ToListAsync();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            boxes = boxes
                .Where(box =>
                    contentHits.Contains(box.Id)
                    || ContainsText(box.BoxCode, keyword, keywordCaseSensitive)
                    || ContainsText(box.BoxSpecification, keyword, keywordCaseSensitive)
                    || ContainsText(box.Remarks, keyword, keywordCaseSensitive))
                .ToList();
        }

        if (boxes.Count == 0)
        {
            return new List<StockContainerOverviewRow>();
        }

        var boxIds = boxes.Select(box => box.Id).ToList();
        var links = await _dbContext.HistoryArchiveBoxLedgerLinks
            .AsNoTracking()
            .Where(link => boxIds.Contains(link.HistoryArchiveBoxId))
            .ToListAsync();

        var (titlesByBoxId, categoriesByBoxId) = await BuildHistoryTitlesAndCategoriesByBoxIdAsync(links);

        var rows = new List<StockContainerOverviewRow>(boxes.Count);
        foreach (var box in boxes)
        {
            titlesByBoxId.TryGetValue(box.Id, out var titles);
            titles ??= new List<string>();
            categoriesByBoxId.TryGetValue(box.Id, out var categories);
            bool isInactive = HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(box.LifecycleStatus);

            rows.Add(new StockContainerOverviewRow
            {
                ContainerKind = StockContainerKind.HistoryBox,
                ContainerId = box.Id,
                // 历史盒无独立容器编号（盒号即位置），总览「容器编号/年度」占位为「-」。
                ContainerCode = "-",
                ProjectOrCategory = BuildHistoryCategoryLabel(categories),
                Year = "-",
                StorageLocation = box.BoxCode?.Trim() ?? string.Empty,
                LifecycleStatusDisplay = NormalizeHistoryLifecycleDisplay(box.LifecycleStatus),
                ItemCount = titles.Count,
                ContentSummary = BuildContentSummary(titles),
                Specs = box.BoxSpecification?.Trim() ?? string.Empty,
                PlacementMode = box.PlacementMode?.Trim() ?? string.Empty,
                ArchivedBy = box.ArchivedBy?.Trim() ?? string.Empty,
                ArchivedDate = box.ArchivedDate,
                Remarks = box.Remarks?.Trim() ?? string.Empty,
                HasBorrowedContent = false,
                IsInactive = isInactive
            });
        }

        return rows;
    }

    private async Task<IReadOnlyList<StockContainerContentItem>> GetYearlyFilingContentsAsync(
        ArchiveContainerKind containerKind,
        int containerId,
        string stockKind)
    {
        var facts = await _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.ContainerKind == containerKind && fact.ContainerId == containerId)
            .OrderBy(fact => fact.FilingFactNo)
            .ToListAsync();

        return facts
            .Select(fact => new StockContainerContentItem
            {
                ContainerKind = stockKind,
                ContainerId = containerId,
                SourceEntity = StockContainerContentSourceEntity.FilingFact,
                SourceId = fact.Id,
                MaterialKindDisplay = fact.MediaKind?.Trim() ?? string.Empty,
                Title = fact.MaterialName?.Trim() ?? string.Empty,
                ItemName = fact.ItemName?.Trim() ?? string.Empty,
                ConfidentialLevel = fact.ConfidentialLevel?.Trim() ?? string.Empty,
                ContentCount = fact.ContentCount,
                LifecycleStatus = fact.LifecycleStatus?.Trim() ?? string.Empty,
                LifecycleStatusDisplay = MapFilingLifecycleDisplay(fact.LifecycleStatus),
                ArchiveCopyRoleDisplay = MapCopyRoleDisplay(fact.ArchiveCopyRole),
                MediumCode = fact.MediumCode?.Trim() ?? string.Empty,
                FormNo = fact.FormNo?.Trim() ?? string.Empty,
                ExtraInfo = fact.FilingFactNo?.Trim() ?? string.Empty,
                MediaItemId = string.Equals(
                    fact.MediaKind?.Trim(),
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    StringComparison.Ordinal)
                    ? fact.MediaItemId
                    : 0
            })
            .ToList();
    }

    private async Task<IReadOnlyList<StockContainerContentItem>> GetHistoryBoxContentsAsync(int containerId)
    {
        var links = await _dbContext.HistoryArchiveBoxLedgerLinks
            .AsNoTracking()
            .Where(link => link.HistoryArchiveBoxId == containerId)
            .OrderBy(link => link.Id)
            .ToListAsync();

        if (links.Count == 0)
        {
            return Array.Empty<StockContainerContentItem>();
        }

        var topoIds = links
            .Where(link => link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindTopoMap)
            .Select(link => link.RecordId)
            .Distinct()
            .ToList();
        var aerialIds = links
            .Where(link => link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto)
            .Select(link => link.RecordId)
            .Distinct()
            .ToList();
        var otherIds = links
            .Where(link => link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindOtherMap)
            .Select(link => link.RecordId)
            .Distinct()
            .ToList();

        var topoById = topoIds.Count == 0
            ? new Dictionary<int, TopoMap>()
            : await _dbContext.TopoMaps.AsNoTracking()
                .Where(row => topoIds.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id);
        var aerialById = aerialIds.Count == 0
            ? new Dictionary<int, AerialPhoto>()
            : await _dbContext.AerialPhotos.AsNoTracking()
                .Where(row => aerialIds.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id);
        var otherById = otherIds.Count == 0
            ? new Dictionary<int, OtherMap>()
            : await _dbContext.OtherMaps.AsNoTracking()
                .Where(row => otherIds.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id);

        var items = new List<StockContainerContentItem>(links.Count);
        foreach (var link in links)
        {
            if (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindTopoMap
                && topoById.TryGetValue(link.RecordId, out var topo))
            {
                items.Add(new StockContainerContentItem
                {
                    ContainerKind = StockContainerKind.HistoryBox,
                    ContainerId = containerId,
                    SourceEntity = StockContainerContentSourceEntity.TopoMap,
                    SourceId = topo.Id,
                    MaterialKindDisplay = "地形图",
                    Title = PreferTitle(topo.MapName, topo.MapNumber),
                    ItemName = topo.CurrentMapNumber?.Trim() ?? string.Empty,
                    ConfidentialLevel = string.Empty,
                    ContentCount = topo.SheetCount,
                    LifecycleStatus = topo.LifecycleStatus?.Trim() ?? string.Empty,
                    LifecycleStatusDisplay = NormalizeHistoryLifecycleDisplay(topo.LifecycleStatus),
                    ExtraInfo = topo.Scale?.Trim() ?? string.Empty
                });
            }
            else if (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto
                && aerialById.TryGetValue(link.RecordId, out var aerial))
            {
                items.Add(new StockContainerContentItem
                {
                    ContainerKind = StockContainerKind.HistoryBox,
                    ContainerId = containerId,
                    SourceEntity = StockContainerContentSourceEntity.AerialPhoto,
                    SourceId = aerial.Id,
                    MaterialKindDisplay = "航摄",
                    Title = PreferTitle(aerial.BoxContents, aerial.SurveyArea),
                    ItemName = aerial.SurveyArea?.Trim() ?? string.Empty,
                    ConfidentialLevel = string.Empty,
                    ContentCount = aerial.PhotoCount,
                    LifecycleStatus = aerial.LifecycleStatus?.Trim() ?? string.Empty,
                    LifecycleStatusDisplay = NormalizeHistoryLifecycleDisplay(aerial.LifecycleStatus),
                    ExtraInfo = aerial.Scale?.Trim() ?? string.Empty
                });
            }
            else if (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindOtherMap
                && otherById.TryGetValue(link.RecordId, out var other))
            {
                items.Add(new StockContainerContentItem
                {
                    ContainerKind = StockContainerKind.HistoryBox,
                    ContainerId = containerId,
                    SourceEntity = StockContainerContentSourceEntity.OtherMap,
                    SourceId = other.Id,
                    MaterialKindDisplay = "其他资料",
                    Title = PreferTitle(other.MapName, other.MaterialCategory),
                    ItemName = other.SequenceNumber?.Trim() ?? string.Empty,
                    ConfidentialLevel = string.Empty,
                    ContentCount = 1,
                    LifecycleStatus = other.LifecycleStatus?.Trim() ?? string.Empty,
                    LifecycleStatusDisplay = NormalizeHistoryLifecycleDisplay(other.LifecycleStatus),
                    ExtraInfo = BuildOtherYearRange(other.StartYear, other.EndYear)
                });
            }
        }

        return items;
    }

    private async Task<(
        Dictionary<int, List<string>> TitlesByBoxId,
        Dictionary<int, List<string>> CategoriesByBoxId)> BuildHistoryTitlesAndCategoriesByBoxIdAsync(
        IReadOnlyList<HistoryArchiveBoxLedgerLink> links)
    {
        var titlesByBoxId = new Dictionary<int, List<string>>();
        var categoriesByBoxId = new Dictionary<int, List<string>>();
        if (links.Count == 0)
        {
            return (titlesByBoxId, categoriesByBoxId);
        }

        var topoIds = links
            .Where(link => link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindTopoMap)
            .Select(link => link.RecordId)
            .Distinct()
            .ToList();
        var aerialIds = links
            .Where(link => link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto)
            .Select(link => link.RecordId)
            .Distinct()
            .ToList();
        var otherIds = links
            .Where(link => link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindOtherMap)
            .Select(link => link.RecordId)
            .Distinct()
            .ToList();

        var topoById = topoIds.Count == 0
            ? new Dictionary<int, (string Title, string Category)>()
            : await _dbContext.TopoMaps.AsNoTracking()
                .Where(row => topoIds.Contains(row.Id))
                .ToDictionaryAsync(
                    row => row.Id,
                    row => (PreferTitle(row.MapName, row.MapNumber), row.Category?.Trim() ?? string.Empty));
        var aerialById = aerialIds.Count == 0
            ? new Dictionary<int, (string Title, string Category)>()
            : await _dbContext.AerialPhotos.AsNoTracking()
                .Where(row => aerialIds.Contains(row.Id))
                .ToDictionaryAsync(
                    row => row.Id,
                    row => (PreferTitle(row.BoxContents, row.SurveyArea), row.Category?.Trim() ?? string.Empty));
        var otherById = otherIds.Count == 0
            ? new Dictionary<int, (string Title, string Category)>()
            : await _dbContext.OtherMaps.AsNoTracking()
                .Where(row => otherIds.Contains(row.Id))
                .ToDictionaryAsync(
                    row => row.Id,
                    row => (PreferTitle(row.MapName, row.MaterialCategory), row.Category?.Trim() ?? string.Empty));

        foreach (var link in links)
        {
            string? title = null;
            string? category = null;
            if (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindTopoMap
                && topoById.TryGetValue(link.RecordId, out var topo))
            {
                title = topo.Item1;
                category = topo.Item2;
            }
            else if (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto
                && aerialById.TryGetValue(link.RecordId, out var aerial))
            {
                title = aerial.Item1;
                category = aerial.Item2;
            }
            else if (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindOtherMap
                && otherById.TryGetValue(link.RecordId, out var other))
            {
                title = other.Item1;
                category = other.Item2;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                if (!titlesByBoxId.TryGetValue(link.HistoryArchiveBoxId, out var titleList))
                {
                    titleList = new List<string>();
                    titlesByBoxId[link.HistoryArchiveBoxId] = titleList;
                }

                titleList.Add(title);
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            if (!categoriesByBoxId.TryGetValue(link.HistoryArchiveBoxId, out var categoryList))
            {
                categoryList = new List<string>();
                categoriesByBoxId[link.HistoryArchiveBoxId] = categoryList;
            }

            if (!categoryList.Contains(category, StringComparer.Ordinal))
            {
                categoryList.Add(category);
            }
        }

        return (titlesByBoxId, categoriesByBoxId);
    }

    /// <summary>
    /// 历史盒「项目/类别」：展示台账分类（如「地形图：****」「航片：****」「其他：****」）。
    /// </summary>
    private static string BuildHistoryCategoryLabel(IReadOnlyList<string>? categories)
    {
        if (categories == null || categories.Count == 0)
        {
            return "历史存档";
        }

        return string.Join("、", categories);
    }

    private static HashSet<string> ResolveKinds(IReadOnlyList<string>? kinds)
    {
        if (kinds == null || kinds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal)
            {
                StockContainerKind.YearlyBox,
                StockContainerKind.ElectronicBag,
                StockContainerKind.HistoryBox
            };
        }

        return kinds
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Select(kind => kind.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string BuildElectronicBagSpecsDisplay(string? linkedMediumCodes)
    {
        string codes = linkedMediumCodes?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codes))
        {
            return ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc;
        }

        return $"{ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk}（{codes}）";
    }

    private static string ResolveYearlyBoxLocation(YearlyArchiveBox box)
    {
        if (!string.IsNullOrWhiteSpace(box.BoxLocationCode))
        {
            return box.BoxLocationCode.Trim();
        }

        return box.LastStorageLocation?.Trim() ?? string.Empty;
    }

    private static string BuildYearlyLifecycleDisplay(
        string? containerStatus,
        int borrowedCount,
        int inArchiveCount)
    {
        if (string.Equals(containerStatus, ArchiveContainerLifecycleStatus.InUse, StringComparison.Ordinal))
        {
            if (borrowedCount > 0 && inArchiveCount > 0)
            {
                return StockContainerLifecycleDisplay.PartialBorrowed;
            }

            if (borrowedCount > 0)
            {
                return StockContainerLifecycleDisplay.Borrowed;
            }

            return StockContainerLifecycleDisplay.InUse;
        }

        return containerStatus switch
        {
            ArchiveContainerLifecycleStatus.Emptied => StockContainerLifecycleDisplay.Emptied,
            ArchiveContainerLifecycleStatus.Retired => StockContainerLifecycleDisplay.Retired,
            ArchiveContainerLifecycleStatus.Relocated => StockContainerLifecycleDisplay.Relocated,
            ArchiveContainerLifecycleStatus.Disposed => StockContainerLifecycleDisplay.Disposed,
            _ => string.IsNullOrWhiteSpace(containerStatus) ? "—" : containerStatus
        };
    }

    private static string NormalizeHistoryLifecycleDisplay(string? status)
    {
        string normalized = HistoryArchiveDisposalDomainValues.NormalizeLifecycleStatus(status);
        if (HistoryArchiveDisposalDomainValues.IsInStockLifecycle(normalized))
        {
            return StockContainerLifecycleDisplay.HistoryInStock;
        }

        if (HistoryArchiveDisposalDomainValues.IsLockedLifecycle(normalized))
        {
            return StockContainerLifecycleDisplay.HistoryLocked;
        }

        if (HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(normalized))
        {
            return StockContainerLifecycleDisplay.HistoryDisposed;
        }

        return string.IsNullOrWhiteSpace(status) ? "—" : status.Trim();
    }

    private static string MapFilingLifecycleDisplay(string? status) => status switch
    {
        FilingFactLifecycleStatus.InArchive => "在库",
        FilingFactLifecycleStatus.Borrowed => "借出中",
        FilingFactLifecycleStatus.Transferred => "已转移",
        FilingFactLifecycleStatus.Destroyed => "已销毁",
        FilingFactLifecycleStatus.Disposed => "已处置",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status
    };

    private static string MapCopyRoleDisplay(string? role) => role switch
    {
        FilingFactArchiveCopyRole.Original => "原件",
        FilingFactArchiveCopyRole.Backup => "备份",
        _ => string.Empty
    };

    private static string BuildContentSummary(IEnumerable<string> titles)
    {
        var cleaned = titles
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => title.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (cleaned.Count == 0)
        {
            return string.Empty;
        }

        if (cleaned.Count <= ContentSummaryMaxItems)
        {
            return string.Join("；", cleaned);
        }

        return string.Join("；", cleaned.Take(ContentSummaryMaxItems)) + $" 等{cleaned.Count}项";
    }

    private static string PreferTitle(string? primary, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return fallback?.Trim() ?? string.Empty;
    }

    private static string BuildOtherYearRange(string? startYear, string? endYear)
    {
        string start = startYear?.Trim() ?? string.Empty;
        string end = endYear?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(start))
        {
            return end;
        }

        if (string.IsNullOrWhiteSpace(end) || string.Equals(start, end, StringComparison.Ordinal))
        {
            return start;
        }

        return $"{start}-{end}";
    }

    private static bool ContainsText(string? source, string keyword, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        return caseSensitive
            ? source.Contains(keyword, StringComparison.Ordinal)
            : source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static int? ParseYear(string? yearText)
    {
        if (string.IsNullOrWhiteSpace(yearText))
        {
            return null;
        }

        return int.TryParse(yearText.Trim(), out int year) ? year : null;
    }

    private sealed record FactStat(
        int ContainerId,
        string MaterialName,
        string ItemName,
        string LifecycleStatus);
}
