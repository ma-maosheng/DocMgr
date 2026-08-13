using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 档外资料入网借出硬盘归还相关快照与校验。
/// </summary>
internal static class NetworkInboundReturnHardDiskSupport
{
    /// <summary>
    /// 是否展示借出硬盘归还区块（仅档外资料且已声明归还或已有明细）。
    /// </summary>
    public static bool ShouldExposeReturnHardDiskSection(NetworkInboundRecord record) =>
        NetworkTransferDomainValues.IsExternalOfflineSource(record.SourceKind)
        && (record.ReturnBorrowedHardDiskWithInbound || record.ReturnHardDiskItems.Count > 0);

    /// <summary>
    /// 从候选与已选编号构建待持久化的归还硬盘明细。
    /// </summary>
    public static IReadOnlyList<NetworkInboundReturnHardDiskItem> BuildReturnHardDiskItems(
        bool returnWithInbound,
        IReadOnlyCollection<string> selectedDiskCodes,
        IReadOnlyList<HardDiskMediaReturnCandidate> applicantCandidates,
        IReadOnlyList<NetworkInboundReturnHardDiskItem>? existingItems = null)
    {
        if (!returnWithInbound)
        {
            return Array.Empty<NetworkInboundReturnHardDiskItem>();
        }

        if (selectedDiskCodes == null || selectedDiskCodes.Count == 0)
        {
            return Array.Empty<NetworkInboundReturnHardDiskItem>();
        }

        var candidateMap = applicantCandidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.DiskCode))
            .GroupBy(candidate => candidate.DiskCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var existingSlotMap = (existingItems ?? Array.Empty<NetworkInboundReturnHardDiskItem>())
            .Where(item => !string.IsNullOrWhiteSpace(item.DiskCode))
            .GroupBy(item => item.DiskCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().TargetBlankSlotLocation?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        DateTime now = DateTime.Now;
        int sort = 1;
        var built = new List<NetworkInboundReturnHardDiskItem>();
        foreach (string diskCode in selectedDiskCodes
                     .Select(code => code?.Trim() ?? string.Empty)
                     .Where(code => !string.IsNullOrWhiteSpace(code))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(code => code, StringComparer.OrdinalIgnoreCase))
        {
            if (!candidateMap.TryGetValue(diskCode, out HardDiskMediaReturnCandidate? candidate))
            {
                throw new InvalidOperationException($"硬盘编号 [{diskCode}] 不是当前申请人名下可归还的借出硬盘。");
            }

            existingSlotMap.TryGetValue(diskCode, out string? savedSlot);
            built.Add(new NetworkInboundReturnHardDiskItem
            {
                SortOrder = sort++,
                MediumId = candidate.MediumId,
                DiskCode = candidate.DiskCode.Trim(),
                SourceApplicationId = candidate.SourceApplicationId,
                SourceOutboundRecordId = candidate.SourceOutboundRecordId,
                TargetBlankSlotLocation = savedSlot ?? string.Empty,
                CreatedAt = now
            });
        }

        return built;
    }

    /// <summary>
    /// 校验提交前借出硬盘归还声明。
    /// </summary>
    public static void ValidateForSubmit(NetworkInboundRecord header, IReadOnlyList<NetworkInboundReturnHardDiskItem> items, List<string> errors)
    {
        if (!NetworkTransferDomainValues.IsExternalOfflineSource(header.SourceKind))
        {
            return;
        }

        if (!header.ReturnBorrowedHardDiskWithInbound)
        {
            return;
        }

        if (items == null || items.Count == 0)
        {
            errors.Add("已在资料介质（电子）中声明借出硬盘归还，请选择介质编号。");
        }
    }

    /// <summary>
    /// 校验办结前空白硬盘归位档口是否已指定。
    /// </summary>
    public static void ValidateForComplete(NetworkInboundRecord record, List<string> errors)
    {
        if (!record.ReturnBorrowedHardDiskWithInbound || record.ReturnHardDiskItems.Count == 0)
        {
            return;
        }

        foreach (NetworkInboundReturnHardDiskItem item in record.ReturnHardDiskItems.OrderBy(row => row.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation))
            {
                errors.Add($"• 借出硬盘 [{item.DiskCode.Trim()}] 尚未指定空白硬盘归位档口。");
            }
        }
    }

    /// <summary>
    /// 将审批环节指定的归位档口写回明细。
    /// </summary>
    public static void ApplyApprovalSlotLocations(
        NetworkInboundRecord existing,
        IReadOnlyList<NetworkInboundReturnHardDiskItem> slotInputs)
    {
        if (!existing.ReturnBorrowedHardDiskWithInbound || existing.ReturnHardDiskItems.Count == 0)
        {
            return;
        }

        foreach (NetworkInboundReturnHardDiskItem persisted in existing.ReturnHardDiskItems)
        {
            NetworkInboundReturnHardDiskItem? input = slotInputs.FirstOrDefault(item =>
                item.Id > 0 && item.Id == persisted.Id)
                ?? slotInputs.FirstOrDefault(item =>
                    string.Equals(item.DiskCode?.Trim(), persisted.DiskCode?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (input == null)
            {
                continue;
            }

            persisted.TargetBlankSlotLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(input.TargetBlankSlotLocation);
        }
    }
}
