using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网电子介质树校验。申请阶段无法扫描目录/文件，提交不核验数据量与明细个数；办结前须补全数据量。
/// </summary>
internal static class NetworkOutboundExternalMediaValidationSupport
{
    internal static int CountMediaItems(IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries) =>
        NetworkInboundExternalMediaValidationSupport.CountMediaItems(mediaEntries);

    internal static void ValidateForSubmit(
        NetworkOutboundRecord header,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(header.ServerPath))
        {
            errors.Add("请选择服务器路径。");
        }

        if (string.IsNullOrWhiteSpace(header.MaterialPath))
        {
            errors.Add("出网资料所在具体路径尚未生成，请先选择服务器路径并完善年度、项目与资料名称。");
        }

        ValidateMediaTree(header.DestinationKind, mediaEntries, errors, requireDataSize: false);
        if (NetworkTransferDomainValues.IsExternalOfflineDestination(header.DestinationKind))
        {
            NetworkOutboundExternalHardDiskRequisitionSupport.ValidateForSubmit(mediaEntries, errors);
        }
    }

    internal static void ValidateForComplete(
        string? destinationKind,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries,
        List<string> errors)
    {
        ValidateMediaTree(destinationKind, mediaEntries, errors, requireDataSize: true);
        if (NetworkTransferDomainValues.IsExternalOfflineDestination(destinationKind))
        {
            NetworkOutboundExternalHardDiskRequisitionSupport.ValidateForSubmit(mediaEntries, errors);
        }
    }

    private static void ValidateMediaTree(
        string? destinationKind,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries,
        List<string> errors,
        bool requireDataSize)
    {
        var entries = mediaEntries?.Where(RegisterMediaTreeSupport.IsElectronicMediaEntity).ToList()
            ?? [];
        if (entries.Count == 0 || entries.All(media => media.Items == null || media.Items.Count == 0))
        {
            errors.Add("请至少录入一条出网明细（资料介质电子子项）。");
            return;
        }

        int mediaSeq = 0;
        foreach (YearlyArchiveRegisterMedia media in entries)
        {
            mediaSeq++;
            if (string.IsNullOrWhiteSpace(media.MediaType))
            {
                errors.Add($"• 第{mediaSeq}条介质：请选择电子介质类型");
            }
            else if (!NetworkOutboundRegisterMediaRulesSupport.IsAllowedOutboundElectronicMediaType(
                         destinationKind,
                         media.MediaType))
            {
                string allowedHint = NetworkOutboundRegisterMediaRulesSupport.FormatAllowedMediaTypesHint(destinationKind);
                errors.Add($"• 第{mediaSeq}条介质：当前目的地仅允许选择{allowedHint}");
            }

            if (!string.IsNullOrWhiteSpace(media.Disposition)
                && !NetworkOutboundRegisterMediaRulesSupport.IsDispositionAllowedForDestination(
                    destinationKind,
                    media.Disposition))
            {
                string required = NetworkOutboundRegisterMediaRulesSupport.ResolveRequiredDisposition(destinationKind);
                errors.Add($"• 第{mediaSeq}条介质：当前目的地仅允许处置方式为「{required}」");
            }

            if (media.Items == null || media.Items.Count == 0)
            {
                errors.Add($"• 第{mediaSeq}条介质至少需要填写一条内容明细");
                continue;
            }

            for (int itemIndex = 0; itemIndex < media.Items.Count; itemIndex++)
            {
                ValidateMediaItem(media.Items[itemIndex], mediaSeq, itemIndex + 1, errors, requireDataSize);
            }
        }
    }

    private static void ValidateMediaItem(
        YearlyArchiveRegisterMediaItem item,
        int mediaSeq,
        int itemSeq,
        List<string> errors,
        bool requireDataSize)
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

        if (requireDataSize && detail.DataSizeMb <= 0)
        {
            errors.Add($"• {label}：请从离线介质读取或填写有效的数据量（MB）");
        }
    }
}
