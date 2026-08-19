using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 存量硬盘根目录四级扫描：年度 / 项目 / 资料名称 / 子项。
    /// </summary>
    public static class StockHardDiskDirectFilingDirectorySupport
    {
        private static readonly Regex YearFolderRegex = new(@"^(?<year>19\d{2}|20\d{2})", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly HashSet<string> IgnoredFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "$Recycle.Bin",
            "System Volume Information",
            "Recovery",
            "Config.Msi",
            "Documents and Settings",
            "found.000",
            "found.001"
        };

        /// <summary>
        /// 扫描根目录，要求有且仅有一套年度/项目。
        /// </summary>
        public static StockHardDiskDirectoryScanResult ScanRoot(string? rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return StockHardDiskDirectoryScanResult.Fail("请选择可访问的硬盘根目录。");
            }

            string root = Path.GetFullPath(rootPath.Trim());
            var warnings = new List<string>();
            var yearFolders = EnumerateBusinessDirectories(root).ToList();
            if (yearFolders.Count == 0)
            {
                return StockHardDiskDirectoryScanResult.Fail("根目录下未找到年度文件夹。请整理为「年度/项目/资料名称/子项」。");
            }

            var yearPairs = new List<(DirectoryInfo Dir, string Year)>();
            foreach (var folder in yearFolders)
            {
                if (!TryParseYearFolder(folder.Name, out string parsedYear))
                {
                    warnings.Add($"已忽略无法识别为年度的文件夹：{folder.Name}");
                    continue;
                }

                yearPairs.Add((folder, parsedYear));
            }

            var distinctYears = yearPairs.Select(item => item.Year).Distinct(StringComparer.Ordinal).ToList();
            if (distinctYears.Count == 0)
            {
                return StockHardDiskDirectoryScanResult.Fail("根目录下没有四位年度文件夹（如 2018）。");
            }

            if (distinctYears.Count > 1)
            {
                return StockHardDiskDirectoryScanResult.Fail(
                    $"一块硬盘只能存放唯一一个年度/项目。当前根目录出现多个年度：{string.Join("、", distinctYears)}。");
            }

            if (yearPairs.Count > 1)
            {
                warnings.Add($"同一年度存在多个文件夹，已使用 [{yearPairs[0].Dir.Name}]。");
            }

            var yearDir = yearPairs[0].Dir;
            string year = yearPairs[0].Year;
            var projectFolders = EnumerateBusinessDirectories(yearDir.FullName).ToList();
            if (projectFolders.Count == 0)
            {
                return StockHardDiskDirectoryScanResult.Fail($"年度 [{year}] 下未找到项目文件夹。");
            }

            if (projectFolders.Count > 1)
            {
                return StockHardDiskDirectoryScanResult.Fail(
                    $"一块硬盘只能存放唯一一个年度/项目。年度 [{year}] 下出现多个项目文件夹：{string.Join("、", projectFolders.Select(item => item.Name))}。");
            }

            var projectDir = projectFolders[0];
            string projectName = projectDir.Name.Trim();
            var materialFolders = EnumerateBusinessDirectories(projectDir.FullName).ToList();
            if (materialFolders.Count == 0)
            {
                return StockHardDiskDirectoryScanResult.Fail($"项目 [{projectName}] 下未找到资料名称文件夹。");
            }

            var materials = new List<StockHardDiskMaterialDraft>();
            foreach (var materialDir in materialFolders.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                var itemFolders = EnumerateBusinessDirectories(materialDir.FullName).ToList();
                if (itemFolders.Count == 0)
                {
                    warnings.Add($"资料 [{materialDir.Name}] 下没有子项文件夹，已跳过。");
                    continue;
                }

                var items = new List<StockHardDiskItemDraft>();
                foreach (var itemDir in itemFolders.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                {
                    items.Add(CreateItemDraft(root, year, projectName, materialDir.Name, itemDir));
                }

                materials.Add(new StockHardDiskMaterialDraft
                {
                    MaterialName = materialDir.Name.Trim(),
                    FullPath = materialDir.FullName,
                    Items = items
                });
            }

            if (materials.Count == 0)
            {
                return StockHardDiskDirectoryScanResult.Fail("未扫描到任何资料子项。请确认第四级为子项资料文件夹。");
            }

            return new StockHardDiskDirectoryScanResult
            {
                Succeeded = true,
                RootPath = root,
                Year = year,
                ProjectName = projectName,
                Materials = materials,
                Warnings = warnings
            };
        }

        private static StockHardDiskItemDraft CreateItemDraft(
            string rootPath,
            string year,
            string projectName,
            string materialName,
            DirectoryInfo itemDir)
        {
            var childDirs = EnumerateBusinessDirectories(itemDir.FullName).ToList();
            var childFiles = EnumerateFilesSafe(itemDir.FullName);

            var entries = new List<ElectronicMediaContentScanEntry>();
            foreach (var child in childDirs.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                var stats = SummarizeDirectory(child.FullName);
                entries.Add(new ElectronicMediaContentScanEntry
                {
                    EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindDirectory,
                    EntryName = child.Name,
                    RelativePath = Path.GetRelativePath(itemDir.FullName, child.FullName),
                    SizeMb = stats.SizeMb,
                    CreatedAt = stats.CreatedAt,
                    ModifiedAt = stats.ModifiedAt
                });
            }

            foreach (var file in childFiles.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(new ElectronicMediaContentScanEntry
                {
                    EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindFile,
                    EntryName = file.Name,
                    RelativePath = file.Name,
                    SizeMb = BytesToMb(file.Length),
                    CreatedAt = file.CreationTime,
                    ModifiedAt = file.LastWriteTime
                });
            }

            var total = SummarizeDirectory(itemDir.FullName);
            string filingPath = ElectronicFilingStoragePathSupport.BuildDefaultFilingStoragePath(
                year,
                projectName,
                materialName,
                itemDir.Name);

            return new StockHardDiskItemDraft
            {
                ItemName = itemDir.Name.Trim(),
                FullPath = itemDir.FullName,
                StoragePath = itemDir.FullName,
                FilingStoragePath = filingPath,
                DataOrganizationForm = ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                DataSizeMb = total.SizeMb,
                FileCount = total.FileCount,
                Entries = entries
            };
        }

        private static IEnumerable<DirectoryInfo> EnumerateBusinessDirectories(string path)
        {
            DirectoryInfo[] directories;
            try
            {
                directories = new DirectoryInfo(path).GetDirectories();
            }
            catch (IOException)
            {
                return Array.Empty<DirectoryInfo>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<DirectoryInfo>();
            }

            return directories.Where(IsBusinessDirectory).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<FileInfo> EnumerateFilesSafe(string path)
        {
            try
            {
                return new DirectoryInfo(path).GetFiles();
            }
            catch (IOException)
            {
                return Array.Empty<FileInfo>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<FileInfo>();
            }
        }

        private static bool IsBusinessDirectory(DirectoryInfo directory)
        {
            string name = directory.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (name.StartsWith(".", StringComparison.Ordinal) || name.StartsWith("$", StringComparison.Ordinal))
            {
                return false;
            }

            if (IgnoredFolderNames.Contains(name))
            {
                return false;
            }

            try
            {
                if (directory.Attributes.HasFlag(FileAttributes.Hidden) || directory.Attributes.HasFlag(FileAttributes.System))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }

        private static bool TryParseYearFolder(string folderName, out string year)
        {
            Match match = YearFolderRegex.Match(folderName.Trim());
            if (!match.Success)
            {
                year = string.Empty;
                return false;
            }

            year = match.Groups["year"].Value;
            return true;
        }

        private static (decimal SizeMb, int FileCount, DateTime? CreatedAt, DateTime? ModifiedAt) SummarizeDirectory(string path)
        {
            int fileCount = 0;
            long bytes = 0;
            DateTime? createdAt = null;
            DateTime? modifiedAt = null;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        fileCount++;
                        bytes += info.Length;
                        if (createdAt == null || info.CreationTime < createdAt)
                        {
                            createdAt = info.CreationTime;
                        }

                        if (modifiedAt == null || info.LastWriteTime > modifiedAt)
                        {
                            modifiedAt = info.LastWriteTime;
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return (BytesToMb(bytes), fileCount, createdAt, modifiedAt);
        }

        private static decimal BytesToMb(long bytes)
        {
            if (bytes <= 0)
            {
                return 0;
            }

            return Math.Round(bytes / 1024m / 1024m, 4);
        }
    }

    public sealed class StockHardDiskDirectoryScanResult
    {
        public bool Succeeded { get; init; }

        public string ErrorMessage { get; init; } = string.Empty;

        public string RootPath { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public IReadOnlyList<StockHardDiskMaterialDraft> Materials { get; init; } = Array.Empty<StockHardDiskMaterialDraft>();

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public static StockHardDiskDirectoryScanResult Fail(string message)
            => new() { Succeeded = false, ErrorMessage = message };
    }

    public sealed class StockHardDiskMaterialDraft
    {
        public string MaterialName { get; set; } = string.Empty;

        public string FullPath { get; init; } = string.Empty;

        public IReadOnlyList<StockHardDiskItemDraft> Items { get; set; } = Array.Empty<StockHardDiskItemDraft>();
    }

    public sealed class StockHardDiskItemDraft
    {
        public string ItemName { get; set; } = string.Empty;

        public string FullPath { get; init; } = string.Empty;

        public string StoragePath { get; init; } = string.Empty;

        public string FilingStoragePath { get; set; } = string.Empty;

        public string DataOrganizationForm { get; init; } = string.Empty;

        public decimal DataSizeMb { get; init; }

        public int FileCount { get; init; }

        public IReadOnlyList<ElectronicMediaContentScanEntry> Entries { get; init; } = Array.Empty<ElectronicMediaContentScanEntry>();
    }
}
