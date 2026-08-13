using DocMgr.Models.NetworkTransfer;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网申请单打印：借出硬盘随资料归还描述。
/// </summary>
internal static class NetworkInboundReturnHardDiskPrintSupport
{
    /// <summary>
    /// 构建打印用借出硬盘归还说明；无归还声明时返回 null。
    /// </summary>
    public static string? BuildReturnHardDiskDescription(NetworkInboundRecord record)
    {
        if (!record.ReturnBorrowedHardDiskWithInbound || record.ReturnHardDiskItems.Count == 0)
        {
            return null;
        }

        IReadOnlyList<string> diskCodes = record.ReturnHardDiskItems
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select(item => item.DiskCode?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (diskCodes.Count == 0)
        {
            return null;
        }

        return diskCodes.Count == 1
            ? $"是；拟归还硬盘编号：{diskCodes[0]}"
            : $"是；拟归还硬盘编号：{string.Join("、", diskCodes)}";
    }
}
