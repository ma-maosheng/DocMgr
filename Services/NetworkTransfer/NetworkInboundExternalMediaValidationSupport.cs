using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 档外入网登记介质树提交校验（与 YA 电子介质规则对齐的精简版）。
/// </summary>
internal static class NetworkInboundExternalMediaValidationSupport
{
    internal static int CountMediaItems(IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries) =>
        mediaEntries?
            .Where(RegisterMediaTreeSupport.IsElectronicMediaEntity)
            .Sum(media => media.Items?.Count ?? 0) ?? 0;

    internal static void ValidateForSubmit(
        NetworkInboundRecord header,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(header.TargetServerPath))
        {
            errors.Add("请选择服务器路径。");
        }

        if (string.IsNullOrWhiteSpace(header.MaterialPath))
        {
            errors.Add("请填写资料路径。");
        }

        var entries = mediaEntries?.Where(RegisterMediaTreeSupport.IsElectronicMediaEntity).ToList()
            ?? [];
        if (entries.Count == 0 || entries.All(media => media.Items == null || media.Items.Count == 0))
        {
            errors.Add("请至少录入一条入网明细（资料介质电子子项）。");
            return;
        }

        int mediaSeq = 0;
        foreach (YearlyArchiveRegisterMedia media in entries)
        {
            mediaSeq++;
            ValidateMediaDisposition(media, mediaSeq, errors);

            if (media.Items == null || media.Items.Count == 0)
            {
                errors.Add($"• 第{mediaSeq}条介质至少需要填写一条内容明细");
                continue;
            }

            for (int itemIndex = 0; itemIndex < media.Items.Count; itemIndex++)
            {
                ValidateMediaItem(media.Items[itemIndex], mediaSeq, itemIndex + 1, errors);
            }
        }

        NetworkInboundReturnHardDiskMediaBridgeSupport.ValidateBorrowedHardDiskRegistration(
            entries,
            errors,
            requireBorrowedHardDiskForRetained: true);
    }

    private static void ValidateMediaItem(
        YearlyArchiveRegisterMediaItem item,
        int mediaSeq,
        int itemSeq,
        List<string> errors)
    {
        string label = $"第{mediaSeq}条介质第{itemSeq}项";
        if (string.IsNullOrWhiteSpace(item.ContentDesc))
        {
            errors.Add($"• {label}：分项资料名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(item.ConfidentialLevel))
        {
            errors.Add($"• {label}：请选择密级");
        }

        YearlyArchiveRegisterElectronicMediaItemDetail? detail = item.ElectronicDetail;
        if (detail == null)
        {
            errors.Add($"• {label}：缺少电子介质扩展信息");
            return;
        }

        if (string.IsNullOrWhiteSpace(detail.MaterialCategory))
        {
            errors.Add($"• {label}：请选择资料类型");
        }

        if (string.IsNullOrWhiteSpace(detail.SubCategory))
        {
            errors.Add($"• {label}：请选择所属子类");
        }

        if (string.IsNullOrWhiteSpace(detail.DataOrganizationForm))
        {
            errors.Add($"• {label}：请选择数据组织形式");
        }

        if (detail.DataSizeMb <= 0)
        {
            errors.Add($"• {label}：请填写有效的数据量（MB）");
        }

        if (!ElectronicMediaItemSupport.TryValidateRegistrationStoragePath(
                item.StoragePath,
                out _,
                out string? storagePathError))
        {
            errors.Add($"• {label}：{storagePathError}");
        }
    }

    private static void ValidateMediaDisposition(
        YearlyArchiveRegisterMedia media,
        int mediaSeq,
        List<string> errors)
    {
        var allowedDispositions = NetworkInboundRegisterMediaRulesSupport.GetAllowedElectronicDispositions(
            media.MediaType,
            [media.Disposition?.Trim() ?? string.Empty]);

        string disposition = media.Disposition?.Trim() ?? string.Empty;
        if (allowedDispositions.Count > 0
            && !allowedDispositions.Any(option => string.Equals(option, disposition, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"• 第{mediaSeq}条介质：处置方式与介质类型不匹配");
        }
    }
}
