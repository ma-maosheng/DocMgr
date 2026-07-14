using System.Text;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 资料明细折叠态摘要文案生成。
    /// </summary>
    public static class ItemDetailsPanelSummarySupport
    {
        public static string BuildGenericCountSummary<T>(IReadOnlyList<T> items) =>
            items.Count == 0 ? "暂无资料" : $"已选 {items.Count} 条资料";

        public static string BuildOutboundItemSummary(IReadOnlyList<ArchiveOutboundItemRowViewModel> items)
        {
            if (items.Count == 0)
            {
                return "暂无资料";
            }

            var segments = new List<string> { $"已选 {items.Count} 条" };

            var usageParts = items
                .GroupBy(row => row.UsageModeDisplay)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key} {group.Count()}");
            segments.Add(string.Join(" / ", usageParts));

            var years = items
                .Select(row => row.ItemArchiveYearDisplay?.Trim() ?? string.Empty)
                .Where(year => !string.IsNullOrWhiteSpace(year) && year != "—")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(year => year, StringComparer.Ordinal)
                .ToList();
            if (years.Count > 0)
            {
                segments.Add($"年度 {string.Join("、", years)}");
            }

            return string.Join(" · ", segments);
        }

        public static string BuildReturnItemSummary(IReadOnlyList<ArchiveReturnItemEditRowViewModel> items)
        {
            if (items.Count == 0)
            {
                return "暂无归还明细";
            }

            var mediaParts = items
                .GroupBy(row => row.MediaKind?.Trim() ?? string.Empty)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .Select(group => $"{group.Key} {group.Count()}");
            return $"共 {items.Count} 条 · {string.Join(" / ", mediaParts)}";
        }

        public static string BuildNamedItemSummary(
            IReadOnlyList<(string Label, int Count)> groups,
            int totalCount,
            string emptyText = "暂无资料")
        {
            if (totalCount == 0)
            {
                return emptyText;
            }

            if (groups.Count == 0)
            {
                return $"共 {totalCount} 条";
            }

            var body = string.Join(" / ", groups.Select(group => $"{group.Label} {group.Count}"));
            return $"共 {totalCount} 条 · {body}";
        }

        public static string BuildTextColumnSummary<T>(
            IReadOnlyList<T> items,
            Func<T, string> labelSelector,
            string emptyText = "暂无资料")
        {
            if (items.Count == 0)
            {
                return emptyText;
            }

            var groups = items
                .Select(item => labelSelector(item)?.Trim() ?? string.Empty)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .GroupBy(label => label, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (group.Key, group.Count()))
                .ToList();

            return BuildNamedItemSummary(groups, items.Count, emptyText);
        }
    }
}
