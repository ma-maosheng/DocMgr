using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.NetworkTransfer;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.ViewModels.NetworkTransfer;

public sealed partial class NetworkOutboundEditDialogViewModel
{
    public ElectronicMediaEditingViewModel ElectronicMediaEditor => _electronicMediaEditor;

    public bool ShowExternalElectronicMediaSection => true;

    public string ExternalDataSourceSectionTitle => "出网资料（电子介质） *";

    private async Task InitializeElectronicMediaEditorAsync()
    {
        ApplyOutboundElectronicMediaEditorRules();
        _electronicMediaEditor.CanEditForm = CanEditForm || CanEditApprovalPaths;
        _electronicMediaEditor.CanEditItemConfidentialLevel = CanEditItemConfidentialLevel;
        _electronicMediaEditor.LockElectronicMediaTypeAndDisposition = false;
        _electronicMediaEditor.SectionHeader = ExternalDataSourceSectionTitle;
        await _electronicMediaEditor.InitializeAsync();
        _electronicMediaEditor.SyncFromEntities(_record.MediaEntries?.ToList() ?? []);
        _electronicMediaEditor.RefreshOutboundDestinationDependentSettings();
    }

    private void SyncElectronicMediaEditorEditState()
    {
        ApplyOutboundElectronicMediaEditorRules();
        _electronicMediaEditor.CanEditForm = CanEditForm || CanEditApprovalPaths;
        _electronicMediaEditor.CanEditItemConfidentialLevel = CanEditItemConfidentialLevel;
        _electronicMediaEditor.SectionHeader = ExternalDataSourceSectionTitle;
    }

    private void NotifyOutboundDestinationDependentUi()
    {
        ApplyOutboundElectronicMediaEditorRules();
        RefreshOutboundElectronicMediaTypeOptions();
        _electronicMediaEditor.RefreshOutboundDestinationDependentSettings();
    }

    private void ApplyOutboundElectronicMediaEditorRules()
    {
        bool isExternalOffline = NetworkTransferDomainValues.IsExternalOfflineDestination(DestinationKind);
        bool isArchiveFiling = NetworkTransferDomainValues.IsArchiveFilingDestination(DestinationKind);

        // 资料室立档：不在本单录入借出硬盘；具体硬盘使用由后续立档负责。
        _electronicMediaEditor.AllowedMediaTypeOptionsResolver = isExternalOffline || isArchiveFiling
            ? (allOptions => NetworkOutboundRegisterMediaRulesSupport.GetAllowedElectronicMediaTypes(
                DestinationKind,
                allOptions))
            : null;
        _electronicMediaEditor.AllowedDispositionOptionsResolver = isExternalOffline || isArchiveFiling
            ? ((mediaType, allOptions) =>
                NetworkOutboundRegisterMediaRulesSupport.GetAllowedElectronicDispositions(
                    DestinationKind,
                    mediaType,
                    allOptions))
            : null;
        _electronicMediaEditor.EnableRetainedHardDiskBorrowedRegistration = false;
        _electronicMediaEditor.RestrictRetainedHardDiskToBorrowedOnly = false;
        _electronicMediaEditor.ShowOutboundExternalHardDiskRequisitionFields = isExternalOffline;
        _electronicMediaEditor.EnableOutboundItemStoragePathMode = true;
        _electronicMediaEditor.OutboundStoragePathHeaderResolver = BuildOutboundStoragePathHeader;
        // 申请阶段无法读取具体文件/目录；仅审批补录时允许扫描。
        _electronicMediaEditor.AllowElectronicContentScan = CanEditApprovalPaths;
    }

    private NetworkOutboundItemStoragePathSupport.HeaderSnapshot BuildOutboundStoragePathHeader() =>
        new()
        {
            Year = Year,
            ProjectName = ProjectName,
            MaterialName = MaterialName,
            MaterialPath = CurrentRecord.MaterialPath,
            ServerPhysicalPath = NetworkOutboundItemStoragePathSupport.ResolveServerPhysicalPath(
                SelectedServerPath,
                CurrentRecord.ServerPath),
            DestinationKind = DestinationKind,
            MediaType = _electronicMediaEditor.SelectedElectronicMediaType,
            CanEditForm = CanEditForm || CanEditApprovalPaths
        };

    private void RefreshOutboundElectronicMediaTypeOptions()
    {
        _electronicMediaEditor.RefreshAllowedElectronicMediaTypeOptions();
    }

    private List<YearlyArchiveRegisterMedia> BuildExternalMediaEntriesForSave() =>
        _electronicMediaEditor.BuildEntities();

    private int GetExternalMediaItemCount() =>
        _electronicMediaEditor.MediaEntries.Sum(entry => entry.Items.Count);
}
