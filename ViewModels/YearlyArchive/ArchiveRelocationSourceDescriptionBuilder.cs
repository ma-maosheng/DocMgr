using DocMgr.Models.YearlyArchive;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocMgr.ViewModels.YearlyArchive
{
    internal static class ArchiveRelocationSourceDescriptionBuilder
    {
        private const int MaxListedEntries = 5;

        public static string BuildItemsDescription(IReadOnlyList<ArchiveRelocationItemSummary> items)
        {
            if (items == null || items.Count == 0)
            {
                return "容器内暂无关联资料子项。";
            }

            var entries = items
                .Select(FormatItemEntry)
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .ToList();

            if (entries.Count == 0)
            {
                return $"共 {items.Count} 项，暂无可用表单号或子项名称。";
            }

            var builder = new StringBuilder();
            builder.Append(items.Count == 1 ? "共 1 项资料" : $"共 {items.Count} 项资料");

            builder.AppendLine();
            int displayCount = System.Math.Min(entries.Count, MaxListedEntries);
            for (int index = 0; index < displayCount; index++)
            {
                builder.Append('·').Append(' ').AppendLine(entries[index]);
            }

            if (entries.Count > MaxListedEntries)
            {
                builder.Append("· … 其余 ").Append(entries.Count - MaxListedEntries).Append(" 项未列出");
            }
            else if (builder.Length > 0 && builder[builder.Length - 1] == '\n')
            {
                builder.Length -= 1;
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatItemEntry(ArchiveRelocationItemSummary item)
        {
            var parts = new List<string>();
            string formNo = item.FormNo?.Trim() ?? string.Empty;
            string itemName = item.ItemName?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(formNo))
            {
                parts.Add(formNo);
            }

            if (!string.IsNullOrWhiteSpace(itemName))
            {
                parts.Add(itemName);
            }

            return parts.Count == 0 ? "（未命名子项）" : string.Join(" / ", parts);
        }
    }
}
