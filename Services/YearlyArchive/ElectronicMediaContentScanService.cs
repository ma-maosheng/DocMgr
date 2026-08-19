using System.IO;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public class ElectronicMediaContentScanService : IElectronicMediaContentScanService
    {
        /// <summary>
        /// 将所选目录视为子项根目录，扫描其直接子项（一级子目录与根下文件可同时存在）。
        /// </summary>
        public ElectronicMediaContentScanResult ScanDirectories(IReadOnlyList<string> directoryPaths, string? storageRootDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(directoryPaths);

            var normalizedDirectories = directoryPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(path.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedDirectories.Count == 0)
            {
                throw new InvalidOperationException("未选择任何目录。");
            }

            foreach (var directoryPath in normalizedDirectories)
            {
                if (!Directory.Exists(directoryPath))
                {
                    throw new DirectoryNotFoundException($"目录不存在：{directoryPath}");
                }
            }

            string rootPath = string.IsNullOrWhiteSpace(storageRootDirectory)
                ? ResolveCommonRootDirectory(normalizedDirectories)
                : Path.GetFullPath(storageRootDirectory.Trim());

            if (!Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException($"存储根目录不存在：{rootPath}");
            }

            var entries = new List<ElectronicMediaContentScanEntry>();
            int totalFileCount = 0;
            long totalBytes = 0;

            foreach (var directoryPath in normalizedDirectories)
            {
                var (treeBytes, treeFileCount) = SummarizeDirectoryTree(directoryPath);
                totalBytes += treeBytes;
                totalFileCount += treeFileCount;

                foreach (var childDirectory in EnumerateImmediateBusinessDirectories(directoryPath))
                {
                    var (childBytes, _) = SummarizeDirectoryTree(childDirectory.FullName);
                    var times = ResolveDirectoryTimes(childDirectory.FullName, childDirectory);
                    entries.Add(new ElectronicMediaContentScanEntry
                    {
                        EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindDirectory,
                        EntryName = childDirectory.Name,
                        RelativePath = Path.GetRelativePath(rootPath, childDirectory.FullName),
                        SizeMb = BytesToMb(childBytes),
                        CreatedAt = times.CreatedAt,
                        ModifiedAt = times.ModifiedAt
                    });
                }

                foreach (var childFile in EnumerateImmediateFiles(directoryPath))
                {
                    entries.Add(new ElectronicMediaContentScanEntry
                    {
                        EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindFile,
                        EntryName = childFile.Name,
                        RelativePath = Path.GetRelativePath(rootPath, childFile.FullName),
                        SizeMb = BytesToMb(childFile.Length),
                        CreatedAt = childFile.CreationTime,
                        ModifiedAt = childFile.LastWriteTime
                    });
                }
            }

            if (entries.Count == 0)
            {
                throw new InvalidOperationException("所选子项根目录下没有可登记的文件或一级子目录。");
            }

            return new ElectronicMediaContentScanResult
            {
                RootPath = rootPath,
                Entries = entries
                    .OrderBy(entry => entry.EntryKind, StringComparer.Ordinal)
                    .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                FileCount = totalFileCount,
                TotalSizeMb = BytesToMb(totalBytes)
            };
        }

        public ElectronicMediaContentScanResult ScanFiles(IReadOnlyList<string> filePaths, string? storageRootDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(filePaths);

            var normalizedFiles = filePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(path.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedFiles.Count == 0)
            {
                throw new InvalidOperationException("未选择任何文件。");
            }

            foreach (var filePath in normalizedFiles)
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"文件不存在：{filePath}", filePath);
                }
            }

            string rootPath = string.IsNullOrWhiteSpace(storageRootDirectory)
                ? ResolveCommonRootDirectory(normalizedFiles)
                : Path.GetFullPath(storageRootDirectory.Trim());

            if (!Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException($"存储根目录不存在：{rootPath}");
            }

            var entries = new List<ElectronicMediaContentScanEntry>();
            long totalBytes = 0;

            foreach (var filePath in normalizedFiles)
            {
                var fileInfo = new FileInfo(filePath);
                string relativePath = Path.GetRelativePath(rootPath, filePath);
                entries.Add(new ElectronicMediaContentScanEntry
                {
                    EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindFile,
                    EntryName = fileInfo.Name,
                    RelativePath = relativePath,
                    SizeMb = BytesToMb(fileInfo.Length),
                    CreatedAt = fileInfo.CreationTime,
                    ModifiedAt = fileInfo.LastWriteTime
                });
                totalBytes += fileInfo.Length;
            }

            return new ElectronicMediaContentScanResult
            {
                RootPath = rootPath,
                Entries = entries,
                FileCount = entries.Count,
                TotalSizeMb = BytesToMb(totalBytes)
            };
        }

        private static string ResolveCommonRootDirectory(IReadOnlyList<string> paths)
        {
            string? commonRoot = null;

            foreach (var path in paths)
            {
                string? directory = Directory.Exists(path)
                    ? path
                    : Path.GetDirectoryName(path);

                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                commonRoot = commonRoot == null
                    ? directory
                    : GetCommonPath(commonRoot, directory);
            }

            if (string.IsNullOrWhiteSpace(commonRoot))
            {
                throw new InvalidOperationException("无法确定所选目录/文件的公共根目录。");
            }

            return Path.GetFullPath(commonRoot);
        }

        private static string GetCommonPath(string firstPath, string secondPath)
        {
            var firstParts = Path.GetFullPath(firstPath).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var secondParts = Path.GetFullPath(secondPath).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var commonParts = new List<string>();

            int maxLength = Math.Min(firstParts.Length, secondParts.Length);
            for (int index = 0; index < maxLength; index++)
            {
                if (!string.Equals(firstParts[index], secondParts[index], StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                commonParts.Add(firstParts[index]);
            }

            if (commonParts.Count == 0)
            {
                throw new InvalidOperationException("所选目录/文件不在同一目录树下，请分别登记或手动指定存储根目录。");
            }

            return Path.Combine(commonParts.ToArray());
        }

        private static IEnumerable<DirectoryInfo> EnumerateImmediateBusinessDirectories(string directoryPath)
        {
            DirectoryInfo[] directories;
            try
            {
                directories = new DirectoryInfo(directoryPath).GetDirectories();
            }
            catch (IOException)
            {
                return Array.Empty<DirectoryInfo>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<DirectoryInfo>();
            }

            return directories
                .Where(IsBusinessDirectory)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<FileInfo> EnumerateImmediateFiles(string directoryPath)
        {
            FileInfo[] files;
            try
            {
                files = new DirectoryInfo(directoryPath).GetFiles();
            }
            catch (IOException)
            {
                return Array.Empty<FileInfo>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<FileInfo>();
            }

            return files.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsBusinessDirectory(DirectoryInfo directory)
        {
            string name = directory.Name.Trim();
            if (string.IsNullOrWhiteSpace(name)
                || name.StartsWith(".", StringComparison.Ordinal)
                || name.StartsWith("$", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                if (directory.Attributes.HasFlag(FileAttributes.Hidden)
                    || directory.Attributes.HasFlag(FileAttributes.System))
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

        private static (long Bytes, int FileCount) SummarizeDirectoryTree(string directoryPath)
        {
            long bytes = 0;
            int fileCount = 0;
            try
            {
                foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        bytes += new FileInfo(filePath).Length;
                        fileCount++;
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

            return (bytes, fileCount);
        }

        private static (DateTime? CreatedAt, DateTime? ModifiedAt) ResolveDirectoryTimes(
            string directoryPath,
            DirectoryInfo directoryInfo)
        {
            DateTime? latestModifiedAt = null;
            DateTime? earliestCreatedAt = null;
            try
            {
                foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (latestModifiedAt == null || fileInfo.LastWriteTime > latestModifiedAt)
                        {
                            latestModifiedAt = fileInfo.LastWriteTime;
                        }

                        if (earliestCreatedAt == null || fileInfo.CreationTime < earliestCreatedAt)
                        {
                            earliestCreatedAt = fileInfo.CreationTime;
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

            earliestCreatedAt ??= SafeDirectoryTime(() => directoryInfo.CreationTime);
            latestModifiedAt ??= SafeDirectoryTime(() => directoryInfo.LastWriteTime);
            return (earliestCreatedAt, latestModifiedAt);
        }

        private static DateTime? SafeDirectoryTime(Func<DateTime> readTime)
        {
            try
            {
                return readTime();
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

        private static decimal BytesToMb(long bytes)
        {
            if (bytes <= 0)
            {
                return 0;
            }

            return Math.Round(bytes / 1024m / 1024m, 4);
        }
    }
}
