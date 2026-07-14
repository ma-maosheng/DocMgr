using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还办结时，立档入袋硬盘的台账、流转记录与介质袋位置反向同步。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        private static bool RequiresFiledHardDiskReturnSync(
            YearlyArchiveReturnItem item,
            YearlyArchiveOutboundItem outboundItem,
            YearlyArchiveFilingFact fact) =>
            string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
            && string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
            && ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(ResolveReturnStorageCarrierType(item, outboundItem, fact));

        private async Task CompleteFiledHardDiskReturnAsync(
            YearlyArchiveReturnRecord record,
            YearlyArchiveReturnItem item,
            YearlyArchiveOutboundItem outboundItem,
            YearlyArchiveFilingFact fact,
            string operatorName,
            DateTime operatedAt)
        {
            var media = await ResolveFiledHardDiskMediaAsync(fact);
            if (media.Count == 0)
            {
                throw new InvalidOperationException(
                    $"未找到与归还明细 [{item.ItemName}] 关联的数据硬盘台账，无法办结资料归还。");
            }

            string restoreLocation = ResolveFiledHardDiskRestoreLocation(item, fact, media[0]);
            string relatedArchiveTitle = !string.IsNullOrWhiteSpace(item.MaterialName)
                ? item.MaterialName.Trim()
                : item.ItemName?.Trim() ?? string.Empty;

            await SyncElectronicArchiveUnitReturnLocationAsync(fact, restoreLocation);
            fact.CurrentStorageLocation = restoreLocation;

            foreach (var medium in media)
            {
                var ledger = EnsureHardDiskLedger(medium, operatedAt);
                string currentStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
                if (IsFiledHardDiskInStockStatus(currentStatus))
                {
                    continue;
                }

                if (!IsFiledHardDiskOutboundStatus(currentStatus))
                {
                    throw new InvalidOperationException(
                        $"硬盘 [{medium.DiskCode}] 当前状态为“{currentStatus}”，无法按资料归还办结入库；请核对台账后重试。");
                }

                var before = HardDiskLedgerSyncSupport.CaptureSnapshot(medium);
                ledger.MediaNature = HardDiskMedium.NatureDataCarrier;
                ledger.MediaStatus = HardDiskMedium.StatusInStockData;
                ledger.HolderOrOrganization = "资料室";
                ledger.StorageLocation = restoreLocation;
                ledger.NeedReturn = false;
                ledger.UpdatedTime = operatedAt;
                medium.UpdatedTime = operatedAt;

                if (!HardDiskLedgerSyncSupport.HasLedgerMaterialChange(before, ledger))
                {
                    continue;
                }

                _hardDiskMediaRepository.AddTransaction(
                    HardDiskLedgerSyncSupport.BuildArchiveReturnSyncTransaction(
                        medium,
                        before,
                        operatorName,
                        operatedAt,
                        $"资料归还办结（{record.ReturnNo}）",
                        $"立档数据硬盘收回入库；明细：{item.ItemName}",
                        record.ReturnNo,
                        relatedArchiveTitle));
            }
        }

        private async Task SyncElectronicArchiveUnitReturnLocationAsync(YearlyArchiveFilingFact fact, string restoreLocation)
        {
            if (fact.ContainerId <= 0)
            {
                return;
            }

            var unit = await _outboundRepository.GetElectronicArchiveUnitByIdForUpdateAsync(fact.ContainerId);
            if (unit != null)
            {
                unit.StorageLocation = restoreLocation;
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

        private static string ResolveFiledHardDiskRestoreLocation(
            YearlyArchiveReturnItem item,
            YearlyArchiveFilingFact fact,
            HardDiskMedium medium)
        {
            string ledgerLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty;
            return FiledElectronicArchiveLocationSupport.ResolveRestoreLocation(item, fact, ledgerLocation);
        }

        private static HardDiskLedger EnsureHardDiskLedger(HardDiskMedium medium, DateTime now)
        {
            medium.Ledger ??= new HardDiskLedger
            {
                MediumId = medium.Id,
                DiskCode = medium.DiskCode,
                MediaStatus = HardDiskMedium.StatusInStockData,
                MediaNature = HardDiskMedium.NatureDataCarrier,
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

        private static bool IsFiledHardDiskInStockStatus(string status) =>
            string.Equals(status, HardDiskMedium.StatusInStockData, StringComparison.Ordinal);

        private static bool IsFiledHardDiskOutboundStatus(string status) =>
            string.Equals(status, HardDiskMedium.StatusOutTemporary, StringComparison.Ordinal)
            || string.Equals(status, HardDiskMedium.StatusOutLongTerm, StringComparison.Ordinal)
            || string.Equals(status, HardDiskMedium.StatusOutPermanent, StringComparison.Ordinal);
    }
}
