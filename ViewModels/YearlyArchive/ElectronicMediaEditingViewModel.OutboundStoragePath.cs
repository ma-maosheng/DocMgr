using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.NetworkTransfer;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive;

public partial class ElectronicMediaEditingViewModel
{
    /// <summary>
    /// 出网申请：按介质类型与目的地自动维护子项存储目录。
    /// </summary>
    public bool EnableOutboundItemStoragePathMode { get; set; }

    /// <summary>
    /// 出网表头快照（年度、项目、资料名称、服务器路径等）。
    /// </summary>
    public Func<NetworkOutboundItemStoragePathSupport.HeaderSnapshot?>? OutboundStoragePathHeaderResolver { get; set; }

    /// <summary>
    /// 按当前表头刷新全部出网子项存储目录与提示。
    /// </summary>
    public void RefreshOutboundItemStoragePaths()
    {
        if (!EnableOutboundItemStoragePathMode)
        {
            return;
        }

        foreach (MediaEntryViewModel media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
        {
            foreach (MediaItemViewModel item in media.Items)
            {
                RefreshOutboundItemStoragePath(item, forceGenerated: false);
            }
        }
    }

    /// <summary>
    /// 资料室存档：确认实物交接前核验各子项所选父目录是否符合四级路径规则。
    /// </summary>
    public IReadOnlyList<string> CollectArchiveFilingScannedPathComplianceErrors()
    {
        if (!EnableOutboundItemStoragePathMode)
        {
            return Array.Empty<string>();
        }

        NetworkOutboundItemStoragePathSupport.HeaderSnapshot? header = OutboundStoragePathHeaderResolver?.Invoke();
        if (header == null
            || !NetworkOutboundItemStoragePathSupport.ShouldForceGeneratedStoragePath(header.DestinationKind))
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();
        int mediaSeq = 0;
        foreach (MediaEntryViewModel media in MediaEntries.Where(RegisterMediaTreeSupport.IsDataElectronic))
        {
            mediaSeq++;
            for (int itemIndex = 0; itemIndex < media.Items.Count; itemIndex++)
            {
                MediaItemViewModel item = media.Items[itemIndex];
                string label = $"第{mediaSeq}条介质第{itemIndex + 1}项";
                if (string.IsNullOrWhiteSpace(item.StorageRootFullPath))
                {
                    errors.Add(
                        $"• {label}：请选择子项资料所属目录并扫描，以核验资料是否按「{NetworkOutboundItemStoragePathSupport.ArchiveFilingMediaPathRuleDescription}」存放。");
                    continue;
                }

                if (!NetworkOutboundItemStoragePathSupport.TryValidateArchiveFilingScannedPath(
                        item.StorageRootFullPath,
                        header.Year,
                        header.ProjectName,
                        header.MaterialName,
                        item.ContentDesc,
                        out _,
                        out string pathError))
                {
                    errors.Add($"• {label}：{pathError}");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// 资料室存档扫描前：所选父目录必须等于或末尾为表头生成的四级路径。
    /// </summary>
    private bool TryEnsureArchiveFilingScannedPathAllowed(
        MediaItemViewModel item,
        string? scannedRootPath,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!EnableOutboundItemStoragePathMode)
        {
            return true;
        }

        NetworkOutboundItemStoragePathSupport.HeaderSnapshot? header = OutboundStoragePathHeaderResolver?.Invoke();
        if (header == null
            || !NetworkOutboundItemStoragePathSupport.ShouldForceGeneratedStoragePath(header.DestinationKind))
        {
            return true;
        }

        return NetworkOutboundItemStoragePathSupport.TryValidateArchiveFilingScannedPath(
            scannedRootPath,
            header.Year,
            header.ProjectName,
            header.MaterialName,
            item.ContentDesc,
            out _,
            out errorMessage);
    }

    private void FillDefaultOutboundStoragePath(MediaItemViewModel? item)
    {
        RefreshOutboundItemStoragePath(item, forceGenerated: true);
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshOutboundItemStoragePath(MediaItemViewModel? item) =>
        RefreshOutboundItemStoragePath(item, forceGenerated: false);

    private void RefreshOutboundItemStoragePath(MediaItemViewModel? item, bool forceGenerated)
    {
        if (item == null || !EnableOutboundItemStoragePathMode)
        {
            return;
        }

        NetworkOutboundItemStoragePathSupport.HeaderSnapshot? header = OutboundStoragePathHeaderResolver?.Invoke();
        if (header == null)
        {
            return;
        }

        string mediaType = string.IsNullOrWhiteSpace(header.MediaType)
            ? ResolveSelectedElectronicMediaType()
            : header.MediaType.Trim();
        string generated = NetworkOutboundItemStoragePathSupport.BuildMediaStoragePath(
            header.Year,
            header.ProjectName,
            header.MaterialName,
            item.ContentDesc);
        forceGenerated = forceGenerated
            || NetworkOutboundItemStoragePathSupport.ShouldForceGeneratedStoragePath(header.DestinationKind);

        item.StoragePathLabel = NetworkOutboundItemStoragePathSupport.ResolveStoragePathLabel(mediaType);
        item.IsStoragePathEditable = NetworkOutboundItemStoragePathSupport.IsStoragePathEditable(
            header.DestinationKind,
            header.CanEditForm);
        item.ShowOutboundStoragePathHint = true;
        item.OutboundServerFullPathHint = NetworkOutboundItemStoragePathSupport.BuildServerFullPathHint(
            header.ServerPhysicalPath,
            header.MaterialPath,
            item.ContentDesc);

        // 仅首次判定：加载时若库内路径与当前生成规则不一致，视为用户曾自定义。
        // 子项名称/表头后续变更不得再把「旧自动路径 ≠ 新自动路径」误判为自定义。
        if (!item.OutboundStoragePathCustomizationResolved)
        {
            item.HasCustomizedOutboundStoragePath =
                !forceGenerated
                && !string.IsNullOrWhiteSpace(item.StoragePath)
                && !NetworkOutboundItemStoragePathSupport.StoragePathsEqual(item.StoragePath, generated);
            item.OutboundStoragePathCustomizationResolved = true;
        }

        bool shouldWriteGenerated = forceGenerated
            || string.IsNullOrWhiteSpace(item.StoragePath)
            || !item.HasCustomizedOutboundStoragePath;
        if (shouldWriteGenerated)
        {
            item.SetStoragePathFromSystem(generated);
        }
    }

    private void ApplyScannedStoragePath(MediaItemViewModel item, string? scannedRootPath)
    {
        if (!EnableOutboundItemStoragePathMode)
        {
            item.StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(scannedRootPath);
            return;
        }

        NetworkOutboundItemStoragePathSupport.HeaderSnapshot? header = OutboundStoragePathHeaderResolver?.Invoke();
        if (header != null
            && NetworkOutboundItemStoragePathSupport.ShouldForceGeneratedStoragePath(header.DestinationKind))
        {
            // 资料室存档：存储目录仍由表头规则生成；扫描只补目录/文件明细与数据量。
            RefreshOutboundItemStoragePath(item);
            return;
        }

        // 院内/院外离线：选择子项资料所属目录后立即回写介质目录文本框（含已自定义或再次选择）。
        item.StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(scannedRootPath);
        item.HasCustomizedOutboundStoragePath = true;
        item.OutboundStoragePathCustomizationResolved = true;
    }
}
