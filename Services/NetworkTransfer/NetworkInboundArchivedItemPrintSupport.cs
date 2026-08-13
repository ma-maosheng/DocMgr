using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 已立档入网明细打印行组装。
/// </summary>
public static class NetworkInboundArchivedItemPrintSupport
{
    public static YearlyArchiveSearchResultSetItem ResolveLinkedResultSetItem(
        NetworkInboundItem inboundItem,
        IReadOnlyDictionary<int, YearlyArchiveSearchResultSetItem> resultSetItemsById,
        IReadOnlyDictionary<int, FiledArchiveSearchHit> hitsByFactId)
    {
        ArgumentNullException.ThrowIfNull(inboundItem);
        ArgumentNullException.ThrowIfNull(resultSetItemsById);
        ArgumentNullException.ThrowIfNull(hitsByFactId);

        if (inboundItem.SourceResultSetItemId is int resultSetItemId && resultSetItemId > 0
            && resultSetItemsById.TryGetValue(resultSetItemId, out YearlyArchiveSearchResultSetItem? linked)
            && linked != null)
        {
            return linked;
        }

        if (inboundItem.SourceFilingFactId is int factId && factId > 0)
        {
            YearlyArchiveSearchResultSetItem? byFact = resultSetItemsById.Values
                .FirstOrDefault(item => item.FilingFactId == factId);
            if (byFact != null)
            {
                return byFact;
            }

            if (hitsByFactId.TryGetValue(factId, out FiledArchiveSearchHit? hit) && hit != null)
            {
                return CreateSnapshotResultSetItem(inboundItem, hit);
            }
        }

        return new YearlyArchiveSearchResultSetItem
        {
            Id = inboundItem.SourceResultSetItemId ?? 0,
            FilingFactId = inboundItem.SourceFilingFactId ?? 0,
            FormNo = inboundItem.FormNo,
            MaterialName = inboundItem.MaterialName,
            ItemName = inboundItem.ItemName,
            ContainerCode = inboundItem.ContainerCode,
            StorageLocation = inboundItem.StorageLocation,
            SelectionScopeKind = ArchiveSearchSelectionScopeKind.WholeMediaItem,
            RequestedCopyCount = 1
        };
    }

    public static string BuildArchivedItemLine(
        NetworkInboundItem item,
        FiledArchiveSearchHit hit,
        YearlyArchiveSearchResultSetItem resultSetItem,
        int rowNo)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(resultSetItem);

        string formNo = FirstNonEmpty(item.FormNo, hit.FormNo);
        string materialName = FirstNonEmpty(item.MaterialName, hit.MaterialName);
        string itemName = FirstNonEmpty(item.ItemName, hit.ItemName);
        string head = $"{rowNo}. {Empty(formNo)} {Empty(materialName)} / {Empty(itemName)}";

        var segments = new List<string>();
        AppendCategorySegment(segments, hit.MaterialCategory, hit.SubCategory);
        AppendSegment(segments, "组织", hit.DataOrganizationForm);
        AppendSegment(segments, "介质", hit.StorageCarrierTypeDisplay);
        AppendCopyCountSegment(segments, resultSetItem);
        AppendScopeSegments(segments, resultSetItem, hit);
        AppendSegment(segments, "目录", FirstNonEmpty(hit.FilingStoragePath, item.StorageLocation));
        AppendSegment(segments, "密级", ResolveConfidentialLevel(item, hit));
        AppendSegment(segments, "数据量", ResolveDataSizeText(item, hit));
        AppendSegment(segments, "盒号", FirstNonEmpty(item.ContainerCode, hit.ContainerCode));
        AppendSegment(segments, "位置", FirstNonEmpty(item.StorageLocation, hit.StorageLocation));

