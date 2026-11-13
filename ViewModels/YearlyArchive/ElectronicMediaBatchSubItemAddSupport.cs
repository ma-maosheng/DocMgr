using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive;

/// <summary>
/// 从资料名称文件夹的一级子文件夹批量生成电子资料子项。
/// </summary>
internal static class ElectronicMediaBatchSubItemAddSupport
{
    /// <summary>新增电子介质或子项前，询问是否按四级目录扫描。</summary>
    internal const string FourLayerScanPrompt =
        "当电子介质内的资料组织形式符合【年度\\项目\\资料名称\\子项名称】四级结构时，可以通过扫描资料介质来直接获取相关资料详情，该介质是否需要扫描？";

    /// <summary>
    /// 选择资料名称文件夹，将其一级子文件夹追加为资料子项。本组仅有一条未改动的空子项时，用本次结果替换它。
    /// </summary>
    public static async Task AddAsync(
        MediaEntryViewModel? media,
        IDialogService dialogService,
        IElectronicMediaContentScanService scanService,
        Func<MediaItemViewModel> createItem,
        Func<MediaItemViewModel, string, string?> validateFolder,
        Func<MediaItemViewModel, string, Task<string?>> scanAndApplyAsync,
        Action<ElectronicArchiveFourLayerResolveResult>? confirmParsedHeader = null,
        string? pickFolderTitle = null,
        MediaMaterialGroupViewModel? targetGroup = null,
        Func<ElectronicArchiveFourLayerResolveResult, Task>? importRegisterMaterials = null,
        bool acceptDriveRoot = false,
        string? requiredYear = null,
        string? requiredProjectName = null)
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(scanService);
        ArgumentNullException.ThrowIfNull(createItem);
        ArgumentNullException.ThrowIfNull(validateFolder);
        ArgumentNullException.ThrowIfNull(scanAndApplyAsync);

        if (media == null || !RegisterMediaTreeSupport.IsDataElectronic(media))
        {
            return;
        }

        string? folder = dialogService.PickFolder(
            string.IsNullOrWhiteSpace(pickFolderTitle)
                ? "选择资料名称文件夹（其一级子文件夹将作为资料子项）"
                : pickFolderTitle);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        bool limitToCommittedProject = !string.IsNullOrWhiteSpace(requiredYear)
                                        && !string.IsNullOrWhiteSpace(requiredProjectName);
        if (acceptDriveRoot && ElectronicMediaItemSupport.IsDriveRoot(folder))
        {
            ElectronicArchiveFourLayerResolveResult driveResolved =
                StockHardDiskDirectFilingDirectorySupport.ResolveRegistrationDrive(
                    folder,
                    requiredYear,
                    requiredProjectName);
            if (driveResolved.Status == ElectronicArchiveFourLayerResolveStatus.ProjectMismatch)
            {
                dialogService.ShowMessage(ScannedProjectLibraryMatchSupport.BuildUnmatchedFourLayerRescanMessage(
                    requiredYear,
                    requiredProjectName,
                    driveResolved.CandidateLabels));
                return;
            }

            if (driveResolved.Status == ElectronicArchiveFourLayerResolveStatus.Ambiguous)
            {
                dialogService.ShowMessage(BuildAmbiguousHeaderMessage(driveResolved.CandidateLabels));
                return;
            }

            if (driveResolved.Status == ElectronicArchiveFourLayerResolveStatus.Unique
                && importRegisterMaterials != null)
            {
                await importRegisterMaterials(driveResolved).ConfigureAwait(true);
                return;
            }

            dialogService.ShowMessage("所选盘符下没有符合「年度\\项目\\资料名称\\子项名称」的目录。");
            return;
        }

        if (confirmParsedHeader != null || importRegisterMaterials != null)
        {
            ElectronicArchiveFourLayerResolveResult resolved =
                StockHardDiskDirectFilingDirectorySupport.ResolveRegistrationFolder(
                    folder,
                    requiredYear,
                    requiredProjectName);
            if (resolved.Status == ElectronicArchiveFourLayerResolveStatus.ProjectMismatch)
            {
                dialogService.ShowMessage(ScannedProjectLibraryMatchSupport.BuildUnmatchedFourLayerRescanMessage(
                    requiredYear,
                    requiredProjectName,
                    resolved.CandidateLabels));
                return;
            }

            if (resolved.Status == ElectronicArchiveFourLayerResolveStatus.Ambiguous)
            {
                dialogService.ShowMessage(BuildAmbiguousHeaderMessage(resolved.CandidateLabels));
                return;
            }

            if (resolved.Status == ElectronicArchiveFourLayerResolveStatus.Unique
                && importRegisterMaterials != null)
            {
                await importRegisterMaterials(resolved).ConfigureAwait(true);
                return;
            }

            if (resolved.Status == ElectronicArchiveFourLayerResolveStatus.Unique)
            {
                confirmParsedHeader?.Invoke(resolved);
                folder = resolved.MaterialFolderPath;
            }
            else if (limitToCommittedProject)
            {
                dialogService.ShowMessage("所选文件夹下没有符合「年度\\项目\\资料名称\\子项名称」的目录。");
                return;
            }
        }

