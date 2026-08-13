using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网明细打印行文本。
/// </summary>
public static class NetworkInboundItemPrintSupport
{
    public static IReadOnlyList<string> BuildItemLines(
        NetworkInboundRecord record,
        NetworkInboundItemPrintContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (NetworkTransferDomainValues.IsExternalOfflineSource(record.SourceKind))
        {
            return BuildExternalMediaItemLines(record.MediaEntries);
        }

        bool isArchived = NetworkTransferDomainValues.IsArchivedElectronicSearchSource(record.SourceKind);
        bool isExternal = !isArchived;
        NetworkInboundItemPrintContext printContext = context ?? NetworkInboundItemPrintContext.Empty;

        return record.Items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select((item, index) => isArchived
                ? BuildArchivedItemLine(item, index + 1, printContext)
                : BuildFlatItemLine(item, index + 1, isExternal))
            .ToList();
    }

    private static string BuildArchivedItemLine(
        NetworkInboundItem item,
        int rowNo,
        NetworkInboundItemPrintContext context)
    {
        if (item.SourceFilingFactId is not int factId
            || factId <= 0
            || !context.HitsByFactId.TryGetValue(factId, out FiledArchiveSearchHit? hit)
            || hit == null)
        {
            return BuildFlatItemLine(item, rowNo, isExternal: false);
        }

        YearlyArchiveSearchResultSetItem resultSetItem = NetworkInboundArchivedItemPrintSupport.ResolveLinkedResultSetItem(
            item,
            context.ResultSetItemsById,
            context.HitsByFactId);
        return NetworkInboundArchivedItemPrintSupport.BuildArchivedItemLine(item, hit, resultSetItem, rowNo);
    }

    private static IReadOnlyList<string> BuildExternalMediaItemLines(IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        int rowNo = 0;
        var lines = new List<string>();
        foreach (YearlyArchiveRegisterMediaItem mediaItem in NetworkInboundOnNetAssetMappingSupport.EnumerateElectronicMediaItems(mediaEntries ?? []))
        {
            rowNo++;
            lines.Add(BuildExternalMediaItemLine(mediaItem, rowNo));
        }

        return lines;
    }

    private static string BuildExternalMediaItemLine(YearlyArchiveRegisterMediaItem mediaItem, int rowNo)
    {
        YearlyArchiveRegisterElectronicMediaItemDetail? detail = mediaItem.ElectronicDetail;
        string assetKind = NetworkInboundOnNetAssetMappingSupport.ResolveAssetKind(
            detail?.MaterialCategory,
            detail?.SubCategory);
        string subCategory = detail?.SubCategory?.Trim() ?? string.Empty;
        string head = $"{rowNo}. [{Empty(assetKind)}] {Empty(mediaItem.ContentDesc)} / {Empty(subCategory)}";

        var segments = new List<string>();
        foreach (string extra in ElectronicMediaItemSupport.BuildElectronicItemPrintExtraParts(mediaItem))
        {
            if (!string.IsNullOrWhiteSpace(extra))
            {
                segments.Add(extra);
            }
        }

        if (mediaItem.ContentCount > 0)
        {
            segments.Add($"份数：{mediaItem.ContentCount}");
        }

        if (!string.IsNullOrWhiteSpace(mediaItem.StoragePath))
        {
            segments.Add($"目录：{mediaItem.StoragePath.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(mediaItem.Note))
        {
            segments.Add($"备注：{mediaItem.Note.Trim()}");
        }

        AppendSegment(segments, "密级", mediaItem.ConfidentialLevel);
        string dataSizeText = detail == null
            ? string.Empty
            : NetworkInboundItemDisplaySupport.ComposeDataSizeText(
                detail.DataSizeMb,
                NetworkInboundItemDisplaySupport.DefaultDataSizeUnit);
        if (!segments.Any(segment => segment.StartsWith("数据量：", StringComparison.Ordinal)))
        {
            AppendSegment(segments, "数据量", dataSizeText);
        }

        return segments.Count == 0 ? head : $"{head} | {string.Join(" | ", segments)}";
    }

    private static string BuildFlatItemLine(NetworkInboundItem item, int rowNo, bool isExternal)
    {
        if (isExternal)
        {
            return $"{rowNo}. [{Empty(item.AssetKind)}] {Empty(item.AssetName)} / {Empty(item.ItemName)}"
                   + $" | 密级：{Empty(item.ConfidentialLevel)} | 数据量：{Empty(item.DataSizeText)}";
        }

        return $"{rowNo}. {Empty(item.FormNo)} {Empty(item.MaterialName)} / {Empty(item.ItemName)}"
               + $" | 密级：{Empty(item.ConfidentialLevel)} | 数据量：{Empty(item.DataSizeText)}"
               + $" | 位置：{Empty(item.StorageLocation)}";
    }

    private static void AppendSegment(List<string> segments, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        segments.Add($"{label}：{value.Trim()}");
    }

    private static string Empty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
