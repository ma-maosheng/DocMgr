using DocMgr.Models.NetworkTransfer;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网明细打印行文本。
/// </summary>
public static class NetworkOutboundItemPrintSupport
{
    public static IReadOnlyList<string> BuildItemLines(IEnumerable<NetworkOutboundItem> items)
    {
        return items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select((item, index) => BuildItemLine(item, index + 1))
            .ToList();
    }

    private static string BuildItemLine(NetworkOutboundItem item, int rowNo)
    {
        string assetNo = string.IsNullOrWhiteSpace(item.AssetNo) ? string.Empty : $"[{Empty(item.AssetNo)}] ";
        string itemName = string.IsNullOrWhiteSpace(item.ItemName)
            ? string.Empty
            : $" / {Empty(item.ItemName)}";
        return $"{rowNo}. {assetNo}[{Empty(item.AssetKind)}] {Empty(item.AssetName)}{itemName}"
            + $" | 密级：{Empty(item.ConfidentialLevel)} | 数据量：{Empty(item.DataSizeText)}"
            + $" | 路径：{Empty(item.ServerPath)}";
    }

    private static string Empty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
