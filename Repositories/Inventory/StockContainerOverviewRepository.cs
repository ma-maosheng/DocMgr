using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Inventory;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.Inventory;

/// <summary>
/// 库管资料总览仓储：按档案盒 / 电子介质袋 / 历史存档盒 / 历史杂项组装只读投影。
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
        HashSet<int> historyMiscContentHits = new();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            await CollectContentHitsAsync(
                keyword,
                keywordCaseSensitive,
                criteria.IncludeElectronicEntryNames,
                kinds,
                criteria.HistoryMaterialKind,
                yearlyBoxContentHits,
                electronicBagContentHits,
                historyBoxContentHits,
                historyMiscContentHits);
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

        // 历史盒与杂项无容器级年度字段；选定档案年度时仅查年度盒/袋。
        if (kinds.Contains(StockContainerKind.HistoryBox) && year == null)
        {
            rows.AddRange(await SearchHistoryBoxesAsync(
                keyword,
                keywordCaseSensitive,
                locationKeyword,
                includeInactive,
                archivedFrom,
                archivedToExclusive,
                historyBoxContentHits,
                criteria.HistoryMaterialKind));
        }

        if (kinds.Contains(StockContainerKind.HistoryMisc) && year == null)
        {
            rows.AddRange(await SearchHistoryMiscAsync(
                keyword,
                keywordCaseSensitive,
                locationKeyword,
                includeInactive,
                archivedFrom,
                archivedToExclusive,
                historyMiscContentHits));
        }

        IEnumerable<StockContainerOverviewRow> filtered = rows;
        if (string.Equals(lifecycleFilter, StockContainerLifecycleDisplay.FilterHasBorrowed, StringComparison.Ordinal))
        {
            filtered = filtered.Where(row => row.HasBorrowedContent);
        }
        else if (string.Equals(lifecycleFilter, StockContainerLifecycleDisplay.FilterInactive, StringComparison.Ordinal))
        {
            filtered = filtered.Where(row => row.IsInactive);
        }
        else if (string.Equals(lifecycleFilter, StockContainerLifecycleDisplay.FilterAll, StringComparison.Ordinal))
        {
            // 含已退库：不做生命周期裁剪。
        }
        else
        {
            // 默认 / 在柜容器：排除已退库。
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
        int containerId,
        string? historyMaterialKind = null)
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
            return await GetHistoryBoxContentsAsync(containerId, historyMaterialKind);
        }

        if (string.Equals(kind, StockContainerKind.HistoryMisc, StringComparison.Ordinal))
        {
            return await GetHistoryMiscContentsAsync(containerId);
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
        string? historyMaterialKind,
        HashSet<int> yearlyBoxHits,
        HashSet<int> electronicBagHits,
        HashSet<int> historyBoxHits,
        HashSet<int> historyMiscHits)
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

        if (kinds.Contains(StockContainerKind.HistoryBox))
        {
            await CollectHistoryBoxContentHitsAsync(
                keyword,
                caseSensitive,
                historyMaterialKind,
                historyBoxHits);
        }

        if (kinds.Contains(StockContainerKind.HistoryMisc))
        {
            await CollectHistoryMiscContentHitsAsync(keyword, caseSensitive, historyMiscHits);
        }
    }

    private async Task CollectHistoryBoxContentHitsAsync(
        string keyword,
        bool caseSensitive,
        string? historyMaterialKind,
        HashSet<int> historyBoxHits)
    {
        bool includeTopo = IncludesHistoryMaterial(
            historyMaterialKind,
            HistoryArchiveDisposalDomainValues.MaterialKindTopoMap);
        bool includeAerial = IncludesHistoryMaterial(
            historyMaterialKind,
            HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto);
        bool includeOther = IncludesHistoryMaterial(
            historyMaterialKind,
            HistoryArchiveDisposalDomainValues.MaterialKindOtherMap);

        var topoIds = new List<int>();
        if (includeTopo)
        {
            var topoCandidates = await _dbContext.TopoMaps
                .AsNoTracking()
                .Select(row => new { row.Id, row.MapName, row.MapNumber, row.CurrentMapNumber, row.Category })
                .ToListAsync();
            topoIds = topoCandidates
                .Where(row => ContainsText(row.MapName, keyword, caseSensitive)
                    || ContainsText(row.MapNumber, keyword, caseSensitive)
                    || ContainsText(row.CurrentMapNumber, keyword, caseSensitive)
                    || ContainsText(row.Category, keyword, caseSensitive))
                .Select(row => row.Id)
                .ToList();
        }

        var aerialIds = new List<int>();
        if (includeAerial)
        {
            var aerialCandidates = await _dbContext.AerialPhotos
                .AsNoTracking()
                .Select(row => new { row.Id, row.BoxContents, row.SurveyArea, row.Category })
                .ToListAsync();
            aerialIds = aerialCandidates
                .Where(row => ContainsText(row.BoxContents, keyword, caseSensitive)
                    || ContainsText(row.SurveyArea, keyword, caseSensitive)
                    || ContainsText(row.Category, keyword, caseSensitive))
                .Select(row => row.Id)
                .ToList();
        }

        var otherIds = new List<int>();
        if (includeOther)
        {
            var otherCandidates = await _dbContext.OtherMaps
                .AsNoTracking()
                .Select(row => new { row.Id, row.MapName, row.MaterialCategory, row.SequenceNumber, row.Category })
                .ToListAsync();
            otherIds = otherCandidates
                .Where(row => ContainsText(row.MapName, keyword, caseSensitive)
                    || ContainsText(row.MaterialCategory, keyword, caseSensitive)
                    || ContainsText(row.SequenceNumber, keyword, caseSensitive)
                    || ContainsText(row.Category, keyword, caseSensitive))
                .Select(row => row.Id)
                .ToList();
        }

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

        // 已离库办结后链接已删：按台账 LastStorageLocation ↔ 盒 LastStorageLocation 补命中。
        await CollectDisposedHistoryBoxHitsByLastLocationAsync(
            topoIds,
            aerialIds,
            otherIds,
            historyBoxHits);
    }

    private async Task CollectHistoryMiscContentHitsAsync(
        string keyword,
        bool caseSensitive,
        HashSet<int> historyMiscHits)
    {
        var lineCandidates = await _dbContext.HistoryMiscMaterialLines
            .AsNoTracking()
            .Select(line => new { line.MaterialId, line.LineText })
            .ToListAsync();

        foreach (var line in lineCandidates)
        {
            if (ContainsText(line.LineText, keyword, caseSensitive))
            {
                historyMiscHits.Add(line.MaterialId);
            }
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
        HashSet<int> contentHits,
        string? historyMaterialKind)
    {
        IQueryable<HistoryArchiveBox> query = _dbContext.HistoryArchiveBoxes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(locationKeyword))
        {
            query = query.Where(box =>
                box.BoxCode.Contains(locationKeyword)
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
                    || ContainsText(box.LastStorageLocation, keyword, keywordCaseSensitive)
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
        links = FilterHistoryLinks(links, historyMaterialKind);

        var (titlesByBoxId, categoriesByBoxId) = await BuildHistoryTitlesAndCategoriesByBoxIdAsync(links);

        // 已离库盒办结后链接已删：按最后存放位置从台账补全摘要/类别。
        var disposedNeedingContent = boxes
            .Where(box => HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(box.LifecycleStatus)
                && !string.IsNullOrWhiteSpace(box.LastStorageLocation)
                && !titlesByBoxId.ContainsKey(box.Id))
            .ToList();
        if (disposedNeedingContent.Count > 0)
        {
            await EnrichDisposedHistoryTitlesAndCategoriesAsync(
                disposedNeedingContent,
                titlesByBoxId,
                categoriesByBoxId,
                historyMaterialKind);
        }

        if (!string.IsNullOrWhiteSpace(historyMaterialKind))
        {
            var allowedBoxIds = links
                .Select(link => link.HistoryArchiveBoxId)
                .ToHashSet();
            foreach (int boxId in titlesByBoxId.Keys)
            {
                allowedBoxIds.Add(boxId);
            }

            foreach (int boxId in categoriesByBoxId.Keys)
            {
                allowedBoxIds.Add(boxId);
            }

            boxes = boxes.Where(box => allowedBoxIds.Contains(box.Id)).ToList();
        }

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
                ProjectOrCategory = BuildHistoryCategoryLabel(
                    categories,
                    HistoryMaterialFallback(historyMaterialKind)),
                Year = "-",
                StorageLocation = ResolveHistoryBoxLocation(box),
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

    private async Task<List<StockContainerOverviewRow>> SearchHistoryMiscAsync(
        string keyword,
        bool keywordCaseSensitive,
        string locationKeyword,
        bool includeInactive,
        DateTime? archivedFrom,
        DateTime? archivedToExclusive,
        HashSet<int> contentHits)
    {
        IQueryable<HistoryMiscMaterial> query = _dbContext.HistoryMiscMaterials
            .AsNoTracking()
            .Include(material => material.Lines);

        if (!string.IsNullOrWhiteSpace(locationKeyword))
        {
            query = query.Where(material =>
                material.StorageLocation.Contains(locationKeyword)
                || material.LastStorageLocation.Contains(locationKeyword));
        }

        if (archivedFrom.HasValue)
        {
            query = query.Where(material => material.RegisteredAt >= archivedFrom.Value);
        }

        if (archivedToExclusive.HasValue)
        {
            query = query.Where(material => material.RegisteredAt < archivedToExclusive.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(material =>
                material.LifecycleStatus != HistoryArchiveDisposalDomainValues.LifecycleDisposed);
        }

        var materials = await query.ToListAsync();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            materials = materials
                .Where(material =>
                    contentHits.Contains(material.Id)
                    || ContainsText(material.ContainerCode, keyword, keywordCaseSensitive)
                    || ContainsText(material.ContainerKind, keyword, keywordCaseSensitive)
                    || ContainsText(material.MajorCategory, keyword, keywordCaseSensitive)
                    || ContainsText(material.SubCategory, keyword, keywordCaseSensitive)
                    || ContainsText(material.StorageLocation, keyword, keywordCaseSensitive)
                    || ContainsText(material.LastStorageLocation, keyword, keywordCaseSensitive)
                    || ContainsText(material.Remark, keyword, keywordCaseSensitive)
                    || ContainsText(material.Registrant, keyword, keywordCaseSensitive))
                .ToList();
        }

        var rows = new List<StockContainerOverviewRow>(materials.Count);
        foreach (var material in materials)
        {
            List<string> lineTexts = material.Lines
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.Id)
                .Select(line => line.LineText?.Trim() ?? string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            bool isInactive = HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(material.LifecycleStatus);

            rows.Add(new StockContainerOverviewRow
            {
                ContainerKind = StockContainerKind.HistoryMisc,
                ContainerId = material.Id,
                ContainerCode = string.IsNullOrWhiteSpace(material.ContainerCode)
                    ? "-"
                    : material.ContainerCode.Trim(),
                ProjectOrCategory = BuildMiscCategoryLabel(material.MajorCategory, material.SubCategory),
                MajorCategory = material.MajorCategory?.Trim() ?? string.Empty,
                SubCategory = material.SubCategory?.Trim() ?? string.Empty,
                Year = "-",
                StorageLocation = ResolveMiscLocation(material),
                LifecycleStatusDisplay = NormalizeHistoryLifecycleDisplay(material.LifecycleStatus),
                ItemCount = lineTexts.Count,
                ContentSummary = BuildContentSummary(lineTexts),
                Specs = material.ContainerKind?.Trim() ?? string.Empty,
                PlacementMode = string.Empty,
                ArchivedBy = material.Registrant?.Trim() ?? string.Empty,
                ArchivedDate = material.RegisteredAt,
                Remarks = material.Remark?.Trim() ?? string.Empty,
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
                DataSizeDisplay = fact.DataSizeMb == 0m
                    ? string.Empty
                    : fact.DataSizeMb.ToString("0.##"),
                CarrierText = fact.StorageCarrierType?.Trim() ?? string.Empty,
                PathText = fact.FilingStoragePath?.Trim() ?? string.Empty,
                MediaItemId = string.Equals(
                    fact.MediaKind?.Trim(),
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    StringComparison.Ordinal)
                    ? fact.MediaItemId
                    : 0
            })
            .ToList();
    }

    private async Task<IReadOnlyList<StockContainerContentItem>> GetHistoryBoxContentsAsync(
        int containerId,
        string? historyMaterialKind)
    {
        var links = await _dbContext.HistoryArchiveBoxLedgerLinks
            .AsNoTracking()
            .Where(link => link.HistoryArchiveBoxId == containerId)
            .OrderBy(link => link.Id)
            .ToListAsync();

        var matchedLinks = FilterHistoryLinks(links, historyMaterialKind);
        if (matchedLinks.Count > 0)
        {
            return await BuildHistoryContentItemsFromLinksAsync(containerId, matchedLinks);
        }

        // 已离库办结后链接已删，或盒上只剩其他类别的链接：按最后存放位置回查当前类别。
        var box = await _dbContext.HistoryArchiveBoxes
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == containerId);
        if (box == null
            || !HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(box.LifecycleStatus)
            || string.IsNullOrWhiteSpace(box.LastStorageLocation))
        {
            return Array.Empty<StockContainerContentItem>();
        }

        return await GetDisposedHistoryBoxContentsByLastLocationAsync(
            containerId,
            box.LastStorageLocation.Trim(),
            historyMaterialKind);
    }

    private async Task<IReadOnlyList<StockContainerContentItem>> GetHistoryMiscContentsAsync(int containerId)
    {
        HistoryMiscMaterial? material = await _dbContext.HistoryMiscMaterials
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == containerId);
        if (material == null)
        {
            return Array.Empty<StockContainerContentItem>();
        }

        string category = BuildMiscCategoryLabel(material.MajorCategory, material.SubCategory);
        string lifecycle = material.LifecycleStatus?.Trim() ?? string.Empty;
        string lifecycleDisplay = NormalizeHistoryLifecycleDisplay(material.LifecycleStatus);
        var lines = material.Lines
            .OrderBy(line => line.SortOrder)
            .ThenBy(line => line.Id)
            .ToList();
        if (lines.Count == 0)
        {
            return new[]
            {
                new StockContainerContentItem
                {
                    ContainerKind = StockContainerKind.HistoryMisc,
                    ContainerId = containerId,
                    SourceEntity = StockContainerContentSourceEntity.HistoryMiscMaterial,
                    SourceId = material.Id,
                    MaterialKindDisplay = HistoryArchiveDisposalDomainValues.MaterialKindDisplayMisc,
                    Title = category,
                    ItemName = string.Empty,
                    ConfidentialLevel = string.Empty,
                    ContentCount = 0,
                    LifecycleStatus = lifecycle,
                    LifecycleStatusDisplay = lifecycleDisplay,
                    ExtraInfo = material.ContainerKind?.Trim() ?? string.Empty
                }
            };
        }

        return lines
            .Select(line => new StockContainerContentItem
            {
                ContainerKind = StockContainerKind.HistoryMisc,
                ContainerId = containerId,
                SourceEntity = StockContainerContentSourceEntity.HistoryMiscMaterialLine,
                SourceId = line.Id,
                MaterialKindDisplay = HistoryArchiveDisposalDomainValues.MaterialKindDisplayMisc,
                Title = category,
                ItemName = line.LineText?.Trim() ?? string.Empty,
                ConfidentialLevel = string.Empty,
                ContentCount = 1,
                LifecycleStatus = lifecycle,
                LifecycleStatusDisplay = lifecycleDisplay,
                ExtraInfo = material.ContainerKind?.Trim() ?? string.Empty
            })
            .ToList();
    }

    private async Task CollectDisposedHistoryBoxHitsByLastLocationAsync(
        IReadOnlyList<int> topoIds,
        IReadOnlyList<int> aerialIds,
        IReadOnlyList<int> otherIds,
        HashSet<int> historyBoxHits)
    {
        var lastLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (topoIds.Count > 0)
        {
            foreach (string? location in await _dbContext.TopoMaps.AsNoTracking()
                         .Where(row => topoIds.Contains(row.Id)
                             && row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed)
                         .Select(row => row.LastStorageLocation)
                         .ToListAsync())
            {
                AddHistoryLocationTokens(lastLocations, location);
            }
        }

        if (aerialIds.Count > 0)
        {
            foreach (string? location in await _dbContext.AerialPhotos.AsNoTracking()
                         .Where(row => aerialIds.Contains(row.Id)
                             && row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed)
                         .Select(row => row.LastStorageLocation)
                         .ToListAsync())
            {
                AddHistoryLocationTokens(lastLocations, location);
            }
        }

        if (otherIds.Count > 0)
        {
            foreach (string? location in await _dbContext.OtherMaps.AsNoTracking()
                         .Where(row => otherIds.Contains(row.Id)
                             && row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed)
                         .Select(row => row.LastStorageLocation)
                         .ToListAsync())
            {
                AddHistoryLocationTokens(lastLocations, location);
            }
        }

        if (lastLocations.Count == 0)
        {
            return;
        }

        var disposedBoxes = await _dbContext.HistoryArchiveBoxes
            .AsNoTracking()
            .Where(box => box.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed
                && box.LastStorageLocation != string.Empty)
            .Select(box => new { box.Id, box.LastStorageLocation })
            .ToListAsync();

        foreach (var box in disposedBoxes)
        {
            if (lastLocations.Contains(box.LastStorageLocation)
                || lastLocations.Any(token =>
                    HistoryArchiveBoxCodeSupport.ContainsBoxCode(box.LastStorageLocation, token)))
            {
                historyBoxHits.Add(box.Id);
            }
        }
    }

    private async Task EnrichDisposedHistoryTitlesAndCategoriesAsync(
        IReadOnlyList<HistoryArchiveBox> disposedBoxes,
        Dictionary<int, List<string>> titlesByBoxId,
        Dictionary<int, List<string>> categoriesByBoxId,
        string? historyMaterialKind)
    {
        var locationToBoxIds = BuildDisposedLocationToBoxIds(disposedBoxes);
        if (locationToBoxIds.Count == 0)
        {
            return;
        }

        if (IncludesHistoryMaterial(
                historyMaterialKind,
                HistoryArchiveDisposalDomainValues.MaterialKindTopoMap))
        {
            var disposedTopos = await _dbContext.TopoMaps.AsNoTracking()
                .Where(row => row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed
                    && row.LastStorageLocation != string.Empty)
                .Select(row => new
                {
                    row.MapName,
                    row.MapNumber,
                    row.Category,
                    row.LastStorageLocation
                })
                .ToListAsync();
            foreach (var row in disposedTopos)
            {
                AppendDisposedHistoryTitleCategory(
                    locationToBoxIds,
                    row.LastStorageLocation,
                    PreferTitle(row.MapName, row.MapNumber),
                    row.Category,
                    titlesByBoxId,
                    categoriesByBoxId);
            }
        }

        if (IncludesHistoryMaterial(
                historyMaterialKind,
                HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto))
        {
            var disposedAerials = await _dbContext.AerialPhotos.AsNoTracking()
                .Where(row => row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed
                    && row.LastStorageLocation != string.Empty)
                .Select(row => new
                {
                    row.BoxContents,
                    row.SurveyArea,
                    row.Category,
                    row.LastStorageLocation
                })
                .ToListAsync();
            foreach (var row in disposedAerials)
            {
                AppendDisposedHistoryTitleCategory(
                    locationToBoxIds,
                    row.LastStorageLocation,
                    PreferTitle(row.BoxContents, row.SurveyArea),
                    row.Category,
                    titlesByBoxId,
                    categoriesByBoxId);
            }
        }

        if (IncludesHistoryMaterial(
                historyMaterialKind,
                HistoryArchiveDisposalDomainValues.MaterialKindOtherMap))
        {
            var disposedOthers = await _dbContext.OtherMaps.AsNoTracking()
                .Where(row => row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed
                    && row.LastStorageLocation != string.Empty)
                .Select(row => new
                {
                    row.MapName,
                    row.MaterialCategory,
                    row.Category,
                    row.LastStorageLocation
                })
                .ToListAsync();
            foreach (var row in disposedOthers)
            {
                AppendDisposedHistoryTitleCategory(
                    locationToBoxIds,
                    row.LastStorageLocation,
                    PreferTitle(row.MapName, row.MaterialCategory),
                    row.Category,
                    titlesByBoxId,
                    categoriesByBoxId);
            }
        }
    }

    private async Task<IReadOnlyList<StockContainerContentItem>> GetDisposedHistoryBoxContentsByLastLocationAsync(
        int containerId,
        string lastStorageLocation,
        string? historyMaterialKind)
    {
        var items = new List<StockContainerContentItem>();

        if (IncludesHistoryMaterial(historyMaterialKind, HistoryArchiveDisposalDomainValues.MaterialKindTopoMap))
        {
            var topos = await _dbContext.TopoMaps.AsNoTracking()
                .Where(row => row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed
                    && row.LastStorageLocation != string.Empty)
                .ToListAsync();
            foreach (var topo in topos.Where(row => MatchesDisposedHistoryLocation(row.LastStorageLocation, lastStorageLocation)))
            {
                items.Add(MapTopoContent(containerId, topo));
            }
        }

        if (IncludesHistoryMaterial(historyMaterialKind, HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto))
        {
            var aerials = await _dbContext.AerialPhotos.AsNoTracking()
                .Where(row => row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed
                    && row.LastStorageLocation != string.Empty)
                .ToListAsync();
            foreach (var aerial in aerials.Where(row => MatchesDisposedHistoryLocation(row.LastStorageLocation, lastStorageLocation)))
            {
                items.Add(MapAerialContent(containerId, aerial));
            }
        }

        if (IncludesHistoryMaterial(historyMaterialKind, HistoryArchiveDisposalDomainValues.MaterialKindOtherMap))
        {
            var others = await _dbContext.OtherMaps.AsNoTracking()
                .Where(row => row.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleDisposed
                    && row.LastStorageLocation != string.Empty)
                .ToListAsync();
            foreach (var other in others.Where(row => MatchesDisposedHistoryLocation(row.LastStorageLocation, lastStorageLocation)))
            {
                items.Add(MapOtherContent(containerId, other));
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<StockContainerContentItem>> BuildHistoryContentItemsFromLinksAsync(
        int containerId,
        IReadOnlyList<HistoryArchiveBoxLedgerLink> links)
    {
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
                items.Add(MapTopoContent(containerId, topo));
            }
            else if (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto
                && aerialById.TryGetValue(link.RecordId, out var aerial))
            {
                items.Add(MapAerialContent(containerId, aerial));
            }
            else if (link.MaterialKind == HistoryArchiveDisposalDomainValues.MaterialKindOtherMap
                && otherById.TryGetValue(link.RecordId, out var other))
            {
                items.Add(MapOtherContent(containerId, other));
            }
        }

        return items;
    }

    private static Dictionary<string, List<int>> BuildDisposedLocationToBoxIds(
        IReadOnlyList<HistoryArchiveBox> disposedBoxes)
    {
        var locationToBoxIds = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var box in disposedBoxes)
        {
            string location = box.LastStorageLocation?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(location))
            {
                continue;
            }

            if (!locationToBoxIds.TryGetValue(location, out List<int>? boxIds))
            {
                boxIds = new List<int>();
                locationToBoxIds[location] = boxIds;
            }

            boxIds.Add(box.Id);
        }

        return locationToBoxIds;
    }

    private static void AppendDisposedHistoryTitleCategory(
        IReadOnlyDictionary<string, List<int>> locationToBoxIds,
        string? ledgerLastLocation,
        string title,
        string? category,
        Dictionary<int, List<string>> titlesByBoxId,
        Dictionary<int, List<string>> categoriesByBoxId)
    {
        foreach (var pair in locationToBoxIds)
        {
            if (!MatchesDisposedHistoryLocation(ledgerLastLocation, pair.Key))
            {
                continue;
            }

            foreach (int boxId in pair.Value)
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    if (!titlesByBoxId.TryGetValue(boxId, out List<string>? titleList))
                    {
                        titleList = new List<string>();
                        titlesByBoxId[boxId] = titleList;
                    }

                    titleList.Add(title);
                }

                if (string.IsNullOrWhiteSpace(category))
                {
                    continue;
                }

                if (!categoriesByBoxId.TryGetValue(boxId, out List<string>? categoryList))
                {
                    categoryList = new List<string>();
                    categoriesByBoxId[boxId] = categoryList;
                }

                if (!categoryList.Contains(category, StringComparer.Ordinal))
                {
                    categoryList.Add(category);
                }
            }
        }
    }

    private static void AddHistoryLocationTokens(HashSet<string> target, string? locationField)
    {
        foreach (string code in HistoryArchiveBoxCodeSupport.SplitBoxCodes(locationField))
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                target.Add(code);
            }
        }
    }

    /// <summary>
    /// 台账最后存放位置是否指向某已离库盒号（支持混放多盒号字段）。
    /// </summary>
    private static bool MatchesDisposedHistoryLocation(string? ledgerLastLocation, string boxLastLocation)
    {
        if (string.IsNullOrWhiteSpace(boxLastLocation))
        {
            return false;
        }

        return HistoryArchiveBoxCodeSupport.ContainsBoxCode(ledgerLastLocation, boxLastLocation)
            || string.Equals(
                ledgerLastLocation?.Trim(),
                boxLastLocation.Trim(),
                StringComparison.OrdinalIgnoreCase);
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
    private static string BuildHistoryCategoryLabel(IReadOnlyList<string>? categories, string fallback = "历史存档")
    {
        if (categories == null || categories.Count == 0)
        {
            return fallback;
        }

        return string.Join("、", categories);
    }

    private static bool IncludesHistoryMaterial(string? filter, string materialKind) =>
        string.IsNullOrWhiteSpace(filter)
        || string.Equals(filter.Trim(), materialKind, StringComparison.Ordinal);

    private static string HistoryMaterialFallback(string? materialKind) => materialKind switch
    {
        HistoryArchiveDisposalDomainValues.MaterialKindTopoMap => "地形图",
        HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto => "航片",
        HistoryArchiveDisposalDomainValues.MaterialKindOtherMap => "其他资料",
        _ => "历史存档"
    };

    private static List<HistoryArchiveBoxLedgerLink> FilterHistoryLinks(
        IReadOnlyList<HistoryArchiveBoxLedgerLink> links,
        string? materialKind)
    {
        if (string.IsNullOrWhiteSpace(materialKind))
        {
            return links.ToList();
        }

        string kind = materialKind.Trim();
        return links
            .Where(link => string.Equals(link.MaterialKind, kind, StringComparison.Ordinal))
            .ToList();
    }

    private static StockContainerContentItem MapTopoContent(int containerId, TopoMap topo) =>
        new()
        {
            ContainerKind = StockContainerKind.HistoryBox,
            ContainerId = containerId,
            SourceEntity = StockContainerContentSourceEntity.TopoMap,
            SourceId = topo.Id,
            MaterialKindDisplay = "地形图",
            Title = topo.MapName?.Trim() ?? string.Empty,
            CodeText = topo.MapNumber?.Trim() ?? string.Empty,
            ItemName = topo.CurrentMapNumber?.Trim() ?? string.Empty,
            Scale = topo.Scale?.Trim() ?? string.Empty,
            ContentCount = topo.SheetCount,
            DateText = topo.CreationDate?.Trim() ?? string.Empty,
            AreaText = topo.Region?.Trim() ?? string.Empty,
            Remark = topo.Remark?.Trim() ?? string.Empty,
            LifecycleStatus = topo.LifecycleStatus?.Trim() ?? string.Empty,
            LifecycleStatusDisplay = NormalizeHistoryLifecycleDisplay(topo.LifecycleStatus)
        };

    private static StockContainerContentItem MapAerialContent(int containerId, AerialPhoto aerial) =>
        new()
        {
            ContainerKind = StockContainerKind.HistoryBox,
            ContainerId = containerId,
            SourceEntity = StockContainerContentSourceEntity.AerialPhoto,
            SourceId = aerial.Id,
            MaterialKindDisplay = "航片",
            Title = aerial.BoxContents?.Trim() ?? string.Empty,
            AreaText = aerial.SurveyArea?.Trim() ?? string.Empty,
            Scale = aerial.Scale?.Trim() ?? string.Empty,
            DateText = aerial.PhotographyDate?.Trim() ?? string.Empty,
            ContentCount = aerial.PhotoCount,
            Remark = aerial.Remark?.Trim() ?? string.Empty,
            LifecycleStatus = aerial.LifecycleStatus?.Trim() ?? string.Empty,
            LifecycleStatusDisplay = NormalizeHistoryLifecycleDisplay(aerial.LifecycleStatus)
        };

    private static StockContainerContentItem MapOtherContent(int containerId, OtherMap other) =>
        new()
        {
            ContainerKind = StockContainerKind.HistoryBox,
            ContainerId = containerId,
            SourceEntity = StockContainerContentSourceEntity.OtherMap,
            SourceId = other.Id,
            MaterialKindDisplay = "其他资料",
            Title = other.MapName?.Trim() ?? string.Empty,
            CodeText = other.SequenceNumber?.Trim() ?? string.Empty,
            AreaText = other.MaterialCategory?.Trim() ?? string.Empty,
            DateText = BuildOtherYearRange(other.StartYear, other.EndYear),
            Remark = other.Remark?.Trim() ?? string.Empty,
            LifecycleStatus = other.LifecycleStatus?.Trim() ?? string.Empty,
            LifecycleStatusDisplay = NormalizeHistoryLifecycleDisplay(other.LifecycleStatus)
        };

    private static HashSet<string> ResolveKinds(IReadOnlyList<string>? kinds)
    {
        if (kinds == null || kinds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal)
            {
                StockContainerKind.YearlyBox,
                StockContainerKind.ElectronicBag,
                StockContainerKind.HistoryBox,
                StockContainerKind.HistoryMisc
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

    /// <summary>杂项「项目/类别」：资料大类/资料子类。</summary>
    private static string BuildMiscCategoryLabel(string? majorCategory, string? subCategory)
    {
        string major = majorCategory?.Trim() ?? string.Empty;
        string sub = subCategory?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(major) && !string.IsNullOrWhiteSpace(sub))
        {
            return $"{major}/{sub}";
        }

        if (!string.IsNullOrWhiteSpace(major))
        {
            return major;
        }

        return string.IsNullOrWhiteSpace(sub) ? "杂项资料" : sub;
    }

    /// <summary>杂项存放位置：在库用四段位置；已离库用最后存放位置。</summary>
    private static string ResolveMiscLocation(HistoryMiscMaterial material)
    {
        if (!string.IsNullOrWhiteSpace(material.StorageLocation))
        {
            return material.StorageLocation.Trim();
        }

        return material.LastStorageLocation?.Trim() ?? string.Empty;
    }

    /// <summary>历史盒存放位置：在库用盒号；已离库用最后存放位置（对齐年度盒销号展示）。</summary>
    private static string ResolveHistoryBoxLocation(HistoryArchiveBox box)
    {
        if (!string.IsNullOrWhiteSpace(box.BoxCode))
        {
            return box.BoxCode.Trim();
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
