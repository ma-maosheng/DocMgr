using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质拷贝型立档默认存储路径生成。
    /// </summary>
    public static class ElectronicFilingStoragePathSupport
    {
        public static string BuildDefaultFilingStoragePath(
            string? year,
            string? projectName,
            string? materialName,
            string? itemName,
            int sequence)
        {
            int normalizedSequence = sequence > 0 ? sequence : 1;
            string folderName = $"{SanitizePathSegment(itemName, "未命名子项")}_{normalizedSequence:D3}";

            return string.Join("\\", new[]
            {
                string.Empty,
                SanitizePathSegment(year, "未知年度"),
                SanitizePathSegment(projectName, "未知项目"),
                SanitizePathSegment(materialName, "未命名资料"),
                folderName,
                string.Empty
            });
        }

        public static IReadOnlyDictionary<int, int> BuildSequenceByMediaItemId(
            IEnumerable<PendingFilingStoragePathItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            var sequenceByMediaItemId = new Dictionary<int, int>();
            var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items
                         .OrderBy(entry => entry.FormNo, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(entry => entry.MaterialName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(entry => entry.ItemName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(entry => entry.MediaItemId))
            {
                if (item.MediaItemId <= 0)
                {
                    continue;
                }

                string key = $"{item.MaterialName}|{item.ItemName}";
                counters.TryGetValue(key, out int nextSequence);
                nextSequence++;
                counters[key] = nextSequence;
                sequenceByMediaItemId[item.MediaItemId] = nextSequence;
            }

            return sequenceByMediaItemId;
        }

        /// <summary>
        /// 将资料子项立档根路径与登记目录/文件相对路径合并为条目级立档路径。
        /// </summary>
        public static string BuildEntryFilingPath(string? filingStoragePath, string? entryRelativePath)
        {
            string relative = (entryRelativePath ?? string.Empty).Trim().Replace('/', '\\');
            string basePath = (filingStoragePath ?? string.Empty).Trim().Replace('/', '\\');

            if (string.IsNullOrWhiteSpace(basePath))
            {
                return relative;
            }

            if (string.IsNullOrWhiteSpace(relative))
            {
                return basePath;
            }

            if (Path.IsPathRooted(relative))
            {
                return relative;
            }

            bool baseHasLeadingSlash = basePath.StartsWith("\\", StringComparison.Ordinal);
            basePath = basePath.TrimEnd('\\');
            relative = relative.TrimStart('\\');

            string combined = basePath + '\\' + relative;
            if (baseHasLeadingSlash && !combined.StartsWith("\\", StringComparison.Ordinal))
            {
                combined = '\\' + combined;
            }

            return combined;
        }

        private static string SanitizePathSegment(string? value, string fallback)
        {
            string segment = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            segment = segment.Replace('/', '\\');

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                segment = segment.Replace(invalidChar, '_');
            }

            segment = segment.Replace(':', '_').Trim('\\', '.', ' ');
            return string.IsNullOrWhiteSpace(segment) ? fallback : segment;
        }

        public readonly record struct PendingFilingStoragePathItem(
            int MediaItemId,
            string FormNo,
            string MaterialName,
            string ItemName);
    }
}
