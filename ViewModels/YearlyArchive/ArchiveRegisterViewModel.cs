using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Models.Shared;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace DocMgr.ViewModels.YearlyArchive
{
    // 使用 partial 关键字
    public partial class ArchiveRegisterViewModel : ViewModelBase
    {
        private bool _isInitialized;
        private bool _hasCommittedChanges;
        private bool _isDialogMode;
        private bool _suppressProvideUnitDefault;

        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IProjectService _projectService;
        private readonly IUserService _userService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IElectronicMediaContentScanService _electronicMediaContentScanService;
        private readonly IArchiveRegisterWordExportService _archiveRegisterWordExportService;

        #region Properties

        private YearlyArchiveRegisterRecord? _currentRecord;
        public YearlyArchiveRegisterRecord? CurrentRecord
        {
            get => _currentRecord;
            set { if (SetProperty(ref _currentRecord, value)) OnCurrentRecordChanged(); }
        }

        private bool _isLoadingRecord;
        public bool IsLoadingRecord
        {
            get => _isLoadingRecord;
            private set { if (SetProperty(ref _isLoadingRecord, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        // Collections
        public ObservableCollection<string> ProjectYears { get; } = new();
        public ObservableCollection<ProjectInfo> Projects { get; } = new();
        public ObservableCollection<Department> Departments { get; } = new();
        public ObservableCollection<string> SourceTypeOptions { get; } = new();
        public ObservableCollection<string> UserBorrowedHardDiskCodes { get; } = new();
        public ObservableCollection<string> ArchivePurposeOptions { get; } = new();
        public ObservableCollection<string> SimulatedMediaKindOptions { get; } = new();
        public ObservableCollection<string> DataItemTypeOptions { get; } = new();
        public ObservableCollection<string> ProofItemTypeOptions { get; } = new();
        public ObservableCollection<string> DataElectronicMediaTypeOptions { get; } = new();
        public ObservableCollection<string> DataSimulatedMediaTypeOptions { get; } = new();
        public ObservableCollection<string> ProofSimulatedMediaTypeOptions { get; } = new();
        public ObservableCollection<string> DataElectronicDispositionOptions { get; } = new();
        public ObservableCollection<string> DataSimulatedDispositionOptions { get; } = new();
        public ObservableCollection<string> ElectronicMaterialCategoryOptions { get; } = new();
        public ObservableCollection<string> ElectronicDataOrganizationFormOptions { get; } = new();
        public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();
        public ObservableCollection<string> ProdOpinionOptions { get; } = new();
        public ObservableCollection<string> RndOpinionOptions { get; } = new();
        public ObservableCollection<string> DeputyOpinionOptions { get; } = new();

        public ObservableCollection<MediaEntryViewModel> MediaEntries { get; } = new();
        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        // Selections
        private string? _selectedProjectYear;
        public string? SelectedProjectYear
        {
            get => _selectedProjectYear;
            set { if (SetProperty(ref _selectedProjectYear, value)) LoadProjects(); }
        }

        private ProjectInfo? _selectedProject;
        public ProjectInfo? SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (SetProperty(ref _selectedProject, value))
                {
                    if (CurrentRecord != null && value != null)
                    {
                        CurrentRecord.ProjectId = value.Id;
                        CurrentRecord.ProjectName = value.ProjectName;
                    }
                }
            }
        }

        private string _selectedSourceType = string.Empty;
        public string SelectedSourceType
        {
            get => _selectedSourceType;
            set
            {
                if (SetProperty(ref _selectedSourceType, value))
                {
                    IsExternalSource = _archiveRegisterService.IsExternalSourceType(value);
                    if (CurrentRecord != null)
                    {
                        CurrentRecord.SourceType = value;
                        if (!_suppressProvideUnitDefault && !IsExternalSource)
                        {
                            ApplyDefaultProvideUnitForInternalSource(onlyWhenEmpty: false);
                        }
                    }
                }
            }
        }

        private string _selectedArchivePurpose = string.Empty;
        public string SelectedArchivePurpose
        {
            get => _selectedArchivePurpose;
            set
            {
                if (SetProperty(ref _selectedArchivePurpose, value))
                {
                    if (CurrentRecord != null)
                    {
                        CurrentRecord.ArchivePurpose = value ?? string.Empty;
                    }

                    OnPropertyChanged(nameof(IsArchivePurposeOtherSelected));
                }
            }
        }

        public bool IsArchivePurposeOtherSelected =>
            string.Equals(SelectedArchivePurpose?.Trim(), "其他", StringComparison.Ordinal);

        private bool _isExternalSource;
        public bool IsExternalSource
        {
            get => _isExternalSource;
            private set => SetProperty(ref _isExternalSource, value);
        }

        private bool _isBorrowedHardDisk;
        /// <summary>
        /// 由介质明细同步的第一条电子介质行的「借出硬盘」勾选状态（界面已迁至各行，仅供内部投影与兼容）。
        /// </summary>
        public bool IsBorrowedHardDisk
        {
            get => _isBorrowedHardDisk;
            set
            {
                if (SetProperty(ref _isBorrowedHardDisk, value))
                {
                    OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
                }
            }
        }

        private string _borrowedHardDiskCode = string.Empty;
        /// <summary>
        /// 由介质明细同步的第一条电子介质行的介质编号（界面已迁至各行）。
        /// </summary>
        public string BorrowedHardDiskCode
        {
            get => _borrowedHardDiskCode;
            set => SetProperty(ref _borrowedHardDiskCode, value);
        }

        public bool IsBorrowedHardDiskRegistrationVisible =>
            string.Equals(SelectedElectronicMediaType?.Trim(), ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SelectedElectronicDisposition?.Trim(), ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 任一条「硬盘 + 留存」电子介质勾选借出时需填写介质编号。
        /// </summary>
        public bool IsBorrowedHardDiskCodeRequired =>
            IsBorrowedHardDiskRegistrationVisible
            && MediaEntries.Any(m =>
                IsDataElectronic(m) && m.IsRetainedHardDiskScenario && m.IsBorrowedHardDisk);

        // Permissions
        private bool _canEditForm;
        public bool CanEditForm { get => _canEditForm; set => SetProperty(ref _canEditForm, value); }
        private bool _canApproveProd;
        public bool CanApproveProd { get => _canApproveProd; set => SetProperty(ref _canApproveProd, value); }
        private bool _canApproveRnd;
        public bool CanApproveRnd { get => _canApproveRnd; set => SetProperty(ref _canApproveRnd, value); }
        private bool _canApproveDeputy;
        public bool CanApproveDeputy { get => _canApproveDeputy; set => SetProperty(ref _canApproveDeputy, value); }
        private bool _canConfirmDeliver;
        public bool CanConfirmDeliver { get => _canConfirmDeliver; set => SetProperty(ref _canConfirmDeliver, value); }
        private bool _canUpload;
        public bool CanUpload { get => _canUpload; set => SetProperty(ref _canUpload, value); }
        private bool _canEditItemConfidentialLevel;
        public bool CanEditItemConfidentialLevel
        {
            get => _canEditItemConfidentialLevel;
            set => SetProperty(ref _canEditItemConfidentialLevel, value);
        }

        private bool _attachmentsMeetMandatoryRequirements;
        public bool AttachmentsMeetMandatoryRequirements
        {
            get => _attachmentsMeetMandatoryRequirements;
            private set => SetProperty(ref _attachmentsMeetMandatoryRequirements, value);
        }

        private string _attachmentRequirementHint = string.Empty;
        public string AttachmentRequirementHint
        {
            get => _attachmentRequirementHint;
            private set => SetProperty(ref _attachmentRequirementHint, value);
        }

        public bool CanApprovePass => ResolveApprovalButtonState().CanApprovePass;
        public bool CanUploadSignedAttachment => ResolveApprovalButtonState().CanUploadSignedAttachment;
        public bool CanCompleteApproval => ResolveApprovalButtonState().CanConfirmComplete;
        public bool CanPrintHandoverSheet => ResolveApprovalButtonState().CanPrintApprovalForm;
        public string ApproveHintText => CanApprovePass
            ? "请先根据线下审批结果核实资料子项密级，再执行审批通过。"
            : "仅「已提交」状态可执行审批通过。";
        public string UploadHintText => CanUploadSignedAttachment
            ? "请上传登记申请单、资料照片各1个。"
            : "请先执行「审批通过」。";
        public string CompleteHintText => CanCompleteApproval
            ? "确认办结后，可打印交接单。"
            : "请先审批通过后再确认办结。";
        public string PrintHintText => CanPrintHandoverSheet
            ? "流程已办结，可打印交接单。"
            : "请先完成「确认办结」。";

        // Commands & Views
        public ListCollectionView DataElectronicMediaView { get; private set; } = null!;
        public ListCollectionView DataSimulatedMediaView { get; private set; } = null!;
        public ListCollectionView ProofSimulatedMediaView { get; private set; } = null!;

        public RelayCommand? GenerateIdCommand { get; }
        public RelayCommand SaveApprovalCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitApplicationsCommand { get; }
        public RelayCommand PrintApplicationsCommand { get; }
        public RelayCommand PrintApprovalCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand CloseCommand { get; }

        public RelayCommand AddDataElectronicMediaEntryCommand { get; }
        public RelayCommand AddDataSimulatedMediaEntryCommand { get; }
        public RelayCommand AddProofSimulatedMediaEntryCommand { get; }

        public RelayCommand<MediaEntryViewModel> AddMediaItemCommand { get; }
        public RelayCommand<MediaEntryViewModel> RemoveMediaEntryCommand { get; }
        public RelayCommand<MediaItemViewModel> RemoveMediaItemCommand { get; }
        public RelayCommand<MediaItemViewModel> PickFolderAndScanElectronicContentCommand { get; }
        public RelayCommand<MediaItemViewModel> PickFilesAndScanElectronicContentCommand { get; }
        public RelayCommand<MediaItemViewModel> RescanElectronicContentCommand { get; }
        public RelayCommand<MediaItemViewModel> ClearElectronicContentCommand { get; }
        public RelayCommand<MediaItemViewModel> ViewElectronicContentEntriesCommand { get; }

        public RelayCommand UploadAttachmentCommand { get; }
        public RelayCommand<SystemAttachment> DeleteAttachmentCommand { get; }
        public RelayCommand<SystemAttachment> ViewAttachmentCommand { get; }
        public RelayCommand FillDefaultApprovalInfoCommand { get; }

        #endregion

        public ArchiveRegisterViewModel(
            IArchiveRegisterService archiveRegisterService,
            IProjectService projectService,
            IUserService userService,
            IUserContextService userContextService,
            IDialogService dialogService,
            IHardDiskMediaService hardDiskMediaService,
            IElectronicMediaContentScanService electronicMediaContentScanService,
            IArchiveRegisterWordExportService archiveRegisterWordExportService)
        {
            _archiveRegisterService = archiveRegisterService ?? throw new ArgumentNullException(nameof(archiveRegisterService));
            _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _hardDiskMediaService = hardDiskMediaService ?? throw new ArgumentNullException(nameof(hardDiskMediaService));
            _electronicMediaContentScanService = electronicMediaContentScanService ?? throw new ArgumentNullException(nameof(electronicMediaContentScanService));
            _archiveRegisterWordExportService = archiveRegisterWordExportService ?? throw new ArgumentNullException(nameof(archiveRegisterWordExportService));

            SaveApprovalCommand = new RelayCommand(async _ => await SaveApprovalAsync(), _ => CanApprovePass);
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanExecuteApplicationSaveDraft());
            SubmitApplicationsCommand = new RelayCommand(async _ => await SubmitApplication(), _ => CanExecuteApplicationSubmit());



            PrintApplicationsCommand = new RelayCommand(_ => PrintApplication(), _ => CanExecuteApplicationPrint());
            PrintApprovalCommand = new RelayCommand(_ => PrintApprovalPage(), _ => CanPrintHandoverSheet);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanCompleteApproval);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            AddDataElectronicMediaEntryCommand = new RelayCommand(_ => AddDataElectronicMediaEntry(), _ => CanEditForm);
            AddDataSimulatedMediaEntryCommand = new RelayCommand(_ => AddDataSimulatedMediaEntry(), _ => CanEditForm);
            AddProofSimulatedMediaEntryCommand = new RelayCommand(_ => AddProofSimulatedMediaEntry(), _ => CanEditForm);
            AddMediaItemCommand = new RelayCommand<MediaEntryViewModel>(m => AddMediaItem(m), _ => CanEditForm);
            RemoveMediaEntryCommand = new RelayCommand<MediaEntryViewModel>(m => RemoveMediaEntry(m), _ => CanEditForm);
            RemoveMediaItemCommand = new RelayCommand<MediaItemViewModel>(m => RemoveMediaItem(m), _ => CanEditForm);
            PickFolderAndScanElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                async item => await PickFolderAndScanElectronicContentAsync(item),
                item => CanEditForm && item != null && item.IsDirectoryOrganizationForm);
            PickFilesAndScanElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                async item => await PickFilesAndScanElectronicContentAsync(item),
                item => CanEditForm && item != null && item.IsFileOrganizationForm);
            RescanElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                async item => await RescanElectronicContentAsync(item),
                item => CanEditForm && item != null && CanRescanElectronicContent(item));
            ClearElectronicContentCommand = new RelayCommand<MediaItemViewModel>(
                item => ClearElectronicContent(item),
                item => CanEditForm && item != null && item.HasScannedEntries);
            ViewElectronicContentEntriesCommand = new RelayCommand<MediaItemViewModel>(
                item => ViewElectronicContentEntries(item),
                item => item != null && item.HasScannedEntries && CanViewElectronicContentEntries());

            UploadAttachmentCommand = new RelayCommand(async _ => await UploadSignedAttachmentAsync(), _ => CanUploadSignedAttachment);
            DeleteAttachmentCommand = new RelayCommand<SystemAttachment>(async a => await DeleteAttachment(a), _ => CanUpload);
            ViewAttachmentCommand = new RelayCommand<SystemAttachment>(a => ViewAttachment(a));
            FillDefaultApprovalInfoCommand = new RelayCommand(async _ => await FillDefaultApprovalInfoAsync(), _ => CanApproveProd && CurrentRecord != null);
            InitializeMediaViews();
            LoadDepartments();
            LoadProjectYears();
        }

        public async Task InitializeAsync(int? initialRecordId = null)
        {
            if (_isInitialized) return;

            await LoadDomainOptionCollectionsAsync();
            await RefreshUserBorrowedHardDiskCodesAsync();

            if (initialRecordId.HasValue)
            {
                await LoadRecordDetailAsync(initialRecordId.Value);
            }
            else if (CurrentRecord == null && IsApplicantUser())
            {
                await ResetRecordWithAutoFormNoAsync();
            }

            _isInitialized = true;
            HasCommittedChanges = false;
        }

        public bool IsApplicantUser()
        {
            return _archiveRegisterService.IsApplicantUser(_userContextService.CurrentUser);
        }

        private bool CanViewElectronicContentEntries()
        {
            return CanEditForm
                || _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);
        }

        private ArchiveRegisterWorkspaceMode _workspaceMode = ArchiveRegisterWorkspaceMode.Application;

        /// <summary>当前导航入口对应的工作台模式（申请 / 审批）。</summary>
        public ArchiveRegisterWorkspaceMode WorkspaceMode => _workspaceMode;

        /// <summary>是否显示第 4 步审批、附件与底部审批保存区（审批工作台显示）。</summary>
        public bool ShowApprovalWorkflowPanel =>
            _workspaceMode == ArchiveRegisterWorkspaceMode.Approval;

        /// <summary>是否显示「保存草稿」「提交申请」（申请工作台显示）。</summary>
        public bool ShowApplicationSubmitActions =>
            _workspaceMode == ArchiveRegisterWorkspaceMode.Application;

        /// <summary>顶部流程说明文案，随工作台切换。</summary>
        public string RegisterWorkspaceBannerText => _workspaceMode switch
        {
            ArchiveRegisterWorkspaceMode.Application =>
                "请填写资料与介质明细，可使用「保存草稿」「提交申请」。提交后由资料室在「资料登记（审批）」办理。",
            ArchiveRegisterWorkspaceMode.Approval =>
                "请先根据线下审批结果核实并登记各资料子项密级，再填写审批流程，按“审批通过→上传签字件→确认办结→打印交接单”办理。",
            _ => string.Empty
        };

        /// <summary>窗体标题栏与页眉标题（流程名 · 单号 · 状态）。</summary>
        public string WindowTitle
        {
            get
            {
                string formNo = string.IsNullOrWhiteSpace(CurrentRecord?.FormNo)
                    ? "待编单"
                    : CurrentRecord.FormNo.Trim();
                string status = CurrentRecord?.StatusStr ?? "未提交";
                return $"资料立档 · {formNo} · {status}";
            }
        }

        public string DialogTitle => _workspaceMode switch
        {
            ArchiveRegisterWorkspaceMode.Application => "资料登记（申请）",
            ArchiveRegisterWorkspaceMode.Approval => "资料登记（审批）",
            _ => "资料登记"
        };

        public bool HasCommittedChanges
        {
            get => _hasCommittedChanges;
            private set => SetProperty(ref _hasCommittedChanges, value);
        }

        public bool IsDialogMode
        {
            get => _isDialogMode;
            private set => SetProperty(ref _isDialogMode, value);
        }

        public bool ShowEmbeddedActionButtons => !IsDialogMode;

        public bool ShowFooterActionBar => IsDialogMode;

        public event Action<bool?>? RequestClose;

        public void SetDialogMode(bool isDialogMode)
        {
            if (IsDialogMode == isDialogMode)
            {
                return;
            }

            IsDialogMode = isDialogMode;
            OnPropertyChanged(nameof(ShowEmbeddedActionButtons));
            OnPropertyChanged(nameof(ShowFooterActionBar));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanExecuteApplicationSaveDraft()
        {
            if (_workspaceMode != ArchiveRegisterWorkspaceMode.Application)
            {
                return false;
            }

            return ApplicationFormActionSupport.Resolve(
                CurrentRecord?.Id ?? 0,
                CurrentRecord?.IsDraft ?? true).CanSaveDraft;
        }

        private bool CanExecuteApplicationSubmit()
        {
            if (_workspaceMode != ArchiveRegisterWorkspaceMode.Application)
            {
                return false;
            }

            return ApplicationFormActionSupport.Resolve(
                CurrentRecord?.Id ?? 0,
                CurrentRecord?.IsDraft ?? true).CanSubmitApplication;
        }

        private bool CanExecuteApplicationPrint()
        {
            if (_workspaceMode != ArchiveRegisterWorkspaceMode.Application)
            {
                return false;
            }

            return ApplicationFormActionSupport.Resolve(
                CurrentRecord?.Id ?? 0,
                CurrentRecord?.IsDraft ?? true).CanPrintApplication;
        }

        /// <summary>由承载页在 InitializeAsync 之前设置工作台。</summary>
        public void SetWorkspaceMode(ArchiveRegisterWorkspaceMode mode)
        {
            if (_workspaceMode == mode)
            {
                return;
            }

            _workspaceMode = mode;
            OnPropertyChanged(nameof(WorkspaceMode));
            OnPropertyChanged(nameof(ShowApprovalWorkflowPanel));
            OnPropertyChanged(nameof(ShowApplicationSubmitActions));
            OnPropertyChanged(nameof(RegisterWorkspaceBannerText));
            OnPropertyChanged(nameof(DialogTitle));
            OnPropertyChanged(nameof(WindowTitle));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}