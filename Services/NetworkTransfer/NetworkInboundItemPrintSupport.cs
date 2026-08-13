using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网明细打印行文本。
/// </summary>
public static class NetworkInboundItemPrintSupport
{
    public static IReadOnlyList<string> BuildItemLines(NetworkInboundRecord record)
    {
        if (NetworkTransferDomainValues.IsExternalOfflineSource(record.SourceKind))
        {
            return BuildExternalMediaItemLines(record.MediaEntries);
        }

        bool isExternal = !NetworkTransferDomainValues.IsArchivedElectronicSearchSource(record.SourceKind);
        return record.Items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select((item, index) => BuildFlatItemLine(item, index + 1, isExternal))
            .ToList();
    }

    private static IReadOnlyList<string> BuildExternalMediaItemLines(IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        int rowNo = 0;
        var lines = new List<string>();
        foreach (YearlyArchiveRegisterMediaItem mediaItem in NetworkInboundOnNetAssetMappingSupport.EnumerateElectronicMediaItems(mediaEntries ?? []))
        {
            rowNo++;
            YearlyArchiveRegisterElectronicMediaItemDetail? detail = mediaItem.ElectronicDetail;
            string assetKind = NetworkInboundOnNetAssetMappingSupport.ResolveAssetKind(
                detail?.MaterialCategory,
                detail?.SubCategory);
            string dataSizeText = detail == null
                ? "-"
                : NetworkInboundItemDisplaySupport.ComposeDataSizeText(
                    detail.DataSizeMb,
                    NetworkInboundItemDisplaySupport.DefaultDataSizeUnit);
            string subCategory = detail?.SubCategory?.Trim() ?? string.Empty;
            lines.Add($"{rowNo}. [{Empty(assetKind)}] {Empty(mediaItem.ContentDesc)} / {Empty(subCategory)}"
                      + $" | 密级：{Empty(mediaItem.ConfidentialLevel)} | 数据量：{Empty(dataSizeText)}");
        }

        return lines;
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

    private static string Empty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
