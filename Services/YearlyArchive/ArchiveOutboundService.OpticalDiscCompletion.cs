using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料出库办结时，立档数据光盘的台账、流转记录与介质袋位置同步。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        private static bool RequiresFiledOpticalDiscWithdrawalSync(
            YearlyArchiveOutboundItem item,
            YearlyArchiveFilingFact fact) =>
            string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
            && string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
            && IsOpticalDiscArchiveCarrier(ResolveStorageCarrierType(item, fact));

        private static string ResolveStorageCarrierType(YearlyArchiveOutboundItem item, YearlyArchiveFilingFact fact)
        {
            if (!string.IsNullOrWhiteSpace(item.StorageCarrierType))
            {
                return item.StorageCarrierType.Trim();
            }

            return fact.StorageCarrierType?.Trim() ?? string.Empty;
        }

        private static bool IsOpticalDiscArchiveCarrier(string? storageCarrierType) =>
            ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(storageCarrierType);

        private async Task CompleteFiledOpticalDiscWithdrawalAsync(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item,
            YearlyArchiveFilingFact fact,
            string operatorName,
            DateTime operatedAt)
        {
            var discs = await ResolveFiledOpticalDiscMediaAsync(fact);
            if (discs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"未找到与立档事实 [{fact.ItemName}] 关联的数据光盘台账，无法办结资料出库提档。");
            }

            bool needReturn = item.NeedReturn;
            string holder = ResolveOutboundHolder(record);
            string targetOrganization = ResolveOutboundTargetOrganization(record);
            string restoreReferenceLocation = ResolveFiledOpticalDiscRestoreReferenceLocation(fact, discs[0]);
            string afterLocation = ResolveOutboundStorageLocation(record, restoreReferenceLocation);
            string relatedArchiveTitle = BuildRelatedArchiveTitle(item);

            await SyncElectronicArchiveUnitOutboundLocationAsync(fact, afterLocation);
            fact.CurrentStorageLocation = afterLocation;

            foreach (var disc in discs)
            {
                var ledger = EnsureOpticalDiscLedger(disc, operatedAt);
                string currentStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
                if (!string.Equals(currentStatus, OpticalDiscMedium.StatusInStock, StringComparison.Ordinal))
                {
                    if (string.Equals(currentStatus, OpticalDiscMedium.StatusOut, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"光盘 [{disc.DiscCode}] 当前状态为“{currentStatus}”，无法按资料出库提档办结；请核对台账后重试。");
                }

                var before = OpticalDiscLedgerSyncSupport.CaptureSnapshot(disc);
                ledger.MediaStatus = OpticalDiscLedgerSyncSupport.ResolveArchiveOutboundMediaStatus(needReturn);
                ledger.HolderOrOrganization = string.IsNullOrWhiteSpace(targetOrganization) ? holder : targetOrganization;
                ledger.StorageLocation = afterLocation;
                ledger.NeedReturn = needReturn;
                ledger.UpdatedTime = operatedAt;
                disc.UpdatedTime = operatedAt;

                if (!OpticalDiscLedgerSyncSupport.HasLedgerMaterialChange(before, ledger))
                {
                    continue;
                }

                disc.Transactions.Add(
                    OpticalDiscLedgerSyncSupport.BuildArchiveOutboundSyncTransaction(
                        disc,
                        before,
                        operatorName,
                        operatedAt,
                        $"资料出库提档办结（{record.OutboundNo}）",
                        $"立档数据光盘交予领用人；明细：{item.ItemName}",
                        record.OutboundNo,
                        relatedArchiveTitle,
                        holder,
                        targetOrganization,
                        needReturn,
                        item.ExpectedReturnDate ?? record.ExpectedReturnDate));
            }
        }

        private async Task<List<OpticalDiscMedium>> ResolveFiledOpticalDiscMediaAsync(YearlyArchiveFilingFact fact)
        {
            if (fact.ContainerId > 0)
            {
                var discs = await _outboundRepository.GetOpticalDiscMediaByElectronicUnitIdForUpdateAsync(fact.ContainerId);
                if (discs.Count > 0)
                {
                    return discs;
                }
            }

            if (!string.IsNullOrWhiteSpace(fact.MediumCode))
            {
                var disc = await _outboundRepository.GetOpticalDiscMediumByCodeForUpdateAsync(fact.MediumCode);
                if (disc != null)
                {
                    return [disc];
                }
            }

            if (!string.IsNullOrWhiteSpace(fact.ContainerCode))
            {
                var disc = await _outboundRepository.GetOpticalDiscMediumByCodeForUpdateAsync(fact.ContainerCode);
                if (disc != null)
                {
                    return [disc];
                }
            }

            return new List<OpticalDiscMedium>();
        }

        private static string ResolveFiledOpticalDiscRestoreReferenceLocation(
            YearlyArchiveFilingFact fact,
            OpticalDiscMedium disc)
        {
            string ledgerLocation = disc.Ledger?.StorageLocation?.Trim() ?? string.Empty;
            return FiledElectronicArchiveLocationSupport.ResolveReferenceLocation(fact, ledgerLocation);
        }

        private static OpticalDiscLedger EnsureOpticalDiscLedger(OpticalDiscMedium medium, DateTime now)
        {
            medium.Ledger ??= new OpticalDiscLedger
            {
                MediumId = medium.Id,
                DiscCode = medium.DiscCode,
                MediaStatus = OpticalDiscMedium.StatusInStock,
                HolderOrOrganization = "资料室",
                StorageLocation = string.Empty,
                RegisterPerson = medium.RegisterPerson,
                RegisterDate = medium.RegisterDate,
                CreatedTime = medium.CreatedTime == default ? now : medium.CreatedTime,
                UpdatedTime = now
            };

            return medium.Ledger;
        }
    }
}
