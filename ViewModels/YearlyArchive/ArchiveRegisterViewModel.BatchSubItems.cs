using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive;

public partial class ArchiveRegisterViewModel
{
    private Task BatchAddElectronicMediaItemsAsync(object? parameter) =>
        BatchAddElectronicMediaItemsAsync(parameter, "选择资料名称文件夹，或年度所在目录", acceptDriveRoot: false);

    private async Task BatchAddElectronicMediaItemsAsync(
        object? parameter,
        string pickFolderTitle,
        bool acceptDriveRoot = false)
    {
        if (!CanEditForm)
        {
            return;
        }

        MediaMaterialGroupViewModel? currentGroup = parameter as MediaMaterialGroupViewModel;
        MediaEntryViewModel? media = currentGroup?.Owner ?? parameter as MediaEntryViewModel;
        if (media == null)
        {
            return;
        }

        TryGetCommittedYearAndProject(out string? requiredYear, out string? requiredProjectName);
        await ElectronicMediaBatchSubItemAddSupport.AddAsync(
            media,
            _dialogService,
            _electronicMediaContentScanService,
            CreateDefaultElectronicMediaItem,
            (_, _) => null,
            ScanBatchSubItemAsync,
            pickFolderTitle: pickFolderTitle,
            targetGroup: currentGroup,
            importRegisterMaterials: parsed => ImportResolvedMaterialsAsync(media, currentGroup, parsed),
            acceptDriveRoot: acceptDriveRoot,
            requiredYear: requiredYear,
            requiredProjectName: requiredProjectName);
    }

    /// <summary>表头实施年度和项目名称都已明确时，扫描只认这一对。</summary>
    private bool TryGetCommittedYearAndProject(out string? year, out string? projectName)
    {
        year = SelectedProject?.ImplementYear?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(year) || string.Equals(year, "全部", StringComparison.Ordinal))
        {
            year = SelectedProjectYear?.Trim() ?? string.Empty;
        }

        if (string.Equals(year, "全部", StringComparison.Ordinal))
        {
            year = string.Empty;
        }

