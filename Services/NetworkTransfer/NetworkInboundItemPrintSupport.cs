using DocMgr.Models.NetworkTransfer;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网明细打印行文本。
/// </summary>
public static class NetworkInboundItemPrintSupport
{
    public static IReadOnlyList<string> BuildItemLines(IEnumerable<NetworkInboundItem> items, string sourceKind)
    {
        bool isExternal = !NetworkTransferDomainValues.IsArchivedElectronicSearchSource(sourceKind);
        return items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select((item, index) => BuildItemLine(item, index + 1, isExternal))
            .ToList();
    }

    private static string BuildItemLine(NetworkInboundItem item, int rowNo, bool isExternal)
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
