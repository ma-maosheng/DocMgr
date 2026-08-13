using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网审批阶段对申请内容的有限补录（密级、服务器路径等）。
/// </summary>
internal static class NetworkInboundApprovalAmendmentSupport
{
    internal static void MergeExternalMediaConfidentialLevels(
        NetworkInboundRecord target,
        IReadOnlyList<YearlyArchiveRegisterMedia>? sourceMediaEntries)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (sourceMediaEntries == null || sourceMediaEntries.Count == 0)
        {
            return;
        }

        foreach (YearlyArchiveRegisterMedia sourceMedia in sourceMediaEntries)
        {
            YearlyArchiveRegisterMedia? targetMedia = target.MediaEntries
                .FirstOrDefault(item => item.Id > 0 && item.Id == sourceMedia.Id);
            if (targetMedia?.Items == null)
            {
                continue;
            }

            foreach (YearlyArchiveRegisterMediaItem sourceItem in sourceMedia.Items)
            {
                if (sourceItem.Id <= 0)
                {
                    continue;
                }

                YearlyArchiveRegisterMediaItem? targetItem = targetMedia.Items
                    .FirstOrDefault(item => item.Id == sourceItem.Id);
                if (targetItem == null)
                {
                    continue;
                }

                targetItem.ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(
                    sourceItem.ConfidentialLevel);
            }
        }
    }

    internal static IReadOnlyList<string> ValidateExternalMediaConfidentialLevels(
        IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        var errors = new List<string>();
        int mediaSeq = 0;
        foreach (YearlyArchiveRegisterMedia media in mediaEntries ?? [])
        {
            if (!RegisterMediaTreeSupport.IsElectronicMediaEntity(media))
            {
                continue;
            }

            mediaSeq++;
            if (media.Items == null)
            {
                continue;
            }

            for (int itemIndex = 0; itemIndex < media.Items.Count; itemIndex++)
            {
                YearlyArchiveRegisterMediaItem item = media.Items[itemIndex];
                if (string.IsNullOrWhiteSpace(item.ConfidentialLevel))
                {
                    errors.Add($"• 第{mediaSeq}条介质第{itemIndex + 1}项：请选择密级");
                }
            }
        }

        return errors;
    }
}