        projectName = SelectedProject?.ProjectName?.Trim()
            ?? CurrentRecord?.ProjectName?.Trim()
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(year) || string.IsNullOrWhiteSpace(projectName))
        {
            year = null;
            projectName = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 同一年度、同一项目下的多份资料名称建成本介质的多个组。选中的就是某一份资料名称文件夹时，只往当前组追加子项。
    /// </summary>
    private async Task ImportResolvedMaterialsAsync(
        MediaEntryViewModel media,
        MediaMaterialGroupViewModel? currentGroup,
        ElectronicArchiveFourLayerResolveResult parsed)
    {
        if (!MatchesCommittedProject(parsed) || !EnsureScannedProjectRegistered(parsed))
        {
            return;
        }

        if (!media.UsesRegisterMaterialGroups)
        {
            media.EnableRegisterMaterialGroups();
        }

        ParsedHeaderApplyResult header = OfferParsedYearAndProject(parsed);
        if (parsed.SelectedIsMaterialFolder)
        {
            MediaMaterialGroupViewModel group = currentGroup
                ?? media.Materials.LastOrDefault()
                ?? media.AddMaterialGroup(parsed.MaterialName);
            if (string.IsNullOrWhiteSpace(group.MaterialName))
            {
                group.MaterialName = parsed.MaterialName;
            }

            bool replaceCurrentGroup = group.Items.Count == 1
                && string.IsNullOrWhiteSpace(group.Items[0].ContentDesc);
            await AppendMaterialFolderAsync(group, parsed.MaterialFolderPath, replaceCurrentGroup);
            return;
        }
        int writtenGroups = 0;
        foreach (ElectronicArchiveMaterialFolder folder in parsed.Materials)
        {
            MediaMaterialGroupViewModel? group = media.Materials.FirstOrDefault(item =>
                !IsBlankMaterialGroup(item)
                && string.Equals(item.MaterialName?.Trim(), folder.MaterialName?.Trim(), StringComparison.Ordinal));
            bool fillBlank = false;
            if (group == null)
            {
                group = media.Materials.FirstOrDefault(IsBlankMaterialGroup);
                fillBlank = group != null;
                if (group != null)
                {
                    group.MaterialName = folder.MaterialName;
                }
            }

            group ??= media.AddMaterialGroup(folder.MaterialName);
            await AppendMaterialFolderAsync(group, folder.MaterialFolderPath, fillBlank);
            if (group.Items.Any(item => !IsBlankSubItem(item)))
            {
                writtenGroups++;
            }
        }

        if (writtenGroups > 0)
        {
            foreach (MediaMaterialGroupViewModel blank in media.Materials.Where(IsBlankMaterialGroup).ToList())
            {
                media.RemoveMaterialGroup(blank);
            }

            string headerLine = DescribeParsedHeader(parsed, header);
            _dialogService.ShowMessage($"已按 {parsed.Materials.Count} 份资料名称写入当前介质。{headerLine}");
        }
    }

    /// <summary>表头已确定年度和项目时，扫描结果必须是同一对，否则不写入、不改表头。</summary>
    private bool MatchesCommittedProject(ElectronicArchiveFourLayerResolveResult parsed)
    {
        if (!TryGetCommittedYearAndProject(out string? year, out string? projectName))
        {
            return true;
        }

        if (ScannedProjectLibraryMatchSupport.SameYearAndProject(year, projectName, parsed.Year, parsed.ProjectName))
        {
            return true;
        }

        _dialogService.ShowMessage(ScannedProjectLibraryMatchSupport.BuildUnmatchedFourLayerRescanMessage(
            year,
            projectName,
            [$"{parsed.Year}\\{parsed.ProjectName}"]));
        return false;
    }

    /// <summary>
    /// 扫描结果的实施年度和项目名称必须已在项目库中。没有则停止本次扫描，不写入资料。
    /// </summary>
    private bool EnsureScannedProjectRegistered(ElectronicArchiveFourLayerResolveResult parsed)
    {
        if (FindProjectForParsedHeader(parsed.Year, parsed.ProjectName) != null)
        {
            return true;
        }

        _dialogService.ShowMessage(
            ScannedProjectLibraryMatchSupport.BuildMissingProjectMessage(parsed.Year, parsed.ProjectName));
        return false;
    }

    /// <summary>整组都是未填写的资料名称和子项，扫描结果应先占用它。</summary>
    private static bool IsReusableBlankElectronicMedium(MediaEntryViewModel entry) =>
        entry.UsesRegisterMaterialGroups
        && string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
        && (entry.Materials.Count == 0 || entry.Materials.All(IsBlankMaterialGroup));

    private static bool IsBlankMaterialGroup(MediaMaterialGroupViewModel group) =>
        string.IsNullOrWhiteSpace(group.MaterialName)
        && group.Items.All(IsBlankSubItem);

    private static bool IsBlankSubItem(MediaItemViewModel item) =>
        string.IsNullOrWhiteSpace(item.ContentDesc)
        && string.IsNullOrWhiteSpace(item.StorageRootFullPath)
        && !item.HasScannedEntries
        && item.DataSizeMb <= 0;

    private async Task AppendMaterialFolderAsync(
        MediaMaterialGroupViewModel group,
        string folder,
        bool replacePlaceholder)
    {
        ElectronicMediaMaterialFolderListing listing;
        try
        {
            listing = _electronicMediaContentScanService.ListMaterialFolderSubItemDirectories(folder);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
            return;
        }

        if (listing.SubItemDirectories.Count == 0)
        {
            _dialogService.ShowMessage($"「{group.MaterialName}」下没有可作为资料子项的一级子文件夹。");
            return;
        }

        var created = new List<MediaItemViewModel>();
        _dialogService.SetBusyState(true);
        try
        {
            foreach (string directory in listing.SubItemDirectories)
            {
                string name = System.IO.Path.GetFileName(directory.TrimEnd(
                    System.IO.Path.DirectorySeparatorChar,
                    System.IO.Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                MediaItemViewModel item = CreateDefaultElectronicMediaItem();
                item.ContentDesc = name;
                string? scanError = await ScanBatchSubItemAsync(item, directory);
                if (!string.IsNullOrWhiteSpace(scanError))
                {
                    continue;
                }

                created.Add(item);
            }
        }
        finally
        {
            _dialogService.SetBusyState(false);
        }

        if (created.Count == 0)
        {
            return;
        }

        if (replacePlaceholder)
        {
            group.Items.Clear();
        }

        foreach (MediaItemViewModel item in created)
        {
            group.Items.Add(item);
        }
    }

    /// <summary>
    /// 扫描结果只有一套年度和项目时，尽量写入表头。尚未选择项目时直接写入；已选项目且不一致时再询问。
    /// </summary>
    private ParsedHeaderApplyResult OfferParsedYearAndProject(ElectronicArchiveFourLayerResolveResult parsed)
    {
        if (CurrentRecord == null
            || parsed.Status != ElectronicArchiveFourLayerResolveStatus.Unique
            || string.IsNullOrWhiteSpace(parsed.Year))
        {
            return default;
        }

        string parsedProject = parsed.ProjectName?.Trim() ?? string.Empty;
        if (!HeaderHasCommittedProject())
        {
            return ApplyParsedFolderHeader(parsed.Year.Trim(), parsedProject);
        }

        string currentYear = SelectedProject?.ImplementYear?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentYear))
        {
            currentYear = SelectedProjectYear?.Trim() ?? string.Empty;
        }

        if (string.Equals(currentYear, "全部", StringComparison.Ordinal))
        {
            currentYear = string.Empty;
        }

        string currentProject = SelectedProject?.ProjectName?.Trim()
            ?? CurrentRecord.ProjectName?.Trim()
            ?? string.Empty;
        if (ScannedProjectLibraryMatchSupport.SameYearAndProject(
                currentYear,
                currentProject,
                parsed.Year,
                parsedProject))
        {
            return ApplyParsedFolderHeader(parsed.Year.Trim(), parsedProject);
        }

        string projectLine = $"项目：{parsedProject}";
        string message = string.Join(
            Environment.NewLine,
            "文件夹中的年度或项目与表头不一致。是否改用文件夹中的年度和项目？",
            "选择「否」仍会添加资料名称和子项，但不修改表头。",
            $"年度：{parsed.Year.Trim()}",
            projectLine);
        if (!_dialogService.ShowConfirm(message, "覆盖表头的年度和项目"))
        {
            return default;
        }

        return ApplyParsedFolderHeader(parsed.Year.Trim(), parsedProject);
    }

    private ParsedHeaderApplyResult ApplyParsedFolderHeader(string year, string projectName)
    {
        if (CurrentRecord == null || string.IsNullOrWhiteSpace(year))
        {
            return default;
        }

        ProjectInfo? project = FindProjectForParsedHeader(year, projectName);
        if (project == null)
        {
            return default;
        }

        EnsureProjectYearOption(year);
        SelectedProjectYear = year;
        SelectParsedProject(project);
        OnPropertyChanged(nameof(MaterialNameDisplay));
        return new ParsedHeaderApplyResult(true, true, false);
    }

    /// <summary>
    /// 项目列表在切换年度时会被清空，下拉框会把选中项写回空。写入后在界面刷新完成时再选一次。
    /// </summary>
    private void SelectParsedProject(ProjectInfo project)
    {
        ProjectInfo? listed = Projects.FirstOrDefault(item => item.Id == project.Id) ?? project;
        SelectedProject = listed;
        if (Application.Current?.Dispatcher is not { } dispatcher
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        int projectId = listed.Id;
        dispatcher.BeginInvoke(() =>
        {
            ProjectInfo? again = Projects.FirstOrDefault(item => item.Id == projectId);
            if (again != null && !ReferenceEquals(SelectedProject, again))
            {
                SelectedProject = again;
            }
        }, DispatcherPriority.Background);
    }

    private ProjectInfo? FindProjectForParsedHeader(string year, string projectName)
    {
        if (string.IsNullOrWhiteSpace(year) || string.IsNullOrWhiteSpace(projectName))
        {
            return null;
        }

        ProjectInfo? listed = ScannedProjectLibraryMatchSupport.Find(Projects, year, projectName);
        if (listed != null)
        {
            return listed;
        }

        try
        {
            return ScannedProjectLibraryMatchSupport.Find(
                _projectService.SearchProjects(year.Trim(), projectName.Trim()),
                year,
                projectName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool HeaderHasCommittedProject() =>
        SelectedProject != null || !string.IsNullOrWhiteSpace(CurrentRecord?.ProjectName);

    private static string DescribeParsedHeader(ElectronicArchiveFourLayerResolveResult parsed, ParsedHeaderApplyResult header)
    {
        if (!header.Applied)
        {
            return "表头年度和项目未改。";
        }

        return $"表头已填入年度 {parsed.Year}、项目 {parsed.ProjectName}。";
    }

    private readonly record struct ParsedHeaderApplyResult(bool Applied, bool ProjectMatched, bool ProjectAdded);

    private void EnsureProjectYearOption(string year)
    {
        if (ProjectYears.Any(item => string.Equals(item, year, StringComparison.Ordinal)))
        {
            return;
        }

        if (ProjectYears.Count == 0
            || !string.Equals(ProjectYears[0], "全部", StringComparison.Ordinal))
        {
            ProjectYears.Insert(0, "全部");
        }

        int insertAt = ProjectYears.Count;
        for (int i = 1; i < ProjectYears.Count; i++)
        {
            if (string.Compare(ProjectYears[i], year, StringComparison.Ordinal) < 0)
            {
                insertAt = i;
                break;
            }
        }

        ProjectYears.Insert(insertAt, year);
    }

    private async Task<string?> ScanBatchSubItemAsync(MediaItemViewModel item, string directory)
    {
        try
        {
            ElectronicMediaContentScanResult result = await Task.Run(() =>
                _electronicMediaContentScanService.ScanDirectories([directory], allowEmpty: true));
            ApplyScanResult(item, result, [directory]);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
