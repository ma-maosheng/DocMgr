using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案盒 / 电子介质袋内容表格展示字段解析。
    /// </summary>
    public static class CabinetArchiveBoxContentDisplaySupport
    {
        /// <summary>
        /// 取介质类型与载体类别中更具体的一项（排除“模拟/电子”等介质种类泛称）。
        /// </summary>
        public static string ResolveCarrierTypeText(
            YearlyArchiveFilingFact fact,
            CabinetArchiveBoxMediaItemSupplement supplement)
        {
            string mediaType = supplement.MediaType?.Trim() ?? string.Empty;
            string storageCarrierType = fact.StorageCarrierType?.Trim() ?? string.Empty;
            string mediaKind = fact.MediaKind?.Trim() ?? string.Empty;

            if (IsSpecificCarrierLabel(mediaType))
            {
                return mediaType;
            }

            if (IsSpecificCarrierLabel(storageCarrierType))
            {
                return storageCarrierType;
            }

            return FirstNonEmpty(mediaType, storageCarrierType, mediaKind);
        }

        public static string ResolveProjectYear(string registerProjectYear, string? containerYear)
        {
            string normalizedRegisterYear = registerProjectYear?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedRegisterYear))
            {
                return normalizedRegisterYear;
            }

            return containerYear?.Trim() ?? string.Empty;
        }

        private static bool IsSpecificCarrierLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            return !string.Equals(normalized, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal)
                && !string.Equals(normalized, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }
}