        return segments.Count == 0 ? head : $"{head} | {string.Join(" | ", segments)}";
    }

    private static YearlyArchiveSearchResultSetItem CreateSnapshotResultSetItem(
        NetworkInboundItem inboundItem,
        FiledArchiveSearchHit hit)
    {
        return new YearlyArchiveSearchResultSetItem
        {
            Id = inboundItem.SourceResultSetItemId ?? 0,
            FilingFactId = hit.FilingFactId > 0 ? hit.FilingFactId : inboundItem.SourceFilingFactId ?? 0,
            FormNo = FirstNonEmpty(inboundItem.FormNo, hit.FormNo),
            MaterialName = FirstNonEmpty(inboundItem.MaterialName, hit.MaterialName),
            ItemName = FirstNonEmpty(inboundItem.ItemName, hit.ItemName),
            ContainerCode = FirstNonEmpty(inboundItem.ContainerCode, hit.ContainerCode),
            StorageLocation = FirstNonEmpty(inboundItem.StorageLocation, hit.StorageLocation),
            SelectionScopeKind = ArchiveSearchSelectionScopeKind.WholeMediaItem,
            RequestedCopyCount = 1,
            LifecycleStatus = hit.LifecycleStatus,
            BorrowHintLevel = hit.BorrowHintLevel,
            BorrowHintText = hit.BorrowHintText
        };
    }

    private static void AppendCategorySegment(List<string> segments, string? materialCategory, string? subCategory)
    {
        string category = materialCategory?.Trim() ?? string.Empty;
        string sub = subCategory?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(sub))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sub))
        {
            segments.Add($"类型：{category}");
            return;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            segments.Add($"子类：{sub}");
            return;
        }

        segments.Add($"类型：{category}/{sub}");
    }

    private static void AppendCopyCountSegment(List<string> segments, YearlyArchiveSearchResultSetItem resultSetItem)
    {
        int copyCount = resultSetItem.RequestedCopyCount > 0 ? resultSetItem.RequestedCopyCount : 1;
        bool isWholeMediaItem = string.Equals(
            resultSetItem.SelectionScopeKind,
            ArchiveSearchSelectionScopeKind.WholeMediaItem,
            StringComparison.Ordinal);
        if (copyCount > 1 || !isWholeMediaItem)
        {
            segments.Add($"份数：{copyCount}");
        }
    }

    private static void AppendScopeSegments(
        List<string> segments,
        YearlyArchiveSearchResultSetItem resultSetItem,
        FiledArchiveSearchHit hit)
    {
        string scopeDisplay = ArchiveSearchPoolSupport.ResolveSelectionScopeDisplay(
            resultSetItem.SelectionScopeKind,
            resultSetItem.ContentEntryKind,
            resultSetItem.ContentEntryName,
            resultSetItem.ContentEntryRelativePath);
        AppendSegment(segments, "范围", scopeDisplay);

        string matchedSummary = ArchiveSearchPoolSupport.ResolveMatchedContentEntrySummary(
            resultSetItem.SelectionScopeKind,
            resultSetItem.ContentEntryKind,
            resultSetItem.ContentEntryName,
            resultSetItem.ContentEntryRelativePath,
            hit.MatchedContentEntrySummary);
        if (!string.IsNullOrWhiteSpace(matchedSummary)
            && !string.Equals(matchedSummary, scopeDisplay, StringComparison.Ordinal))
        {
            AppendSegment(segments, "命中", matchedSummary);
        }
    }

    private static void AppendSegment(List<string> segments, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        segments.Add($"{label}：{value.Trim()}");
    }

    private static string ResolveConfidentialLevel(NetworkInboundItem item, FiledArchiveSearchHit hit) =>
        FirstNonEmpty(item.ConfidentialLevel, hit.ConfidentialLevel);

    private static string ResolveDataSizeText(NetworkInboundItem item, FiledArchiveSearchHit hit)
    {
        if (NetworkInboundItemDisplaySupport.TryParseDataSizeText(item.DataSizeText, out decimal value, out string unit))
        {
            return NetworkInboundItemDisplaySupport.ComposeDataSizeText(value, unit);
        }

        if (hit.DataSizeMb > 0)
        {
            return hit.DataSizeDisplay;
        }

        return item.DataSizeText?.Trim() ?? string.Empty;
    }

    private static string FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        return string.IsNullOrWhiteSpace(second) ? string.Empty : second.Trim();
    }

    private static string Empty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
