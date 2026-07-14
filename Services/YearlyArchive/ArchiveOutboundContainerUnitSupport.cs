using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 借出申请明细按档案盒/电子介质袋分组的键与标签。
    /// </summary>
    public static class ArchiveOutboundContainerUnitSupport
    {
        public static string BuildUnitKey(YearlyArchiveOutboundItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            string containerCode = item.ContainerCode?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(containerCode))
            {
                return $"{item.MediaKind}|{containerCode}";
            }

            return $"item|{item.FilingFactId}|{item.SortOrder}";
        }

        public static string GetContainerKindLabel(string mediaKind) =>
            string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                ? "电子介质袋"
                : "档案盒";

        public static string FormatUnitTitle(string mediaKind, string containerCode)
        {
            string kindLabel = GetContainerKindLabel(mediaKind);
            string code = containerCode?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(code)
                ? $"{kindLabel}（未登记编号）"
                : $"{kindLabel} {code}";
        }

        public static IEnumerable<IGrouping<string, YearlyArchiveOutboundItem>> GroupItems(
            IEnumerable<YearlyArchiveOutboundItem> items) =>
            items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .GroupBy(BuildUnitKey, StringComparer.Ordinal);
    }
}
