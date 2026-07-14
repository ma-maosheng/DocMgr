using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料出库办结时，立档入袋硬盘的台账、流转记录与介质袋位置同步。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        private static bool RequiresFiledHardDiskWithdrawalSync(
            YearlyArchiveOutboundItem item,
            YearlyArchiveFilingFact fact) =>
            string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
            && string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
            && !RequiresInStockBlankDiskCompletion(item)
            && ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(ResolveStorageCarrierType(item, fact));

        private async Task CompleteFiledHardDiskWithdrawalAsync(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item,
            YearlyArchiveFilingFact fact,
            string operatorName,
            DateTime operatedAt)
        {
            var media = await ResolveFiledHardDiskMediaAsync(fact);
            if (media.Count == 0)
            {
                throw new InvalidOperationException(
                    $"未找到与立档事实 [{fact.ItemName}] 关联的数据硬盘台账，无法办结资料出库提档。");
            }

            bool needReturn = item.NeedReturn;
            bool isExternalDestination = ArchiveOutboundDomainValues.IsExternalDestination(record.DestinationKind);
            string holder = ResolveOutboundHolder(record);
            string targetOrganization = ResolveOutboundTargetOrganization(record);
            string relatedArchiveTitle = BuildRelatedArchiveTitle(item);
            string restoreReferenceLocation = ResolveFiledHardDiskRestoreReferenceLocation(fact, media[0]);
            string afterLocation = ResolveOutboundStorageLocation(record, restoreReferenceLocation);

            await SyncElectronicArchiveUnitOutboundLocationAsync(fact, afterLocation);
            fact.CurrentStorageLocation = afterLocation;

            foreach (var medium in media)
            {
                var ledger = EnsureHardDiskLedger(medium, operatedAt);
                string currentStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
                if (IsFiledHardDiskOutboundStatus(currentStatus))
                {
                    continue;
                }

                if (!IsFiledHardDiskInStockStatus(currentStatus))
                {
                    throw new InvalidOperationException(
                        $"硬盘 [{medium.DiskCode}] 当前状态为“{currentStatus}”，无法按资料出库提档办结；请核对台账后重试。");
                }

                var before = HardDiskLedgerSyncSupport.CaptureSnapshot(medium);
                ledger.MediaNature = HardDiskMedium.NatureDataCarrier;
                ledger.MediaStatus = HardDiskLedgerSyncSupport.ResolveArchiveOutboundMediaStatus(needReturn, isExternalDestination);
                ledger.HolderOrOrganization = string.IsNullOrWhiteSpace(targetOrganization) ? holder : targetOrganization;
                ledger.StorageLocation = afterLocation;
                ledger.NeedReturn = needReturn;
                ledger.UpdatedTime = operatedAt;
                medium.UpdatedTime = operatedAt;

                if (!HardDiskLedgerSyncSupport.HasLedgerMaterialChange(before, ledger))
                {
                    continue;
                }

                _hardDiskMediaRepository.AddTransaction(
                    HardDiskLedgerSyncSupport.BuildArchiveOutboundSyncTransaction(
                        medium,
                        before,
                        operatorName,
                        operatedAt,
                        $"资料出库提档办结（{record.OutboundNo}）",
                        $"立档数据硬盘交予领用人；明细：{item.ItemName}",
                        record.OutboundNo,
                        relatedArchiveTitle,
                        holder,
                        targetOrganization,
                        needReturn,
                        item.ExpectedReturnDate ?? record.ExpectedReturnDate,
                        isExternalDestination));
            }
        }

        private async Task SyncElectronicArchiveUnitOutboundLocationAsync(YearlyArchiveFilingFact fact, string afterLocation)
        {
            if (fact.ContainerId <= 0)
            {
                return;
            }

            var unit = await _outboundRepository.GetElectronicArchiveUnitByIdForUpdateAsync(fact.ContainerId);
            if (unit != null)
            {
                unit.StorageLocation = afterLocation;
            }
        }

        private async Task<List<HardDiskMedium>> ResolveFiledHardDiskMediaAsync(YearlyArchiveFilingFact fact)
        {
            if (fact.ContainerId > 0)
            {
                var media = await _outboundRepository.GetHardDiskMediaByElectronicUnitIdForUpdateAsync(fact.ContainerId);
                if (media.Count > 0)
                {
                    return media;
                }
            }

            if (!string.IsNullOrWhiteSpace(fact.MediumCode))
            {
                var medium = await _outboundRepository.GetHardDiskMediumByCodeForUpdateAsync(fact.MediumCode);
                if (medium != null)
                {
                    return [medium];
                }
            }

            if (!string.IsNullOrWhiteSpace(fact.ContainerCode))
            {
                var medium = await _outboundRepository.GetHardDiskMediumByCodeForUpdateAsync(fact.ContainerCode);
                if (medium != null)
                {
                    return [medium];
                }
            }

            return new List<HardDiskMedium>();
        }

        private static string ResolveFiledHardDiskRestoreReferenceLocation(
            YearlyArchiveFilingFact fact,
            HardDiskMedium medium)
        {
            string ledgerLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty;
            return FiledElectronicArchiveLocationSupport.ResolveReferenceLocation(fact, ledgerLocation);
        }

        private static bool IsFiledHardDiskInStockStatus(string status) =>
            string.Equals(status, HardDiskMedium.StatusInStockData, StringComparison.Ordinal);

        private static bool IsFiledHardDiskOutboundStatus(string status) =>
            string.Equals(status, HardDiskMedium.StatusOutTemporary, StringComparison.Ordinal)
            || string.Equals(status, HardDiskMedium.StatusOutLongTerm, StringComparison.Ordinal)
            || string.Equals(status, HardDiskMedium.StatusOutPermanent, StringComparison.Ordinal);
    }
}
