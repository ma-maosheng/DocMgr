using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.NetworkTransfer;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.YearlyArchive;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 出网申请编辑弹窗（结构对齐入网申请）。
/// </summary>
public sealed partial class NetworkOutboundEditDialogViewModel : ViewModelBase
{
    private readonly INetworkTransferService _service;
    private readonly IDialogService _dialogService;
    private readonly IUserContextService _userContextService;
    private readonly IProjectService _projectService;
    private readonly IUserService _userService;
    private readonly IServerPathSettingService _serverPathSettingService;
    private readonly IArchiveRegisterService _archiveRegisterService;
    private readonly ElectronicMediaEditingViewModel _electronicMediaEditor;
    private readonly NetworkTransferWorkspaceMode _mode;
    private bool _suppressProjectBinding;
    private bool _suppressServerPathSelection;
    private NetworkOutboundRecord _record;
    private bool _hasCommittedChanges;
    private string _outboundNo = string.Empty;
    private string _destinationKind = string.Empty;
    private string _projectName = string.Empty;
    private string _year = string.Empty;
    private ProjectInfo? _selectedProject;
    private ServerPathSetting? _selectedServerPath;
    private string _applicantName = string.Empty;
    private string _applicantDept = string.Empty;
    private DateTime _applyTime;
    private bool _hasProofMaterialSelected;
    private string _prodLeader = string.Empty;
    private DateTime? _prodDate = DateTime.Today;
    private string _rndLeader = string.Empty;
    private DateTime? _rndDate = DateTime.Today;
    private string _deputyLeader = string.Empty;
    private DateTime? _deputyDate = DateTime.Today;
    private string _deliverer = string.Empty;
    private DateTime? _deliverDate = DateTime.Today;
    private string _administrator = string.Empty;
    private DateTime? _adminDate = DateTime.Today;
    private string _deptLeader = string.Empty;
    private DateTime? _deptDate = DateTime.Today;

