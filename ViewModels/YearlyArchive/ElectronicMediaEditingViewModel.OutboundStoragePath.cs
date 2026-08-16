using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.NetworkTransfer;

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

        if (!forceGenerated
            && !string.IsNullOrWhiteSpace(item.StoragePath)
            && !NetworkOutboundItemStoragePathSupport.StoragePathsEqual(item.StoragePath, generated))
        {
            item.HasCustomizedOutboundStoragePath = true;
        }

        bool shouldWriteGenerated = forceGenerated
            || string.IsNullOrWhiteSpace(item.StoragePath)
            || !item.HasCustomizedOutboundStoragePath;
        if (shouldWriteGenerated)
        {
            item.SetStoragePathFromSystem(generated);
            item.HasCustomizedOutboundStoragePath = false;
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
            RefreshOutboundItemStoragePath(item);
            return;
        }

        if (!item.HasCustomizedOutboundStoragePath)
        {
            item.SetStoragePathFromSystem(ElectronicMediaItemSupport.FormatStoragePathForRegistration(scannedRootPath));
            item.HasCustomizedOutboundStoragePath = true;
        }
    }
}
