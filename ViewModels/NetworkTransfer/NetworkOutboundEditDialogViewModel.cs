using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
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
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 出网申请办理弹窗（草稿编辑 + 审批交接办结）。明细可表格内手工录入；目的地=立档时不跟踪中间过程介质。
    /// </summary>
    public sealed class NetworkOutboundEditDialogViewModel : ViewModelBase
    {
        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IProjectService _projectService;
        private readonly IUserService _userService;
        private readonly IServerPathSettingService _serverPathSettingService;
        private readonly IArchiveRegisterService _archiveRegisterService;
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
        private string _reason = string.Empty;
        private string _remark = string.Empty;
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
        private string _uploadCategory = NetworkTransferDomainValues.AttachmentCategorySignedForm;
        private string _applicantName = string.Empty;
        private string _applicantDept = string.Empty;
        private DateTime _applyTime;
        private bool _hasProofMaterialSelected;

        public NetworkOutboundEditDialogViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService,
            IProjectService projectService,
            IUserService userService,
            IServerPathSettingService serverPathSettingService,
            IArchiveRegisterService archiveRegisterService,
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
            _record = record;
            _mode = mode;

            AddItemCommand = new RelayCommand(_ => AddItem(), _ => CanEditHeader);
            RemoveItemCommand = new RelayCommand(
                item => RemoveItemRow(item as NetworkOutboundItemRowViewModel),
                item => CanEditHeader && item is NetworkOutboundItemRowViewModel);
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmHandoverCommand = new RelayCommand(async _ => await ConfirmHandoverAsync(), _ => CanConfirmHandover);
            UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
            DeleteAttachmentCommand = new RelayCommand(
                async item => await DeleteAttachmentAsync(item as SystemAttachment),
                item => item is SystemAttachment && CanUploadAttachment);
            ViewAttachmentCommand = new RelayCommand(item =>
            {
                if (item is SystemAttachment attachment)
                {
                    _dialogService.ShowSystemAttachmentView(attachment);
                }
            }, item => item is SystemAttachment);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            PrintApplicationCommand = new RelayCommand(async _ => await PrintApplicationAsync(), _ => CanPrintApplication);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            RefreshUploadCategoryOptions();
            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"出网申请 · {(string.IsNullOrWhiteSpace(OutboundNo) ? "待编单" : OutboundNo)} · {StatusDisplay}";

        public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(_record.Status);

        public string BannerText =>
            "按业务目的发起：转资料室立档时以本张出网申请为主单，办结后自动生成已带资料明细的建档草稿；用户只需确认介质类型、所属子类及归档目的。";

        public string DestinationHint =>
            "目的地为「资料室立档」时，办结后自动生成完整建档草稿，不登记中间过程介质。";

        public string BusinessChainProgressDisplay => _record.BusinessChainProgressDisplay;

        public ObservableCollection<string> OutboundDestinationKindOptions { get; } =
            new(NetworkTransferDomainValues.OutboundDestinationKindOptions);

        public ObservableCollection<string> AssetKindOptions { get; } =
            new(NetworkTransferDomainValues.AssetKindOptions);

        public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();

        public IReadOnlyList<string> DataSizeUnitOptions => NetworkInboundItemDisplaySupport.DataSizeUnitOptions;

        public ObservableCollection<string> UploadCategoryOptions { get; } = new();

        public ObservableCollection<NetworkOutboundItem> Items { get; } = new();

        public ObservableCollection<NetworkOutboundItemRowViewModel> ItemRows { get; } = new();

        public ObservableCollection<ServerPathSetting> ApplicantServerPathOptions { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<string> ProjectYears { get; } = new();

        public ObservableCollection<ProjectInfo> Projects { get; } = new();

        public bool IsArchiveFilingDestination =>
            NetworkTransferDomainValues.IsArchiveFilingDestination(DestinationKind);

        public bool CanEditHeader =>
            _mode == NetworkTransferWorkspaceMode.Application
            && _record.Status == NetworkOutboundRecord.StatusDraft
            && (ArchiveRegisterBusinessRules.CanSubmitApplication(_userContextService.CurrentUser)
                || ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

        public bool CanSubmit => CanEditHeader && Items.Count > 0;

        public bool IsOutboundItemGridReadOnly => !CanEditHeader;

        public bool CanEditServerPath => CanEditHeader;

        public bool CanPrintApplication =>
            _record.Id > 0
            && _record.Status >= NetworkOutboundRecord.StatusSubmitted
            && _record.Status != NetworkOutboundRecord.StatusWithdrawn
            && _record.Status != NetworkOutboundRecord.StatusForceWithdrawn;

        public bool CanApprove =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkOutboundRecord.StatusSubmitted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanConfirmHandover =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkOutboundRecord.StatusApproved
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanUploadAttachment =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status is NetworkOutboundRecord.StatusApproved or NetworkOutboundRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanComplete =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkOutboundRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool ShowApprovalPanel =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status >= NetworkOutboundRecord.StatusSubmitted
            && _record.Status != NetworkOutboundRecord.StatusWithdrawn
            && _record.Status != NetworkOutboundRecord.StatusForceWithdrawn;

        public bool ShowApplicationActions => _mode == NetworkTransferWorkspaceMode.Application;

        public bool ShowApprovalActions => ShowApprovalPanel;

        public bool ShowItemToolbar => CanEditHeader;

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
                    OnPropertyChanged(nameof(IsArchiveFilingDestination));
                }
            }
        }

        public ServerPathSetting? SelectedServerPath
        {
            get => _selectedServerPath;
            set
            {
                if (SetProperty(ref _selectedServerPath, value) && !_suppressServerPathSelection)
                {
                    ApplySharedServerPathToItems();
                    OnPropertyChanged(nameof(SelectedServerPathInfo));
                }
            }
        }

        public string SelectedServerPathInfo
        {
            get
            {
                if (SelectedServerPath == null)
                {
                    string savedPath = Items.Select(item => item.ServerPath?.Trim())
                        .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
                    return string.IsNullOrWhiteSpace(savedPath)
                        ? "请选择服务器路径（本单全部明细共用同一路径）。"
                        : $"当前路径：{savedPath}（列表中未匹配到路径预设，请重新选择。）";
                }

                ServerPathSetting path = SelectedServerPath;
                return $"物理地址 {path.PhysicalPath} · 权限 {path.Permission} · 容量上限 {path.CapacityTb:0.##} TB · 所属 {path.DepartmentName}";
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

        public string ProjectName
        {
            get => _projectName;
            private set => SetProperty(ref _projectName, value);
        }

        public string Year
        {
            get => _year;
            set
            {
                if (SetProperty(ref _year, value) && !_suppressProjectBinding)
                {
                    LoadProjects();
                    ClearSelectedProjectIfNotInList();
                }
            }
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

        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        public string Remark
        {
            get => _remark;
            set => SetProperty(ref _remark, value);
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

                NotifyProofMaterialStateChanged();
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

                string normalized = value?.Trim() ?? string.Empty;
                if (string.Equals(_record.ProofMaterialNote, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _record.ProofMaterialNote = normalized;
                NotifyProofMaterialStateChanged();
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

        public string UploadCategory
        {
            get => _uploadCategory;
            set => SetProperty(ref _uploadCategory, value);
        }

        public RelayCommand AddItemCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmHandoverCommand { get; }
        public RelayCommand UploadAttachmentCommand { get; }
        public RelayCommand DeleteAttachmentCommand { get; }
        public RelayCommand ViewAttachmentCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand PrintApplicationCommand { get; }
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

                await LoadConfidentialLevelOptionsAsync();
                LoadProjectYears();
                BindFromRecord();
                LoadApplicantServerPathOptions();
                SyncSelectedServerPathFromItems();
                await RebuildItemRowsAsync();
                await TryAutoFillDefaultApprovalInfoAsync();
                await ReloadAttachmentsAsync();
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
            DestinationKind = string.IsNullOrWhiteSpace(_record.DestinationKind)
                ? NetworkTransferDomainValues.DestinationKindExternalOffline
                : _record.DestinationKind.Trim();
            if (!NetworkTransferDomainValues.IsAllowedOutboundDestinationKind(DestinationKind)
                && !string.Equals(DestinationKind, NetworkTransferDomainValues.DestinationKindOther, StringComparison.Ordinal))
            {
                DestinationKind = NetworkTransferDomainValues.DestinationKindExternalOffline;
            }

            BindProjectSelectionFromRecord();
            Reason = _record.Reason;
            Remark = _record.Remark;
            ApplicantName = _record.ApplicantName;
            ApplicantDept = _record.ApplicantDept;
            ApplyTime = _record.ApplyTime == default ? DateTime.Now : _record.ApplyTime;
            ProdLeader = _record.ProdLeader;
            ProdDate = _record.ProdDate ?? DateTime.Today;
            RndLeader = _record.RndLeader;
            RndDate = _record.RndDate ?? DateTime.Today;
            DeputyLeader = _record.DeputyLeader;
            DeputyDate = _record.DeputyDate ?? DateTime.Today;
            Deliverer = string.IsNullOrWhiteSpace(_record.Deliverer)
                ? _record.ApplicantName
                : _record.Deliverer;
            DeliverDate = _record.DeliverDate ?? DateTime.Today;
            Administrator = string.IsNullOrWhiteSpace(_record.Administrator)
                ? _userContextService.CurrentUser?.RealName ?? string.Empty
                : _record.Administrator;
            AdminDate = _record.AdminDate ?? DateTime.Today;
            DeptLeader = _record.DeptLeader;
            DeptDate = _record.DeptDate ?? DateTime.Today;
            _hasProofMaterialSelected = ArchiveRegisterDomainValues.HasProofMaterial(_record.ProofMaterialNote);
            if (!_hasProofMaterialSelected && string.IsNullOrWhiteSpace(_record.ProofMaterialNote))
            {
                _record.ProofMaterialNote = ArchiveRegisterDomainValues.ProofMaterialNoneText;
            }

            Items.Clear();
            foreach (var item in _record.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            {
                Items.Add(CloneItem(item));
            }

            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(BusinessChainProgressDisplay));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(IsOutboundItemGridReadOnly));
            OnPropertyChanged(nameof(CanEditServerPath));
            OnPropertyChanged(nameof(ShowApprovalPanel));
            OnPropertyChanged(nameof(ShowApplicationActions));
            OnPropertyChanged(nameof(ShowApprovalActions));
            OnPropertyChanged(nameof(ShowItemToolbar));
            OnPropertyChanged(nameof(IsArchiveFilingDestination));
            OnPropertyChanged(nameof(CanPrintApplication));
            NotifyProofMaterialStateChanged();
            NotifyWorkflowCommandStateChanged();
        }

        private void NotifyWorkflowCommandStateChanged()
        {
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmHandover));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanComplete));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task TryAutoFillDefaultApprovalInfoAsync()
        {
            if (_mode != NetworkTransferWorkspaceMode.Approval
                || _record.Status != NetworkOutboundRecord.StatusSubmitted)
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
                // 自动回填失败不阻断页面打开。
            }
        }

        private void SyncApprovalFieldsFromRecord()
        {
            DeptLeader = _record.DeptLeader;
            DeptDate = _record.DeptDate ?? DateTime.Today;
            ProdLeader = _record.ProdLeader;
            ProdDate = _record.ProdDate ?? DateTime.Today;
            RndLeader = _record.RndLeader;
            RndDate = _record.RndDate ?? DateTime.Today;
            DeputyLeader = _record.DeputyLeader;
            DeputyDate = _record.DeputyDate ?? DateTime.Today;
            Deliverer = string.IsNullOrWhiteSpace(_record.Deliverer)
                ? _record.ApplicantName
                : _record.Deliverer;
            DeliverDate = _record.DeliverDate ?? DateTime.Today;
            Administrator = string.IsNullOrWhiteSpace(_record.Administrator)
                ? _userContextService.CurrentUser?.RealName ?? string.Empty
                : _record.Administrator;
            AdminDate = _record.AdminDate ?? DateTime.Today;
        }

        private void NotifyProofMaterialStateChanged()
        {
            OnPropertyChanged(nameof(HasProofMaterial));
            OnPropertyChanged(nameof(ProofMaterialName));
            RefreshUploadCategoryOptions();
        }

        private void RefreshUploadCategoryOptions()
        {
            string previous = UploadCategory;
            UploadCategoryOptions.Clear();
            foreach (string option in NetworkTransferDomainValues.BuildOutboundAttachmentCategoryOptions(HasProofMaterial))
            {
                UploadCategoryOptions.Add(option);
            }

            if (!string.IsNullOrWhiteSpace(previous)
                && UploadCategoryOptions.Contains(previous, StringComparer.Ordinal))
            {
                UploadCategory = previous;
            }
            else if (UploadCategoryOptions.Count > 0)
            {
                UploadCategory = UploadCategoryOptions[0];
            }
        }

        private void ApplyProofMaterialNoteToDraft(NetworkOutboundRecord draft)
        {
            draft.ProofMaterialNote = HasProofMaterial
                ? ProofMaterialName.Trim()
                : ArchiveRegisterDomainValues.ProofMaterialNoneText;
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

        private void LoadProjectYears()
        {
            int currentYear = DateTime.Now.Year;
            ProjectYears.Clear();
            ProjectYears.Add("全部");
            for (int i = 0; i < 10; i++)
            {
                ProjectYears.Add((currentYear - i).ToString());
            }

            if (string.IsNullOrWhiteSpace(Year))
            {
                Year = currentYear.ToString();
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

        private void BindProjectSelectionFromRecord()
        {
            _suppressProjectBinding = true;
            try
            {
                ProjectName = _record.ProjectName?.Trim() ?? string.Empty;
                Year = string.IsNullOrWhiteSpace(_record.Year)
                    ? DateTime.Now.Year.ToString()
                    : _record.Year.Trim();
                LoadProjects();

                ProjectInfo? matched = string.IsNullOrWhiteSpace(ProjectName)
                    ? null
                    : Projects.FirstOrDefault(project =>
                        string.Equals(project.ProjectName?.Trim(), ProjectName, StringComparison.Ordinal));

                if (matched == null && !string.IsNullOrWhiteSpace(ProjectName))
                {
                    matched = _projectService.SearchProjects(year: null, keyword: null)
                        .FirstOrDefault(project =>
                            string.Equals(project.ProjectName?.Trim(), ProjectName, StringComparison.Ordinal));
                    if (matched != null && !string.IsNullOrWhiteSpace(matched.ImplementYear))
                    {
                        Year = matched.ImplementYear.Trim();
                        LoadProjects();
                        matched = Projects.FirstOrDefault(project => project.Id == matched.Id)
                                  ?? matched;
                    }
                }

                SelectedProject = matched;
            }
            finally
            {
                _suppressProjectBinding = false;
            }
        }

        private void ClearSelectedProjectIfNotInList()
        {
            if (SelectedProject == null)
            {
                ProjectName = string.Empty;
                return;
            }

            bool stillExists = Projects.Any(project =>
                project.Id == SelectedProject.Id
                || string.Equals(project.ProjectName?.Trim(), SelectedProject.ProjectName?.Trim(), StringComparison.Ordinal));
            if (!stillExists)
            {
                SelectedProject = null;
            }
        }

        private void LoadApplicantServerPathOptions()
        {
            string department = string.IsNullOrWhiteSpace(ApplicantDept)
                ? ResolveApplicantUser().Department?.Trim() ?? string.Empty
                : ApplicantDept.Trim();

            ApplicantServerPathOptions.Clear();
            foreach (ServerPathSetting path in _serverPathSettingService.GetWritablePathsForDepartment(department))
            {
                ApplicantServerPathOptions.Add(path);
            }
        }

        private void SyncSelectedServerPathFromItems()
        {
            _suppressServerPathSelection = true;
            try
            {
                string savedPath = Items.Select(item => item.ServerPath?.Trim())
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
                SelectedServerPath = string.IsNullOrWhiteSpace(savedPath)
                    ? null
                    : ApplicantServerPathOptions.FirstOrDefault(path =>
                        string.Equals(path.PathName, savedPath, StringComparison.Ordinal)
                        || string.Equals(path.PhysicalPath, savedPath, StringComparison.Ordinal));
                OnPropertyChanged(nameof(SelectedServerPathInfo));
            }
            finally
            {
                _suppressServerPathSelection = false;
            }
        }

        private void ApplySharedServerPathToItems()
        {
            string pathName = SelectedServerPath?.PathName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pathName))
            {
                return;
            }

            foreach (NetworkOutboundItem item in Items)
            {
                item.ServerPath = pathName;
            }
        }

        private Task RebuildItemRowsAsync()
        {
            ItemRows.Clear();
            foreach (NetworkOutboundItem item in Items.OrderBy(row => row.SortOrder).ThenBy(row => row.Id))
            {
                ItemRows.Add(NetworkOutboundItemRowViewModel.Create(item));
            }

            return Task.CompletedTask;
        }

        private User ResolveApplicantUser()
        {
            if (_record.ApplicantUserId > 0)
            {
                User? applicant = _userService.GetAllUsers()
                    .FirstOrDefault(user => user.Id == _record.ApplicantUserId);
                if (applicant != null)
                {
                    return applicant;
                }
            }

            return _userContextService.CurrentUser
                   ?? throw new InvalidOperationException("当前用户无效。");
        }

        private void AddItem()
        {
            Items.Add(new NetworkOutboundItem
            {
                SortOrder = Items.Count + 1,
                AssetKind = NetworkTransferDomainValues.AssetKindJobData,
                AssetName = string.Empty,
                ServerPath = SelectedServerPath?.PathName?.Trim() ?? string.Empty,
                CreatedAt = DateTime.Now
            });
            ApplySharedServerPathToItems();
            _ = RebuildItemRowsAsync();
            NotifyWorkflowCommandStateChanged();
        }

        private void RemoveItemRow(NetworkOutboundItemRowViewModel? row)
        {
            if (row == null)
            {
                return;
            }

            Items.Remove(row.Item);
            int sort = 1;
            foreach (NetworkOutboundItem item in Items)
            {
                item.SortOrder = sort++;
            }

            _ = RebuildItemRowsAsync();
            NotifyWorkflowCommandStateChanged();
        }

        private static NetworkOutboundItem CloneItem(NetworkOutboundItem item) => new()
        {
            Id = item.Id,
            SortOrder = item.SortOrder,
            OnNetAssetId = item.OnNetAssetId,
            AssetNo = item.AssetNo,
            AssetKind = item.AssetKind,
            AssetName = item.AssetName,
            ItemName = item.ItemName,
            ServerPath = item.ServerPath,
            ConfidentialLevel = item.ConfidentialLevel,
            DataSizeText = item.DataSizeText,
            ProjectName = item.ProjectName,
            Year = item.Year,
            CreatedAt = item.CreatedAt
        };

        private NetworkOutboundRecord BuildDraftSnapshot()
        {
            var draft = new NetworkOutboundRecord
            {
                Id = _record.Id,
                OutboundNo = OutboundNo,
                DestinationKind = DestinationKind,
                ProjectName = ProjectName,
                Year = Year,
                Reason = Reason,
                Remark = Remark
            };
            ApplyProofMaterialNoteToDraft(draft);
            return draft;
        }

        private async Task SaveDraftAsync()
        {
            try
            {
                ApplySharedServerPathToItems();
                var user = RequireUser();
                var draft = BuildDraftSnapshot();
                _record = _record.Id > 0
                    ? await _service.UpdateOutboundDraftAsync(draft, Items.ToList(), user)
                    : await _service.CreateOutboundDraftAsync(draft, Items.ToList(), user);
                _hasCommittedChanges = true;
                BindFromRecord();
                LoadApplicantServerPathOptions();
                SyncSelectedServerPathFromItems();
                await RebuildItemRowsAsync();
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
                ApplySharedServerPathToItems();
                NetworkOutboundRecord draft = BuildDraftSnapshot();
                if (HasProofMaterial && string.IsNullOrWhiteSpace(ProofMaterialName))
                {
                    _dialogService.ShowError("已选择附有证明材料，请填写证明材料名称。");
                    return;
                }

                IReadOnlyList<string> validationErrors = NetworkOutboundApplicationValidationSupport.ValidateForSubmit(
                    draft,
                    Items.ToList());
                if (validationErrors.Count > 0)
                {
                    _dialogService.ShowError(string.Join(Environment.NewLine, validationErrors));
                    return;
                }

                await SaveDraftAsync();
                await _service.SubmitOutboundAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("已提交审批。");
                RequestClose?.Invoke(true);
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
                    Owner = Application.Current.MainWindow
                };

                await _service.RecordOutboundPrintAsync(_record.Id);
                previewWindow.ShowDialog();
                await ReloadAsync();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("打印生成失败：" + ex.Message);
            }
        }

        private async Task ApproveAsync()
        {
            try
            {
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
                await ReloadAsync();
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
                await ReloadAsync();
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
                await _service.CompleteOutboundAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                string tip = "出网单已办结。";
                var latest = await _service.GetOutboundByIdAsync(_record.Id);
                if (latest != null && !string.IsNullOrWhiteSpace(latest.TargetRegisterFormNo))
                {
                    tip += $" 已生成并预填建档草稿【{latest.TargetRegisterFormNo}】。";
                    if (_dialogService.ShowConfirm(
                            tip + Environment.NewLine + Environment.NewLine + "是否立即打开建档草稿继续确认？",
                            "跨域业务已衔接"))
                    {
                        _dialogService.ShowArchiveRegisterEditDialog(
                            ArchiveRegisterWorkspaceMode.Application,
                            out _,
                            latest.TargetRegisterRecordId);
                        RequestClose?.Invoke(true);
                        return;
                    }
                }

                _dialogService.ShowMessage(tip);
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task UploadAttachmentAsync()
        {
            try
            {
                string? path = _dialogService.OpenFileDialog("所有文件|*.*", "选择附件");
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return;
                }

                byte[] content = await File.ReadAllBytesAsync(path);
                string fileName = Path.GetFileName(path);
                string extension = Path.GetExtension(path);
                var (ok, message, _) = await _service.UploadAttachmentAsync(
                    NetworkTransferDomainValues.OutboundAttachmentBusinessType,
                    _record.Id,
                    _record.OutboundNo,
                    UploadCategory,
                    fileName,
                    extension,
                    content.LongLength,
                    content,
                    RequireUser());
                if (!ok)
                {
                    _dialogService.ShowError(message);
                    return;
                }

                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
                await ReloadAsync();
                _dialogService.ShowMessage(message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
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

        private async Task ReloadAttachmentsAsync()
        {
            Attachments.Clear();
            if (string.IsNullOrWhiteSpace(_record.OutboundNo))
            {
                return;
            }

            var list = await _service.GetAttachmentsAsync(
                NetworkTransferDomainValues.OutboundAttachmentBusinessType,
                _record.OutboundNo);
            foreach (var item in list)
            {
                Attachments.Add(item);
            }
        }

        private async Task ReloadAsync()
        {
            var latest = await _service.GetOutboundByIdAsync(_record.Id);
            if (latest == null)
            {
                return;
            }

            _record = latest;
            BindFromRecord();
            LoadApplicantServerPathOptions();
            SyncSelectedServerPathFromItems();
            await RebuildItemRowsAsync();
            await TryAutoFillDefaultApprovalInfoAsync();
            await ReloadAttachmentsAsync();
        }

        private User RequireUser() =>
            _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
    }
}
