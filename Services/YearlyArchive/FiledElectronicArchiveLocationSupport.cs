using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 立档电子介质（硬盘/光盘）提档、归还时的档口位置解析与展示规则。
    /// </summary>
    internal static class FiledElectronicArchiveLocationSupport
    {
        public static bool IsSlotLikeLocation(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            return ArchiveSlotLocationSupport.TryParseSlotLocation(location, out _, out _, out _, out _);
        }

        /// <summary>
        /// 提档办结前解析参考位置：优先在库介质台账档口，其次立档事实当前/原始位置。
        /// </summary>
        public static string ResolveReferenceLocation(
            YearlyArchiveFilingFact fact,
            string? ledgerLocation)
        {
            ArgumentNullException.ThrowIfNull(fact);

            string trimmedLedgerLocation = ledgerLocation?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(trimmedLedgerLocation))
            {
                return trimmedLedgerLocation;
            }

            if (!string.IsNullOrWhiteSpace(fact.CurrentStorageLocation))
            {
                return fact.CurrentStorageLocation.Trim();
            }

            return fact.StorageLocation?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 归还办结时解析归位档口：优先归还明细原存位置，其次在库台账档口键，再次立档事实位置。
        /// </summary>
        public static string ResolveRestoreLocation(
            YearlyArchiveReturnItem item,
            YearlyArchiveFilingFact fact,
            string? ledgerLocation)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(fact);

            if (!string.IsNullOrWhiteSpace(item.StorageLocation))
            {
                return item.StorageLocation.Trim();
            }

            string trimmedLedgerLocation = ledgerLocation?.Trim() ?? string.Empty;
            if (IsSlotLikeLocation(trimmedLedgerLocation))
            {
                return trimmedLedgerLocation;
            }

            if (!string.IsNullOrWhiteSpace(fact.CurrentStorageLocation) && IsSlotLikeLocation(fact.CurrentStorageLocation))
            {
                return fact.CurrentStorageLocation.Trim();
            }

            return fact.StorageLocation?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 检索/借出申请展示用：在库且台账为档口键时优先台账，否则沿用立档事实当前位置。
        /// </summary>
        public static string ResolveCurrentDisplayLocation(
            YearlyArchiveFilingFact fact,
            string? ledgerLocation)
        {
            ArgumentNullException.ThrowIfNull(fact);

            string fromFact = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                ? fact.StorageLocation?.Trim() ?? string.Empty
                : fact.CurrentStorageLocation.Trim();

            string trimmedLedgerLocation = ledgerLocation?.Trim() ?? string.Empty;
            if (IsSlotLikeLocation(trimmedLedgerLocation))
            {
                return trimmedLedgerLocation;
            }

            return fromFact;
        }
    }
}
