using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 存量光盘目录扫描。不要求盘上存在「年度/项目/资料/子项」，只列出根目录与一级业务文件夹，供操作员指派编目身份。
    /// 所属目录一个子项和按文件夹指派共用同一套目录统计：子文件夹合计来自其子树，整盘合计来自一级文件夹与根目录散文件。按后缀指派另做统计，不改这两套结果。
    /// </summary>
    public static class StockOpticalDiscDirectFilingDirectorySupport
    {
        /// <summary>整盘作为一个子项时的默认子项名。</summary>
        public const string WholeDiscItemName = "整盘";

        /// <summary>盘符根目录无法当作资料名称时的默认资料名。</summary>
        public const string WholeDiscMaterialName = "整盘资料";

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
        /// 只跳过重解析点，避免光盘或挂载点上的目录联接把递归统计绕回去。
        /// 不改共用的 <see cref="ElectronicMediaItemSupport.RecursiveIgnoreInaccessibleEnumerationOptions"/>。
        /// </summary>
        private static readonly EnumerationOptions RecursiveFileOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        /// <summary>
        /// 扫描光盘根目录。年度与项目由页面填写，不从文件夹名推断。
        /// </summary>
        public static StockOpticalDiscDirectoryScanResult ScanRoot(string? rootPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                {
                    return StockOpticalDiscDirectoryScanResult.Fail("请选择可访问的资料所属目录。");
                }

                string root = Path.GetFullPath(rootPath.Trim());
                var warnings = new List<string>();
                var folders = EnumerateBusinessDirectories(root).ToList();
                var looseFiles = EnumerateFilesSafe(root);
                if (folders.Count == 0 && looseFiles.Count == 0)
                {
                    return StockOpticalDiscDirectoryScanResult.Fail("资料所属目录下没有可登记的文件或文件夹。");
                }

                if (looseFiles.Count > 0)
                {
                    warnings.Add(
                        $"资料所属目录有 {looseFiles.Count} 个散文件。所属目录一个子项会把它们记入「{WholeDiscItemName}」；按文件夹指派时这些散文件不建子项；按后缀指派时，带后缀的散文件各成一个子项。");
                }

                string defaultMaterialName = ResolveDefaultMaterialName(root);
                var folderSnapshots = new List<DirectorySnapshot>(folders.Count);
                foreach (var folder in folders)
                {
                    var snapshot = BuildDirectorySnapshot(
                        folder.FullName,
                        defaultMaterialName,
                        folder.Name.Trim(),
                        isWholeDisc: false);
                    if (snapshot.Draft.Entries.Count == 0)
                    {
                        warnings.Add($"文件夹 [{folder.Name}] 下没有一级文件或子目录，纳入后无法立档。");
                    }

                    folderSnapshots.Add(snapshot);
                }

                var wholeDisc = BuildWholeDiscDraft(root, defaultMaterialName, folderSnapshots, looseFiles);
                var extensionDrafts = CollectExtensionDrafts(root, defaultMaterialName, out int extensionlessFileCount);
                if (extensionlessFileCount > 0)
                {
                    warnings.Add($"资料所属目录及其子目录有 {extensionlessFileCount} 个无后缀文件。按后缀指派时不建子项，确认前须勾选忽略。");
                }

                return new StockOpticalDiscDirectoryScanResult
                {
                    Succeeded = true,
                    RootPath = root,
                    DefaultMaterialName = defaultMaterialName,
                    LooseFileCount = looseFiles.Count,
                    ExtensionlessFileCount = extensionlessFileCount,
                    WholeDisc = wholeDisc,
                    Folders = folderSnapshots.Select(item => item.Draft).ToList(),
                    Extensions = extensionDrafts,
                    Warnings = warnings
                };
            }
            catch (IOException ex)
            {
                return StockOpticalDiscDirectoryScanResult.Fail("读取光盘目录失败：" + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StockOpticalDiscDirectoryScanResult.Fail("读取光盘目录失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 盘符根目录用「整盘资料」，其余用文件夹名。
        /// </summary>
        public static string ResolveDefaultMaterialName(string rootPath)
        {
            string name = new DirectoryInfo(rootPath).Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Contains(':', StringComparison.Ordinal))
            {
                return WholeDiscMaterialName;
            }

            return name;
        }

        /// <summary>
        /// 按页面上的年度、项目、资料名称、子项名称生成编目路径。
        /// </summary>
        public static string BuildCatalogPath(string? year, string? projectName, string? materialName, string? itemName)
        {
            return ElectronicFilingStoragePathSupport.BuildDefaultFilingStoragePath(
                year,
                projectName,
                materialName,
                itemName);
        }

        private readonly record struct DirectorySnapshot(
            StockOpticalDiscItemDraft Draft,
            long Bytes,
            int FileCount,
            DateTime? CreatedAt,
            DateTime? ModifiedAt);

        private readonly record struct ExtensionFile(
            string RelativeName,
            long Bytes,
            DateTime? CreatedAt,
            DateTime? ModifiedAt);

        /// <summary>
        /// 一级文件夹的数据量等于子树，文件数只计该目录直接包含的文件和文件夹，与明细条数一致。
        /// </summary>
        private static DirectorySnapshot BuildDirectorySnapshot(
            string directoryPath,
            string materialName,
            string itemName,
            bool isWholeDisc)
        {
            var childDirs = EnumerateBusinessDirectories(directoryPath).ToList();
            var childFiles = EnumerateFilesSafe(directoryPath);
            var entries = new List<ElectronicMediaContentScanEntry>();
            long bytes = 0;
            DateTime? createdAt = null;
            DateTime? modifiedAt = null;
            foreach (var child in childDirs)
            {
                var stats = SummarizeDirectory(child.FullName);
                entries.Add(new ElectronicMediaContentScanEntry
                {
                    EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindDirectory,
                    EntryName = child.Name,
                    SizeMb = BytesToMb(stats.Bytes),
                    CreatedAt = stats.CreatedAt,
                    ModifiedAt = stats.ModifiedAt
                });
                bytes += stats.Bytes;
                MergeTimeRange(ref createdAt, ref modifiedAt, stats.CreatedAt, stats.ModifiedAt);
            }

            foreach (var file in childFiles)
            {
                var fileTimes = ReadFileMeasure(file);
                entries.Add(new ElectronicMediaContentScanEntry
                {
                    EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindFile,
                    EntryName = file.Name,
                    SizeMb = BytesToMb(fileTimes.Bytes),
                    CreatedAt = fileTimes.CreatedAt,
                    ModifiedAt = fileTimes.ModifiedAt
                });
                bytes += fileTimes.Bytes;
                MergeTimeRange(ref createdAt, ref modifiedAt, fileTimes.CreatedAt, fileTimes.ModifiedAt);
            }

            var draft = new StockOpticalDiscItemDraft
            {
                MaterialName = materialName,
                ItemName = itemName,
                FullPath = directoryPath,
                StoragePath = directoryPath,
                IsWholeDisc = isWholeDisc,
                Include = true,
                DataOrganizationForm = ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                DataSizeMb = BytesToMb(bytes),
                FileCount = entries.Count,
                Entries = entries
            };
            return new DirectorySnapshot(draft, bytes, entries.Count, createdAt, modifiedAt);
        }

        /// <summary>
        /// 单独扫描后缀。不改写所属目录一个子项、按文件夹指派使用的文件数、目录明细和数据量。
        /// </summary>
        private static List<StockOpticalDiscItemDraft> CollectExtensionDrafts(
            string root,
            string materialName,
            out int extensionlessFileCount)
        {
            var groups = new Dictionary<string, List<ExtensionFile>>(StringComparer.OrdinalIgnoreCase);
            extensionlessFileCount = 0;
            AddExtensionFiles(root, EnumerateFilesSafe(root), groups, ref extensionlessFileCount);
            foreach (var directory in EnumerateBusinessDirectories(root))
            {
                try
                {
                    foreach (var info in new DirectoryInfo(directory.FullName).EnumerateFiles("*", RecursiveFileOptions))
                    {
                        try
                        {
                            AddExtensionFile(root, info, ReadFileMeasure(info), groups, ref extensionlessFileCount);
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
            }

            return BuildExtensionDrafts(root, materialName, groups);
        }

        /// <summary>
        /// 按后缀生成子项。文件来自资料所属目录及其子目录，明细名称用相对该目录的路径。
        /// </summary>
        private static List<StockOpticalDiscItemDraft> BuildExtensionDrafts(
            string root,
            string materialName,
            Dictionary<string, List<ExtensionFile>> groups)
        {
            var drafts = new List<StockOpticalDiscItemDraft>(groups.Count);
            foreach (var pair in groups.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                var entries = new List<ElectronicMediaContentScanEntry>();
                long bytes = 0;
                foreach (var file in pair.Value.OrderBy(item => item.RelativeName, StringComparer.OrdinalIgnoreCase))
                {
                    entries.Add(new ElectronicMediaContentScanEntry
                    {
                        EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindFile,
                        EntryName = file.RelativeName,
                        SizeMb = BytesToMb(file.Bytes),
                        CreatedAt = file.CreatedAt,
                        ModifiedAt = file.ModifiedAt
                    });
                    bytes += file.Bytes;
                }

                drafts.Add(new StockOpticalDiscItemDraft
                {
                    MaterialName = materialName,
                    ItemName = pair.Key,
                    FullPath = root,
                    StoragePath = root,
                    IsWholeDisc = false,
                    Include = true,
                    DataOrganizationForm = ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                    DataSizeMb = BytesToMb(bytes),
                    FileCount = pair.Value.Count,
                    Entries = entries
                });
            }

            return drafts;
        }

        private static void AddExtensionFiles(
            string scanRoot,
            IReadOnlyList<FileInfo> files,
            Dictionary<string, List<ExtensionFile>> groups,
            ref int extensionlessFileCount)
        {
            foreach (var file in files)
            {
                AddExtensionFile(scanRoot, file, ReadFileMeasure(file), groups, ref extensionlessFileCount);
            }
        }

        private static void AddExtensionFile(
            string scanRoot,
            FileInfo file,
            FileMeasure measure,
            Dictionary<string, List<ExtensionFile>> groups,
            ref int extensionlessFileCount)
        {
            if (!measure.Counted)
            {
                return;
            }

            string extension = NormalizeFileExtension(file.Name);
            if (string.IsNullOrEmpty(extension))
            {
                extensionlessFileCount++;
                return;
            }

            if (!groups.TryGetValue(extension, out var list))
            {
                list = new List<ExtensionFile>();
                groups.Add(extension, list);
            }

            list.Add(new ExtensionFile(ToRelativeName(scanRoot, file.FullName), measure.Bytes, measure.CreatedAt, measure.ModifiedAt));
        }

        private static string ToRelativeName(string scanRoot, string fullName)
        {
            try
            {
                string relative = Path.GetRelativePath(scanRoot, fullName).Replace('/', '\\').Trim();
                if (!string.IsNullOrWhiteSpace(relative)
                    && !string.Equals(relative, ".", StringComparison.Ordinal)
                    && !relative.StartsWith("..", StringComparison.Ordinal))
                {
                    return relative;
                }
            }
            catch (ArgumentException)
            {
            }

            return Path.GetFileName(fullName);
        }

        /// <summary>
        /// 取最后一个后缀并忽略大小写。没有后缀时返回空字符串。
        /// </summary>
        private static string NormalizeFileExtension(string fileName)
        {
            string extension = Path.GetExtension(fileName)?.Trim() ?? string.Empty;
            if (extension.Length <= 1 || extension[0] != '.')
            {
                return string.Empty;
            }

            return extension[1..].ToLowerInvariant();
        }

        /// <summary>
        /// 整盘数据量使用一级文件夹子树与根目录散文件。文件数只计这一层的文件夹和散文件。
        /// </summary>
        private static StockOpticalDiscItemDraft BuildWholeDiscDraft(
            string root,
            string materialName,
            IReadOnlyList<DirectorySnapshot> folders,
            IReadOnlyList<FileInfo> looseFiles)
        {
            var entries = new List<ElectronicMediaContentScanEntry>();
            long bytes = 0;
            foreach (var folder in folders)
            {
                entries.Add(new ElectronicMediaContentScanEntry
                {
                    EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindDirectory,
                    EntryName = folder.Draft.ItemName,
                    SizeMb = folder.Draft.DataSizeMb,
                    CreatedAt = folder.CreatedAt,
                    ModifiedAt = folder.ModifiedAt
                });
                bytes += folder.Bytes;
            }

            foreach (var file in looseFiles)
            {
                var fileTimes = ReadFileMeasure(file);
                entries.Add(new ElectronicMediaContentScanEntry
                {
                    EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindFile,
                    EntryName = file.Name,
                    SizeMb = BytesToMb(fileTimes.Bytes),
                    CreatedAt = fileTimes.CreatedAt,
                    ModifiedAt = fileTimes.ModifiedAt
                });
                bytes += fileTimes.Bytes;
            }

            return new StockOpticalDiscItemDraft
            {
                MaterialName = materialName,
                ItemName = WholeDiscItemName,
                FullPath = root,
                StoragePath = root,
                IsWholeDisc = true,
                Include = true,
                DataOrganizationForm = ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                DataSizeMb = BytesToMb(bytes),
                FileCount = entries.Count,
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
                return new DirectoryInfo(path).GetFiles()
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
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
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return true;
        }

        private static (long Bytes, int FileCount, DateTime? CreatedAt, DateTime? ModifiedAt) SummarizeDirectory(string path)
        {
            int fileCount = 0;
            long bytes = 0;
            DateTime? createdAt = null;
            DateTime? modifiedAt = null;
            try
            {
                foreach (var info in new DirectoryInfo(path).EnumerateFiles("*", RecursiveFileOptions))
                {
                    var measure = ReadFileMeasure(info);
                    if (!measure.Counted)
                    {
                        continue;
                    }

                    fileCount++;
                    bytes += measure.Bytes;
                    MergeTimeRange(ref createdAt, ref modifiedAt, measure.CreatedAt, measure.ModifiedAt);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return (bytes, fileCount, createdAt, modifiedAt);
        }

        private readonly record struct FileMeasure(long Bytes, DateTime? CreatedAt, DateTime? ModifiedAt, bool Counted);

        /// <summary>
        /// 使用枚举时已经带回的文件信息，避免再按路径 new FileInfo 对光盘做第二次查询。
        /// </summary>
        private static FileMeasure ReadFileMeasure(FileInfo info)
        {
            try
            {
                return new FileMeasure(info.Length, info.CreationTime, info.LastWriteTime, true);
            }
            catch (IOException)
            {
                return default;
            }
            catch (UnauthorizedAccessException)
            {
                return default;
            }
        }

        private static void MergeTimeRange(
            ref DateTime? createdAt,
            ref DateTime? modifiedAt,
            DateTime? created,
            DateTime? modified)
        {
            if (created != null && (createdAt == null || created < createdAt))
            {
                createdAt = created;
            }

            if (modified != null && (modifiedAt == null || modified > modifiedAt))
            {
                modifiedAt = modified;
            }
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

    /// <summary>
    /// 存量光盘根目录扫描结果。
    /// </summary>
    public sealed class StockOpticalDiscDirectoryScanResult
    {
        public bool Succeeded { get; init; }

        public string ErrorMessage { get; init; } = string.Empty;

        public string RootPath { get; init; } = string.Empty;

        public string DefaultMaterialName { get; init; } = string.Empty;

        public int LooseFileCount { get; init; }

        /// <summary>资料所属目录下没有后缀的文件数。按后缀指派时不建子项。</summary>
        public int ExtensionlessFileCount { get; init; }

        public StockOpticalDiscItemDraft? WholeDisc { get; init; }

        public IReadOnlyList<StockOpticalDiscItemDraft> Folders { get; init; } = Array.Empty<StockOpticalDiscItemDraft>();

        /// <summary>资料所属目录下一层文件按后缀分成的子项。子项名是后缀，资料名称默认同目录名。</summary>
        public IReadOnlyList<StockOpticalDiscItemDraft> Extensions { get; init; } = Array.Empty<StockOpticalDiscItemDraft>();

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public static StockOpticalDiscDirectoryScanResult Fail(string message)
            => new() { Succeeded = false, ErrorMessage = message };
    }

    /// <summary>
    /// 可指派为一条资料子项的盘上目录。
    /// </summary>
    public sealed class StockOpticalDiscItemDraft
    {
        public string MaterialName { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string FullPath { get; init; } = string.Empty;

        public string StoragePath { get; init; } = string.Empty;

        public string FilingStoragePath { get; set; } = string.Empty;

        /// <summary>是否为「所属目录一个子项」。</summary>
        public bool IsWholeDisc { get; init; }

        /// <summary>按文件夹指派时是否纳入本次立档。</summary>
        public bool Include { get; set; } = true;

        public string DataOrganizationForm { get; init; } = string.Empty;

        public decimal DataSizeMb { get; init; }

        public int FileCount { get; init; }

        public string SourceType { get; set; } = string.Empty;

        public string ProvideUnit { get; set; } = string.Empty;

        public string ConfidentialLevel { get; set; } = string.Empty;

        public string MaterialCategory { get; set; } = string.Empty;

        public string SubCategory { get; set; } = string.Empty;

        public IReadOnlyList<ElectronicMediaContentScanEntry> Entries { get; init; } = Array.Empty<ElectronicMediaContentScanEntry>();
    }
}
