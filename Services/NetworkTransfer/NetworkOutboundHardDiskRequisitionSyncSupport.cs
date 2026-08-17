using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网（院内/院外）库内空盘征用：提交加锁、办结同步台账与流转、撤回释锁。
/// </summary>
internal static class NetworkOutboundHardDiskRequisitionSyncSupport
{
    internal static async Task ApplyRequisitionLocksAsync(
        IHardDiskMediaRepository hardDiskMediaRepository,
        NetworkOutboundRecord record)
    {
        if (!NetworkTransferDomainValues.IsExternalOfflineDestination(record.DestinationKind))
        {
            return;
        }

        foreach (YearlyArchiveRegisterMedia media in NetworkOutboundExternalHardDiskRequisitionSupport
                     .EnumerateBlankHardDiskRequisitions(record.MediaEntries)
                     .GroupBy(item => item.RequisitionedMediumId!.Value)
                     .Select(group => group.First()))
        {
            await ApplyRequisitionLockAsync(
                hardDiskMediaRepository,
                record,
                media.RequisitionedMediumId!.Value,
                media.RequisitionedHardDiskCode);
        }
    }

    internal static async Task CompleteRequisitionsAsync(
        IHardDiskMediaRepository hardDiskMediaRepository,
        NetworkOutboundRecord record,
        string operatorName,
        DateTime operatedAt)
    {
        if (!NetworkTransferDomainValues.IsExternalOfflineDestination(record.DestinationKind))
        {
            return;
        }

        var completedMediumIds = new HashSet<int>();
        foreach (YearlyArchiveRegisterMedia media in NetworkOutboundExternalHardDiskRequisitionSupport
                     .EnumerateBlankHardDiskRequisitions(record.MediaEntries))
        {
            int mediumId = media.RequisitionedMediumId!.Value;
            if (!completedMediumIds.Add(mediumId))
            {
                continue;
            }

            await CompleteRequisitionAsync(
                hardDiskMediaRepository,
                record,
                media,
                operatorName,
                operatedAt);
        }
    }

    internal static async Task ReleaseRequisitionLocksAsync(
        IHardDiskMediaRepository hardDiskMediaRepository,
        NetworkOutboundRecord record)
    {
        if (!NetworkTransferDomainValues.IsExternalOfflineDestination(record.DestinationKind))
        {
            return;
        }

        foreach (int mediumId in NetworkOutboundExternalHardDiskRequisitionSupport
                     .EnumerateBlankHardDiskRequisitions(record.MediaEntries)
                     .Select(item => item.RequisitionedMediumId!.Value)
                     .Distinct())
        {
            await TryReleaseRequisitionLockAsync(hardDiskMediaRepository, mediumId, record.OutboundNo);
        }
    }

    private static async Task ApplyRequisitionLockAsync(
        IHardDiskMediaRepository hardDiskMediaRepository,
        NetworkOutboundRecord record,
        int mediumId,
        string? expectedDiskCode)
    {
        HardDiskMedium? medium = await hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(mediumId);
        if (medium == null)
        {
            throw new InvalidOperationException($"未找到库内空盘 [{expectedDiskCode?.Trim() ?? mediumId.ToString()}]。");
        }

        if (medium.RegisterLock != null)
        {
            HardDiskRegisterLock lockItem = medium.RegisterLock;
            if (string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeNetworkOutboundRequisition, StringComparison.Ordinal)
                && string.Equals(lockItem.BusinessNo, record.OutboundNo, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 已被【{lockItem.BusinessNo}】占用，无法征用。");
        }

        string currentStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty;
        if (!string.Equals(currentStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 当前状态为“{currentStatus}”，不可征用。");
        }

        medium.RegisterLock = new HardDiskRegisterLock
        {
            MediumId = medium.Id,
            BusinessType = HardDiskRegisterLock.BusinessTypeNetworkOutboundRequisition,
            BusinessRecordId = record.Id > 0 ? record.Id : null,
            BusinessNo = record.OutboundNo,
            PreviousStatus = currentStatus,
            LockedTime = DateTime.Now
        };
        medium.UpdatedTime = DateTime.Now;
    }

    private static async Task CompleteRequisitionAsync(
        IHardDiskMediaRepository hardDiskMediaRepository,
        NetworkOutboundRecord record,
        YearlyArchiveRegisterMedia media,
        string operatorName,
        DateTime operatedAt)
    {
        int mediumId = media.RequisitionedMediumId!.Value;
        HardDiskMedium? medium = await hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(mediumId)
            ?? throw new InvalidOperationException($"未找到库内空盘 [{media.RequisitionedHardDiskCode}]。");

        HardDiskLedger ledger = EnsureHardDiskLedger(medium, operatedAt);
        string currentStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
        if (!string.Equals(currentStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"硬盘 [{medium.DiskCode}] 当前状态为“{currentStatus}”，无法按出网办结；请核对台账后重试。");
        }

        if (medium.RegisterLock != null)
        {
            HardDiskRegisterLock lockItem = medium.RegisterLock;
            if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeNetworkOutboundRequisition, StringComparison.Ordinal)
                || !string.Equals(lockItem.BusinessNo, record.OutboundNo, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"硬盘 [{medium.DiskCode}] 已被【{lockItem.BusinessNo}】占用，无法办结出网库内空盘征用。");
            }
        }

        HardDiskLedgerSyncSupport.LedgerSnapshot before = HardDiskLedgerSyncSupport.CaptureSnapshot(medium);
        bool needReturn = media.RequisitionedDiskNeedReturn;
        bool isExternalDestination = NetworkTransferDomainValues.IsOutboundExternalDestination(record.DestinationKind);
        string afterStatus = HardDiskLedgerSyncSupport.ResolveArchiveOutboundMediaStatus(needReturn, isExternalDestination);
        string holder = ResolveHolder(record);
        string targetOrganization = ResolveTargetOrganization(record);
        string afterLocation = ResolveStorageLocation(record, before.Location, targetOrganization, holder);

        ledger.MediaNature = HardDiskMedium.NatureDataCarrier;
        ledger.MediaStatus = afterStatus;
        ledger.HolderOrOrganization = holder;
        ledger.StorageLocation = afterLocation;
        ledger.NeedReturn = needReturn;
        ledger.UpdatedTime = operatedAt;
        medium.RegisterLock = null;
        medium.UpdatedTime = operatedAt;

        if (!HardDiskLedgerSyncSupport.HasLedgerMaterialChange(before, ledger))
        {
            return;
        }

        hardDiskMediaRepository.AddTransaction(
            HardDiskLedgerSyncSupport.BuildArchiveOutboundSyncTransaction(
                medium,
                before,
                operatorName,
                operatedAt,
                $"出网申请办结（{record.OutboundNo}）",
                BuildCompletionRemark(record, media),
                record.OutboundNo,
                record.MaterialName?.Trim() ?? string.Empty,
                holder,
                targetOrganization,
                needReturn,
                media.ExpectedReturnDate,
                isExternalDestination));
    }

