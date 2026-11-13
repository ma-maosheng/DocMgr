using System;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.ViewModels.YearlyArchive;

public partial class ElectronicMediaEditingViewModel
{
    private Task BatchAddElectronicMediaItemsAsync(MediaEntryViewModel? media) =>
        BatchAddElectronicMediaItemsAsync(media, targetGroup: null);

    private async Task BatchAddElectronicMediaItemsAsync(
        MediaMaterialGroupViewModel? group)
    {
        await BatchAddElectronicMediaItemsAsync(group?.Owner, group);
    }

    private Task BatchAddElectronicMediaItemsAsync(
        MediaEntryViewModel? media,
        MediaMaterialGroupViewModel? targetGroup) =>
        BatchAddElectronicMediaItemsAsync(media, targetGroup, pickFolderTitle: null, acceptDriveRoot: false);

    private async Task BatchAddElectronicMediaItemsAsync(
        MediaEntryViewModel? media,
        MediaMaterialGroupViewModel? targetGroup,
        string? pickFolderTitle,
        bool acceptDriveRoot)
    {
        if (media == null || !AllowElectronicContentScan)
        {
            return;
        }

        if (!CanEditForm && !UseRegisterMaterialGroups)
        {
            return;
        }

        TryGetCommittedYearAndProject(out string? requiredYear, out string? requiredProjectName);
        await ElectronicMediaBatchSubItemAddSupport.AddAsync(
            media,
            _dialogService,
            _electronicMediaContentScanService,
            CreateDefaultElectronicMediaItem,
            ValidateBatchSubItemFolder,
            ScanBatchSubItemAsync,
            pickFolderTitle: pickFolderTitle
                ?? (UseRegisterMaterialGroups ? "选择资料名称文件夹，或年度所在目录" : null),
            targetGroup: targetGroup,
            importRegisterMaterials: UseRegisterMaterialGroups
                ? parsed => ImportResolvedMaterialsAsync(media, targetGroup, parsed)
                : null,
            acceptDriveRoot: acceptDriveRoot,
            requiredYear: requiredYear,
            requiredProjectName: requiredProjectName);
    }

    /// <summary>
    /// 四级目录扫描结果写入当前介质的资料名称分组。页面还没选定项目时，回填这次扫描认定的实施年度和项目名称。
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

        bool headerFilled = ApplyScannedProjectIfHeaderUnset(parsed);
        if (!media.UsesRegisterMaterialGroups)
        {
            media.EnableRegisterMaterialGroups();
        }

        if (parsed.SelectedIsMaterialFolder)
        {
            MediaMaterialGroupViewModel group = currentGroup
                ?? media.Materials.LastOrDefault()
                ?? media.AddMaterialGroup(parsed.MaterialName);
            if (string.IsNullOrWhiteSpace(group.MaterialName))
            {
                group.MaterialName = parsed.MaterialName;
            }

            bool replaceCurrentGroup = group.Items.Count == 1 && IsBlankNetworkSubItem(group.Items[0]);
            await AppendMaterialFolderAsync(group, parsed.MaterialFolderPath, replaceCurrentGroup);
            return;
        }

        int writtenGroups = 0;
        foreach (ElectronicArchiveMaterialFolder folder in parsed.Materials)
        {
            MediaMaterialGroupViewModel? group = media.Materials.FirstOrDefault(item =>
                !IsBlankNetworkMaterialGroup(item)
                && string.Equals(item.MaterialName?.Trim(), folder.MaterialName?.Trim(), StringComparison.Ordinal));
            bool fillBlank = false;
            if (group == null)
            {
                group = media.Materials.FirstOrDefault(IsBlankNetworkMaterialGroup);
                fillBlank = group != null;
                if (group != null)
                {
                    group.MaterialName = folder.MaterialName;
                }
            }

            group ??= media.AddMaterialGroup(folder.MaterialName);
            await AppendMaterialFolderAsync(group, folder.MaterialFolderPath, fillBlank);
            if (group.Items.Any(item => !IsBlankNetworkSubItem(item)))
            {
                writtenGroups++;
            }
        }

        if (writtenGroups == 0)
        {
            return;
        }

        foreach (MediaMaterialGroupViewModel blank in media.Materials.Where(IsBlankNetworkMaterialGroup).ToList())
        {
            media.RemoveMaterialGroup(blank);
        }

