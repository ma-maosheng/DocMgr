using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子登记介质明细辅助方法。
    /// </summary>
    public static class ElectronicMediaItemSupport
    {
        /// <summary>
        /// 登记申请存储目录格式说明。
        /// </summary>
        public const string RegistrationStoragePathFormatDescription =
            "登记格式：\\目录\\子目录（不含盘符，层级用反斜杠分隔）";

        /// <summary>
        /// 登记申请存储目录规范性示例。
        /// </summary>
        public const string RegistrationStoragePathFormatExamples =
            "格式说明：[\\年度\\项目\\子项名称\\]，示例：[\\2026\\基础测绘\\设计文件\\]";

        private static readonly Regex RegistrationStoragePathPattern =
            new(@"^\\[^\\]+(\\[^\\]+)*$", RegexOptions.CultureInvariant);
        public static string ResolveEntryKind(string? dataOrganizationForm)
        {
            if (string.Equals(dataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory, StringComparison.Ordinal))
            {
                return ArchiveRegisterDomainValues.ElectronicEntryKindDirectory;
            }

            if (string.Equals(dataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormFile, StringComparison.Ordinal))
            {
                return ArchiveRegisterDomainValues.ElectronicEntryKindFile;
            }

            return string.Empty;
        }

        public static string ResolveSubCategoryScope(string? materialCategory)
        {
            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal))
            {
                return ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocumentScope;
            }

            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal))
            {
                return ArchiveRegisterDomainValues.ElectronicMaterialCategoryDataScope;
            }

            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftware, StringComparison.Ordinal))
            {
                return ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftwareScope;
            }

            return string.Empty;
        }

        public static string BuildStoragePathSummary(YearlyArchiveRegisterMedia media)
        {
            ArgumentNullException.ThrowIfNull(media);

            return JoinDistinctValues(
                media.Items.Select(item => item.StoragePath),
                "；",
                StringComparer.OrdinalIgnoreCase);
        }

        public static decimal ResolveMediaDataSizeMb(YearlyArchiveRegisterMedia media)
        {
            ArgumentNullException.ThrowIfNull(media);

            return media.Items.Sum(ResolveMediaItemDataSizeMb);
        }

        public static decimal ResolveMediaItemDataSizeMb(YearlyArchiveRegisterMediaItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            var detail = item.ElectronicDetail;
            if (detail == null)
            {
                return 0;
            }

            if (detail.DataSizeMb > 0)
            {
                return detail.DataSizeMb;
            }

            return detail.Entries.Sum(entry => entry.SizeMb ?? 0);
        }

        /// <summary>
        /// 登记申请用存储目录：去掉盘符，统一为 “\目录\子目录” 形式。
        /// </summary>
        public static string FormatStoragePathForRegistration(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            path = path.Trim().Replace('/', '\\');

            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                path = "\\" + path[2..];
            }
            else if (path.Length >= 2 && path[1] == ':')
            {
                path = path[2..];
            }

            path = path.TrimStart('\\').TrimEnd('\\');
            return string.IsNullOrEmpty(path) ? "\\" : "\\" + path;
        }

        /// <summary>
        /// 校验并规范化登记申请用的存储目录。
        /// </summary>
        public static bool TryValidateRegistrationStoragePath(
            string? path,
            out string normalizedPath,
            out string errorMessage)
        {
            normalizedPath = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "未填写";
                return false;
            }

            normalizedPath = FormatStoragePathForRegistration(path);
            if (normalizedPath == "\\")
            {
                errorMessage = "至少包含一级目录名";
                return false;
            }

            if (!RegistrationStoragePathPattern.IsMatch(normalizedPath))
            {
                errorMessage = $"格式不规范，请使用 {RegistrationStoragePathFormatDescription}（{RegistrationStoragePathFormatExamples}）";
                return false;
            }

            var segments = normalizedPath[1..].Split('\\', StringSplitOptions.None);
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    errorMessage = "目录层级不能包含空段或连续反斜杠";
                    return false;
                }

                if (segment is "." or "..")
                {
                    errorMessage = "目录名不能为 . 或 ..";
                    return false;
                }

                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    errorMessage = $"目录名“{segment}”包含非法字符";
                    return false;
                }
            }

            return true;
        }

        public static string BuildStoragePathDisplaySummary(YearlyArchiveRegisterMedia media)
        {
            ArgumentNullException.ThrowIfNull(media);

            return string.Join("\n", CollectStoragePathDisplayLines(media));
        }

        public static IReadOnlyList<string> CollectStoragePathDisplayLines(YearlyArchiveRegisterMedia media)
        {
            return CollectMediaContentPathLines(media)
                .Select(line => line.StoragePath)
                .ToList();
        }

        public static IReadOnlyList<ElectronicMediaContentPathLine> CollectMediaContentPathLines(YearlyArchiveRegisterMedia media)
        {
            ArgumentNullException.ThrowIfNull(media);

            return OrderMediaItems(media.Items)
                .SelectMany(CollectMediaItemContentPathLines)
                .Where(line => !string.IsNullOrWhiteSpace(line.ItemName)
                    || !string.IsNullOrWhiteSpace(line.StoragePath))
                .ToList();
        }

        /// <summary>
        /// 单条资料子项在列表中的子项名称与来源路径（子项内多段时用换行合并为同一行展示）。
        /// </summary>
        public static ElectronicMediaContentPathLine ResolveMediaItemDisplayContentPathLine(YearlyArchiveRegisterMediaItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            var lines = CollectMediaItemContentPathLines(item).ToList();
            if (lines.Count == 0)
            {
                return new ElectronicMediaContentPathLine(
                    item.ContentDesc?.Trim() ?? string.Empty,
                    FormatStoragePathForRegistration(item.StoragePath));
            }

            if (lines.Count == 1)
            {
                return lines[0];
            }

            return new ElectronicMediaContentPathLine(
                string.Join("\n", lines.Select(line => line.ItemName)),
                string.Join("\n", lines.Select(line => line.StoragePath)));
        }

        public static string BuildItemNameDisplay(YearlyArchiveRegisterMedia media)
        {
            ArgumentNullException.ThrowIfNull(media);

            return string.Join("\n", CollectItemNameDisplayLines(media));
        }

        public static IReadOnlyList<string> CollectItemNameDisplayLines(YearlyArchiveRegisterMedia media)
        {
            return CollectMediaContentPathLines(media)
                .Select(line => line.ItemName)
                .ToList();
        }

        private static IEnumerable<YearlyArchiveRegisterMediaItem> OrderMediaItems(
            IEnumerable<YearlyArchiveRegisterMediaItem> items)
        {
            return items
                .OrderBy(item => item.ItemType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ContentDesc, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id);
        }

        private static IEnumerable<ElectronicMediaContentPathLine> CollectMediaItemContentPathLines(
            YearlyArchiveRegisterMediaItem item)
        {
            var summaries = SplitDelimitedTextSegments(item.ContentDesc).ToList();
            var paths = ResolveMediaItemStoragePathSegments(item).ToList();

            if (summaries.Count == 0 && paths.Count == 0)
            {
                yield break;
            }

            if (summaries.Count <= 1 && paths.Count <= 1)
            {
                yield return new ElectronicMediaContentPathLine(
                    summaries.FirstOrDefault() ?? string.Empty,
                    paths.FirstOrDefault() ?? string.Empty);
                yield break;
            }

            int lineCount = Math.Max(summaries.Count, paths.Count);
            for (int index = 0; index < lineCount; index++)
            {
                yield return new ElectronicMediaContentPathLine(
                    index < summaries.Count ? summaries[index] : string.Empty,
                    index < paths.Count ? paths[index] : string.Empty);
            }
        }

        private static IEnumerable<string> ResolveMediaItemStoragePathSegments(YearlyArchiveRegisterMediaItem item)
        {
            var storagePathSegments = SplitPathSegments(item.StoragePath)
                .Select(NormalizePathLine)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();

            if (storagePathSegments.Count > 0)
            {
                return storagePathSegments;
            }

            return CollectElectronicEntryFullPaths(item)
                .Select(NormalizePathLine)
                .Where(path => !string.IsNullOrWhiteSpace(path));
        }

        private static string NormalizePathLine(string path)
        {
            path = path.Trim().Replace('/', '\\');
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            // 登记存储目录：\目录\子目录（无盘符），保持原样。
            if (path.StartsWith("\\", StringComparison.Ordinal))
            {
                return path;
            }

            if (!Path.IsPathRooted(path))
            {
                return path;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (IOException)
            {
                return path;
            }
            catch (ArgumentException)
            {
                return path;
            }
        }

        private static IEnumerable<string> CollectElectronicEntryFullPaths(YearlyArchiveRegisterMediaItem item)
        {
            if (item.ElectronicDetail?.Entries == null || item.ElectronicDetail.Entries.Count == 0)
            {
                yield break;
            }

            string root = item.StoragePath?.Trim() ?? string.Empty;
            foreach (var entry in item.ElectronicDetail.Entries.OrderBy(e => e.SortOrder))
            {
                string relative = entry.RelativePath?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(relative))
                {
                    continue;
                }

                if (Path.IsPathRooted(relative))
                {
                    yield return Path.GetFullPath(relative);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(root))
                {
                    yield return relative;
                    continue;
                }

                yield return Path.GetFullPath(Path.Combine(root, relative));
            }
        }

        private static IEnumerable<string> SplitPathSegments(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (var segment in value.Split(['；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    yield return segment;
                }
            }
        }

        private static IEnumerable<string> SplitDelimitedTextSegments(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (var segment in value.Split(['；', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    yield return segment;
                }
            }
        }

        private static string JoinDistinctValues(
            IEnumerable<string?> values,
            string separator,
            StringComparer comparer)
        {
            return string.Join(separator, values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(comparer)!);
        }

        public static string FormatSizeMb(decimal sizeMb)
        {
            if (sizeMb <= 0)
            {
                return "0 MB";
            }

            if (sizeMb >= 1024)
            {
                return $"{Math.Round(sizeMb / 1024m, 2):0.##} GB";
            }

            return $"{Math.Round(sizeMb, 2):0.##} MB";
        }

        /// <summary>
        /// 构建电子子项打印扩展信息片段（资料类型、所属子类、组织形式、数据量、目录/文件个数）。
        /// </summary>
        public static IReadOnlyList<string> BuildElectronicItemPrintExtraParts(YearlyArchiveRegisterMediaItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            var detail = item.ElectronicDetail;
            if (detail == null)
            {
                return Array.Empty<string>();
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(detail.MaterialCategory))
            {
                parts.Add($"资料类型：{detail.MaterialCategory}");
            }

            if (!string.IsNullOrWhiteSpace(detail.SubCategory))
            {
                parts.Add($"所属子类：{detail.SubCategory}");
            }

            if (!string.IsNullOrWhiteSpace(detail.DataOrganizationForm))
            {
                parts.Add($"组织形式：{detail.DataOrganizationForm}");
            }

            if (detail.DataSizeMb > 0)
            {
                parts.Add($"数据量：{FormatSizeMb(detail.DataSizeMb)}");
            }

            int entryCount = detail.Entries?.Count ?? 0;
            if (string.Equals(detail.DataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory, StringComparison.Ordinal))
            {
                parts.Add($"目录个数：{entryCount}");
            }
            else if (string.Equals(detail.DataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormFile, StringComparison.Ordinal))
            {
                parts.Add($"文件个数：{entryCount}");
            }
            else if (entryCount > 0)
            {
                parts.Add($"目录个数：{entryCount}");
            }

            return parts;
        }

        public static string FormatModifiedDate(DateTime? modifiedAt)
        {
            return modifiedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";
        }

        public static DateTime? ResolveEntryCreatedAt(string fullPath, string? entryKind)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return null;
            }

            if (string.Equals(entryKind, ArchiveRegisterDomainValues.ElectronicEntryKindFile, StringComparison.Ordinal))
            {
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                return new FileInfo(fullPath).CreationTime;
            }

            if (!Directory.Exists(fullPath))
            {
                return null;
            }

            DateTime? earliest = null;
            foreach (var filePath in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var creationTime = new FileInfo(filePath).CreationTime;
                    if (earliest == null || creationTime < earliest)
                    {
                        earliest = creationTime;
                    }
                }
                catch (IOException)
                {
                    // 个别文件读取失败时跳过，继续在其余文件中求最早创建时间。
                }
                catch (UnauthorizedAccessException)
                {
                    // 无访问权限的文件跳过，继续遍历其余文件。
                }
            }

            if (earliest != null)
            {
                return earliest;
            }

            try
            {
                return new DirectoryInfo(fullPath).CreationTime;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static DateTime? ResolveEntryModifiedAt(string fullPath, string? entryKind)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return null;
            }

            if (string.Equals(entryKind, ArchiveRegisterDomainValues.ElectronicEntryKindFile, StringComparison.Ordinal))
            {
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                return new FileInfo(fullPath).LastWriteTime;
            }

            if (!Directory.Exists(fullPath))
            {
                return null;
            }

            DateTime? latest = null;
            foreach (var filePath in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var writeTime = new FileInfo(filePath).LastWriteTime;
                    if (latest == null || writeTime > latest)
                    {
                        latest = writeTime;
                    }
                }
                catch (IOException)
                {
                    // 个别文件读取失败时跳过，继续在其余文件中求最近修改时间。
                }
                catch (UnauthorizedAccessException)
                {
                    // 无访问权限的文件跳过，继续遍历其余文件。
                }
            }

            if (latest != null)
            {
                return latest;
            }

            try
            {
                return new DirectoryInfo(fullPath).LastWriteTime;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static string BuildContentScanSummary(
            string? dataOrganizationForm,
            int entryCount,
            int fileCount,
            decimal totalSizeMb)
        {
            if (entryCount <= 0)
            {
                return "尚未扫描目录/文件明细";
            }

            string sizeText = FormatSizeMb(totalSizeMb);
            if (string.Equals(dataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory, StringComparison.Ordinal))
            {
                return fileCount > 0
                    ? $"已扫描 {entryCount} 个目录，包含 {fileCount} 个文件，合计 {sizeText}"
                    : $"已扫描 {entryCount} 个目录，合计 {sizeText}";
            }

            if (string.Equals(dataOrganizationForm, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormFile, StringComparison.Ordinal))
            {
                return $"已扫描 {entryCount} 个文件，合计 {sizeText}";
            }

            return $"已加载 {entryCount} 条明细，合计 {sizeText}";
        }
    }
}
