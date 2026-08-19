using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档默认存储路径生成。
    /// 同一资料名称、同一目标袋内：首个子项文件夹为「子项」，其后为「子项_001」「子项_002」；
    /// 不回改已占用路径；若袋内已有带序号的同名文件夹，则继续编号而不再插入无序号名。
    /// </summary>
    public static class ElectronicFilingStoragePathSupport
    {
        private static readonly Regex NumberedFolderRegex = new(
            @"^(?<base>.+)_(?<seq>\d{3})$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string BuildDefaultFilingStoragePath(
            string? year,
            string? projectName,
            string? materialName,
            string? itemFolderName)
        {
            return string.Join("\\", new[]
            {
                string.Empty,
                SanitizePathSegment(year, "未知年度"),
                SanitizePathSegment(projectName, "未知项目"),
                SanitizePathSegment(materialName, "未命名资料"),
                SanitizePathSegment(itemFolderName, "未命名子项"),
                string.Empty
            });
        }

        /// <summary>
        /// 按待立档子项顺序，在已占用文件夹名之后分配「子项 / 子项_001 / …」。
        /// </summary>
        public static IReadOnlyDictionary<int, string> BuildItemFolderNameByMediaItemId(
            IEnumerable<PendingFilingStoragePathItem> items,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> occupiedFolderNamesByMaterial)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(occupiedFolderNamesByMaterial);

            var folderByMediaItemId = new Dictionary<int, string>();
            var occupiedByMaterial = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in occupiedFolderNamesByMaterial)
            {
                string materialKey = pair.Key?.Trim() ?? string.Empty;
                occupiedByMaterial[materialKey] = new HashSet<string>(
                    (pair.Value ?? Array.Empty<string>())
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name.Trim()),
                    StringComparer.OrdinalIgnoreCase);
            }

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

                string materialKey = item.MaterialName?.Trim() ?? string.Empty;
                if (!occupiedByMaterial.TryGetValue(materialKey, out HashSet<string>? occupied))
                {
                    occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    occupiedByMaterial[materialKey] = occupied;
                }

                string folderName = ResolveNextItemFolderName(item.ItemName, occupied);
                occupied.Add(folderName);
                folderByMediaItemId[item.MediaItemId] = folderName;
            }

            return folderByMediaItemId;
        }

        /// <summary>
        /// 在已占用名集合中解析下一个子项文件夹名。
        /// </summary>
        public static string ResolveNextItemFolderName(string? itemName, IReadOnlyCollection<string> occupiedFolderNames)
        {
            string baseName = SanitizePathSegment(itemName, "未命名子项");
            var occupied = new HashSet<string>(
                (occupiedFolderNames ?? Array.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            bool hasNumberedSibling = occupied.Any(name => IsNumberedVariantOf(baseName, name));
            if (!hasNumberedSibling && !occupied.Contains(baseName))
            {
                return baseName;
            }

            int nextSequence = 1;
            while (true)
            {
                string candidate = $"{baseName}_{nextSequence:D3}";
                if (!occupied.Contains(candidate))
                {
                    return candidate;
                }

                nextSequence++;
            }
        }

        /// <summary>
        /// 从立档根路径取出子项文件夹名（最后一段）。
        /// </summary>
        public static string ExtractItemFolderName(string? filingStoragePath)
        {
            string path = (filingStoragePath ?? string.Empty).Trim().Replace('/', '\\').Trim('\\');
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            int lastSlash = path.LastIndexOf('\\');
            return lastSlash < 0 ? path : path[(lastSlash + 1)..];
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

        public static string SanitizePathSegment(string? value, string fallback)
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

        private static bool IsNumberedVariantOf(string baseName, string folderName)
        {
            Match match = NumberedFolderRegex.Match(folderName.Trim());
            return match.Success
                && string.Equals(match.Groups["base"].Value, baseName, StringComparison.OrdinalIgnoreCase);
        }

        public readonly record struct PendingFilingStoragePathItem(
            int MediaItemId,
            string FormNo,
            string MaterialName,
            string ItemName);
    }
}