        string headerLine = headerFilled
            ? $" 实施年度和项目名称已填入 {parsed.Year?.Trim()}、{parsed.ProjectName?.Trim()}。"
            : string.Empty;
        _dialogService.ShowMessage($"已按 {parsed.Materials.Count} 份资料名称写入当前介质。{headerLine}");
    }

    /// <summary>尚未选定项目时，把扫描认定且已在项目库中的年度和项目交给宿主写入页面。</summary>
    private bool ApplyScannedProjectIfHeaderUnset(ElectronicArchiveFourLayerResolveResult parsed)
    {
        TryGetCommittedYearAndProject(out string? year, out string? projectName);
        if (!string.IsNullOrWhiteSpace(year) && !string.IsNullOrWhiteSpace(projectName))
        {
            return false;
        }

        if (ApplyScannedProjectWhenUnset == null || string.IsNullOrWhiteSpace(parsed.Year))
        {
            return false;
        }

        ProjectInfo? project = FindScannedLibraryProject(parsed);
        if (project == null)
        {
            return false;
        }

        ApplyScannedProjectWhenUnset(parsed.Year.Trim(), project);
        return true;
    }

    private void TryGetCommittedYearAndProject(out string? year, out string? projectName)
    {
        year = null;
        projectName = null;
        if (ResolveCommittedProject == null)
        {
            return;
        }

        (string? resolvedYear, string? resolvedProjectName) = ResolveCommittedProject();
        year = resolvedYear?.Trim() ?? string.Empty;
        projectName = resolvedProjectName?.Trim() ?? string.Empty;
        if (string.Equals(year, "全部", StringComparison.Ordinal))
        {
            year = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(year) || string.IsNullOrWhiteSpace(projectName))
        {
            year = null;
            projectName = null;
        }
    }

    /// <summary>表头已确定年度和项目时，扫描结果必须是同一对，否则不写入、不改表头。</summary>
    private bool MatchesCommittedProject(ElectronicArchiveFourLayerResolveResult parsed)
    {
        TryGetCommittedYearAndProject(out string? year, out string? projectName);
        if (string.IsNullOrWhiteSpace(year) || string.IsNullOrWhiteSpace(projectName))
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
    /// 扫描结果的实施年度和项目名称必须已在项目库中。没有则停止本次扫描，不写入资料，也不改表头。
    /// </summary>
    private bool EnsureScannedProjectRegistered(ElectronicArchiveFourLayerResolveResult parsed)
    {
        try
        {
            if (FindScannedLibraryProject(parsed) != null)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("核对项目库失败：" + ex.Message);
            return false;
        }

        _dialogService.ShowMessage(
            ScannedProjectLibraryMatchSupport.BuildMissingProjectMessage(parsed.Year, parsed.ProjectName));
        return false;
    }

    /// <summary>按扫描到的实施年度和项目名称，在项目库中找已登记的那一条。</summary>
    private ProjectInfo? FindScannedLibraryProject(ElectronicArchiveFourLayerResolveResult parsed)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
        return ScannedProjectLibraryMatchSupport.Find(
            projectService.SearchProjects(parsed.Year, parsed.ProjectName),
            parsed.Year,
            parsed.ProjectName);
    }

    /// <summary>整组都是未填写的资料名称和子项时，扫描盘符应先占用它。</summary>
    private static bool IsReusableBlankElectronicMedium(MediaEntryViewModel entry) =>
        entry.UsesRegisterMaterialGroups
        && string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
        && (entry.Materials.Count == 0 || entry.Materials.All(IsBlankNetworkMaterialGroup));

    private static bool IsBlankNetworkMaterialGroup(MediaMaterialGroupViewModel group) =>
        string.IsNullOrWhiteSpace(group.MaterialName)
        && group.Items.All(IsBlankNetworkSubItem);

    private static bool IsBlankNetworkSubItem(MediaItemViewModel item) =>
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
            RefreshOutboundItemStoragePath(item);
        }
    }

    private string? ValidateBatchSubItemFolder(MediaItemViewModel item, string directory)
    {
        return TryEnsureArchiveFilingScannedPathAllowed(item, directory, out string pathError)
            ? null
            : pathError;
    }

    private async Task<string?> ScanBatchSubItemAsync(MediaItemViewModel item, string directory)
    {
        try
        {
            ElectronicMediaContentScanResult result = await Task.Run(() =>
                _electronicMediaContentScanService.ScanDirectories([directory], allowEmpty: true));
            if (!TryEnsureArchiveFilingScannedPathAllowed(item, result.RootPath, out string pathError))
            {
                return pathError;
            }

            ApplyScanResult(item, result, [directory]);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
