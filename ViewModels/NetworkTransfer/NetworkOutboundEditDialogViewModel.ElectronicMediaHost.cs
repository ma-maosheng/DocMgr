using System.Threading;
using System.Threading.Tasks;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.NetworkTransfer;
using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;

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
        await _electronicMediaEditor.InitializeAsync().ConfigureAwait(true);
        // 先刷并档目标再 Sync：容量刷新走独立 scope，不与后续附件加载抢页面 DbContext。
        await RefreshArchiveFilingAppendTargetsAsync().ConfigureAwait(true);
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
        OnPropertyChanged(nameof(ArchiveDeputyPresidentTitle));
        ApplyOutboundElectronicMediaEditorRules();
        RefreshOutboundElectronicMediaTypeOptions();
        _electronicMediaEditor.RefreshOutboundDestinationDependentSettings();
        RequestArchiveFilingAppendTargetRefresh();
    }

    private void RequestArchiveFilingAppendTargetRefresh()
    {
        // 初始化/回填期间由 InitializeAsync 顺序调用 Refresh，避免与页面级 DbContext 叠查。
        if (_suppressProjectBinding || _suppressDestinationDependentUi || _isInitializing)
        {
            return;
        }

        _ = RefreshArchiveFilingAppendTargetsAsync();
    }

    private async Task RefreshArchiveFilingAppendTargetsAsync()
    {
        int generation = Interlocked.Increment(ref _archiveFilingAppendTargetsGeneration);
        if (!NetworkTransferDomainValues.IsArchiveFilingDestination(DestinationKind)
            || string.IsNullOrWhiteSpace(ProjectName)
            || string.IsNullOrWhiteSpace(Year))
        {
            if (generation == Volatile.Read(ref _archiveFilingAppendTargetsGeneration))
            {
                _electronicMediaEditor.ReplaceArchiveFilingAppendTargets([]);
            }

            return;
        }

        string projectName = ProjectName.Trim();
        string year = Year.Trim();
        await _archiveFilingAppendTargetsGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (generation != Volatile.Read(ref _archiveFilingAppendTargetsGeneration))
            {
                return;
            }

            List<YearlyElectronicArchiveUnit> units;
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                var filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                units = await filing.GetExistingElectronicUnitsForProjectAsync(projectName, year)
                    .ConfigureAwait(true);
            }

            if (generation != Volatile.Read(ref _archiveFilingAppendTargetsGeneration))
            {
                return;
            }

            List<ArchiveFilingAppendTargetOption> options = units
                .Where(unit =>
                    string.Equals(unit.UnitLifecycleStatus, ArchiveContainerLifecycleStatus.InUse, StringComparison.Ordinal)
                    && ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(unit.StorageCarrierType)
                    && !string.IsNullOrWhiteSpace(unit.LinkedMediumCodes))
                .Select(ArchiveFilingAppendTargetOption.From)
                .ToList();
            _electronicMediaEditor.ReplaceArchiveFilingAppendTargets(options, retainUnlistedSavedTargets: true);
        }
        finally
        {
            _archiveFilingAppendTargetsGate.Release();
        }
    }

    private void ApplyOutboundElectronicMediaEditorRules()
    {
        bool isExternalOffline = NetworkTransferDomainValues.IsExternalOfflineDestination(DestinationKind);
        bool isArchiveFiling = NetworkTransferDomainValues.IsArchiveFilingDestination(DestinationKind);

        // 资料室存档：类型可选光盘或硬盘，处置固定为无需处置（页眉不展示）。
        _electronicMediaEditor.LockElectronicMediaTypeAndDisposition = false;
        _electronicMediaEditor.ShowArchiveFilingHardDiskFields = isArchiveFiling;
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
