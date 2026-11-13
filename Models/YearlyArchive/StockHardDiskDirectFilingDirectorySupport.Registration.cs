using System.IO;

namespace DocMgr.Models.YearlyArchive
{
    public enum ElectronicArchiveFourLayerResolveStatus
    {
        None = 0,
        Unique = 1,
        Ambiguous = 2,

        /// <summary>介质里有四级目录，但与申请单上已填的年度、项目都不一致。</summary>
        ProjectMismatch = 3
    }

    /// <summary>
    /// 从所选文件夹识别「年度/项目/资料名称/子项」后的表头结果。
    /// </summary>
    public sealed class ElectronicArchiveFourLayerResolveResult
    {
        public ElectronicArchiveFourLayerResolveStatus Status { get; init; }

        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        /// <summary>第四级子项的父目录，即资料名称文件夹。多份资料时为空。</summary>
        public string MaterialFolderPath { get; init; } = string.Empty;

        /// <summary>所选文件夹本身就是某一份资料名称文件夹。</summary>
        public bool SelectedIsMaterialFolder { get; init; }

        public IReadOnlyList<ElectronicArchiveMaterialFolder> Materials { get; init; } =
            Array.Empty<ElectronicArchiveMaterialFolder>();

        public IReadOnlyList<string> CandidateLabels { get; init; } = Array.Empty<string>();

        public static ElectronicArchiveFourLayerResolveResult None { get; } = new();
    }

    /// <summary>四级目录中的一份资料名称及其文件夹。</summary>
    public sealed class ElectronicArchiveMaterialFolder
    {
        public string MaterialName { get; init; } = string.Empty;

        public string MaterialFolderPath { get; init; } = string.Empty;
    }

    public static partial class StockHardDiskDirectFilingDirectorySupport
    {
        /// <summary>
        /// 文件夹名是否为年度信息：四位年份，或四位年份后只带「年」「年度」。
        /// </summary>
        public static bool TryParseYearInfoFolder(string? folderName, out string year)
        {
            string name = folderName?.Trim() ?? string.Empty;
            if (!TryParseYearFolder(name, out year))
            {
                return false;
            }

            string suffix = name[year.Length..];
            if (suffix.Length == 0
                || string.Equals(suffix, "年", StringComparison.Ordinal)
                || string.Equals(suffix, "年度", StringComparison.Ordinal))
            {
                return true;
            }

            year = string.Empty;
            return false;
        }

        /// <summary>
        /// 按介质盘符扫描：只采纳盘符下名称是年度的文件夹，忽略其余目录。
        /// </summary>
        public static ElectronicArchiveFourLayerResolveResult ResolveRegistrationDrive(
            string? driveRoot,
            string? requiredYear = null,
            string? requiredProjectName = null)
        {
            if (!TryGetExistingDirectory(driveRoot, out string fullPath)
                || !ElectronicMediaItemSupport.IsDriveRoot(fullPath))
            {
                return ElectronicArchiveFourLayerResolveResult.None;
            }

            var hits = new List<MaterialHit>();
            foreach (DirectoryInfo child in EnumerateBusinessDirectories(fullPath))
            {
                if (TryParseYearInfoFolder(child.Name, out string year))
                {
                    CollectMaterialsUnderYear(child, year, hits);
                }
            }

            return Finish(hits, fullPath, requiredYear, requiredProjectName);
        }

        /// <summary>
        /// 识别所选文件夹是否只对应一套「年度/项目/资料名称」，且首级文件夹是年度。
        /// 所选位置可以是年度的上级（含盘符）、年度文件夹，或资料名称文件夹。
        /// </summary>
        public static ElectronicArchiveFourLayerResolveResult ResolveRegistrationFolder(
            string? selectedFolder,
            string? requiredYear = null,
            string? requiredProjectName = null)
        {
            if (!TryGetExistingDirectory(selectedFolder, out string fullPath))
            {
                return ElectronicArchiveFourLayerResolveResult.None;
            }

            var hits = new List<MaterialHit>();
            List<DirectoryInfo> children = EnumerateBusinessDirectories(fullPath).ToList();
            if (children.Count > 0 && children.All(dir => TryParseYearInfoFolder(dir.Name, out _)))
            {
                foreach (DirectoryInfo yearDir in children)
                {
                    if (TryParseYearInfoFolder(yearDir.Name, out string year))
                    {
                        CollectMaterialsUnderYear(yearDir, year, hits);
                    }
                }

                if (hits.Count > 0)
                {
                    return Finish(hits, fullPath, requiredYear, requiredProjectName);
                }

                hits.Clear();
            }

            DirectoryInfo selected = new(fullPath);
            if (TryParseYearInfoFolder(selected.Name, out string selectedYear))
            {
                CollectMaterialsUnderYear(selected, selectedYear, hits);
                if (hits.Count > 0)
                {
                    return Finish(hits, fullPath, requiredYear, requiredProjectName);
                }

                hits.Clear();
            }

            DirectoryInfo? projectDir = selected.Parent;
            DirectoryInfo? ancestorDir = projectDir?.Parent;
            if (projectDir != null
                && ancestorDir != null
                && TryParseYearInfoFolder(ancestorDir.Name, out string ancestorYear)
                && !string.IsNullOrWhiteSpace(projectDir.Name)
                && !string.IsNullOrWhiteSpace(selected.Name)
                && children.Count > 0)
            {
                hits.Add(new MaterialHit(
                    ancestorYear,
                    projectDir.Name.Trim(),
                    selected.Name.Trim(),
                    selected.FullName));
            }

            return Finish(hits, fullPath, requiredYear, requiredProjectName);
        }

