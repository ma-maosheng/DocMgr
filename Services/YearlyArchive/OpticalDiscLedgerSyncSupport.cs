using DocMgr.Models.OpticalDiscMedia;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 数据光盘台账与流转记录同步规则（资料出库/归还伴生业务）。
    /// </summary>
    internal static class OpticalDiscLedgerSyncSupport
    {
        public readonly record struct LedgerSnapshot(string Status, string Location, string Holder);

        public static LedgerSnapshot CaptureSnapshot(OpticalDiscMedium medium)
        {
            var ledger = medium.Ledger;
            return new LedgerSnapshot(
                ledger?.MediaStatus?.Trim() ?? string.Empty,
                ledger?.StorageLocation?.Trim() ?? string.Empty,
                ledger?.HolderOrOrganization?.Trim() ?? string.Empty);
        }

        public static bool HasLedgerMaterialChange(LedgerSnapshot before, OpticalDiscLedger after)
        {
            return !string.Equals(before.Status, after.MediaStatus?.Trim(), StringComparison.Ordinal)
                || !string.Equals(before.Location, after.StorageLocation?.Trim(), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(before.Holder, after.HolderOrOrganization?.Trim(), StringComparison.Ordinal);
        }

        public static string ResolveArchiveOutboundMediaStatus(bool needReturn) =>
            needReturn ? OpticalDiscMedium.StatusOut : OpticalDiscMedium.StatusDestroyed;

        public static string ResolveArchiveOutboundTransactionType(bool needReturn) =>
            needReturn
                ? OpticalDiscMediaTransaction.TypeOutboundTemporary
                : OpticalDiscMediaTransaction.TypeDestroy;

        public static OpticalDiscMediaTransaction BuildArchiveOutboundSyncTransaction(
            OpticalDiscMedium medium,
            LedgerSnapshot before,
            string operatorName,
            DateTime operatedAt,
            string description,
            string remark,
            string businessNo,
            string relatedArchiveTitle,
            string relatedPerson,
            string targetOrganization,
            bool needReturn,
            DateTime? expectedReturnDate)
        {
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"光盘 [{medium.DiscCode}] 缺少台账信息。");

            return new OpticalDiscMediaTransaction
            {
                Medium = medium,
                TransactionType = ResolveArchiveOutboundTransactionType(needReturn),
                BusinessNo = businessNo,
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
                RelatedBatch = businessNo,
                RelatedArchiveTitle = relatedArchiveTitle,
                Description = description,
                Remark = remark
            };
        }

        public static OpticalDiscMediaTransaction BuildArchiveReturnSyncTransaction(
            OpticalDiscMedium medium,
            LedgerSnapshot before,
            string operatorName,
            DateTime operatedAt,
            string description,
            string remark,
            string businessNo,
            string relatedArchiveTitle)
        {
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"光盘 [{medium.DiscCode}] 缺少台账信息。");

            return new OpticalDiscMediaTransaction
            {
                Medium = medium,
                TransactionType = OpticalDiscMediaTransaction.TypeReturnRegistration,
                BusinessNo = businessNo,
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