    public NetworkOutboundEditDialogViewModel(
        INetworkTransferService service,
        IDialogService dialogService,
        IUserContextService userContextService,
        IProjectService projectService,
        IUserService userService,
        IServerPathSettingService serverPathSettingService,
        IArchiveRegisterService archiveRegisterService,
        ElectronicMediaEditingViewModel electronicMediaEditor,
        NetworkOutboundRecord record,
        NetworkTransferWorkspaceMode mode)
    {
        ArgumentNullException.ThrowIfNull(record);
        _service = service;
        _dialogService = dialogService;
        _userContextService = userContextService;
        _projectService = projectService;
        _userService = userService;
        _serverPathSettingService = serverPathSettingService;
        _archiveRegisterService = archiveRegisterService;
        _electronicMediaEditor = electronicMediaEditor;
        _record = record;
        _mode = mode;

        SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
        DeleteAttachmentCommand = new RelayCommand(
            async item => await DeleteAttachmentAsync(item as SystemAttachment),
            item => item is SystemAttachment && CanUploadSignedAttachment);
        ViewAttachmentCommand = new RelayCommand(item =>
        {
            if (item is SystemAttachment attachment)
            {
                _dialogService.ShowSystemAttachmentView(attachment);
            }
        }, item => item is SystemAttachment);
        CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanCompleteApproval);
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        InitializeApprovalCommands();
        _ = InitializeAsync();
    }

    public event Action<bool?>? RequestClose;

    public bool HasCommittedChanges => _hasCommittedChanges;

    public NetworkOutboundRecord CurrentRecord => _record;

    public int RecordId => _record.Id;

    public string WindowTitle =>
        $"出网申请 · {(string.IsNullOrWhiteSpace(OutboundNo) ? "待编单" : OutboundNo)} · {StatusDisplay}";

    public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(_record.Status);

    public string BannerText =>
        "本系统与生产网隔离：请手工填写资料信息与电子介质明细；拷贝完成后可从离线介质读取目录与数据量。办结后写入出网台账；目的地为资料室存档时生成待立档任务，归档载体在资料立档时选择。";

    public string BusinessChainProgressDisplay => _record.BusinessChainProgressDisplay;

    public ObservableCollection<string> OutboundDestinationKindOptions { get; } =
        new(NetworkTransferDomainValues.OutboundDestinationKindOptions);

    public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();

    public ObservableCollection<ServerPathSetting> ApplicantServerPathOptions { get; } = new();

    public ObservableCollection<SystemAttachment> Attachments { get; } = new();

    public ObservableCollection<string> ProjectYears { get; } = new();

    public ObservableCollection<ProjectInfo> Projects { get; } = new();

    public bool CanEditForm => CanEditHeader;

    public bool CanEditHeader =>
        _mode == NetworkTransferWorkspaceMode.Application
        && _record.Status == NetworkOutboundRecord.StatusDraft
        && (ArchiveRegisterBusinessRules.CanSubmitApplication(_userContextService.CurrentUser)
            || ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

    public bool CanSubmit => CanEditHeader && GetExternalMediaItemCount() > 0;

    public bool CanPrintApplication =>
        _record.Id > 0
        && _record.Status >= NetworkOutboundRecord.StatusSubmitted
        && _record.Status != NetworkOutboundRecord.StatusWithdrawn
        && _record.Status != NetworkOutboundRecord.StatusForceWithdrawn;

    public bool CanEditApprovalPaths =>
        _mode == NetworkTransferWorkspaceMode.Approval
        && _record.Status == NetworkOutboundRecord.StatusSubmitted
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

    /// <summary>
    /// 审批通过后、确认实物交接前，资料室管理员可从离线介质补录目录/文件并回写数据量与文件个数。
    /// </summary>
    public bool CanSupplementElectronicContentScan =>
        _mode == NetworkTransferWorkspaceMode.Approval
        && _record.Status == NetworkOutboundRecord.StatusApproved
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

    public bool CanEditServerPath => CanEditHeader || CanEditApprovalPaths;

    public bool CanEditItemConfidentialLevel => CanEditForm;

    public string OutboundNo
    {
        get => _outboundNo;
        set => SetProperty(ref _outboundNo, value);
    }

    public string DestinationKind
    {
        get => _destinationKind;
        set
        {
            if (SetProperty(ref _destinationKind, value))
            {
                OnPropertyChanged(nameof(DestinationKindDisplay));
                OnPropertyChanged(nameof(IsArchiveFilingDestination));
                if (!IsArchiveFilingDestination)
                {
                    SelectedArchivePurpose = string.Empty;
                }
                else if (string.IsNullOrWhiteSpace(SelectedArchivePurpose))
                {
                    SelectedArchivePurpose = ArchivePurposeOptions.FirstOrDefault() ?? string.Empty;
                }

                NotifyOutboundDestinationDependentUi();
            }
        }
    }

    public string DestinationKindDisplay =>
        string.IsNullOrWhiteSpace(DestinationKind) ? "-" : DestinationKind.Trim();

    public bool IsArchiveFilingDestination =>
        NetworkTransferDomainValues.IsArchiveFilingDestination(DestinationKind);

    public ObservableCollection<string> ArchivePurposeOptions { get; } = new();

    private string _selectedArchivePurpose = string.Empty;

    public string SelectedArchivePurpose
    {
        get => _selectedArchivePurpose;
        set
        {
            if (SetProperty(ref _selectedArchivePurpose, value ?? string.Empty))
            {
                _record.ArchivePurpose = IsArchiveFilingDestination
                    ? _selectedArchivePurpose.Trim()
                    : string.Empty;
            }
        }
    }

    public string ApplicantName
    {
        get => _applicantName;
        private set => SetProperty(ref _applicantName, value);
    }

    public string ApplicantDept
    {
        get => _applicantDept;
        private set => SetProperty(ref _applicantDept, value);
    }

    public DateTime ApplyTime
    {
        get => _applyTime;
        private set => SetProperty(ref _applyTime, value);
    }

    public string MaterialName
    {
        get => _record.MaterialName;
        set
        {
            _record.MaterialName = value ?? string.Empty;
            OnPropertyChanged();
            RefreshMaterialPath();
        }
    }

    public string MaterialPathDisplay =>
        string.IsNullOrWhiteSpace(_record.MaterialPath)
            ? "请先选择服务器路径，并完善年度、项目与资料名称。系统将按规范生成相对路径。"
            : _record.MaterialPath;

    public string MaterialPathHint =>
        "请将拟出网资料先放入上述相对路径（位于所选服务器路径之下）后再申请。路径为「出网\\年度\\项目\\资料名称」（共用服务器路径时前加申请部门）；各子项名称作为该路径下的子目录。";

    public string ProjectName
    {
        get => _projectName;
        private set
        {
            if (SetProperty(ref _projectName, value))
            {
                RefreshMaterialPath();
            }
        }
    }

    public string Year
    {
        get => _year;
        set
        {
            if (SetProperty(ref _year, value) && !_suppressProjectBinding)
            {
                LoadProjects();
                RefreshMaterialPath();
            }
        }
    }

    public string? SelectedProjectYear
    {
        get => Year;
        set => Year = value ?? string.Empty;
    }

    public ProjectInfo? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value) && !_suppressProjectBinding)
            {
                ProjectName = value?.ProjectName?.Trim() ?? string.Empty;
            }
        }
    }

    public bool HasProofMaterial
    {
        get => _hasProofMaterialSelected;
        set
        {
            if (_hasProofMaterialSelected == value)
            {
                return;
            }

            _hasProofMaterialSelected = value;
            if (!value)
            {
                _record.ProofMaterialNote = ArchiveRegisterDomainValues.ProofMaterialNoneText;
            }
            else if (string.Equals(
                         _record.ProofMaterialNote?.Trim(),
                         ArchiveRegisterDomainValues.ProofMaterialNoneText,
                         StringComparison.Ordinal)
                     || string.IsNullOrWhiteSpace(_record.ProofMaterialNote))
            {
                _record.ProofMaterialNote = string.Empty;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(ProofMaterialName));
            OnPropertyChanged(nameof(ProofMaterialDisplay));
        }
    }

    public string ProofMaterialName
    {
        get => HasProofMaterial ? _record.ProofMaterialNote?.Trim() ?? string.Empty : string.Empty;
        set
        {
            if (!HasProofMaterial)
            {
                return;
            }

            _record.ProofMaterialNote = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProofMaterialDisplay));
        }
    }

    public string ProofMaterialDisplay =>
        HasProofMaterial
            ? (string.IsNullOrWhiteSpace(_record.ProofMaterialNote) ? "-" : _record.ProofMaterialNote.Trim())
            : ArchiveRegisterDomainValues.ProofMaterialNoneText;

    public ServerPathSetting? SelectedServerPath
    {
        get => _selectedServerPath;
        set
        {
            if (SetProperty(ref _selectedServerPath, value))
            {
                if (!_suppressServerPathSelection)
                {
                    _record.ServerPath = value?.PathName?.Trim() ?? string.Empty;
                }

                OnPropertyChanged(nameof(SelectedServerPathInfo));
                if (!_suppressServerPathSelection)
                {
                    RefreshMaterialPath();
                }
            }
        }
    }

    public string SelectedServerPathInfo
    {
        get
        {
            if (SelectedServerPath == null)
            {
                string savedPath = _record.ServerPath?.Trim() ?? string.Empty;
                return string.IsNullOrWhiteSpace(savedPath)
                    ? "请选择生产网来源服务器路径（本单共用）。"
                    : $"当前路径：{savedPath}（列表中未匹配到路径预设，请重新选择。）";
            }

            ServerPathSetting path = SelectedServerPath;
            return $"物理地址 {path.PhysicalPath} · 权限 {path.Permission} · 容量上限 {path.CapacityTb:0.##} TB · 所属 {path.DepartmentName}";
        }
    }

    public string ProdLeader { get => _prodLeader; set => SetProperty(ref _prodLeader, value); }
    public DateTime? ProdDate { get => _prodDate; set => SetProperty(ref _prodDate, value); }
    public string RndLeader { get => _rndLeader; set => SetProperty(ref _rndLeader, value); }
    public DateTime? RndDate { get => _rndDate; set => SetProperty(ref _rndDate, value); }
    public string DeputyLeader { get => _deputyLeader; set => SetProperty(ref _deputyLeader, value); }
    public DateTime? DeputyDate { get => _deputyDate; set => SetProperty(ref _deputyDate, value); }
    public string Deliverer { get => _deliverer; set => SetProperty(ref _deliverer, value); }
    public DateTime? DeliverDate { get => _deliverDate; set => SetProperty(ref _deliverDate, value); }
    public string Administrator { get => _administrator; set => SetProperty(ref _administrator, value); }
    public DateTime? AdminDate { get => _adminDate; set => SetProperty(ref _adminDate, value); }
    public string DeptLeader { get => _deptLeader; set => SetProperty(ref _deptLeader, value); }
    public DateTime? DeptDate { get => _deptDate; set => SetProperty(ref _deptDate, value); }

    public RelayCommand SaveDraftCommand { get; }
    public RelayCommand DeleteAttachmentCommand { get; }
    public RelayCommand ViewAttachmentCommand { get; }
    public RelayCommand CompleteCommand { get; }
    public RelayCommand CloseCommand { get; }

    private async Task InitializeAsync()
    {
        try
        {
            if (_record.Id > 0)
            {
                var latest = await _service.GetOutboundByIdAsync(_record.Id);
                if (latest != null)
                {
                    _record = latest;
                }
            }
            else if (string.IsNullOrWhiteSpace(_record.OutboundNo))
            {
                _record.OutboundNo = await _service.GenerateNextOutboundNoAsync();
            }

            LoadProjectYears();
            BindFromRecord();
            await TryAutoFillDefaultApprovalInfoAsync();
            await LoadConfidentialLevelOptionsAsync();
            await LoadArchivePurposeOptionsAsync();
            await InitializeElectronicMediaEditorAsync();
            LoadApplicantServerPathOptions();
            await ReloadAttachmentsAsync();
            UpdateApprovalUiState();
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private void BindFromRecord()
    {
        OutboundNo = _record.OutboundNo;
        DestinationKind = NetworkTransferDomainValues.NormalizeOutboundDestinationKind(_record.DestinationKind);
        BindProjectSelectionFromRecord();
        SelectedArchivePurpose = _record.ArchivePurpose?.Trim() ?? string.Empty;
        ApplicantName = _record.ApplicantName;
        ApplicantDept = _record.ApplicantDept;
        ApplyTime = _record.ApplyTime == default ? DateTime.Now : _record.ApplyTime;
        ProdLeader = _record.ProdLeader;
        ProdDate = _record.ProdDate ?? DateTime.Today;
        RndLeader = _record.RndLeader;
        RndDate = _record.RndDate ?? DateTime.Today;
        DeputyLeader = _record.DeputyLeader;
        DeputyDate = _record.DeputyDate ?? DateTime.Today;
        DeptLeader = _record.DeptLeader;
        DeptDate = _record.DeptDate ?? DateTime.Today;
        BindHandoverFieldsFromRecord();
        _hasProofMaterialSelected = ArchiveRegisterDomainValues.RequiresProofMaterialAttachment(_record.ProofMaterialNote);
        OnPropertyChanged(nameof(HasProofMaterial));
        OnPropertyChanged(nameof(ProofMaterialName));
        OnPropertyChanged(nameof(MaterialName));
        OnPropertyChanged(nameof(MaterialPathDisplay));
        OnPropertyChanged(nameof(CurrentRecord));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(BusinessChainProgressDisplay));
        OnPropertyChanged(nameof(CanEditHeader));
        OnPropertyChanged(nameof(CanEditForm));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CanPrintApplication));
    }

    private void BindProjectSelectionFromRecord()
    {
        _suppressProjectBinding = true;
        try
        {
            Year = _record.Year;
            LoadProjects();
            SelectedProject = Projects.FirstOrDefault(item =>
                string.Equals(item.ProjectName?.Trim(), _record.ProjectName?.Trim(), StringComparison.Ordinal));
            ProjectName = _record.ProjectName;
        }
        finally
        {
            _suppressProjectBinding = false;
        }
    }

    private void LoadProjectYears()
    {
        ProjectYears.Clear();
        int currentYear = DateTime.Today.Year;
        for (int year = currentYear; year >= currentYear - 10; year--)
        {
            ProjectYears.Add(year.ToString());
        }
    }

    private void LoadProjects()
    {
        try
        {
            if (string.IsNullOrEmpty(Year))
            {
                return;
            }

            string searchYear = Year == "全部" ? string.Empty : Year.Trim();
            Projects.Clear();
            foreach (ProjectInfo project in _projectService.SearchProjects(searchYear, keyword: null))
            {
                Projects.Add(project);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("加载项目异常: " + ex.Message);
        }
    }

    private void LoadApplicantServerPathOptions()
    {
        // 必须先抑制：Clear() 会把 ComboBox.SelectedItem 置空并回写 ServerPath。
        _suppressServerPathSelection = true;
        try
        {
            string saved = _record.ServerPath?.Trim() ?? string.Empty;
            ApplicantServerPathOptions.Clear();
            string department = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty;
            foreach (ServerPathSetting setting in _serverPathSettingService.GetWritablePathsForDepartment(department)
                         .OrderBy(item => item.DepartmentName)
                         .ThenBy(item => item.PathName))
            {
                ApplicantServerPathOptions.Add(setting);
            }

            SelectedServerPath = ApplicantServerPathOptions.FirstOrDefault(item =>
                string.Equals(item.PathName?.Trim(), saved, StringComparison.Ordinal));
        }
        finally
        {
            _suppressServerPathSelection = false;
        }

        RefreshMaterialPath();
    }

    private void RefreshMaterialPath()
    {
        if (!CanEditServerPath)
        {
            OnPropertyChanged(nameof(MaterialPathDisplay));
            return;
        }

        _record.MaterialPath = NetworkOutboundMaterialPathSupport.BuildMaterialPath(
            SelectedServerPath,
            ApplicantDept,
            Year,
            ProjectName,
            _record.MaterialName);
        OnPropertyChanged(nameof(MaterialPathDisplay));
        OnPropertyChanged(nameof(CurrentRecord));
        _electronicMediaEditor.RefreshOutboundItemStoragePaths();
    }

    private async Task LoadConfidentialLevelOptionsAsync()
    {
        var domainOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();
        ConfidentialLevelOptions.Clear();
        foreach (string level in domainOptions.ConfidentialLevels)
        {
            if (!string.IsNullOrWhiteSpace(level))
            {
                ConfidentialLevelOptions.Add(level.Trim());
            }
        }

        if (ConfidentialLevelOptions.Count == 0)
        {
            ConfidentialLevelOptions.Add(ArchiveRegisterDomainValues.ConfidentialLevelNone);
        }
    }

    private async Task LoadArchivePurposeOptionsAsync()
    {
        var domainOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();
        ArchivePurposeOptions.Clear();
        foreach (string purpose in domainOptions.ArchivePurposes)
        {
            if (!string.IsNullOrWhiteSpace(purpose))
            {
                ArchivePurposeOptions.Add(purpose.Trim());
            }
        }

        if (IsArchiveFilingDestination && string.IsNullOrWhiteSpace(SelectedArchivePurpose))
        {
            SelectedArchivePurpose = ArchivePurposeOptions.FirstOrDefault() ?? string.Empty;
        }
    }

    private NetworkOutboundRecord BuildDraftSnapshot()
    {
        var draft = new NetworkOutboundRecord
        {
            Id = _record.Id,
            OutboundNo = OutboundNo,
            DestinationKind = DestinationKind,
            MaterialName = MaterialName,
            ServerPath = SelectedServerPath?.PathName?.Trim() ?? _record.ServerPath?.Trim() ?? string.Empty,
            MaterialPath = _record.MaterialPath?.Trim() ?? string.Empty,
            ProjectName = ProjectName,
            ArchivePurpose = IsArchiveFilingDestination ? SelectedArchivePurpose : string.Empty,
            Year = Year,
            Reason = _record.Reason,
            OtherRequests = _record.OtherRequests,
            Remark = _record.Remark,
            MediaEntries = BuildExternalMediaEntriesForSave()
        };
        if (!HasProofMaterial)
        {
            draft.ProofMaterialNote = ArchiveRegisterDomainValues.ProofMaterialNoneText;
        }
        else
        {
            draft.ProofMaterialNote = ProofMaterialName;
        }

        return draft;
    }

    private async Task SaveDraftAsync()
    {
        try
        {
            var user = RequireUser();
            NetworkOutboundRecord draft = BuildDraftSnapshot();
            _record = _record.Id > 0
                ? await _service.UpdateOutboundDraftAsync(draft, [], user)
                : await _service.CreateOutboundDraftAsync(draft, [], user);
            _hasCommittedChanges = true;
            BindFromRecord();
            // 保存后仅同步表头字段，避免重载电子介质区把已选类型重置为域值首项（U盘）。
            LoadApplicantServerPathOptions();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(CanSubmit));
            CommandManager.InvalidateRequerySuggested();
            _dialogService.ShowMessage("草稿已保存。");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private async Task SubmitAsync()
    {
        try
        {
            NetworkOutboundRecord draft = BuildDraftSnapshot();
            if (HasProofMaterial && string.IsNullOrWhiteSpace(ProofMaterialName))
            {
                _dialogService.ShowError("已选择附有证明材料，请填写证明材料名称。");
                return;
            }

            IReadOnlyList<string> validationErrors = NetworkOutboundApplicationValidationSupport.ValidateForSubmit(
                draft,
                draft.MediaEntries?.ToList());
            if (validationErrors.Count > 0)
            {
                _dialogService.ShowError(string.Join(Environment.NewLine, validationErrors));
                return;
            }

            await SaveDraftAsync();
            await _service.SubmitOutboundAsync(_record.Id, RequireUser());
            _hasCommittedChanges = true;
            _dialogService.ShowMessage("已提交审批。");
            await ReloadRecordAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private async Task PrintApplicationAsync()
    {
        try
        {
            bool blankApproval = _record.Status < NetworkOutboundRecord.StatusApproved;
            var data = await _service.BuildOutboundPrintDataAsync(_record.Id, blankApproval);
            var document = NetworkOutboundPrintDocumentFactory.Create(data);
            var previewWindow = new PrintPreviewWindow(document)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            await _service.RecordOutboundPrintAsync(_record.Id);
            previewWindow.ShowDialog();
            await SyncOutboundPrintMetadataAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("打印生成失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 打印后仅同步打印次数字段，避免整单重载导致电子介质类型等界面状态被重置。
    /// </summary>
    private async Task SyncOutboundPrintMetadataAsync()
    {
        var latest = await _service.GetOutboundByIdAsync(_record.Id);
        if (latest == null)
        {
            return;
        }

        _record.PrintCount = latest.PrintCount;
        _record.LastPrintedAt = latest.LastPrintedAt;
        _record.UpdatedAt = latest.UpdatedAt;
    }

    private async Task ApproveAsync()
    {
        try
        {
            if (_record.Status == NetworkOutboundRecord.StatusSubmitted)
            {
                await PersistOutboundMediaIfNeededAsync();
            }

            await _service.ApproveOutboundAsync(new NetworkOutboundRecord
            {
                Id = _record.Id,
                DeptLeader = DeptLeader,
                DeptDate = DeptDate,
                ProdLeader = ProdLeader,
                ProdDate = ProdDate,
                RndLeader = RndLeader,
                RndDate = RndDate,
                DeputyLeader = DeputyLeader,
                DeputyDate = DeputyDate
            }, RequireUser());
            _hasCommittedChanges = true;
            _dialogService.ShowMessage("审批已通过。");
            await ReloadRecordAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private async Task ConfirmHandoverAsync()
    {
        try
        {
            await PersistOutboundMediaIfNeededAsync();

            IReadOnlyList<string> validationErrors = CollectHandoverValidationErrors();
            if (validationErrors.Count > 0)
            {
                _dialogService.ShowError(
                    "交接确认前校验未通过：" + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, validationErrors));
                return;
            }

            await _service.ConfirmOutboundHandoverAsync(new NetworkOutboundRecord
            {
                Id = _record.Id,
                Deliverer = Deliverer,
                DeliverDate = DeliverDate,
                Administrator = Administrator,
                AdminDate = AdminDate
            }, RequireUser());
            _hasCommittedChanges = true;
            _dialogService.ShowMessage("出网交接已确认。");
            await ReloadRecordAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private async Task CompleteAsync()
    {
        try
        {
            await PersistOutboundMediaIfNeededAsync();

            IReadOnlyList<string> validationErrors = CollectCompleteValidationErrors();
            if (validationErrors.Count > 0)
            {
                _dialogService.ShowError(
                    "办结前信息完整性校验未通过：" + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, validationErrors));
                await RefreshAttachmentRequirementsAsync();
                return;
            }

            await _service.CompleteOutboundAsync(_record.Id, RequireUser());
            _hasCommittedChanges = true;
            if (NetworkTransferDomainValues.IsArchiveFilingDestination(_record.DestinationKind))
            {
                _dialogService.ShowMessage("出网申请已办结。已写入出网台账，并生成待立档任务，请在「资料立档」中选择光盘、空白硬盘或并入同项目资料盘。");
                RequestClose?.Invoke(true);
                return;
            }

            _dialogService.ShowMessage("出网申请已办结，出网台账已写入。");
            await ReloadRecordAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private async Task PersistOutboundMediaIfNeededAsync()
    {
        if (_mode != NetworkTransferWorkspaceMode.Approval
            || _record.Status is not (
                NetworkOutboundRecord.StatusSubmitted
                or NetworkOutboundRecord.StatusApproved
                or NetworkOutboundRecord.StatusSignedUploaded))
        {
            return;
        }

        await _service.UpdateOutboundMediaAsync(
            _record.Id,
            BuildExternalMediaEntriesForSave(),
            RequireUser(),
            SelectedServerPath?.PathName ?? _record.ServerPath,
            _record.MaterialPath);
    }

    private async Task ReloadAttachmentsAsync()
    {
        Attachments.Clear();
        if (_record.Id <= 0 || string.IsNullOrWhiteSpace(_record.OutboundNo))
        {
            RedistributeAttachmentsByCategory();
            await RefreshAttachmentRequirementsAsync();
            return;
        }

        foreach (SystemAttachment attachment in await _service.GetAttachmentsAsync(
                     NetworkTransferDomainValues.OutboundAttachmentBusinessType,
                     _record.OutboundNo))
        {
            Attachments.Add(attachment);
        }

        RedistributeAttachmentsByCategory();
        await RefreshAttachmentRequirementsAsync();
    }

    private async Task DeleteAttachmentAsync(SystemAttachment? attachment)
    {
        if (attachment == null)
        {
            return;
        }

        try
        {
            var (ok, message) = await _service.DeleteAttachmentAsync(attachment.Id, RequireUser());
            if (!ok)
            {
                _dialogService.ShowError(message);
                return;
            }

            _hasCommittedChanges = true;
            await ReloadAttachmentsAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    /// <summary>
    /// 重载单据表头、审批态与附件；不重载电子介质明细，避免已选类型被域值首项（U盘）覆盖。
    /// </summary>
    private async Task ReloadRecordAsync()
    {
        var latest = await _service.GetOutboundByIdAsync(_record.Id);
        if (latest == null)
        {
            return;
        }

        _record = latest;
        BindFromRecord();
        await TryAutoFillDefaultApprovalInfoAsync();
        SyncElectronicMediaEditorEditState();
        LoadApplicantServerPathOptions();
        await ReloadAttachmentsAsync();
        UpdateApprovalUiState();
    }

    private async Task TryAutoFillDefaultApprovalInfoAsync()
    {
        if (_mode != NetworkTransferWorkspaceMode.Approval
            || (_record.Status != NetworkOutboundRecord.StatusSubmitted
                && _record.Status != NetworkOutboundRecord.StatusApproved))
        {
            return;
        }

        var user = _userContextService.CurrentUser;
        if (user == null || !ArchiveRegisterBusinessRules.IsArchiveAdminUser(user))
        {
            return;
        }

        try
        {
            await _archiveRegisterService.ApplyDefaultNetworkOutboundApprovalInfoAsync(_record, user);
            SyncApprovalFieldsFromRecord();
        }
        catch
        {
            // 自动回填失败不阻断打开；交接默认值仍由 BindHandoverFieldsFromRecord 保障。
            BindHandoverFieldsFromRecord();
        }
    }

    /// <summary>
    /// 将审批节点与资料交接字段同步到界面绑定。
    /// </summary>
    private void SyncApprovalFieldsFromRecord()
    {
        ProdLeader = _record.ProdLeader;
        ProdDate = _record.ProdDate ?? DateTime.Today;
        RndLeader = _record.RndLeader;
        RndDate = _record.RndDate ?? DateTime.Today;
        DeputyLeader = _record.DeputyLeader;
        DeputyDate = _record.DeputyDate ?? DateTime.Today;
        DeptLeader = _record.DeptLeader;
        DeptDate = _record.DeptDate ?? DateTime.Today;
        BindHandoverFieldsFromRecord();
        OnPropertyChanged(nameof(CurrentRecord));
    }

    /// <summary>
    /// 审批打开时同步资料交接默认值：移交人=申请人，资料员=当前资料管理员，日期缺省为当天。
    /// </summary>
    private void BindHandoverFieldsFromRecord()
    {
        if (_mode == NetworkTransferWorkspaceMode.Approval)
        {
            if (string.IsNullOrWhiteSpace(_record.Deliverer))
            {
                _record.Deliverer = _record.ApplicantName;
            }

            if (!_record.DeliverDate.HasValue)
            {
                _record.DeliverDate = DateTime.Today;
            }

            if (string.IsNullOrWhiteSpace(_record.Administrator))
            {
                _record.Administrator = _userContextService.CurrentUser?.RealName ?? string.Empty;
            }

            if (!_record.AdminDate.HasValue)
            {
                _record.AdminDate = DateTime.Today;
            }
        }

        Deliverer = string.IsNullOrWhiteSpace(_record.Deliverer)
            ? _record.ApplicantName
            : _record.Deliverer;
        DeliverDate = _record.DeliverDate ?? DateTime.Today;
        Administrator = string.IsNullOrWhiteSpace(_record.Administrator)
            ? _userContextService.CurrentUser?.RealName ?? string.Empty
            : _record.Administrator;
        AdminDate = _record.AdminDate ?? DateTime.Today;
    }

    private User RequireUser() =>
        _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
}
