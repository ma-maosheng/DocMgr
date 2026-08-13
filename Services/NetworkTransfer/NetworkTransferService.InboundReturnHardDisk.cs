using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.NetworkTransfer;

public sealed partial class NetworkTransferService
{
    /// <summary>
    /// 资料入网办结后，为已声明归还的借出硬盘办理空盘归还登记。
    /// </summary>
    private async Task CompleteInboundReturnHardDisksAsync(
        NetworkInboundRecord record,
        User currentUser,
        DateTime completedAt)
    {
        if (!record.ReturnBorrowedHardDiskWithInbound || record.ReturnHardDiskItems.Count == 0)
        {
            return;
        }

        string inboundNo = record.InboundNo.Trim();
        string projectName = record.ProjectName.Trim();
        foreach (NetworkInboundReturnHardDiskItem item in record.ReturnHardDiskItems.OrderBy(row => row.SortOrder))
        {
            HardDiskMediaReturnCandidate? candidate =
                await _hardDiskMediaService.GetReturnRegistrationCandidateByDiskCodeAsync(item.DiskCode);
            if (candidate == null)
            {
                throw new InvalidOperationException($"未找到借出硬盘 [{item.DiskCode}] 的归还登记候选信息。");
            }

            string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.TargetBlankSlotLocation);
            if (string.IsNullOrWhiteSpace(targetLocation))
            {
                throw new InvalidOperationException($"借出硬盘 [{item.DiskCode}] 缺少空白硬盘归位档口。");
            }

            await _hardDiskMediaService.CompleteBlankReturnFromNetworkInboundAsync(
                candidate,
                targetLocation,
                inboundNo,
                projectName,
                currentUser,
                completedAt);
        }
    }
}