    private static async Task TryReleaseRequisitionLockAsync(
        IHardDiskMediaRepository hardDiskMediaRepository,
        int mediumId,
        string outboundNo)
    {
        HardDiskMedium? medium = await hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(mediumId);
        if (medium?.RegisterLock == null)
        {
            return;
        }

        HardDiskRegisterLock lockItem = medium.RegisterLock;
        if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeNetworkOutboundRequisition, StringComparison.Ordinal)
            || !string.Equals(lockItem.BusinessNo, outboundNo, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        medium.RegisterLock = null;
        medium.UpdatedTime = DateTime.Now;
    }

    private static HardDiskLedger EnsureHardDiskLedger(HardDiskMedium medium, DateTime now)
    {
        medium.Ledger ??= new HardDiskLedger
        {
            MediumId = medium.Id,
            DiskCode = medium.DiskCode,
            MediaStatus = HardDiskMedium.StatusInStockBlank,
            MediaNature = HardDiskMedium.NatureBlank,
            StorageLocation = string.Empty,
            HolderOrOrganization = "资料室",
            NeedReturn = false,
            RegisterPerson = medium.RegisterPerson,
            RegisterDate = medium.RegisterDate,
            Remark = medium.Remark,
            CreatedTime = medium.CreatedTime == default ? now : medium.CreatedTime,
            UpdatedTime = now
        };

        return medium.Ledger;
    }

    private static string ResolveHolder(NetworkOutboundRecord record) =>
        string.IsNullOrWhiteSpace(record.ApplicantName)
            ? string.Empty
            : record.ApplicantName.Trim();

    private static string ResolveTargetOrganization(NetworkOutboundRecord record)
    {
        if (NetworkTransferDomainValues.IsOutboundExternalDestination(record.DestinationKind))
        {
            return ResolveHolder(record);
        }

        return string.IsNullOrWhiteSpace(record.ApplicantDept)
            ? ResolveHolder(record)
            : record.ApplicantDept.Trim();
    }

    private static string ResolveStorageLocation(
        NetworkOutboundRecord record,
        string beforeLocation,
        string targetOrganization,
        string holder)
    {
        if (!string.IsNullOrWhiteSpace(targetOrganization))
        {
            return targetOrganization;
        }

        if (!string.IsNullOrWhiteSpace(holder))
        {
            return $"借出-{holder}";
        }

        return beforeLocation?.Trim() ?? string.Empty;
    }

    private static string BuildCompletionRemark(NetworkOutboundRecord record, YearlyArchiveRegisterMedia media)
    {
        string materialSummary = record.MaterialName?.Trim() ?? string.Empty;
        string diskCode = media.RequisitionedHardDiskCode?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(materialSummary)
            ? $"库内空盘 [{diskCode}] 写入资料后交予领用人。"
            : $"库内空盘 [{diskCode}] 写入资料后交予领用人；资料：{materialSummary}。";
    }
}
