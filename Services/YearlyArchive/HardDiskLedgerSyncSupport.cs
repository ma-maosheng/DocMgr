using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 硬盘台账与流转记录同步规则：进出资料室、档口变化、同档口内序号变化均需留痕。
    /// </summary>
    internal static class HardDiskLedgerSyncSupport
    {
        public readonly record struct LedgerSnapshot(string Status, string Location, string Nature);

        public static LedgerSnapshot CaptureSnapshot(HardDiskMedium medium)
        {
            var ledger = medium.Ledger;
            return new LedgerSnapshot(
                ledger?.MediaStatus?.Trim() ?? string.Empty,
                ledger?.StorageLocation?.Trim() ?? string.Empty,
                ledger?.MediaNature?.Trim() ?? string.Empty);
        }

        public static bool IsSameFullLocation(string? left, string? right)
            => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

        public static bool HasLedgerMaterialChange(LedgerSnapshot before, HardDiskLedger after)
        {
            return !string.Equals(before.Status, after.MediaStatus?.Trim(), StringComparison.Ordinal)
                || !IsSameFullLocation(before.Location, after.StorageLocation)
                || !string.Equals(before.Nature, after.MediaNature?.Trim(), StringComparison.Ordinal);
        }

        public static string ResolveSyncTransactionType(LedgerSnapshot before, HardDiskLedger after)
        {
            bool locationChanged = !IsSameFullLocation(before.Location, after.StorageLocation);
            bool statusChanged = !string.Equals(before.Status, after.MediaStatus?.Trim(), StringComparison.Ordinal);

            if (statusChanged
                && string.Equals(before.Status, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                return HardDiskMediaTransaction.TypeRegister;
            }

            if (statusChanged)
            {
                return HardDiskMediaTransaction.TypeReturnRegistration;
            }

            if (locationChanged)
            {
                return HardDiskMediaTransaction.TypeRelocate;
            }

            return HardDiskMediaTransaction.TypeRegister;
        }

        public static HardDiskMediaTransaction BuildSyncTransaction(
            HardDiskMedium medium,
            LedgerSnapshot before,
            string operatorName,
            DateTime operatedAt,
            string description,
            string remark,
            string relatedBatch,
            string relatedArchiveTitle)
        {
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 缺少台账信息。");

            return new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                TransactionType = ResolveSyncTransactionType(before, ledger),
                BeforeStatus = before.Status,
                AfterStatus = ledger.MediaStatus?.Trim() ?? string.Empty,
                BeforeLocation = before.Location,
                AfterLocation = ledger.StorageLocation?.Trim() ?? string.Empty,
                OperatorName = operatorName,
                OperateTime = operatedAt,
                TargetOrganization = "资料室",
                RelatedBatch = relatedBatch,
                RelatedArchiveTitle = relatedArchiveTitle,
                Description = description,
                Remark = remark
            };
        }

        /// <summary>
        /// 资料出库办结：库内空盘写入资料后交予领用人（单次实物交接，不经过在库资料中间态）。
        /// </summary>
        public static string ResolveArchiveOutboundMediaStatus(bool needReturn, bool isExternalDestination)
        {
            if (!needReturn)
            {
                return HardDiskMedium.StatusOutPermanent;
            }

            return isExternalDestination
                ? HardDiskMedium.StatusOutLongTerm
                : HardDiskMedium.StatusOutTemporary;
        }

        public static string ResolveArchiveOutboundTransactionType(bool needReturn, bool isExternalDestination)
        {
            if (!needReturn)
            {
                return HardDiskMediaTransaction.TypeOutboundPermanent;
            }

            return isExternalDestination
                ? HardDiskMediaTransaction.TypeOutboundLongTerm
                : HardDiskMediaTransaction.TypeOutboundTemporary;
        }

        public static HardDiskMediaTransaction BuildArchiveOutboundSyncTransaction(
            HardDiskMedium medium,
            LedgerSnapshot before,
            string operatorName,
            DateTime operatedAt,
            string description,
            string remark,
            string relatedBatch,
            string relatedArchiveTitle,
            string relatedPerson,
            string targetOrganization,
            bool needReturn,
            DateTime? expectedReturnDate,
            bool isExternalDestination)
        {
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 缺少台账信息。");

            return new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                ApplicationId = null,
                TransactionType = ResolveArchiveOutboundTransactionType(needReturn, isExternalDestination),
                BeforeStatus = before.Status,
                AfterStatus = ledger.MediaStatus?.Trim() ?? string.Empty,
                BeforeLocation = before.Location,
                AfterLocation = ledger.StorageLocation?.Trim() ?? string.Empty,
                OperatorName = operatorName,
                OperateTime = operatedAt,
                RelatedPerson = relatedPerson,
                TargetOrganization = targetOrganization,
                NeedReturn = needReturn,
                ExpectedReturnDate = needReturn ? expectedReturnDate : null,
                RelatedBatch = relatedBatch,
                RelatedArchiveTitle = relatedArchiveTitle,
                Description = description,
                Remark = remark
            };
        }

        /// <summary>
        /// 资料归还办结：立档数据硬盘收回入库。
        /// </summary>
        public static HardDiskMediaTransaction BuildArchiveReturnSyncTransaction(
            HardDiskMedium medium,
            LedgerSnapshot before,
            string operatorName,
            DateTime operatedAt,
            string description,
            string remark,
            string businessNo,
            string relatedArchiveTitle)
        {
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 缺少台账信息。");

            return new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                TransactionType = HardDiskMediaTransaction.TypeReturnRegistration,
                BeforeStatus = before.Status,
                AfterStatus = ledger.MediaStatus?.Trim() ?? string.Empty,
                BeforeLocation = before.Location,
                AfterLocation = ledger.StorageLocation?.Trim() ?? string.Empty,
                OperatorName = operatorName,
                OperateTime = operatedAt,
                TargetOrganization = "资料室",
                NeedReturn = false,
                ActualReturnDate = operatedAt,
                RelatedBatch = businessNo,
                RelatedArchiveTitle = relatedArchiveTitle,
                Description = description,
                Remark = remark
            };
        }
    }
}
