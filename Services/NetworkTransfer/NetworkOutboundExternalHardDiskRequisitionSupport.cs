using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网外部离线·硬盘·介质带走场景下的库内空盘征用校验。
/// </summary>
internal static class NetworkOutboundExternalHardDiskRequisitionSupport
{
    internal static bool IsExternalOfflineReturnedHardDiskMedia(YearlyArchiveRegisterMedia media) =>
        RegisterMediaTreeSupport.IsElectronicMediaEntity(media)
        && string.Equals(
            media.MediaType?.Trim(),
            ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk,
            StringComparison.OrdinalIgnoreCase)
        && NetworkTransferDomainValues.IsOutboundTakeAwayDisposition(media.Disposition);

    internal static bool RequiresExpectedReturnDate(YearlyArchiveRegisterMedia media) =>
        media.UseInStockBlankHardDisk
        && media.RequisitionedDiskNeedReturn
        && media.RequisitionedMediumId is > 0;

    internal static void ValidateForSubmit(
        IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries,
        List<string> errors)
    {
        if (mediaEntries == null)
        {
            return;
        }

        int mediaSeq = 0;
        foreach (YearlyArchiveRegisterMedia media in mediaEntries.Where(RegisterMediaTreeSupport.IsElectronicMediaEntity))
        {
            mediaSeq++;
            if (!IsExternalOfflineReturnedHardDiskMedia(media))
            {
                continue;
            }

            if (media.UseInStockBlankHardDisk)
            {
                if (media.RequisitionedMediumId is not > 0
                    || string.IsNullOrWhiteSpace(media.RequisitionedHardDiskCode))
                {
                    errors.Add($"• 第{mediaSeq}条介质：使用库内空盘时须选择资料室库存空盘硬盘编号");
                }

                if (RequiresExpectedReturnDate(media) && media.ExpectedReturnDate == null)
                {
                    errors.Add($"• 第{mediaSeq}条介质：库内空盘需归还时请填写预计归还日期");
                }
            }
            else if (media.RequisitionedMediumId is > 0
                     || !string.IsNullOrWhiteSpace(media.RequisitionedHardDiskCode))
            {
                errors.Add($"• 第{mediaSeq}条介质：未使用库内空盘时不应保留征用硬盘编号");
            }
        }
    }
}
