using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还办结时，立档数据光盘的台账、流转记录与介质袋位置反向同步。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        private static bool RequiresFiledOpticalDiscReturnSync(
            YearlyArchiveReturnItem item,
            YearlyArchiveOutboundItem outboundItem,
            YearlyArchiveFilingFact fact) =>
            string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
            && string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
            && IsOpticalDiscArchiveCarrier(ResolveReturnStorageCarrierType(item, outboundItem, fact));

        private static string ResolveReturnStorageCarrierType(
            YearlyArchiveReturnItem item,
            YearlyArchiveOutboundItem outboundItem,
            YearlyArchiveFilingFact fact)
        {
            if (!string.IsNullOrWhiteSpace(outboundItem.StorageCarrierType))
            {
                return outboundItem.StorageCarrierType.Trim();
            }

            return fact.StorageCarrierType?.Trim() ?? string.Empty;
        }

        private static bool IsOpticalDiscArchiveCarrier(string? storageCarrierType) =>
            ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(storageCarrierType);

        private async Task CompleteFiledOpticalDiscReturnAsync(
            YearlyArchiveReturnRecord record,
            YearlyArchiveReturnItem item,
            YearlyArchiveOutboundItem outboundItem,
            YearlyArchiveFilingFact fact,
            string operatorName,
            DateTime operatedAt)
        {
            var discs = await ResolveFiledOpticalDiscMediaAsync(fact);
            if (discs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"未找到与归还明细 [{item.ItemName}] 关联的数据光盘台账，无法办结资料归还。");
            }

            string restoreLocation = ResolveFiledOpticalDiscRestoreLocation(item, fact, discs[0]);
            string relatedArchiveTitle = !string.IsNullOrWhiteSpace(item.MaterialName)
                ? item.MaterialName.Trim()
                : item.ItemName?.Trim() ?? string.Empty;

            await SyncElectronicArchiveUnitReturnLocationAsync(fact, restoreLocation);
            fact.CurrentStorageLocation = restoreLocation;

            foreach (var disc in discs)
            {
                var ledger = EnsureOpticalDiscLedger(disc, operatedAt);
                string currentStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
                if (string.Equals(currentStatus, OpticalDiscMedium.StatusInStock, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(currentStatus, OpticalDiscMedium.StatusOut, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"光盘 [{disc.DiscCode}] 当前状态为“{currentStatus}”，无法按资料归还办结入库；请核对台账后重试。");
                }

                var before = OpticalDiscLedgerSyncSupport.CaptureSnapshot(disc);
                ledger.MediaStatus = OpticalDiscMedium.StatusInStock;
                ledger.HolderOrOrganization = "资料室";
                ledger.StorageLocation = restoreLocation;
                ledger.NeedReturn = false;
                ledger.UpdatedTime = operatedAt;
                disc.UpdatedTime = operatedAt;

                if (!OpticalDiscLedgerSyncSupport.HasLedgerMaterialChange(before, ledger))
                {
                    continue;
                }

                disc.Transactions.Add(
                    OpticalDiscLedgerSyncSupport.BuildArchiveReturnSyncTransaction(
                        disc,
                        before,
                        operatorName,
                        operatedAt,
                        $"资料归还办结（{record.ReturnNo}）",
                        $"立档数据光盘收回入库；明细：{item.ItemName}",
                        record.ReturnNo,
                        relatedArchiveTitle));
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

        private static string ResolveFiledOpticalDiscRestoreLocation(
            YearlyArchiveReturnItem item,
            YearlyArchiveFilingFact fact,
            OpticalDiscMedium disc)
        {
            string ledgerLocation = disc.Ledger?.StorageLocation?.Trim() ?? string.Empty;
            return FiledElectronicArchiveLocationSupport.ResolveRestoreLocation(item, fact, ledgerLocation);
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