        private static ElectronicArchiveFourLayerResolveResult Finish(
            List<MaterialHit> hits,
            string selectedPath,
            string? requiredYear,
            string? requiredProjectName)
        {
            if (!string.IsNullOrWhiteSpace(requiredYear) && !string.IsNullOrWhiteSpace(requiredProjectName))
            {
                List<MaterialHit> matched = hits
                    .Where(hit => ScannedProjectLibraryMatchSupport.SameYearAndProject(
                        requiredYear,
                        requiredProjectName,
                        hit.Year,
                        hit.ProjectName))
                    .ToList();
                if (hits.Count > 0 && matched.Count == 0)
                {
                    return new ElectronicArchiveFourLayerResolveResult
                    {
                        Status = ElectronicArchiveFourLayerResolveStatus.ProjectMismatch,
                        Year = requiredYear.Trim(),
                        ProjectName = requiredProjectName.Trim(),
                        CandidateLabels = hits
                            .Select(hit => $"{hit.Year}\\{hit.ProjectName}\\{hit.MaterialName}")
                            .ToList()
                    };
                }

                hits = matched;
            }

            return FromHits(hits, selectedPath);
        }

        private static bool TryGetExistingDirectory(string? path, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                fullPath = Path.GetFullPath(path.Trim());
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }

            return Directory.Exists(fullPath);
        }

        private static void CollectMaterialsUnderYear(DirectoryInfo yearDir, string year, List<MaterialHit> hits)
        {
            foreach (DirectoryInfo projectDir in EnumerateBusinessDirectories(yearDir.FullName))
            {
                string projectName = projectDir.Name.Trim();
                if (string.IsNullOrWhiteSpace(projectName))
                {
                    continue;
                }

                foreach (DirectoryInfo materialDir in EnumerateBusinessDirectories(projectDir.FullName))
                {
                    string materialName = materialDir.Name.Trim();
                    if (string.IsNullOrWhiteSpace(materialName))
                    {
                        continue;
                    }

                    if (!EnumerateBusinessDirectories(materialDir.FullName).Any())
                    {
                        continue;
                    }

                    hits.Add(new MaterialHit(year, projectName, materialName, materialDir.FullName));
                }
            }
        }

        private static ElectronicArchiveFourLayerResolveResult FromHits(List<MaterialHit> hits, string selectedPath)
        {
            if (hits.Count == 0)
            {
                return ElectronicArchiveFourLayerResolveResult.None;
            }

            List<MaterialHit> ordered = hits
                .OrderBy(hit => hit.Year, StringComparer.Ordinal)
                .ThenBy(hit => hit.ProjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(hit => hit.MaterialName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> labels = ordered
                .Select(hit => $"{hit.Year}\\{hit.ProjectName}\\{hit.MaterialName}")
                .ToList();
            List<string> years = ordered
                .Select(hit => hit.Year)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            List<string> projects = ordered
                .Select(hit => hit.ProjectName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (years.Count != 1 || projects.Count != 1)
            {
                return new ElectronicArchiveFourLayerResolveResult
                {
                    Status = ElectronicArchiveFourLayerResolveStatus.Ambiguous,
                    CandidateLabels = labels
                };
            }

            List<ElectronicArchiveMaterialFolder> materials = ordered
                .Select(hit => new ElectronicArchiveMaterialFolder
                {
                    MaterialName = hit.MaterialName,
                    MaterialFolderPath = hit.MaterialFolderPath
                })
                .ToList();
            MaterialHit first = ordered[0];
            bool selectedIsMaterialFolder = materials.Count == 1
                && string.Equals(first.MaterialFolderPath, selectedPath, StringComparison.OrdinalIgnoreCase);
            return new ElectronicArchiveFourLayerResolveResult
            {
                Status = ElectronicArchiveFourLayerResolveStatus.Unique,
                Year = first.Year,
                ProjectName = first.ProjectName,
                MaterialName = materials.Count == 1 ? first.MaterialName : string.Empty,
                MaterialFolderPath = materials.Count == 1 ? first.MaterialFolderPath : string.Empty,
                SelectedIsMaterialFolder = selectedIsMaterialFolder,
                Materials = materials,
                CandidateLabels = labels
            };
        }

        private readonly record struct MaterialHit(
            string Year,
            string ProjectName,
            string MaterialName,
            string MaterialFolderPath);
    }
}
