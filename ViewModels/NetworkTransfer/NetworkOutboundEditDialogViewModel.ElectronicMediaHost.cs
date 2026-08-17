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
        _electronicMediaEditor.CanEditForm = CanEditForm;
        _electronicMediaEditor.CanEditItemConfidentialLevel = CanEditItemConfidentialLevel;
        _electronicMediaEditor.SectionHeader = ExternalDataSourceSectionTitle;
        await _electronicMediaEditor.InitializeAsync();
        _electronicMediaEditor.SyncFromEntities(_record.MediaEntries?.ToList() ?? []);
        _electronicMediaEditor.RefreshOutboundDestinationDependentSettings();
    }

    private void SyncElectronicMediaEditorEditState()
    {
        ApplyOutboundElectronicMediaEditorRules();
        _electronicMediaEditor.CanEditForm = CanEditForm;
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

        // 资料室存档：锁定「内网 + 无需处置」，归档载体由后续立档选择。
        _electronicMediaEditor.LockElectronicMediaTypeAndDisposition = isArchiveFiling;
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
        _electronicMediaEditor.ShowElectronicDispositionInHeader = false;
        _electronicMediaEditor.ShowOutboundExternalHardDiskRequisitionFields = isExternalOffline;
        _electronicMediaEditor.EnableOutboundItemStoragePathMode = true;
        _electronicMediaEditor.OutboundStoragePathHeaderResolver = BuildOutboundStoragePathHeader;
        // 申请与审批通过前无法读取具体文件/目录；审批通过后、确认实物交接前允许补录扫描。
        _electronicMediaEditor.AllowElectronicContentScan = CanSupplementElectronicContentScan;
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
            CanEditForm = CanEditForm
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
