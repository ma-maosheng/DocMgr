using System.IO;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public class ElectronicMediaContentScanService : IElectronicMediaContentScanService
    {
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
                var directoryInfo = new DirectoryInfo(directoryPath);
                long directoryBytes = 0;
                int directoryFileCount = 0;
                DateTime? latestModifiedAt = null;
                DateTime? earliestCreatedAt = null;

                foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        directoryBytes += fileInfo.Length;
                        directoryFileCount++;
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
                        // 个别文件被占用或读取失败时跳过，不影响其余文件的容量与时间统计。
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 无访问权限的文件跳过，继续统计其余文件。
                    }
                }

                if (latestModifiedAt == null)
                {
                    try
                    {
                        latestModifiedAt = directoryInfo.LastWriteTime;
                    }
                    catch (IOException)
                    {
                        // 目录时间不可读时保持为空，由上层按缺省值处理。
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 无访问权限时保持为空，由上层按缺省值处理。
                    }
                }

                if (earliestCreatedAt == null)
                {
                    try
                    {
                        earliestCreatedAt = directoryInfo.CreationTime;
                    }
                    catch (IOException)
                    {
                        // 目录时间不可读时保持为空，由上层按缺省值处理。
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 无访问权限时保持为空，由上层按缺省值处理。
                    }
                }

                string relativePath = Path.GetRelativePath(rootPath, directoryPath);
                entries.Add(new ElectronicMediaContentScanEntry
                {
                    EntryKind = ArchiveRegisterDomainValues.ElectronicEntryKindDirectory,
                    EntryName = directoryInfo.Name,
                    RelativePath = relativePath,
                    SizeMb = BytesToMb(directoryBytes),
                    CreatedAt = earliestCreatedAt,
                    ModifiedAt = latestModifiedAt
                });

                totalFileCount += directoryFileCount;
                totalBytes += directoryBytes;
            }

            return new ElectronicMediaContentScanResult
            {
                RootPath = rootPath,
                Entries = entries,
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
