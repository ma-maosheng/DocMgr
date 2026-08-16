using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网明细打印行文本。
/// </summary>
public static class NetworkOutboundItemPrintSupport
{
    /// <summary>
    /// 子项目录、数据量等是否尚未从离线拷贝介质补录。
    /// </summary>
    public static bool HasPendingItemDetailCapture(IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        if (mediaEntries == null)
        {
            return false;
        }

        foreach (YearlyArchiveRegisterMedia media in mediaEntries)
        {
            if (!RegisterMediaTreeSupport.IsElectronicMediaEntity(media))
            {
                continue;
            }

            foreach (YearlyArchiveRegisterMediaItem item in media.Items)
            {
                if (IsItemDetailPendingOfflineCapture(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static IReadOnlyList<string> BuildItemLines(NetworkOutboundRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.MediaEntries is { Count: > 0 })
        {
            return BuildMediaGroupedItemLines(record.MediaEntries);
        }

        return BuildItemLines(record.Items);
    }

    public static IReadOnlyList<string> BuildItemLines(IEnumerable<NetworkOutboundItem> items)
    {
        return items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select((item, index) => BuildLegacyItemLine(item, index + 1))
            .ToList();
    }

    private static IReadOnlyList<string> BuildMediaGroupedItemLines(IEnumerable<YearlyArchiveRegisterMedia> mediaEntries)
    {
        var lines = new List<string>();
        int mediaIndex = 0;

        foreach (YearlyArchiveRegisterMedia media in mediaEntries)
        {
            if (!RegisterMediaTreeSupport.IsElectronicMediaEntity(media))
            {
                continue;
            }

            if (media.Items == null || media.Items.Count == 0)
            {
                continue;
            }

            mediaIndex++;
            lines.Add(FormatCopyMediaDescription(media, mediaIndex));

            int itemIndex = 0;
            foreach (YearlyArchiveRegisterMediaItem mediaItem in media.Items.OrderBy(item => item.Id))
            {
                itemIndex++;
                lines.Add(BuildMediaItemLine(mediaItem, itemIndex));
            }
        }

        return lines;
    }

    private static string FormatCopyMediaDescription(YearlyArchiveRegisterMedia media, int mediaIndex)
    {
        var segments = new List<string> { $"介质{mediaIndex}（拷贝介质）" };

        string mediaSummary = FormatMediaTypeSummary(media);
        if (!string.IsNullOrWhiteSpace(mediaSummary))
        {
            segments.Add(mediaSummary);
        }

        if (!string.IsNullOrWhiteSpace(media.Disposition))
        {
            segments.Add($"处置：{NetworkTransferDomainValues.NormalizeOutboundTakeAwayDisposition(media.Disposition)}");
        }

        AppendExternalOfflineHardDiskRequisitionSegments(media, segments);

        return string.Join("；", segments);
    }

    private static void AppendExternalOfflineHardDiskRequisitionSegments(
        YearlyArchiveRegisterMedia media,
        List<string> segments)
    {
        if (!NetworkOutboundExternalHardDiskRequisitionSupport.IsExternalOfflineReturnedHardDiskMedia(media))
        {
            return;
        }

        if (media.UseInStockBlankHardDisk)
        {
            segments.Add($"库内空盘：是，编号：{Empty(media.RequisitionedHardDiskCode)}");
            if (media.RequisitionedDiskNeedReturn)
            {
                segments.Add("需归还");
                if (media.ExpectedReturnDate.HasValue)
                {
                    segments.Add($"预计归还：{media.ExpectedReturnDate.Value:yyyy-MM-dd}");
                }
            }
            else
            {
                segments.Add("不需归还");
            }

            return;
        }

        segments.Add("库内空盘：否（自带硬盘）");
    }

    private static string FormatMediaTypeSummary(YearlyArchiveRegisterMedia media)
    {
        string kind = string.IsNullOrWhiteSpace(media.MediaKind) ? "电子" : media.MediaKind.Trim();
        string type = media.MediaType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
        {
            return kind;
        }

        string count = media.MediaCount > 0 ? $"×{media.MediaCount}" : string.Empty;
        return $"{kind}/{type}{count}";
    }

    private static string BuildMediaItemLine(YearlyArchiveRegisterMediaItem mediaItem, int itemIndex)
    {
        YearlyArchiveRegisterElectronicMediaItemDetail? detail = mediaItem.ElectronicDetail;
        string assetKind = NetworkInboundOnNetAssetMappingSupport.ResolveAssetKind(
            detail?.MaterialCategory,
            detail?.SubCategory);
        string subCategory = detail?.SubCategory?.Trim() ?? string.Empty;
        string head = $"       {itemIndex}. [{Empty(assetKind)}] {Empty(mediaItem.ContentDesc)} / {Empty(subCategory)}";

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

    private static string BuildLegacyItemLine(NetworkOutboundItem item, int rowNo)
    {
        string assetNo = string.IsNullOrWhiteSpace(item.AssetNo) ? string.Empty : $"[{Empty(item.AssetNo)}] ";
        string itemName = string.IsNullOrWhiteSpace(item.ItemName)
            ? string.Empty
            : $" / {Empty(item.ItemName)}";
        return $"{rowNo}. {assetNo}[{Empty(item.AssetKind)}] {Empty(item.AssetName)}{itemName}"
               + $" | 密级：{Empty(item.ConfidentialLevel)} | 数据量：{Empty(item.DataSizeText)}"
               + $" | 路径：{Empty(item.ServerPath)}";
    }

    private static bool IsItemDetailPendingOfflineCapture(YearlyArchiveRegisterMediaItem item)
    {
        YearlyArchiveRegisterElectronicMediaItemDetail? detail = item.ElectronicDetail;
        if (detail == null)
        {
            return true;
        }

        if (detail.DataSizeMb <= 0)
        {
            return true;
        }

        bool hasStoragePath = !string.IsNullOrWhiteSpace(item.StoragePath);
        bool hasEntries = detail.Entries is { Count: > 0 };
        return !hasStoragePath && !hasEntries;
    }

    private static void AppendSegment(List<string> segments, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            segments.Add($"{label}：-");
            return;
        }

        segments.Add($"{label}：{value.Trim()}");
    }

    private static string Empty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
