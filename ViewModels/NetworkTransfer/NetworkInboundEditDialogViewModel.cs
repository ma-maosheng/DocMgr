using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Projects;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.NetworkTransfer;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.YearlyArchive;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 入网申请编辑弹窗 ViewModel（结构对齐 YA-REG-Ed）。
    /// </summary>
    public sealed partial class NetworkInboundEditDialogViewModel : ViewModelBase
    {
        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IProjectService _projectService;
        private readonly IArchiveFilingSearchService _searchService;
        private readonly IUserService _userService;
        private readonly IServerPathSettingService _serverPathSettingService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly ICabinetService _cabinetService;
        private readonly ElectronicMediaEditingViewModel _electronicMediaEditor;
        private readonly NetworkTransferWorkspaceMode _mode;
        private bool _suppressProjectBinding;
        private bool _suppressSourceKindSideEffects;
        private bool _suppressSearchResultSetSelection;
        private bool _suppressServerPathSelection;
        private NetworkInboundRecord _record;
        private bool _hasCommittedChanges;

        /// <summary>当前入网单（对齐 YA <c>CurrentRecord</c>）。</summary>
        public NetworkInboundRecord CurrentRecord => _record;

        /// <summary>是否可编辑申请表头（对齐 YA <c>CanEditForm</c>）。</summary>
        public bool CanEditForm => CanEditHeader;

        /// <summary>数据来源是否可编辑（对齐 YA <c>IsSourceTypeEditable</c>）。</summary>
        public bool IsSourceKindEditable => CanEditForm;

        /// <summary>跨域业务链进度摘要。</summary>
        public string BusinessChainProgressDisplay => _record.BusinessChainProgressDisplay;

        /// <summary>当前记录 ID（弹窗 UI 状态恢复键）。</summary>
        public int RecordId => _record.Id;

        /// <summary>工作台模式（申请 / 审批）。</summary>
        public NetworkTransferWorkspaceMode WorkspaceMode => _mode;

        private string _inboundNo = string.Empty;
        private string _sourceKind = string.Empty;
        private string _projectName = string.Empty;
        private string _year = string.Empty;
        private ProjectInfo? _selectedProject;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _sourceResultSetNo = string.Empty;
        private int? _sourceResultSetId;
        private SearchPoolListItem? _selectedSearchResultSet;
        private ServerPathSetting? _selectedServerPath;
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
        private SystemAttachment? _selectedAttachment;
        private string _applicantName = string.Empty;
        private string _applicantDept = string.Empty;
        private DateTime _applyTime;
        private bool _hasProofMaterialSelected;

        public NetworkInboundEditDialogViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService,
            IProjectService projectService,
            IArchiveFilingSearchService searchService,
            IUserService userService,
            IServerPathSettingService serverPathSettingService,
            IArchiveRegisterService archiveRegisterService,
            IHardDiskMediaService hardDiskMediaService,
            ICabinetService cabinetService,
            ElectronicMediaEditingViewModel electronicMediaEditor,
            NetworkInboundRecord record,
            NetworkTransferWorkspaceMode mode)
        {
            ArgumentNullException.ThrowIfNull(record);
            _service = service;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _projectService = projectService;
            _searchService = searchService;
            _userService = userService;
            _serverPathSettingService = serverPathSettingService;
            _archiveRegisterService = archiveRegisterService;
            _hardDiskMediaService = hardDiskMediaService;
            _cabinetService = cabinetService;
            _electronicMediaEditor = electronicMediaEditor;
            _record = record;
            _mode = mode;

            RefreshApplicantSearchResultSetsCommand = new RelayCommand(
                async _ => await LoadApplicantSearchResultSetsAsync(),
                _ => IsArchivedSource);
            AddExternalItemCommand = new RelayCommand(_ => AddExternalItem(), _ => CanEditHeader && !IsArchivedSource);
            RemoveItemCommand = new RelayCommand(
                item => RemoveItemRow(item as NetworkInboundItemRowViewModel),
                item => CanEditHeader && item is NetworkInboundItemRowViewModel);
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
            FillDefaultMaterialPathCommand = new RelayCommand(
                _ => FillDefaultMaterialPath(),
                _ => CanEditHeader || CanEditApprovalPaths);
            InitializeApprovalCommands();

            RefreshUploadCategoryOptions();
            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"入网申请 · {(string.IsNullOrWhiteSpace(InboundNo) ? "待编单" : InboundNo)} · {StatusDisplay}";

        public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(_record.Status);

        public string BannerText =>
            "立档资料入网：明细唯一来自电子资料检索结果集，提供部门固定为资料室。档外资料（内部/外部）可手工录入明细。流程：草稿→提交→审批签字→确认入网交接→上传签批单→办结（写入在网台账）。";

        public ObservableCollection<string> SourceKindOptions { get; } = new(NetworkTransferDomainValues.SourceKindOptions);

        public ObservableCollection<string> AssetKindOptions { get; } = new(NetworkTransferDomainValues.AssetKindOptions);

        /// <summary>档外资料录入用密级选项（字段域）。</summary>
        public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();

        /// <summary>数据量单位选项。</summary>
        public IReadOnlyList<string> DataSizeUnitOptions => NetworkInboundItemDisplaySupport.DataSizeUnitOptions;

        public ObservableCollection<string> UploadCategoryOptions { get; } = new();

        public ObservableCollection<NetworkInboundItem> Items { get; } = new();

        public ObservableCollection<NetworkInboundItemRowViewModel> ItemRows { get; } = new();

        public ObservableCollection<ServerPathSetting> ApplicantServerPathOptions { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        /// <summary>项目年度筛选项（实施年度）。</summary>
        public ObservableCollection<string> ProjectYears { get; } = new();

        /// <summary>按年度筛选后的项目列表。</summary>
        public ObservableCollection<ProjectInfo> Projects { get; } = new();

        /// <summary>申请人已有的电子检索集列表。</summary>
        public ObservableCollection<SearchPoolListItem> ApplicantSearchResultSets { get; } = new();

        public bool IsArchivedSource =>
            NetworkTransferDomainValues.IsArchivedElectronicSearchSource(SourceKind);

        public bool CanEditHeader =>
            _mode == NetworkTransferWorkspaceMode.Application
            && _record.Status == NetworkInboundRecord.StatusDraft
            && (ArchiveRegisterBusinessRules.CanSubmitApplication(_userContextService.CurrentUser)
                || ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

        public bool CanSubmit => CanEditHeader && (IsArchivedSource ? Items.Count > 0 : GetExternalMediaItemCount() > 0);

        /// <summary>已提交及之后阶段可打印申请单。</summary>
        public bool CanPrintApplication =>
            _record.Id > 0
            && _record.Status >= NetworkInboundRecord.StatusSubmitted
            && _record.Status != NetworkInboundRecord.StatusWithdrawn
            && _record.Status != NetworkInboundRecord.StatusForceWithdrawn;

        /// <summary>档外资料草稿时表格内直接编辑。</summary>
        public bool IsInboundItemGridReadOnly => !CanEditHeader || IsArchivedSource;

        /// <summary>档外资料时显示添加明细操作。</summary>
        public bool ShowExternalItemToolbar => false;

        /// <summary>当前是否为档外资料来源（明细展示与录入口径）。</summary>
        public bool IsExternalSource => !IsArchivedSource;

        /// <summary>资料室资料管理员在审批通过前可补录服务器路径与资料路径。</summary>
        public bool CanEditApprovalPaths =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkInboundRecord.StatusSubmitted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        /// <summary>审批态已提交/已审批时可持久化路径补录（含交接阶段重保存）。</summary>
        public bool CanEditItemPaths =>
            CanEditApprovalPaths
            || (_mode == NetworkTransferWorkspaceMode.Approval
                && _record.Status == NetworkInboundRecord.StatusApproved
                && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

        /// <summary>明细服务器路径是否只读（草稿可编；审批补录路径时可编）。</summary>
        public bool IsItemPathReadOnly => !CanEditServerPath;

        /// <summary>共享服务器路径与资料路径是否可编辑。</summary>
        public bool CanEditServerPath => CanEditHeader || CanEditApprovalPaths;

        public string InboundNo
        {
            get => _inboundNo;
            set => SetProperty(ref _inboundNo, value);
        }

        public string SourceKind
        {
            get => _sourceKind;
            set
            {
                string previousSourceKind = _sourceKind;
                if (SetProperty(ref _sourceKind, value))
                {
                    OnPropertyChanged(nameof(IsArchivedSource));
                    OnPropertyChanged(nameof(IsExternalSource));
                    OnPropertyChanged(nameof(ShowExternalItemToolbar));
                    OnPropertyChanged(nameof(IsInboundItemGridReadOnly));
                    if (!_suppressSourceKindSideEffects)
                    {
                        ApplyProvideUnitSideEffectsForSourceKind(previousSourceKind);
                        if (IsArchivedSource)
                        {
                            Items.Clear();
                            _ = LoadApplicantSearchResultSetsAsync();
                        }
                        else
                        {
                            ClearArchivedSourceState();
                        }
                    }

                    NotifyInboundSourceKindDependentUi();
                    OnPropertyChanged(nameof(CanSubmit));

                    CommandManager.InvalidateRequerySuggested();
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

        public string ProjectName
        {
            get => _projectName;
            private set => SetProperty(ref _projectName, value);
        }

        /// <summary>入网数据所属年度，同时作为项目下拉筛选条件。</summary>
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

        /// <summary>从项目表选中的项目。</summary>
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

        /// <summary>申请时是否附有证明材料。</summary>
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

        /// <summary>证明材料名称（仅在附有证明材料时有效）。</summary>
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

        public int? SourceResultSetId
        {
            get => _sourceResultSetId;
            set => SetProperty(ref _sourceResultSetId, value);
        }

        public string SourceResultSetNo
        {
            get => _sourceResultSetNo;
            set => SetProperty(ref _sourceResultSetNo, value);
        }

        /// <summary>选中的电子检索集。</summary>
        public SearchPoolListItem? SelectedSearchResultSet
        {
            get => _selectedSearchResultSet;
            set
            {
                if (SetProperty(ref _selectedSearchResultSet, value))
                {
                    OnPropertyChanged(nameof(SelectedSearchResultSetSummary));
                    if (!_suppressSearchResultSetSelection)
                    {
                        _ = ApplySelectedSearchResultSetAsync();
                    }
                }
            }
        }

        /// <summary>本单统一目标服务器路径。</summary>
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

        /// <summary>所选服务器路径的属性说明。</summary>
        public string SelectedServerPathInfo
        {
            get
            {
                if (SelectedServerPath == null)
                {
                    string savedPath = Items.Select(item => item.TargetServerPath?.Trim())
                        .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
                    return string.IsNullOrWhiteSpace(savedPath)
                        ? "请选择目标服务器路径（本单全部明细共用同一路径）。"
                        : $"当前路径：{savedPath}（列表中未匹配到路径预设，请重新选择。）";
                }

                ServerPathSetting path = SelectedServerPath;
                return $"物理地址 {path.PhysicalPath} · 权限 {path.Permission} · 容量上限 {path.CapacityTb:0.##} TB · 所属 {path.DepartmentName}";
            }
        }

        /// <summary>当前选中检索集的基本信息摘要。</summary>
        public string SelectedSearchResultSetSummary
        {
            get
            {
                if (SelectedSearchResultSet != null)
                {
                    SearchPoolListItem pool = SelectedSearchResultSet;
                    return $"编号 {pool.ResultSetNo} · 名称 {pool.Name} · 明细 {pool.ItemCount} 条 · 状态 {pool.StatusDisplay} · 创建 {pool.CreatedAt.ToString("yyyy-MM-dd HH:mm")} · 更新 {pool.UpdatedAtDisplay}";
                }

                if (SourceResultSetId.HasValue && SourceResultSetId.Value > 0)
                {
                    int id = SourceResultSetId.Value;
                    string no = string.IsNullOrWhiteSpace(SourceResultSetNo) ? id.ToString() : SourceResultSetNo.Trim();
                    return $"已关联检索集 {no}（当前列表中未找到，可能已删除或不可见）。";
                }

                return "请从下方列表选择申请人的电子检索集。";
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

        public SystemAttachment? SelectedAttachment
        {
            get => _selectedAttachment;
            set => SetProperty(ref _selectedAttachment, value);
        }

        public RelayCommand RefreshApplicantSearchResultSetsCommand { get; }
        public RelayCommand AddExternalItemCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand DeleteAttachmentCommand { get; }
        public RelayCommand ViewAttachmentCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand FillDefaultMaterialPathCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                if (_record.Id > 0)
                {
                    var latest = await _service.GetInboundByIdAsync(_record.Id);
                    if (latest != null)
                    {
                        _record = latest;
                    }
                }
                else if (string.IsNullOrWhiteSpace(_record.InboundNo))
                {
                    _record.InboundNo = await _service.GenerateNextInboundNoAsync();
                }

                LoadProjectYears();
                LoadInternalDepartments();
                BindFromRecord();
                await TryAutoFillDefaultApprovalInfoAsync();
                await LoadConfidentialLevelOptionsAsync();
                await InitializeElectronicMediaEditorAsync();
                if (IsArchivedSource)
                {
                    await LoadApplicantSearchResultSetsAsync();
                    SyncSelectedSearchResultSetFromRecord();
                }

                LoadApplicantServerPathOptions();
                SyncSelectedServerPathFromItems();
                await RebuildItemRowsAsync();
                await InitializeReturnHardDiskSectionsAsync();
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
            InboundNo = _record.InboundNo;
            _suppressSourceKindSideEffects = true;
            _suppressProvideUnitDefault = true;
            try
            {
                SourceKind = NetworkTransferDomainValues.NormalizeSourceKind(_record.SourceKind);
                BindProvideUnitFromRecord();
            }
            finally
            {
                _suppressSourceKindSideEffects = false;
                _suppressProvideUnitDefault = false;
            }

            BindProjectSelectionFromRecord();
            Reason = _record.Reason;
            Remark = _record.Remark;
            SourceResultSetId = _record.SourceResultSetId;
            SourceResultSetNo = _record.SourceResultSetNo;
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
            _hasProofMaterialSelected = ArchiveRegisterDomainValues.HasProofMaterial(_record.ProofMaterialNote);
            if (!_hasProofMaterialSelected
                && string.IsNullOrWhiteSpace(_record.ProofMaterialNote))
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
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditForm));
            OnPropertyChanged(nameof(CanEditItemConfidentialLevel));
            OnPropertyChanged(nameof(IsSourceKindEditable));
            SyncElectronicMediaEditorEditState();
            OnPropertyChanged(nameof(ShowExternalElectronicMediaSection));
            OnPropertyChanged(nameof(ShowArchivedDataSourceSection));
            OnPropertyChanged(nameof(CurrentRecord));
            OnPropertyChanged(nameof(BusinessChainProgressDisplay));
            OnPropertyChanged(nameof(ShowExternalItemToolbar));
            OnPropertyChanged(nameof(IsInboundItemGridReadOnly));
            OnPropertyChanged(nameof(CanEditApprovalPaths));
            OnPropertyChanged(nameof(CanEditItemPaths));
            OnPropertyChanged(nameof(CanEditServerPath));
            OnPropertyChanged(nameof(IsItemPathReadOnly));
            OnPropertyChanged(nameof(IsArchivedSource));
            OnPropertyChanged(nameof(IsExternalOfflineInternalSource));
            OnPropertyChanged(nameof(IsExternalOfflineExternalSource));
            OnPropertyChanged(nameof(ShowProvideUnitDepartmentCombo));
            OnPropertyChanged(nameof(ShowProvideUnitExternalTextBox));
            OnPropertyChanged(nameof(ShowProvideUnitReadOnlyText));
            OnPropertyChanged(nameof(ProvideUnitDisplay));
            OnPropertyChanged(nameof(CanPrintApplication));
            NotifyProofMaterialStateChanged();
            BindReturnHardDiskFromRecord();
            EnsureProvideUnitDefaultAfterBind();
            NotifyWorkflowCommandStateChanged();
        }

        private void EnsureProvideUnitDefaultAfterBind()
        {
            if (!CanEditForm)
            {
                return;
            }

            if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(SourceKind)
                && string.IsNullOrWhiteSpace(ProvideUnit))
            {
                ProvideUnit = NetworkTransferDomainValues.InboundProvideUnitArchiveRoom;
                return;
            }

            if (NetworkTransferDomainValues.IsExternalOfflineInternalSource(SourceKind))
            {
                ApplyDefaultProvideUnitForInternalOffline(onlyWhenEmpty: true);
            }
        }

        /// <summary>状态变化后刷新审批/交接/附件等命令与控件可用性。</summary>
        private void NotifyWorkflowCommandStateChanged()
        {
            OnPropertyChanged(nameof(CanSubmit));
            UpdateApprovalUiState();
        }

        private async Task TryAutoFillDefaultApprovalInfoAsync()
        {
            if (_mode != NetworkTransferWorkspaceMode.Approval
                || (_record.Status != NetworkInboundRecord.StatusSubmitted
                    && _record.Status != NetworkInboundRecord.StatusApproved))
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
                await _archiveRegisterService.ApplyDefaultInboundApprovalInfoAsync(_record, user);
                SyncApprovalFieldsFromRecord();
            }
            catch
            {
                // 自动回填失败不阻断页面打开，用户仍可手工录入审批信息。
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

        private void NotifyProofMaterialStateChanged()
        {
            OnPropertyChanged(nameof(HasProofMaterial));
            OnPropertyChanged(nameof(ProofMaterialName));
            RefreshUploadCategoryOptions();
        }

        /// <summary>按是否附有证明材料刷新附件分类下拉（证明材料居第二、其他附件居末）。</summary>
        private void RefreshUploadCategoryOptions()
        {
            string previous = UploadCategory;
            UploadCategoryOptions.Clear();
            foreach (string option in NetworkTransferDomainValues.BuildInboundAttachmentCategoryOptions(HasProofMaterial))
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

        private void ApplyProofMaterialNoteToDraft(NetworkInboundRecord draft)
        {
            if (HasProofMaterial)
            {
                draft.ProofMaterialNote = ProofMaterialName.Trim();
            }
            else
            {
                draft.ProofMaterialNote = ArchiveRegisterDomainValues.ProofMaterialNoneText;
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

        private async Task LoadApplicantSearchResultSetsAsync()
        {
            if (!IsArchivedSource)
            {
                ApplicantSearchResultSets.Clear();
                return;
            }

            User applicant = ResolveApplicantUser();
            try
            {
                var pools = await _searchService.ListSearchPoolsAsync(
                    new SearchPoolListCriteria
                    {
                        MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                        Keyword = string.Empty,
                        Status = ArchiveSearchResultSetStatus.Confirmed,
                        OnlyMine = true
                    },
                    applicant,
                    isArchiveAdmin: false);

                ApplicantSearchResultSets.Clear();
                foreach (SearchPoolListItem pool in pools.Where(item => item.ItemCount > 0))
                {
                    ApplicantSearchResultSets.Add(pool);
                }

                SyncSelectedSearchResultSetFromRecord();
            }
            catch (Exception ex)
            {
                ApplicantSearchResultSets.Clear();
                _dialogService.ShowError($"加载电子检索集失败：{ex.Message}");
            }
        }

        private void SyncSelectedSearchResultSetFromRecord()
        {
            _suppressSearchResultSetSelection = true;
            try
            {
                if (SourceResultSetId.HasValue && SourceResultSetId.Value > 0)
                {
                    int id = SourceResultSetId.Value;
                    SelectedSearchResultSet = ApplicantSearchResultSets.FirstOrDefault(pool => pool.Id == id);
                }
                else
                {
                    SelectedSearchResultSet = null;
                }

                OnPropertyChanged(nameof(SelectedSearchResultSetSummary));
            }
            finally
            {
                _suppressSearchResultSetSelection = false;
            }
        }

        private async Task ApplySelectedSearchResultSetAsync()
        {
            if (SelectedSearchResultSet == null)
            {
                SourceResultSetId = null;
                SourceResultSetNo = string.Empty;
                OnPropertyChanged(nameof(SelectedSearchResultSetSummary));
                return;
            }

            SourceResultSetId = SelectedSearchResultSet.Id;
            SourceResultSetNo = SelectedSearchResultSet.ResultSetNo;
            OnPropertyChanged(nameof(SelectedSearchResultSetSummary));

            if (CanEditHeader && IsArchivedSource)
            {
                await ImportSelectedSearchResultSetItemsAsync();
            }
        }

        private async Task ImportSelectedSearchResultSetItemsAsync()
        {
            if (!SourceResultSetId.HasValue || SourceResultSetId.Value <= 0)
            {
                return;
            }

            try
            {
                var imported = await _service.BuildInboundItemsFromElectronicSearchAsync(SourceResultSetId.Value, null);
                Items.Clear();
                foreach (var item in imported)
                {
                    Items.Add(item);
                }

                ApplySharedServerPathToItems();
                await RebuildItemRowsAsync();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void ClearArchivedSourceState()
        {
            _suppressSearchResultSetSelection = true;
            try
            {
                SelectedSearchResultSet = null;
                SourceResultSetId = null;
                SourceResultSetNo = string.Empty;
                ApplicantSearchResultSets.Clear();
                Items.Clear();
                ItemRows.Clear();
                OnPropertyChanged(nameof(SelectedSearchResultSetSummary));
            }
            finally
            {
                _suppressSearchResultSetSelection = false;
            }
        }

        private void LoadApplicantServerPathOptions()
        {
            string department;
            if (_mode == NetworkTransferWorkspaceMode.Approval
                && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser))
            {
                // 审批办理按资料室资料管理员可见路径加载，便于跨部门申请单补录。
                department = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty;
            }
            else
            {
                department = string.IsNullOrWhiteSpace(ApplicantDept)
                    ? ResolveApplicantUser().Department?.Trim() ?? string.Empty
                    : ApplicantDept.Trim();
            }

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
                string savedPath = IsExternalSource
                    ? _record.TargetServerPath?.Trim() ?? string.Empty
                    : Items.Select(item => item.TargetServerPath?.Trim())
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

            foreach (NetworkInboundItem item in Items)
            {
                item.TargetServerPath = pathName;
            }
        }

        private void FillDefaultMaterialPath()
        {
            if (SelectedServerPath == null)
            {
                _dialogService.ShowError("请先选择服务器路径。");
                return;
            }

            if (string.IsNullOrWhiteSpace(Year) || string.Equals(Year.Trim(), "全部", StringComparison.Ordinal))
            {
                _dialogService.ShowError("请先选择具体年度。");
                return;
            }

            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                _dialogService.ShowError("请先选择项目。");
                return;
            }

            if (string.IsNullOrWhiteSpace(_record.MaterialName))
            {
                _dialogService.ShowError("请先填写资料名称。");
                return;
            }

            if (!NetworkInboundMaterialPathSupport.TryExtractInboundNoSuffix(InboundNo, out _))
            {
                _dialogService.ShowError("入网单号尚未生成，无法填入默认资料路径。");
                return;
            }

            _record.MaterialPath = NetworkInboundMaterialPathSupport.BuildDefaultMaterialPath(
                SelectedServerPath,
                ApplicantDept,
                Year,
                ProjectName,
                _record.MaterialName,
                InboundNo,
                SourceKind);
            OnPropertyChanged(nameof(CurrentRecord));
        }

        private async Task RebuildItemRowsAsync()
        {
            List<int> factIds = Items
                .Where(item => item.SourceFilingFactId is > 0)
                .Select(item => item.SourceFilingFactId!.Value)
                .Distinct()
                .ToList();
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> filingFacts =
                await _service.GetFilingFactsByIdsAsync(factIds);
            IReadOnlyDictionary<int, FiledArchiveSearchHit> hits = IsArchivedSource
                ? await _searchService.GetSearchHitsByFilingFactIdsAsync(factIds)
                : new Dictionary<int, FiledArchiveSearchHit>();
            IReadOnlyDictionary<int, string> currentLocations = IsArchivedSource
                ? await _searchService.GetCurrentStorageLocationsByFilingFactIdsAsync(factIds)
                : new Dictionary<int, string>();
            IReadOnlyDictionary<int, YearlyArchiveSearchResultSetItem> resultSetItems =
                await LoadLinkedSearchResultSetItemsAsync();

            bool isExternalSource = IsExternalSource;
            ItemRows.Clear();
            foreach (NetworkInboundItem item in Items.OrderBy(row => row.SortOrder).ThenBy(row => row.Id))
            {
                YearlyArchiveFilingFact? filingFact = null;
                if (item.SourceFilingFactId is int factId && factId > 0)
                {
                    filingFacts.TryGetValue(factId, out filingFact);
                }

                if (IsArchivedSource)
                {
                    NetworkInboundItemDisplaySupport.ApplyFilingFactSnapshot(item, filingFact);
                }

                SearchPoolItemRow? poolItem = TryBuildArchivedSearchPoolItemRow(
                    item,
                    hits,
                    currentLocations,
                    resultSetItems);
                ItemRows.Add(NetworkInboundItemRowViewModel.Create(item, filingFacts, isExternalSource, poolItem));
            }
        }

        /// <summary>
        /// 读取入网单已关联电子检索集的明细（不校验检索池创建人权限）。
        /// </summary>
        private async Task<IReadOnlyDictionary<int, YearlyArchiveSearchResultSetItem>> LoadLinkedSearchResultSetItemsAsync()
        {
            if (!IsArchivedSource || !SourceResultSetId.HasValue || SourceResultSetId.Value <= 0)
            {
                return new Dictionary<int, YearlyArchiveSearchResultSetItem>();
            }

            YearlyArchiveSearchResultSet? resultSet = await _searchService.GetSearchPoolByIdAsync(SourceResultSetId.Value);
            if (resultSet?.Items == null || resultSet.Items.Count == 0)
            {
                return new Dictionary<int, YearlyArchiveSearchResultSetItem>();
            }

            return resultSet.Items
                .Where(item => item.Id > 0)
                .GroupBy(item => item.Id)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private static SearchPoolItemRow? TryBuildArchivedSearchPoolItemRow(
            NetworkInboundItem inboundItem,
            IReadOnlyDictionary<int, FiledArchiveSearchHit> hits,
            IReadOnlyDictionary<int, string> currentLocations,
            IReadOnlyDictionary<int, YearlyArchiveSearchResultSetItem> resultSetItems)
        {
            if (inboundItem.SourceFilingFactId is not int factId || factId <= 0)
            {
                return null;
            }

            if (!hits.TryGetValue(factId, out FiledArchiveSearchHit? hit) || hit == null)
            {
                return null;
            }

            YearlyArchiveSearchResultSetItem? resultSetItem = null;
            if (inboundItem.SourceResultSetItemId is int resultSetItemId && resultSetItemId > 0)
            {
                resultSetItems.TryGetValue(resultSetItemId, out resultSetItem);
            }

            if (resultSetItem == null)
            {
                resultSetItem = resultSetItems.Values.FirstOrDefault(item => item.FilingFactId == factId);
            }

            resultSetItem ??= CreateSnapshotResultSetItem(inboundItem, hit);
            currentLocations.TryGetValue(factId, out string? currentLocation);
            return new SearchPoolItemRow(
                resultSetItem,
                hit,
                currentLocation ?? hit.CurrentStorageLocation);
        }

        private static YearlyArchiveSearchResultSetItem CreateSnapshotResultSetItem(
            NetworkInboundItem inboundItem,
            FiledArchiveSearchHit hit)
        {
            return new YearlyArchiveSearchResultSetItem
            {
                Id = inboundItem.SourceResultSetItemId ?? 0,
                FilingFactId = hit.FilingFactId,
                FormNo = FirstNonEmpty(inboundItem.FormNo, hit.FormNo),
                MaterialName = FirstNonEmpty(inboundItem.MaterialName, hit.MaterialName),
                ItemName = FirstNonEmpty(inboundItem.ItemName, hit.ItemName),
                ContainerCode = FirstNonEmpty(inboundItem.ContainerCode, hit.ContainerCode),
                StorageLocation = FirstNonEmpty(inboundItem.StorageLocation, hit.StorageLocation),
                SelectionScopeKind = ArchiveSearchSelectionScopeKind.WholeMediaItem,
                RequestedCopyCount = 1,
                LifecycleStatus = hit.LifecycleStatus,
                BorrowHintLevel = hit.BorrowHintLevel,
                BorrowHintText = hit.BorrowHintText
            };
        }

        private static string FirstNonEmpty(string? first, string? second)
        {
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first.Trim();
            }

            return string.IsNullOrWhiteSpace(second) ? string.Empty : second.Trim();
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

        private static NetworkInboundItem CloneItem(NetworkInboundItem item) => new()
        {
            Id = item.Id,
            SortOrder = item.SortOrder,
            AssetKind = item.AssetKind,
            AssetName = item.AssetName,
            ConfidentialLevel = item.ConfidentialLevel,
            DataSizeText = item.DataSizeText,
            TargetServerPath = item.TargetServerPath,
            SourceKind = item.SourceKind,
            SourceResultSetItemId = item.SourceResultSetItemId,
            SourceFilingFactId = item.SourceFilingFactId,
            FormNo = item.FormNo,
            MaterialName = item.MaterialName,
            ItemName = item.ItemName,
            ContainerCode = item.ContainerCode,
            StorageLocation = item.StorageLocation,
            OnNetAssetId = item.OnNetAssetId,
            CreatedAt = item.CreatedAt
        };

        private NetworkInboundRecord BuildDraftSnapshot()
        {
            var draft = new NetworkInboundRecord
            {
                Id = _record.Id,
                InboundNo = InboundNo,
                SourceKind = NetworkTransferDomainValues.NormalizeSourceKind(SourceKind),
                ProvideUnit = ResolveDraftProvideUnit(),
                ProjectName = ProjectName,
                Year = Year,
                MaterialName = _record.MaterialName,
                MaterialPath = _record.MaterialPath?.Trim() ?? string.Empty,
                Reason = _record.Reason,
                OtherRequests = _record.OtherRequests,
                Remark = _record.Remark,
                SourceResultSetId = SourceResultSetId,
                SourceResultSetNo = SourceResultSetNo
            };
            if (IsExternalSource)
            {
                draft.MediaEntries = BuildExternalMediaEntriesForSave();
                draft.TargetServerPath = SelectedServerPath?.PathName?.Trim() ?? string.Empty;
            }

            ApplyProofMaterialNoteToDraft(draft);
            draft.TargetServerPath = SelectedServerPath?.PathName?.Trim()
                ?? draft.TargetServerPath?.Trim()
                ?? string.Empty;
            return draft;
        }

        private async Task<NetworkInboundRecord> BuildDraftSnapshotAsync()
        {
            NetworkInboundRecord draft = BuildDraftSnapshot();
            await ApplyReturnHardDiskToDraftAsync(draft);
            return draft;
        }

        private void AddExternalItem()
        {
            Items.Add(new NetworkInboundItem
            {
                SortOrder = Items.Count + 1,
                AssetKind = NetworkTransferDomainValues.AssetKindJobData,
                AssetName = string.Empty,
                TargetServerPath = SelectedServerPath?.PathName?.Trim() ?? string.Empty,
                SourceKind = SourceKind,
                CreatedAt = DateTime.Now
            });
            ApplySharedServerPathToItems();
            _ = RebuildItemRowsAsync();
            CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveItemRow(NetworkInboundItemRowViewModel? row)
        {
            if (row == null)
            {
                return;
            }

            Items.Remove(row.Item);
            int sort = 1;
            foreach (NetworkInboundItem item in Items)
            {
                item.SortOrder = sort++;
            }

            _ = RebuildItemRowsAsync();
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task SaveDraftAsync()
        {
            try
            {
                ApplySharedServerPathToItems();
                var user = RequireUser();
                var draft = await BuildDraftSnapshotAsync();
                IReadOnlyList<NetworkInboundItem> itemsToSave = IsExternalSource
                    ? Array.Empty<NetworkInboundItem>()
                    : Items.ToList();
                _record = _record.Id > 0
                    ? await _service.UpdateInboundDraftAsync(draft, itemsToSave, user)
                    : await _service.CreateInboundDraftAsync(draft, itemsToSave, user);
                _hasCommittedChanges = true;
                BindFromRecord();
                await InitializeElectronicMediaEditorAsync();
                LoadApplicantServerPathOptions();
                SyncSelectedServerPathFromItems();
                await RebuildItemRowsAsync();
                await InitializeReturnHardDiskSectionsAsync();
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
                NetworkInboundRecord draft = await BuildDraftSnapshotAsync();
                if (HasProofMaterial && string.IsNullOrWhiteSpace(ProofMaterialName))
                {
                    _dialogService.ShowError("已选择附有证明材料，请填写证明材料名称。");
                    return;
                }

                IReadOnlyList<string> validationErrors = NetworkInboundApplicationValidationSupport.ValidateForSubmit(
                    draft,
                    IsExternalSource ? Array.Empty<NetworkInboundItem>() : Items.ToList(),
                    IsExternalSource ? draft.MediaEntries?.ToList() : null);
                if (validationErrors.Count > 0)
                {
                    _dialogService.ShowError(string.Join(Environment.NewLine, validationErrors));
                    return;
                }

                await SaveDraftAsync();
                await _service.SubmitInboundAsync(_record.Id, RequireUser());
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
                bool blankApproval = _record.Status < NetworkInboundRecord.StatusApproved;
                var data = await _service.BuildInboundPrintDataAsync(_record.Id, blankApproval);
                var document = NetworkInboundPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };

                await _service.RecordInboundPrintAsync(_record.Id);
                previewWindow.ShowDialog();
                await ReloadRecordAsync();
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
                // 审批前允许资料室补录服务器路径与明细密级
                if (_record.Status == NetworkInboundRecord.StatusSubmitted)
                {
                    if (IsExternalSource)
                    {
                        IReadOnlyList<string> confidentialErrors =
                            NetworkInboundApprovalAmendmentSupport.ValidateExternalMediaConfidentialLevels(
                                BuildExternalMediaEntriesForSave());
                        if (confidentialErrors.Count > 0)
                        {
                            _dialogService.ShowError(string.Join(Environment.NewLine, confidentialErrors));
                            return;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(_record.MaterialPath))
                    {
                        _dialogService.ShowError("请填写资料路径。");
                        return;
                    }

                    await PersistItemPathsIfNeededAsync();
                    await PersistReturnHardDiskSlotsIfNeededAsync();
                }

                await _service.ApproveInboundAsync(new NetworkInboundRecord
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
                await PersistItemPathsIfNeededAsync();
                await PersistReturnHardDiskSlotsIfNeededAsync();
                await _service.ConfirmInboundHandoverAsync(new NetworkInboundRecord
                {
                    Id = _record.Id,
                    Deliverer = Deliverer,
                    DeliverDate = DeliverDate,
                    Administrator = Administrator,
                    AdminDate = AdminDate
                }, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("入网交接已确认。");
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
                await _service.CompleteInboundAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("入网单已办结，已写入在网台账。");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PersistItemPathsIfNeededAsync()
        {
            if (_record.Status is NetworkInboundRecord.StatusSubmitted or NetworkInboundRecord.StatusApproved)
            {
                ApplySharedServerPathToItems();
                string? targetServerPath = SelectedServerPath?.PathName?.Trim();
                await _service.UpdateInboundItemPathsAsync(
                    _record.Id,
                    Items.ToList(),
                    RequireUser(),
                    targetServerPath,
                    _record.MaterialPath?.Trim(),
                    IsExternalSource ? BuildExternalMediaEntriesForSave() : null);
                // 不在此处 Reload：审批/交接区默认信息仅内存回填，提前 Reload 会冲掉界面值；
                // 调用方在主操作成功后再 Reload。
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
                    NetworkTransferDomainValues.InboundAttachmentBusinessType,
                    _record.Id,
                    _record.InboundNo,
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
                await ReloadRecordAsync();
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
                if (!AttachmentsMeetMandatoryRequirements)
                {
                    _dialogService.ShowMessage("附件已删除，当前不满足办结要求：\n\n" + AttachmentRequirementHint);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ReloadAttachmentsAsync()
        {
            Attachments.Clear();
            if (string.IsNullOrWhiteSpace(_record.InboundNo))
            {
                RedistributeAttachmentsByCategory();
                await RefreshAttachmentRequirementsAsync();
                return;
            }

            var list = await _service.GetAttachmentsAsync(
                NetworkTransferDomainValues.InboundAttachmentBusinessType,
                _record.InboundNo);
            foreach (var item in list)
            {
                Attachments.Add(item);
            }

            RedistributeAttachmentsByCategory();
            await RefreshAttachmentRequirementsAsync();
        }

        private async Task ReloadRecordAsync()
        {
            var latest = await _service.GetInboundByIdAsync(_record.Id);
            if (latest != null)
            {
                _record = latest;
                BindFromRecord();
                await TryAutoFillDefaultApprovalInfoAsync();
                if (IsArchivedSource)
                {
                    await LoadApplicantSearchResultSetsAsync();
                    SyncSelectedSearchResultSetFromRecord();
                }

                LoadApplicantServerPathOptions();
                SyncSelectedServerPathFromItems();
                await RebuildItemRowsAsync();
                await InitializeReturnHardDiskSectionsAsync();
                await ReloadAttachmentsAsync();
            }
        }

        private User RequireUser() =>
            _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
    }
}
