using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 档外入网 YA 式借出硬盘登记与 <see cref="NetworkInboundReturnHardDiskItem"/> 之间的桥接。
/// </summary>
internal static class NetworkInboundReturnHardDiskMediaBridgeSupport
{
    internal static IReadOnlyList<string> CollectBorrowedHardDiskCodes(IEnumerable<MediaEntryViewModel> mediaEntries) =>
        mediaEntries
            .Where(entry => entry.IsRetainedHardDiskScenario && entry.IsBorrowedHardDisk)
            .Select(entry => entry.BorrowedHardDiskCode?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static IReadOnlyList<string> CollectBorrowedHardDiskCodes(
        IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries) =>
        mediaEntries?
            .Where(RegisterMediaTreeSupport.IsElectronicMediaEntity)
            .Where(IsRetainedBorrowedHardDiskMedia)
            .Select(media => media.BorrowedHardDiskCode?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

    internal static void ApplyReturnHardDiskItemsToMediaEntries(
        IEnumerable<MediaEntryViewModel> mediaEntries,
        IReadOnlyList<NetworkInboundReturnHardDiskItem>? returnItems,
        bool requireBorrowedHardDiskForRetained = false)
    {
        if (requireBorrowedHardDiskForRetained)
        {
            foreach (MediaEntryViewModel entry in mediaEntries.Where(static entry => entry.IsRetainedHardDiskScenario))
            {
                entry.IsBorrowedHardDisk = true;
            }
        }

        if (returnItems == null || returnItems.Count == 0)
        {
            return;
        }

        List<MediaEntryViewModel> retainedHardDiskMedias = mediaEntries
            .Where(entry => entry.IsRetainedHardDiskScenario)
            .ToList();
        if (retainedHardDiskMedias.Count == 0)
        {
            return;
        }

        if (retainedHardDiskMedias.Any(entry =>
                entry.IsBorrowedHardDisk && !string.IsNullOrWhiteSpace(entry.BorrowedHardDiskCode)))
        {
            return;
        }

        List<string> diskCodes = returnItems
            .Select(item => item.DiskCode?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int index = 0; index < diskCodes.Count && index < retainedHardDiskMedias.Count; index++)
        {
            retainedHardDiskMedias[index].IsBorrowedHardDisk = true;
            retainedHardDiskMedias[index].BorrowedHardDiskCode = diskCodes[index];
        }
    }

    internal static void ValidateBorrowedHardDiskRegistration(
        IEnumerable<YearlyArchiveRegisterMedia> mediaEntries,
        List<string> errors,
        bool requireBorrowedHardDiskForRetained = false)
    {
        int mediaSeq = 0;
        foreach (YearlyArchiveRegisterMedia media in mediaEntries.Where(RegisterMediaTreeSupport.IsElectronicMediaEntity))
        {
            mediaSeq++;
            if (!IsRetainedHardDiskMedia(media))
            {
                continue;
            }

            if (requireBorrowedHardDiskForRetained && !media.IsBorrowedHardDisk)
            {
                errors.Add($"• 第{mediaSeq}条介质：硬盘介质留存须登记为资料室借出硬盘");
                continue;
            }

            if (requireBorrowedHardDiskForRetained || media.IsBorrowedHardDisk)
            {
                if (string.IsNullOrWhiteSpace(media.BorrowedHardDiskCode))
                {
                    errors.Add($"• 第{mediaSeq}条介质：须选择借出硬盘介质编号");
                }
            }
        }
    }

    private static bool IsRetainedHardDiskMedia(YearlyArchiveRegisterMedia media) =>
        string.Equals(
            media.MediaType?.Trim(),
            ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk,
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(
            media.Disposition?.Trim(),
            ArchiveRegisterDomainValues.ElectronicDispositionRetain,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsRetainedBorrowedHardDiskMedia(YearlyArchiveRegisterMedia media) =>
        IsRetainedHardDiskMedia(media) && media.IsBorrowedHardDisk;
}