        if (ElectronicMediaItemSupport.IsDriveRoot(folder))
        {
            dialogService.ShowError("不能把盘符根目录当作资料名称文件夹。请选择代表资料名称的文件夹。");
            return;
        }

        ElectronicMediaMaterialFolderListing listing;
        try
        {
            listing = scanService.ListMaterialFolderSubItemDirectories(folder);
        }
        catch (Exception ex)
        {
            dialogService.ShowError(ex.Message);
            return;
        }

        if (listing.SubItemDirectories.Count == 0)
        {
            dialogService.ShowMessage("所选文件夹下没有可作为资料子项的一级子文件夹。");
            return;
        }

        bool replacePlaceholder = targetGroup != null
            ? targetGroup.Items.Count == 1 && IsUntouchedPlaceholder(targetGroup.Items[0], createItem())
            : media.Items.Count == 1 && IsUntouchedPlaceholder(media.Items[0], createItem());

        var created = new List<MediaItemViewModel>();
        var skipped = new List<string>();
        dialogService.SetBusyState(true);
        try
        {
            foreach (string directory in listing.SubItemDirectories)
            {
                string name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(name))
                {
                    skipped.Add($"{directory}：无法识别子项名称。");
                    continue;
                }

                MediaItemViewModel item = createItem();
                item.ContentDesc = name;
                string? pathError = validateFolder(item, directory);
                if (!string.IsNullOrWhiteSpace(pathError))
                {
                    skipped.Add($"{name}：{pathError}");
                    continue;
                }

                string? scanError = await scanAndApplyAsync(item, directory).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(scanError))
                {
                    skipped.Add($"{name}：{scanError}");
                    continue;
                }

                created.Add(item);
            }
        }
        finally
        {
            dialogService.SetBusyState(false);
        }

        if (created.Count > 0)
        {
            if (targetGroup != null)
            {
                if (replacePlaceholder)
                {
                    targetGroup.Items.Clear();
                }

                foreach (MediaItemViewModel item in created)
                {
                    targetGroup.Items.Add(item);
                }
            }
            else
            {
                if (replacePlaceholder)
                {
                    media.Items.Clear();
                }

                foreach (MediaItemViewModel item in created)
                {
                    media.Items.Add(item);
                }
            }
        }

        string summary = BuildSummary(
            created.Count,
            replacePlaceholder && created.Count > 0,
            listing.LooseFileCount,
            skipped);
        if (created.Count == 0)
        {
            dialogService.ShowError(summary);
            return;
        }

        dialogService.ShowMessage(summary);
    }

    private static bool IsUntouchedPlaceholder(MediaItemViewModel item, MediaItemViewModel fresh)
    {
        return string.IsNullOrWhiteSpace(item.ContentDesc)
            && string.IsNullOrWhiteSpace(item.Note)
            && string.IsNullOrWhiteSpace(item.SubCategory)
            && string.IsNullOrWhiteSpace(item.StorageRootFullPath)
            && !item.HasScannedEntries
            && item.DataSizeMb <= 0
            && item.ContentCount == 1
            && string.Equals(item.MaterialCategory?.Trim(), fresh.MaterialCategory?.Trim(), StringComparison.Ordinal)
            && string.Equals(item.ConfidentialLevel?.Trim(), fresh.ConfidentialLevel?.Trim(), StringComparison.Ordinal)
            && string.Equals(item.SourceType?.Trim(), fresh.SourceType?.Trim(), StringComparison.Ordinal)
            && string.Equals(item.ProvideUnit?.Trim(), fresh.ProvideUnit?.Trim(), StringComparison.Ordinal)
            && string.Equals(NormalizePath(item.StoragePath), NormalizePath(fresh.StoragePath), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? path)
    {
        return (path ?? string.Empty).Trim().TrimEnd('\\', '/');
    }

    private static string BuildSummary(
        int addedCount,
        bool replacedPlaceholder,
        int looseFileCount,
        IReadOnlyList<string> skipped)
    {
        var lines = new List<string>();
        if (addedCount > 0)
        {
            lines.Add(replacedPlaceholder
                ? $"已用 {addedCount} 条资料子项替换原有空白子项。子项名称来自子文件夹名，可继续修改。"
                : $"已追加 {addedCount} 条资料子项。子项名称来自子文件夹名，可继续修改。");
        }
        else
        {
            lines.Add("没有新增资料子项。");
        }

        if (looseFileCount > 0)
        {
            lines.Add($"资料名称文件夹中有 {looseFileCount} 个文件，未作为子项。");
        }

        if (skipped.Count > 0)
        {
            lines.Add("以下子文件夹未添加：");
            foreach (string item in skipped)
            {
                lines.Add($"• {item}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildAmbiguousHeaderMessage(IReadOnlyList<string> labels)
    {
        const int maxShown = 8;
        var lines = new List<string>
        {
            "所选文件夹里有多套年度或多个项目，不能导入到同一张申请单。"
        };
        int shown = Math.Min(maxShown, labels.Count);
        for (int i = 0; i < shown; i++)
        {
            lines.Add($"• {labels[i]}");
        }

        if (labels.Count > maxShown)
        {
            lines.Add($"其余 {labels.Count - maxShown} 套未列出。");
        }

        lines.Add("请改为选择同一个年度、同一个项目下的目录。");
        return string.Join(Environment.NewLine, lines);
    }
}
