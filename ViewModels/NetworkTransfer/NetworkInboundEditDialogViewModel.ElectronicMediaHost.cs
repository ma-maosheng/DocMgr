using System.Windows;
using System.Windows.Threading;

using DocMgr.Models.Projects;
using DocMgr.Models.YearlyArchive;

using DocMgr.Services.NetworkTransfer;

using DocMgr.ViewModels.YearlyArchive;



namespace DocMgr.ViewModels.NetworkTransfer;



public sealed partial class NetworkInboundEditDialogViewModel

{

    /// <summary>档外资料入网：与 YA-REG-Ed 同构的电子介质编辑区。</summary>

    public ElectronicMediaEditingViewModel ElectronicMediaEditor => _electronicMediaEditor;



    /// <summary>档外资料时展示 YA 式资料介质（电子）卡片。</summary>

    public bool ShowExternalElectronicMediaSection => IsExternalSource;



    /// <summary>立档资料时展示检索集与明细预览卡片。</summary>

    public bool ShowArchivedDataSourceSection => IsArchivedSource;



    /// <summary>档外数据来源卡片标题。</summary>

    public string ExternalDataSourceSectionTitle => "数据来源（档外） *";



    /// <summary>立档数据来源卡片标题。</summary>

    public string ArchivedDataSourceSectionTitle => "数据来源（档内） *";



    private async Task InitializeElectronicMediaEditorAsync()

    {

        ApplyInboundElectronicMediaEditorRules();

        _electronicMediaEditor.CanEditForm = CanEditForm;

        _electronicMediaEditor.CanEditItemConfidentialLevel = CanEditItemConfidentialLevel;

        _electronicMediaEditor.LockElectronicMediaTypeAndDisposition = false;

        await _electronicMediaEditor.InitializeAsync();

        _electronicMediaEditor.UngroupedMaterialNameFallback = _record.MaterialName;
        _electronicMediaEditor.SyncFromEntities(_record.MediaEntries?.ToList() ?? []);

        NetworkInboundReturnHardDiskMediaBridgeSupport.ApplyReturnHardDiskItemsToMediaEntries(

            _electronicMediaEditor.MediaEntries,

            _record.ReturnHardDiskItems?.ToList(),

            requireBorrowedHardDiskForRetained: IsExternalSource);

    }



    private void SyncElectronicMediaEditorEditState()

    {

        ApplyInboundElectronicMediaEditorRules();

        _electronicMediaEditor.CanEditForm = CanEditForm;

        _electronicMediaEditor.CanEditItemConfidentialLevel = CanEditItemConfidentialLevel;

    }



    /// <summary>数据来源切换时刷新档内/档外卡片可见性与标题。</summary>

    private void NotifyInboundSourceKindDependentUi()

    {

        OnPropertyChanged(nameof(ShowExternalElectronicMediaSection));

        OnPropertyChanged(nameof(ShowArchivedDataSourceSection));

        SyncElectronicMediaEditorEditState();

    }



    private void ApplyInboundElectronicMediaEditorRules()

    {

        bool isExternal = IsExternalSource;

        _electronicMediaEditor.AllowedDispositionOptionsResolver = isExternal

            ? NetworkInboundRegisterMediaRulesSupport.GetAllowedElectronicDispositions

            : null;

        _electronicMediaEditor.RestrictRetainedHardDiskToBorrowedOnly = isExternal;

        _electronicMediaEditor.UseRegisterMaterialGroups = isExternal;
        _electronicMediaEditor.AllowElectronicContentScan = isExternal;
        _electronicMediaEditor.ResolveCommittedProject = () => (Year, ProjectName);
        _electronicMediaEditor.ApplyScannedProjectWhenUnset = ApplyScannedYearAndProject;
        if (isExternal)

        {

            _electronicMediaEditor.SectionHeader = ExternalDataSourceSectionTitle;

        }

    }



    private List<YearlyArchiveRegisterMedia> BuildExternalMediaEntriesForSave() =>

        _electronicMediaEditor.BuildEntities();



    private int GetExternalMediaItemCount() =>

        _electronicMediaEditor.MediaEntries.Sum(entry => entry.Items.Count);

    /// <summary>
    /// 入网页还没选定项目时，用扫描认定的实施年度和项目名称回填。已经选定的不改。
    /// </summary>
    private void ApplyScannedYearAndProject(string year, ProjectInfo project)
    {
        if (SelectedProject != null || !string.IsNullOrWhiteSpace(ProjectName))
        {
            return;
        }

        string targetYear = year.Trim();
        if (string.IsNullOrWhiteSpace(targetYear))
        {
            return;
        }

        if (!ProjectYears.Any(item => string.Equals(item, targetYear, StringComparison.Ordinal)))
        {
            ProjectYears.Add(targetYear);
        }

        if (!string.Equals(Year, targetYear, StringComparison.Ordinal))
        {
            Year = targetYear;
        }
        else
        {
            LoadProjects();
        }

        ProjectInfo? listed = Projects.FirstOrDefault(item => item.Id == project.Id);
        if (listed == null)
        {
            Projects.Add(project);
            listed = project;
        }

        SelectedProject = listed;
        int projectId = listed.Id;
        if (Application.Current?.Dispatcher is not { } dispatcher
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            ProjectInfo? again = Projects.FirstOrDefault(item => item.Id == projectId);
            if (again != null && !ReferenceEquals(SelectedProject, again))
            {
                SelectedProject = again;
            }
        }, DispatcherPriority.Background);
    }

}

