using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料出库办结时，库内空盘征用后的硬盘台账与流转记录同步。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        private static bool RequiresInStockBlankDiskCompletion(YearlyArchiveOutboundItem item) =>
            item.RequisitionedMediumId is > 0
            && string.Equals(
                item.ElectronicMediaSource,
                ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank,
                StringComparison.Ordinal);

        private async Task CompleteInStockBlankDiskOutboundAsync(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item,
            string operatorName,
            DateTime operatedAt,
            string usageLabel)
        {
            int mediumId = item.RequisitionedMediumId!.Value;
            var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(mediumId)
                ?? throw new InvalidOperationException($"未找到库内空盘 [{item.RequisitionedDiskCode}]。");

            var ledger = EnsureHardDiskLedger(medium, operatedAt);
            string currentStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
            if (!string.Equals(currentStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"硬盘 [{medium.DiskCode}] 当前状态为“{currentStatus}”，无法按资料出库{usageLabel}办结；请核对台账后重试。");
            }

            if (medium.RegisterLock != null)
            {
                var lockItem = medium.RegisterLock;
                if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeArchiveOutboundRequisition, StringComparison.Ordinal)
                    || !string.Equals(lockItem.BusinessNo, record.OutboundNo, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"硬盘 [{medium.DiskCode}] 已被【{lockItem.BusinessNo}】占用，无法办结资料出库{usageLabel}。");
                }
            }

            var before = HardDiskLedgerSyncSupport.CaptureSnapshot(medium);
            bool needReturn = ResolveRequisitionedDiskNeedReturn(item);
            bool isExternalDestination = ArchiveOutboundDomainValues.IsExternalDestination(record.DestinationKind);
            string afterStatus = HardDiskLedgerSyncSupport.ResolveArchiveOutboundMediaStatus(needReturn, isExternalDestination);
            string afterLocation = ResolveOutboundStorageLocation(record, before.Location);
            string holder = ResolveOutboundHolder(record);
            string targetOrganization = ResolveOutboundTargetOrganization(record);

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

            _hardDiskMediaRepository.AddTransaction(
                HardDiskLedgerSyncSupport.BuildArchiveOutboundSyncTransaction(
                    medium,
                    before,
                    operatorName,
                    operatedAt,
                    $"资料出库{usageLabel}办结（{record.OutboundNo}）",
                    $"库内空盘写入资料后交予领用人；明细：{item.ItemName}",
                    record.OutboundNo,
                    BuildRelatedArchiveTitle(item),
                    holder,
                    targetOrganization,
                    needReturn,
                    item.ExpectedReturnDate ?? record.ExpectedReturnDate,
                    isExternalDestination));
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

        private static bool ResolveRequisitionedDiskNeedReturn(YearlyArchiveOutboundItem item) =>
            item.UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate
                ? item.RequisitionedDiskNeedReturn
                : item.NeedReturn;

        private static string ResolveOutboundHolder(YearlyArchiveOutboundRecord record) =>
            string.IsNullOrWhiteSpace(record.ApplicantName)
                ? string.Empty
                : record.ApplicantName.Trim();

        private static string ResolveOutboundTargetOrganization(YearlyArchiveOutboundRecord record)
        {
            if (ArchiveOutboundDomainValues.IsExternalDestination(record.DestinationKind))
            {
                return string.IsNullOrWhiteSpace(record.ExternalUnit)
                    ? string.Empty
                    : record.ExternalUnit.Trim();
            }

            return string.IsNullOrWhiteSpace(record.ApplicantDept)
                ? ResolveOutboundHolder(record)
                : record.ApplicantDept.Trim();
        }

        private static string ResolveOutboundStorageLocation(YearlyArchiveOutboundRecord record, string beforeLocation)
        {
            string targetOrganization = ResolveOutboundTargetOrganization(record);
            if (!string.IsNullOrWhiteSpace(targetOrganization))
            {
                return targetOrganization;
            }

            string holder = ResolveOutboundHolder(record);
            if (!string.IsNullOrWhiteSpace(holder))
            {
                return $"借出-{holder}";
            }

            return beforeLocation?.Trim() ?? string.Empty;
        }

        private static string BuildRelatedArchiveTitle(YearlyArchiveOutboundItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.MaterialName))
            {
                return item.MaterialName.Trim();
            }

            return item.ItemName?.Trim() ?? string.Empty;
        }
    }
}
