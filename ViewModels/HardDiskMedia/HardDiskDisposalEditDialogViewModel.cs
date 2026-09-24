using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘离库处置办理弹窗 ViewModel（草稿编辑 + 审批办结工作台）。
    /// </summary>
    public sealed partial class HardDiskDisposalEditDialogViewModel : ViewModelBase
    {
        private const string AllFilterText = "全部";

        private readonly IHardDiskDisposalService _disposalService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IUserService _userService;
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private readonly List<HardDiskDisposalCandidateViewModel> _mediaPool = new();
        private HardDiskDisposalRecord _record;
        private bool _hasCommittedChanges;
        private bool _isApplyingFilters;
        private bool _suppressBatchMethodApply;
        private string _disposalNo = string.Empty;
        private string _batchDispositionMethod = HardDiskDisposalDomainValues.MethodDirectDestroy;
        private string _otherRemark = string.Empty;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _approvalOpinion = "同意";
        private string _uploadCategory = HardDiskDisposalDomainValues.AttachmentCategorySignedForm;
        private SystemAttachment? _selectedAttachment;
        private string _filterKeyword = string.Empty;
        private string _filterMediaStatus = AllFilterText;
        private string _filterInterfaceType = AllFilterText;
        private string _filterCapacity = AllFilterText;
        private DateTime? _filterFactoryDateFrom;
        private DateTime? _filterFactoryDateTo;
        private string _defaultDeptHead = string.Empty;
        private string _defaultArchiveRoomHead = string.Empty;
        private string _defaultProductionHead = string.Empty;
        private string _defaultArchiveDeputyPresident = string.Empty;
        private string _defaultProductionVicePresident = string.Empty;
        private string _deptHead = string.Empty;
        private DateTime? _deptHeadDate;
        private string _archiveRoomHead = string.Empty;
        private DateTime? _archiveRoomHeadDate;
        private string _productionHead = string.Empty;
        private DateTime? _productionHeadDate;
        private string _archiveDeputyPresident = string.Empty;
        private DateTime? _archiveDeputyPresidentDate;
        private string _productionVicePresident = string.Empty;
        private DateTime? _productionVicePresidentDate;
        private bool _suppressReviewSignerPersist;
        private bool _enableDeptHead;
        private bool _enableArchiveRoomHead = true;
        private bool _enableProductionHead;
        private bool _enableArchiveDeputyPresident = true;
        private bool _enableProductionVicePresident;

        public HardDiskDisposalEditDialogViewModel(
            IHardDiskDisposalService disposalService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IUserService userService,
            IApprovalWorkflowService approvalWorkflowService,
            HardDiskDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _disposalService = disposalService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _userService = userService;
            _approvalWorkflowService = approvalWorkflowService;
            _record = record;

            MoveToDisposalCommand = new RelayCommand(_ => MoveToDisposal(), _ => CanEditHeader && AvailableDisks.Any(item => item.IsSelected));
            MoveToAvailableCommand = new RelayCommand(_ => MoveToAvailable(), _ => CanEditHeader && Items.Any(item => item.IsSelected));
            ApplyDispositionMethodCommand = new RelayCommand(
                _ => ApplyDispositionMethodToSelected(BatchDispositionMethod),
                _ => CanEditHeader
                    && Items.Any(item => item.IsSelected)
                    && HardDiskDisposalDomainValues.IsValidDispositionMethod(BatchDispositionMethod));
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters(), _ => CanEditHeader);
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmUploadCommand = new RelayCommand(async _ => await ConfirmUploadAsync(), _ => CanConfirmUpload);
            UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
            CaptureFromDocumentCameraCommand = new RelayCommand(
                async _ => await CaptureFromDocumentCameraAsync(),
                _ => CanUploadAttachment);
            UploadSignedFormAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskDisposalDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            CaptureSignedFormAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskDisposalDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            UploadDiskPhotoAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskDisposalDomainValues.AttachmentCategoryDiskPhoto),
                _ => CanUploadMandatoryAttachment);
            CaptureDiskPhotoAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskDisposalDomainValues.AttachmentCategoryDiskPhoto),
                _ => CanUploadMandatoryAttachment);
            UploadOtherAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskDisposalDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            CaptureOtherAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskDisposalDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            DeleteAttachmentCommand = new RelayCommand(
                async item => await DeleteAttachmentAsync(item as SystemAttachment),
                item => item is SystemAttachment && CanUploadMandatoryAttachment);
            ViewAttachmentCommand = new RelayCommand(async item => await ViewAttachmentAsync(item as SystemAttachment), item => item is SystemAttachment);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdraw);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"硬盘离库处置 · {(string.IsNullOrWhiteSpace(DisposalNo) ? "待编单" : DisposalNo)} · {StatusDisplay}";

        public string StatusDisplay => HardDiskDisposalDomainValues.ToStatusDisplay(_record.Status);

        public string BannerText =>
            "仅「在库(空盘)」「在库(损坏)」「在库(盘失)」「在库(拟销)」可离库处置。流程：保存草稿 → 提交 → 打印签批单并线下签字 → 审批通过 → 确认可上传 → 分区上传签批单/硬盘照片 → 确认办结。";

        public ObservableCollection<string> DispositionMethodOptions { get; } = new(HardDiskDisposalDomainValues.DispositionMethodOptions);

        public ObservableCollection<string> UploadCategoryOptions { get; } = new(HardDiskDisposalDomainValues.AttachmentCategoryOptions);

        public ObservableCollection<HardDiskDisposalCandidateViewModel> AvailableDisks { get; } = new();

        public ObservableCollection<HardDiskDisposalItemViewModel> Items { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<SystemAttachment> SignedFormAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> DiskPhotoAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> OtherAttachments { get; } = new();

        public ObservableCollection<string> InterfaceTypeFilterOptions { get; } = new();

        public ObservableCollection<string> CapacityFilterOptions { get; } = new();

        public ObservableCollection<string> MediaStatusFilterOptions { get; } = new();

        public string AvailableDisksTitle => $"可选库内盘（{AvailableDisks.Count}）";

        public string DisposalDisksTitle => $"待处置硬盘（{Items.Count}）";

        public string FilterKeyword
        {
            get => _filterKeyword;
            set
            {
                if (SetProperty(ref _filterKeyword, value))
                {
                    RefreshAvailableDisks();
                }
            }
        }

        public string FilterMediaStatus
        {
            get => _filterMediaStatus;
            set
            {
                if (SetProperty(ref _filterMediaStatus, value))
                {
                    RefreshAvailableDisks();
                }
            }
        }

        public string FilterInterfaceType
        {
            get => _filterInterfaceType;
            set
            {
                if (SetProperty(ref _filterInterfaceType, value))
                {
                    RefreshAvailableDisks();
                }
            }
        }

        public string FilterCapacity
        {
            get => _filterCapacity;
            set
            {
                if (SetProperty(ref _filterCapacity, value))
                {
                    RefreshAvailableDisks();
                }
            }
        }

        public DateTime? FilterFactoryDateFrom
        {
            get => _filterFactoryDateFrom;
            set
            {
                if (SetProperty(ref _filterFactoryDateFrom, value))
                {
                    RefreshAvailableDisks();
                }
            }
        }

        public DateTime? FilterFactoryDateTo
        {
            get => _filterFactoryDateTo;
            set
            {
                if (SetProperty(ref _filterFactoryDateTo, value))
                {
                    RefreshAvailableDisks();
                }
            }
        }

        public string DisposalNo
        {
            get => _disposalNo;
            private set
            {
                if (SetProperty(ref _disposalNo, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        public string ApplicantName => _record.ApplicantName;

        public string ApplicantDept => _record.ApplicantDept;

        /// <summary>批量赋值用的处置方式（勾选待处置硬盘后选择，点「赋值」或切换选项写入明细列）。</summary>
        public string BatchDispositionMethod
        {
            get => _batchDispositionMethod;
            set
            {
                if (!SetProperty(ref _batchDispositionMethod, value))
                {
                    return;
                }

                RaiseCommandStates();
                if (!_suppressBatchMethodApply)
                {
                    ApplyDispositionMethodToSelected(value);
                }
            }
        }

        public bool RequiresOtherRemark =>
            Items.Any(item => HardDiskDisposalDomainValues.RequiresOtherRemark(item.DispositionMethod));

        public string OtherRemark
        {
            get => _otherRemark;
            set => SetProperty(ref _otherRemark, value);
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

        public string ApprovalOpinion
        {
            get => _approvalOpinion;
            set => SetProperty(ref _approvalOpinion, value);
        }

        public string DefaultDeptHeadDisplay => EmptyAsDash(_defaultDeptHead);
        public string DefaultArchiveRoomHeadDisplay => EmptyAsDash(_defaultArchiveRoomHead);
        public string DefaultProductionHeadDisplay => EmptyAsDash(_defaultProductionHead);
        public string DefaultArchiveDeputyPresidentDisplay => EmptyAsDash(_defaultArchiveDeputyPresident);
        public string DefaultProductionVicePresidentDisplay => EmptyAsDash(_defaultProductionVicePresident);
        public bool ShowDeptHeadApprover => _enableDeptHead;
        public bool ShowArchiveRoomHeadApprover => _enableArchiveRoomHead;
        public bool ShowProductionHeadApprover => _enableProductionHead;
        public bool ShowArchiveDeputyPresidentApprover => _enableArchiveDeputyPresident;
        public bool ShowProductionVicePresidentApprover => _enableProductionVicePresident;
        public bool ShowDeptHead => ShowDeptHeadApprover;
        public bool ShowArchiveRoomHead => ShowArchiveRoomHeadApprover;
        public bool ShowProductionHead => ShowProductionHeadApprover;
        public bool ShowArchiveDeputyPresident => ShowArchiveDeputyPresidentApprover;
        public bool ShowProductionVicePresident => ShowProductionVicePresidentApprover;
        public bool ShowReviewApproverSection =>
            ShowDeptHeadApprover || ShowArchiveRoomHeadApprover || ShowProductionHeadApprover;
        public bool ShowApproveApproverSection =>
            ShowArchiveDeputyPresidentApprover || ShowProductionVicePresidentApprover;
        public bool ShowReviewSignerSection => ShowReviewApproverSection;
        public bool ShowApproveSignerSection => ShowApproveApproverSection;

        /// <summary>签字卡可编辑：待审批，或已审批/已确认可上传且可操作。</summary>
        public bool CanEditSigners =>
            CanApprove
            || (_record.Status is HardDiskDisposalRecord.StatusApproved
                    or HardDiskDisposalRecord.StatusSignedUploaded
                && CanOperate);

        public DateTime? SignatureDateMin =>
            ApprovalSignatureDateSupport.ResolveMinDate(_record.FirstPrintedAt, _record.LastPrintedAt, _record.PrintCount);

        public string DeptHead
        {
            get => _deptHead;
            set
            {
                if (!SetProperty(ref _deptHead, value ?? string.Empty))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? DeptHeadDate
        {
            get => _deptHeadDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _deptHeadDate, clamped))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public string ArchiveRoomHead
        {
            get => _archiveRoomHead;
            set
            {
                if (!SetProperty(ref _archiveRoomHead, value ?? string.Empty))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? ArchiveRoomHeadDate
        {
            get => _archiveRoomHeadDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _archiveRoomHeadDate, clamped))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public string ProductionHead
        {
            get => _productionHead;
            set
            {
                if (!SetProperty(ref _productionHead, value ?? string.Empty))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? ProductionHeadDate
        {
            get => _productionHeadDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _productionHeadDate, clamped))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public string ArchiveDeputyPresident
        {
            get => _archiveDeputyPresident;
            set
            {
                if (!SetProperty(ref _archiveDeputyPresident, value ?? string.Empty))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? ArchiveDeputyPresidentDate
        {
            get => _archiveDeputyPresidentDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _archiveDeputyPresidentDate, clamped))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public string ProductionVicePresident
        {
            get => _productionVicePresident;
            set
            {
                if (!SetProperty(ref _productionVicePresident, value ?? string.Empty))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? ProductionVicePresidentDate
        {
            get => _productionVicePresidentDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _productionVicePresidentDate, clamped))
                {
                    return;
                }

                _ = PersistReviewSignersAsync();
            }
        }

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

        public bool CanOperate =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanEditHeader =>
            CanOperate && _record.Status == HardDiskDisposalRecord.StatusDraft;

        public bool CanSubmit => CanEditHeader;

        public bool CanApprove =>
            CanOperate && _record.Status == HardDiskDisposalRecord.StatusSubmitted;

        public bool CanConfirmUpload =>
            CanOperate && _record.Status == HardDiskDisposalRecord.StatusApproved;

        /// <summary>办结后资料管理员可增补「其他附件」。</summary>
        public bool CanSupplementOtherAttachments =>
            ApprovalWorkflowButtonSupport.CanSupplementOtherAttachments(
                _record.Status == HardDiskDisposalRecord.StatusCompleted,
                CanOperate);

        public bool CanUploadAttachment =>
            CanOperate
            && (_record.Status is HardDiskDisposalRecord.StatusSignedUploaded or HardDiskDisposalRecord.StatusApproved
                || CanSupplementOtherAttachments);

        /// <summary>签批单/硬盘照片：仅确认可上传后、办结前可传。</summary>
        public bool CanUploadMandatoryAttachment =>
            CanUploadAttachment && !CanSupplementOtherAttachments;

        /// <summary>其他附件：确认可上传后及办结后均可增补。</summary>
        public bool CanUploadOtherAttachment => CanUploadAttachment;

        public bool RequiresDiskPhotoAttachment => true;

        public string UploadAttachmentHintText => CanSupplementOtherAttachments
            ? "办结后仅可增补「其他附件」；不可删除已有附件。"
            : "请在「确认可上传」后分区上传签批单与硬盘照片；办结前仍可继续补传。";

        public string ApproveHintText => CanApprove
            ? "请按线下签批结果执行审批通过；通过后点击「确认可上传」。"
            : "仅「已提交」状态可审批通过。";

        public string ConfirmUploadHintText => CanConfirmUpload
            ? "确认后可分区上传签批单与硬盘照片。"
            : "请先执行「审批通过」。";

        public string CompleteHintText => CanComplete
            ? "确认办结后更新硬盘台账并释放档口。"
            : "请先上传签批单与硬盘照片后再确认办结。";

        public bool CanComplete =>
            CanOperate && _record.Status == HardDiskDisposalRecord.StatusSignedUploaded;

        public bool CanPrint =>
            _record.Id > 0
            && _record.Status is not HardDiskDisposalRecord.StatusDraft
                and not HardDiskDisposalRecord.StatusWithdrawn
                and not HardDiskDisposalRecord.StatusForceWithdrawn;

        public bool CanWithdraw =>
            CanOperate
            && _record.Id > 0
            && _record.Status is not HardDiskDisposalRecord.StatusCompleted
                and not HardDiskDisposalRecord.StatusWithdrawn
                and not HardDiskDisposalRecord.StatusForceWithdrawn;

        public RelayCommand MoveToDisposalCommand { get; }
        public RelayCommand MoveToAvailableCommand { get; }
        public RelayCommand ApplyDispositionMethodCommand { get; }
        public RelayCommand ClearFiltersCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmUploadCommand { get; }
        public RelayCommand UploadAttachmentCommand { get; }
        public RelayCommand CaptureFromDocumentCameraCommand { get; }
        public RelayCommand UploadSignedFormAttachmentCommand { get; }
        public RelayCommand CaptureSignedFormAttachmentCommand { get; }
        public RelayCommand UploadDiskPhotoAttachmentCommand { get; }
        public RelayCommand CaptureDiskPhotoAttachmentCommand { get; }
        public RelayCommand UploadOtherAttachmentCommand { get; }
        public RelayCommand CaptureOtherAttachmentCommand { get; }
        public RelayCommand DeleteAttachmentCommand { get; }
        public RelayCommand ViewAttachmentCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand PrintCommand { get; }
        public RelayCommand WithdrawCommand { get; }
        public RelayCommand CloseCommand { get; }

        private async Task InitializeAsync()
        {
            ApplyRecordToForm(_record);
            if (_record.Id <= 0 && string.IsNullOrWhiteSpace(_record.DisposalNo))
            {
                DisposalNo = await _disposalService.GenerateNextDisposalNoAsync();
                _record.DisposalNo = DisposalNo;
            }
            else
            {
                DisposalNo = _record.DisposalNo?.Trim() ?? string.Empty;
            }

            await ReloadMediaPoolAsync();
            if (_record.Id > 0)
            {
                await ReloadAttachmentsAsync();
            }

            await ReloadDefaultApproversAsync();
            RaiseCommandStates();
        }

        private async Task ReloadDefaultApproversAsync()
        {
            var users = _userService.GetAllUsers();
            var chain = await _approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalWorkflowBusinessTypes.HardDiskDisposal,
                    ApplicantDept = _record.ApplicantDept,
                    FieldValues = ApprovalChainApplySupport.BuildHardDiskDisposalFieldValues(
                        _record,
                        item => item.DisposalReason,
                        item => item.DispositionMethod)
                },
                users);
            var approvers = ArchiveDisposalDefaultApproverSupport.FromChain(chain);
            _defaultDeptHead = PreferNonEmpty(_record.DeptHead, approvers.DeptHead);
            _defaultArchiveRoomHead = PreferNonEmpty(_record.ArchiveRoomHead, approvers.ArchiveRoomHead);
            _defaultProductionHead = PreferNonEmpty(_record.ProductionHead, approvers.ProductionHead);
            _defaultArchiveDeputyPresident = PreferNonEmpty(_record.ArchiveDeputyPresident, approvers.ArchiveDeputyPresident);
            _defaultProductionVicePresident = PreferNonEmpty(_record.ProductionVicePresident, approvers.ProductionVicePresident);
            _enableDeptHead = approvers.EnableDeptHead;
            _enableArchiveRoomHead = approvers.EnableArchiveRoomHead;
            _enableProductionHead = approvers.EnableProductionHead;
            _enableArchiveDeputyPresident = approvers.EnableArchiveDeputyPresident;
            _enableProductionVicePresident = approvers.EnableProductionVicePresident;

            _suppressReviewSignerPersist = true;
            try
            {
                _deptHead = PreferNonEmpty(_record.DeptHead, _defaultDeptHead);
                _deptHeadDate = ApprovalSignatureDateSupport.Clamp(_record.DeptHeadDate, SignatureDateMin);
                _archiveRoomHead = PreferNonEmpty(_record.ArchiveRoomHead, _defaultArchiveRoomHead);
                _archiveRoomHeadDate = ApprovalSignatureDateSupport.Clamp(_record.ArchiveRoomHeadDate, SignatureDateMin);
                _productionHead = PreferNonEmpty(_record.ProductionHead, _defaultProductionHead);
                _productionHeadDate = ApprovalSignatureDateSupport.Clamp(_record.ProductionHeadDate, SignatureDateMin);
                _archiveDeputyPresident = PreferNonEmpty(_record.ArchiveDeputyPresident, _defaultArchiveDeputyPresident);
                _archiveDeputyPresidentDate = ApprovalSignatureDateSupport.Clamp(_record.ArchiveDeputyPresidentDate, SignatureDateMin);
                _productionVicePresident = PreferNonEmpty(_record.ProductionVicePresident, _defaultProductionVicePresident);
                _productionVicePresidentDate = ApprovalSignatureDateSupport.Clamp(_record.ProductionVicePresidentDate, SignatureDateMin);
            }
            finally
            {
                _suppressReviewSignerPersist = false;
            }

            OnPropertyChanged(nameof(DefaultDeptHeadDisplay));
            OnPropertyChanged(nameof(DefaultArchiveRoomHeadDisplay));
            OnPropertyChanged(nameof(DefaultProductionHeadDisplay));
            OnPropertyChanged(nameof(DefaultArchiveDeputyPresidentDisplay));
            OnPropertyChanged(nameof(DefaultProductionVicePresidentDisplay));
            OnPropertyChanged(nameof(ShowDeptHeadApprover));
            OnPropertyChanged(nameof(ShowArchiveRoomHeadApprover));
            OnPropertyChanged(nameof(ShowProductionHeadApprover));
            OnPropertyChanged(nameof(ShowArchiveDeputyPresidentApprover));
            OnPropertyChanged(nameof(ShowProductionVicePresidentApprover));
            OnPropertyChanged(nameof(ShowDeptHead));
            OnPropertyChanged(nameof(ShowArchiveRoomHead));
            OnPropertyChanged(nameof(ShowProductionHead));
            OnPropertyChanged(nameof(ShowArchiveDeputyPresident));
            OnPropertyChanged(nameof(ShowProductionVicePresident));
            OnPropertyChanged(nameof(ShowReviewApproverSection));
            OnPropertyChanged(nameof(ShowApproveApproverSection));
            OnPropertyChanged(nameof(ShowReviewSignerSection));
            OnPropertyChanged(nameof(ShowApproveSignerSection));
            OnPropertyChanged(nameof(DeptHead));
            OnPropertyChanged(nameof(DeptHeadDate));
            OnPropertyChanged(nameof(ArchiveRoomHead));
            OnPropertyChanged(nameof(ArchiveRoomHeadDate));
            OnPropertyChanged(nameof(ProductionHead));
            OnPropertyChanged(nameof(ProductionHeadDate));
            OnPropertyChanged(nameof(ArchiveDeputyPresident));
            OnPropertyChanged(nameof(ArchiveDeputyPresidentDate));
            OnPropertyChanged(nameof(ProductionVicePresident));
            OnPropertyChanged(nameof(ProductionVicePresidentDate));
            OnPropertyChanged(nameof(CanEditSigners));
            OnPropertyChanged(nameof(SignatureDateMin));
        }

        private static string EmptyAsDash(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

        private static string PreferNonEmpty(string? primary, string? fallback) =>
            !string.IsNullOrWhiteSpace(primary) ? primary.Trim() : (fallback?.Trim() ?? string.Empty);

        private void ApplyRecordToForm(HardDiskDisposalRecord record)
        {
            _record = record;
            _suppressBatchMethodApply = true;
            try
            {
                BatchDispositionMethod = HardDiskDisposalDomainValues.MethodDirectDestroy;
            }
            finally
            {
                _suppressBatchMethodApply = false;
            }

            OtherRemark = record.OtherRemark ?? string.Empty;
            Reason = record.Reason ?? string.Empty;
            Remark = record.Remark ?? string.Empty;
            ApprovalOpinion = string.IsNullOrWhiteSpace(record.ApprovalOpinion) ? "同意" : record.ApprovalOpinion;

            RebuildItemsFromRecord(record);
            RefreshAvailableDisks();
            DisposalNo = record.DisposalNo?.Trim() ?? DisposalNo;

            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(ApplicantName));
            OnPropertyChanged(nameof(ApplicantDept));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmUpload));
            OnPropertyChanged(nameof(CanSupplementOtherAttachments));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanUploadMandatoryAttachment));
            OnPropertyChanged(nameof(CanUploadOtherAttachment));
            OnPropertyChanged(nameof(UploadAttachmentHintText));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(ConfirmUploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanWithdraw));
            OnPropertyChanged(nameof(RequiresOtherRemark));
            OnPropertyChanged(nameof(DisposalDisksTitle));
            OnPropertyChanged(nameof(CanEditSigners));
            OnPropertyChanged(nameof(SignatureDateMin));
            NotifyShellAliasPropertiesChanged();

            if (CanSupplementOtherAttachments)
            {
                UploadCategory = HardDiskDisposalDomainValues.AttachmentCategoryOther;
            }
        }

        private void RebuildItemsFromRecord(
            HardDiskDisposalRecord record,
            IReadOnlyDictionary<int, string>? lostBeforeLocations = null)
        {
            Items.Clear();
            int sort = 1;
            foreach (var item in record.Items.OrderBy(detail => detail.SortOrder))
            {
                var candidate = _mediaPool.FirstOrDefault(pool => pool.MediumId == item.MediumId);
                string? locationOverride = null;
                if (string.IsNullOrWhiteSpace(item.BeforeStorageLocation)
                    && (string.Equals(item.BeforeMediaStatus?.Trim(), HardDiskMedium.StatusInStockLost, StringComparison.Ordinal)
                        || string.Equals(item.BeforeMediaStatus?.Trim(), HardDiskMedium.StatusInStockScrap, StringComparison.Ordinal))
                    && lostBeforeLocations != null
                    && lostBeforeLocations.TryGetValue(item.MediumId, out string? recovered)
                    && !string.IsNullOrWhiteSpace(recovered))
                {
                    locationOverride = recovered;
                }

                Items.Add(candidate != null
                    ? HardDiskDisposalItemViewModel.FromCandidate(
                        candidate,
                        sort++,
                        item.DisposalReason,
                        item.DispositionMethod,
                        record.DispositionMethod,
                        locationOverride ?? item.BeforeStorageLocation)
                    : new HardDiskDisposalItemViewModel(item, record.DispositionMethod, locationOverride));
            }

            OnPropertyChanged(nameof(RequiresOtherRemark));
        }

        private async Task ReloadMediaPoolAsync()
        {
            var media = await _disposalService.GetSelectableMediaAsync(
                _record.Id > 0 ? _record.Id : null);
            IReadOnlyDictionary<int, string> locationMap =
                await _disposalService.ResolveBeforeStorageLocationsAsync(media);

            _mediaPool.Clear();
            foreach (var medium in media.OrderBy(item => item.DiskCode, StringComparer.Ordinal))
            {
                locationMap.TryGetValue(medium.Id, out string? resolvedLocation);
                _mediaPool.Add(HardDiskDisposalCandidateViewModel.FromMedium(medium, resolvedLocation));
            }

            var itemIdsNeedingLostLocation = _record.Items
                .Where(item => string.IsNullOrWhiteSpace(item.BeforeStorageLocation)
                               && (string.Equals(
                                       item.BeforeMediaStatus?.Trim(),
                                       HardDiskMedium.StatusInStockLost,
                                       StringComparison.Ordinal)
                                   || string.Equals(
                                       item.BeforeMediaStatus?.Trim(),
                                       HardDiskMedium.StatusInStockScrap,
                                       StringComparison.Ordinal)))
                .Select(item => item.MediumId)
                .Distinct()
                .ToList();
            IReadOnlyDictionary<int, string> lostBeforeLocations = itemIdsNeedingLostLocation.Count == 0
                ? new Dictionary<int, string>()
                : await _disposalService.GetInventoryLostBeforeLocationsAsync(itemIdsNeedingLostLocation);

            RebuildFilterOptions();
            RebuildItemsFromRecord(_record, lostBeforeLocations);
            RefreshAvailableDisks();
        }

        private void RebuildFilterOptions()
        {
            _isApplyingFilters = true;
            try
            {
                string previousInterface = FilterInterfaceType;
                string previousCapacity = FilterCapacity;
                string previousStatus = FilterMediaStatus;

                MediaStatusFilterOptions.Clear();
                MediaStatusFilterOptions.Add(AllFilterText);
                foreach (var value in HardDiskDisposalDomainValues.SelectableMediaStatusOptions)
                {
                    MediaStatusFilterOptions.Add(value);
                }

                InterfaceTypeFilterOptions.Clear();
                InterfaceTypeFilterOptions.Add(AllFilterText);
                foreach (var value in _mediaPool
                    .Select(item => item.InterfaceType)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal))
                {
                    InterfaceTypeFilterOptions.Add(value);
                }

                CapacityFilterOptions.Clear();
                CapacityFilterOptions.Add(AllFilterText);
                foreach (var value in _mediaPool
                    .Select(item => item.Capacity)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal))
                {
                    CapacityFilterOptions.Add(value);
                }

                _filterMediaStatus = MediaStatusFilterOptions.Contains(previousStatus)
                    ? previousStatus
                    : AllFilterText;
                _filterInterfaceType = InterfaceTypeFilterOptions.Contains(previousInterface)
                    ? previousInterface
                    : AllFilterText;
                _filterCapacity = CapacityFilterOptions.Contains(previousCapacity)
                    ? previousCapacity
                    : AllFilterText;
                OnPropertyChanged(nameof(FilterMediaStatus));
                OnPropertyChanged(nameof(FilterInterfaceType));
                OnPropertyChanged(nameof(FilterCapacity));
            }
            finally
            {
                _isApplyingFilters = false;
            }
        }

        private void RefreshAvailableDisks()
        {
            if (_isApplyingFilters)
            {
                return;
            }

            HashSet<int> selectedIds = Items.Select(item => item.MediumId).ToHashSet();
            IEnumerable<HardDiskDisposalCandidateViewModel> query = _mediaPool
                .Where(item => !selectedIds.Contains(item.MediumId));

            if (!string.IsNullOrWhiteSpace(FilterKeyword))
            {
                string keyword = FilterKeyword.Trim();
                query = query.Where(item =>
                    item.DiskCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.SerialNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.Brand.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.StorageLocation.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(FilterMediaStatus, AllFilterText, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(FilterMediaStatus))
            {
                query = query.Where(item =>
                    string.Equals(item.MediaStatus, FilterMediaStatus, StringComparison.Ordinal));
            }

            if (!string.Equals(FilterInterfaceType, AllFilterText, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(FilterInterfaceType))
            {
                query = query.Where(item =>
                    string.Equals(item.InterfaceType, FilterInterfaceType, StringComparison.Ordinal));
            }

            if (!string.Equals(FilterCapacity, AllFilterText, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(FilterCapacity))
            {
                query = query.Where(item =>
                    string.Equals(item.Capacity, FilterCapacity, StringComparison.Ordinal));
            }

            if (FilterFactoryDateFrom.HasValue)
            {
                DateTime from = FilterFactoryDateFrom.Value.Date;
                query = query.Where(item => item.FactoryDate.HasValue && item.FactoryDate.Value.Date >= from);
            }

            if (FilterFactoryDateTo.HasValue)
            {
                DateTime to = FilterFactoryDateTo.Value.Date;
                query = query.Where(item => item.FactoryDate.HasValue && item.FactoryDate.Value.Date <= to);
            }

            AvailableDisks.Clear();
            foreach (var item in query.OrderBy(disk => disk.DiskCode, StringComparer.Ordinal))
            {
                item.IsSelected = false;
                AvailableDisks.Add(item);
            }

            OnPropertyChanged(nameof(AvailableDisksTitle));
            OnPropertyChanged(nameof(DisposalDisksTitle));
            RaiseCommandStates();
        }

        private void ClearFilters()
        {
            _isApplyingFilters = true;
            try
            {
                _filterKeyword = string.Empty;
                _filterMediaStatus = AllFilterText;
                _filterInterfaceType = AllFilterText;
                _filterCapacity = AllFilterText;
                _filterFactoryDateFrom = null;
                _filterFactoryDateTo = null;
                OnPropertyChanged(nameof(FilterKeyword));
                OnPropertyChanged(nameof(FilterMediaStatus));
                OnPropertyChanged(nameof(FilterInterfaceType));
                OnPropertyChanged(nameof(FilterCapacity));
                OnPropertyChanged(nameof(FilterFactoryDateFrom));
                OnPropertyChanged(nameof(FilterFactoryDateTo));
            }
            finally
            {
                _isApplyingFilters = false;
            }

            RefreshAvailableDisks();
        }

        private void MoveToDisposal()
        {
            var selected = AvailableDisks.Where(item => item.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            int sort = Items.Count + 1;
            foreach (var candidate in selected.OrderBy(item => item.DiskCode, StringComparer.Ordinal))
            {
                if (Items.Any(item => item.MediumId == candidate.MediumId))
                {
                    continue;
                }

                Items.Add(HardDiskDisposalItemViewModel.FromCandidate(candidate, sort++));
            }

            RenumberItems();
            RefreshAvailableDisks();
            OnPropertyChanged(nameof(RequiresOtherRemark));
        }

        private void MoveToAvailable()
        {
            var selected = Items.Where(item => item.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var item in selected)
            {
                Items.Remove(item);
            }

            RenumberItems();
            RefreshAvailableDisks();
            OnPropertyChanged(nameof(RequiresOtherRemark));
        }

        private void ApplyDispositionMethodToSelected(string? method)
        {
            if (!CanEditHeader)
            {
                return;
            }

            string normalized = method?.Trim() ?? string.Empty;
            if (!HardDiskDisposalDomainValues.IsValidDispositionMethod(normalized))
            {
                return;
            }

            var selected = Items.Where(item => item.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var distinctStatuses = selected
                .Select(item => item.BeforeMediaStatus?.Trim() ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (distinctStatuses.Count > 1)
            {
                _dialogService.ShowMessage(
                    "所选硬盘的「原状态」不一致，请按同一原状态分批勾选后再赋值。",
                    "无法赋值");
                return;
            }

            string sampleReason = selected[0].DisposalReason?.Trim() ?? string.Empty;
            string? mismatch = HardDiskDisposalDomainValues.TryGetReasonAndDispositionMethodMismatchMessage(
                sampleReason,
                normalized);
            if (!string.IsNullOrWhiteSpace(mismatch))
            {
                _dialogService.ShowMessage(mismatch, "无法赋值");
                return;
            }

            foreach (var item in selected)
            {
                item.DispositionMethod = normalized;
            }

            OnPropertyChanged(nameof(RequiresOtherRemark));
        }

        private void RenumberItems()
        {
            int sort = 1;
            foreach (var item in Items)
            {
                item.SortOrder = sort++;
                item.IsSelected = false;
            }

            OnPropertyChanged(nameof(DisposalDisksTitle));
        }

        private async Task SaveDraftAsync()
        {
            try
            {
                var user = RequireCurrentUser();
                var draft = BuildDraftPayload();
                var mediumIds = Items.Select(item => item.MediumId).ToList();

                HardDiskDisposalRecord saved = _record.Id > 0
                    ? await _disposalService.UpdateDraftAsync(draft, mediumIds, user)
                    : await _disposalService.CreateDraftAsync(draft, mediumIds, user);

                _hasCommittedChanges = true;
                ApplyRecordToForm(saved);
                await ReloadMediaPoolAsync();
                _dialogService.ShowMessage("草稿已保存。");
                RaiseCommandStates();
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
                if (_record.Id <= 0 || HasUnsavedHeaderChanges())
                {
                    await SaveDraftAsync();
                    if (_record.Id <= 0)
                    {
                        return;
                    }
                }

                if (!_dialogService.ShowConfirm("确认提交该离库处置单？提交后将锁定关联硬盘。"))
                {
                    return;
                }

                await _disposalService.SubmitAsync(_record.Id, RequireCurrentUser());
                await ReloadRecordAsync();
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("已提交，待审批。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ApproveAsync()
        {
            try
            {
                if (!_dialogService.ShowConfirm("确认审批通过该离库处置单？"))
                {
                    return;
                }

                await _disposalService.ApproveAsync(_record.Id, ApprovalOpinion, RequireCurrentUser());
                await ReloadRecordAsync();
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("审批已通过。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ConfirmUploadAsync()
        {
            try
            {
                if (!_dialogService.ShowConfirm("确认进入上传签批单阶段？请先打印签批单并完成线下签字。"))
                {
                    return;
                }

                await PersistReviewSignersAsync();
                await _disposalService.ConfirmReadyForUploadAsync(_record.Id, RequireCurrentUser());
                await ReloadRecordAsync();
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("已确认，可上传签批单与硬盘照片。");
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
                if (!_dialogService.ShowConfirm("确认办结？办结后将更新硬盘台账状态并释放档口。"))
                {
                    return;
                }

                await PersistReviewSignersAsync();
                await _disposalService.CompleteAsync(_record.Id, RequireCurrentUser());
                await ReloadRecordAsync();
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("离库处置已办结。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task WithdrawAsync()
        {
            try
            {
                if (!_dialogService.ShowConfirm("确认撤回作废该离库处置单？"))
                {
                    return;
                }

                await _disposalService.WithdrawAsync(_record.Id, null, RequireCurrentUser());
                await ReloadRecordAsync();
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("处置单已撤回作废。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PrintAsync()
        {
            try
            {
                var data = await _disposalService.BuildPrintDataAsync(_record.Id);
                var document = HardDiskDisposalPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document);
                previewWindow.ShowDialog();
                await _disposalService.RecordPrintAsync(_record.Id);
                await ReloadRecordAsync();
                _hasCommittedChanges = true;
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
                if (CanSupplementOtherAttachments)
                {
                    UploadCategory = HardDiskDisposalDomainValues.AttachmentCategoryOther;
                }
                else if (_record.Status == HardDiskDisposalRecord.StatusApproved
                    && !string.Equals(UploadCategory, HardDiskDisposalDomainValues.AttachmentCategoryOther, StringComparison.Ordinal))
                {
                    _dialogService.ShowMessage("请先点击「确认可上传」，再上传签批单或硬盘照片。");
                    return;
                }

                string? path = _dialogService.OpenFileDialog(
                    "图片与文档|*.jpg;*.jpeg;*.png;*.bmp;*.pdf;*.doc;*.docx|所有文件|*.*",
                    "选择附件");
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return;
                }

                byte[] content = await File.ReadAllBytesAsync(path);
                string fileName = Path.GetFileName(path);
                string extension = Path.GetExtension(path);
                var result = await _disposalService.UploadAttachmentAsync(
                    _record.Id,
                    UploadCategory,
                    fileName,
                    extension,
                    content.LongLength,
                    content,
                    RequireCurrentUser());

                if (!result.Ok)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                await ReloadAttachmentsAsync();
                _dialogService.ShowMessage(result.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task CaptureFromDocumentCameraAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(UploadCategory))
                {
                    _dialogService.ShowMessage("请先选择附件分类。");
                    return;
                }

                if (CanSupplementOtherAttachments)
                {
                    UploadCategory = HardDiskDisposalDomainValues.AttachmentCategoryOther;
                }
                else if (_record.Status == HardDiskDisposalRecord.StatusApproved
                    && !string.Equals(UploadCategory, HardDiskDisposalDomainValues.AttachmentCategoryOther, StringComparison.Ordinal))
                {
                    _dialogService.ShowMessage("请先点击「确认可上传」，再上传签批单或硬盘照片。");
                    return;
                }

                DocumentCameraCaptureResult? captured = DocumentCameraAttachmentCaptureSupport.Capture(_dialogService);
                if (captured == null)
                {
                    return;
                }

                string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(DisposalNo, UploadCategory, "盘离处");
                var result = await _disposalService.UploadAttachmentAsync(
                    _record.Id,
                    UploadCategory,
                    fileName,
                    ".jpg",
                    captured.JpegContent.LongLength,
                    captured.JpegContent,
                    RequireCurrentUser());

                if (!result.Ok)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                await ReloadAttachmentsAsync();
                _dialogService.ShowMessage(result.Message);
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
                if (!_dialogService.ShowConfirm($"确认删除附件【{attachment.FileName}】？"))
                {
                    return;
                }

                var result = await _disposalService.DeleteAttachmentAsync(attachment.Id, RequireCurrentUser());
                if (!result.Ok)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                await ReloadAttachmentsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ViewAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            try
            {
                var full = await _disposalService.GetAttachmentByIdAsync(attachment.Id);
                if (full == null)
                {
                    _dialogService.ShowError("附件不存在。");
                    return;
                }

                _dialogService.ShowSystemAttachmentView(full);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ReloadRecordAsync()
        {
            var latest = await _disposalService.GetRecordByIdAsync(_record.Id);
            if (latest != null)
            {
                ApplyRecordToForm(latest);
                await ReloadMediaPoolAsync();
            }

            await ReloadDefaultApproversAsync();
            RaiseCommandStates();
        }

        private async Task ReloadAttachmentsAsync()
        {
            IEnumerable<SystemAttachment> list = string.IsNullOrWhiteSpace(_record.DisposalNo)
                ? Array.Empty<SystemAttachment>()
                : await _disposalService.GetAttachmentsAsync(_record.DisposalNo);
            var policy = ApprovalAttachmentPolicySupport.Get(ApprovalWorkflowBusinessTypes.HardDiskDisposal);
            ApprovalAttachmentPolicySupport.Partition(
                policy,
                list,
                Attachments,
                SignedFormAttachments,
                DiskPhotoAttachments,
                proof: null,
                OtherAttachments);
        }

        private async Task UploadAttachmentByCategoryAsync(string category)
        {
            UploadCategory = category;
            await UploadAttachmentAsync();
        }

        private async Task CaptureAttachmentByCategoryAsync(string category)
        {
            UploadCategory = category;
            await CaptureFromDocumentCameraAsync();
        }

        private HardDiskDisposalRecord BuildDraftPayload()
        {
            return new HardDiskDisposalRecord
            {
                Id = _record.Id,
                DisposalNo = DisposalNo,
                OtherRemark = OtherRemark,
                Reason = Reason,
                Remark = Remark,
                Items = Items.Select(item => new HardDiskDisposalItem
                {
                    MediumId = item.MediumId,
                    SortOrder = item.SortOrder,
                    DisposalReason = item.DisposalReason,
                    DispositionMethod = item.DispositionMethod
                }).ToList()
            };
        }

        private bool HasUnsavedHeaderChanges()
        {
            return !string.Equals(_record.OtherRemark ?? string.Empty, OtherRemark ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(_record.Reason ?? string.Empty, Reason ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(_record.Remark ?? string.Empty, Remark ?? string.Empty, StringComparison.Ordinal)
                || !_record.Items.Select(item => item.MediumId).OrderBy(id => id)
                    .SequenceEqual(Items.Select(item => item.MediumId).OrderBy(id => id))
                || !_record.Items
                    .OrderBy(item => item.MediumId)
                    .Select(item => $"{item.MediumId}|{item.DispositionMethod?.Trim()}")
                    .SequenceEqual(Items
                        .OrderBy(item => item.MediumId)
                        .Select(item => $"{item.MediumId}|{item.DispositionMethod}"));
        }

        private User RequireCurrentUser()
        {
            return _userContextService.CurrentUser
                ?? throw new InvalidOperationException("当前用户无效，请重新登录。");
        }

        private async Task PersistReviewSignersAsync()
        {
            if (_suppressReviewSignerPersist
                || _record.Id <= 0
                || _record.Status is not (HardDiskDisposalRecord.StatusApproved
                    or HardDiskDisposalRecord.StatusSignedUploaded)
                || !CanOperate)
            {
                return;
            }

            try
            {
                await _disposalService.UpdateReviewSignersAsync(
                    _record.Id,
                    _deptHead,
                    _deptHeadDate,
                    _archiveRoomHead,
                    _archiveRoomHeadDate,
                    _productionHead,
                    _productionHeadDate,
                    _archiveDeputyPresident,
                    _archiveDeputyPresidentDate,
                    _productionVicePresident,
                    _productionVicePresidentDate,
                    RequireCurrentUser());
                _hasCommittedChanges = true;
                _record.DeptHead = _deptHead.Trim();
                _record.DeptHeadDate = _deptHeadDate;
                _record.ArchiveRoomHead = _archiveRoomHead.Trim();
                _record.ArchiveRoomHeadDate = _archiveRoomHeadDate;
                _record.ProductionHead = _productionHead.Trim();
                _record.ProductionHeadDate = _productionHeadDate;
                _record.ArchiveDeputyPresident = _archiveDeputyPresident.Trim();
                _record.ArchiveDeputyPresidentDate = _archiveDeputyPresidentDate;
                _record.ProductionVicePresident = _productionVicePresident.Trim();
                _record.ProductionVicePresidentDate = _productionVicePresidentDate;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void RaiseCommandStates()
        {
            NotifyShellAliasPropertiesChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 库内可选硬盘行。
    /// </summary>
    public sealed class HardDiskDisposalCandidateViewModel : ViewModelBase
    {
        private bool _isSelected;

        public static HardDiskDisposalCandidateViewModel FromMedium(HardDiskMedium medium, string? resolvedStorageLocation = null)
        {
            ArgumentNullException.ThrowIfNull(medium);
            string ledgerLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty;
            string mediaStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty;
            return new HardDiskDisposalCandidateViewModel
            {
                MediumId = medium.Id,
                DiskCode = medium.DiskCode?.Trim() ?? string.Empty,
                SerialNumber = medium.SerialNumber?.Trim() ?? string.Empty,
                Brand = medium.Brand?.Trim() ?? string.Empty,
                Capacity = medium.Capacity?.Trim() ?? string.Empty,
                InterfaceType = medium.InterfaceType?.Trim() ?? string.Empty,
                FactoryDate = medium.FactoryDate,
                MediaStatus = mediaStatus,
                StorageLocation = HardDiskDisposalDomainValues.ResolveBeforeStorageLocation(
                    mediaStatus,
                    ledgerLocation,
                    resolvedStorageLocation),
                MediaNature = medium.Ledger?.MediaNature?.Trim() ?? string.Empty
            };
        }

        public int MediumId { get; init; }

        public string DiskCode { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

        public string Capacity { get; init; } = string.Empty;

        public string InterfaceType { get; init; } = string.Empty;

        public DateTime? FactoryDate { get; init; }

        public string MediaStatus { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string MediaNature { get; init; } = string.Empty;

        public string FactoryDateText => FactoryDate?.ToString("yyyy-MM-dd") ?? string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
    }

    /// <summary>
    /// 离库处置明细行展示模型。
    /// </summary>
    public sealed class HardDiskDisposalItemViewModel : ViewModelBase
    {
        private int _sortOrder;
        private bool _isSelected;
        private string _dispositionMethod = string.Empty;

        public HardDiskDisposalItemViewModel(
            HardDiskDisposalItem item,
            string? headerDispositionMethod = null,
            string? storageLocationOverride = null)
        {
            MediumId = item.MediumId;
            DiskCode = item.DiskCode;
            SerialNumber = item.SerialNumber;
            BeforeMediaStatus = item.BeforeMediaStatus;
            BeforeStorageLocation = !string.IsNullOrWhiteSpace(storageLocationOverride)
                ? storageLocationOverride.Trim()
                : (item.BeforeStorageLocation?.Trim() ?? string.Empty);
            BeforeMediaNature = item.BeforeMediaNature;
            DisposalReason = ResolveDisposalReason(item.DisposalReason, item.BeforeMediaStatus);
            _dispositionMethod = ResolveDispositionMethod(
                item.DispositionMethod,
                item.BeforeMediaStatus,
                headerDispositionMethod);
            Capacity = string.Empty;
            InterfaceType = string.Empty;
            FactoryDate = null;
            _sortOrder = item.SortOrder;
        }

        private HardDiskDisposalItemViewModel()
        {
        }

        public static HardDiskDisposalItemViewModel FromCandidate(
            HardDiskDisposalCandidateViewModel candidate,
            int sortOrder,
            string? savedDisposalReason = null,
            string? savedDispositionMethod = null,
            string? headerDispositionMethod = null,
            string? storageLocationOverride = null)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            return new HardDiskDisposalItemViewModel
            {
                MediumId = candidate.MediumId,
                DiskCode = candidate.DiskCode,
                SerialNumber = candidate.SerialNumber,
                BeforeMediaStatus = candidate.MediaStatus,
                BeforeStorageLocation = !string.IsNullOrWhiteSpace(storageLocationOverride)
                    ? storageLocationOverride.Trim()
                    : candidate.StorageLocation,
                BeforeMediaNature = candidate.MediaNature,
                DisposalReason = ResolveDisposalReason(savedDisposalReason, candidate.MediaStatus),
                _dispositionMethod = ResolveDispositionMethod(
                    savedDispositionMethod,
                    candidate.MediaStatus,
                    headerDispositionMethod),
                Capacity = candidate.Capacity,
                InterfaceType = candidate.InterfaceType,
                FactoryDate = candidate.FactoryDate,
                _sortOrder = sortOrder
            };
        }

        private static string ResolveDisposalReason(string? savedReason, string? mediaStatus)
        {
            string reason = savedReason?.Trim() ?? string.Empty;
            if (string.Equals(reason, HardDiskDisposalDomainValues.LegacyReasonDamaged, StringComparison.Ordinal))
            {
                return HardDiskDisposalDomainValues.ReasonDamaged;
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            return HardDiskDisposalDomainValues.ResolveReasonFromMediaStatus(mediaStatus);
        }

        private static string ResolveDispositionMethod(
            string? savedMethod,
            string? mediaStatus,
            string? headerFallback)
        {
            string method = savedMethod?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(method))
            {
                return method;
            }

            method = HardDiskDisposalDomainValues.ResolveDispositionMethodFromMediaStatus(mediaStatus);
            if (!string.IsNullOrWhiteSpace(method))
            {
                return method;
            }

            string header = headerFallback?.Trim() ?? string.Empty;
            if (HardDiskDisposalDomainValues.IsValidDispositionMethod(header)
                && !header.Contains('、', StringComparison.Ordinal))
            {
                return header;
            }

            return string.Empty;
        }

        public int MediumId { get; private init; }

        public string DiskCode { get; private init; } = string.Empty;

        public string SerialNumber { get; private init; } = string.Empty;

        public string BeforeMediaStatus { get; private init; } = string.Empty;

        public string BeforeStorageLocation { get; private init; } = string.Empty;

        public string BeforeMediaNature { get; private init; } = string.Empty;

        public string DisposalReason { get; private init; } = string.Empty;

        public string DispositionMethod
        {
            get => _dispositionMethod;
            set => SetProperty(ref _dispositionMethod, value ?? string.Empty);
        }

        public string Capacity { get; private init; } = string.Empty;

        public string InterfaceType { get; private init; } = string.Empty;

        public DateTime? FactoryDate { get; private init; }

        public string FactoryDateText => FactoryDate?.ToString("yyyy-MM-dd") ?? string.Empty;

        public int SortOrder
        {
            get => _sortOrder;
            set => SetProperty(ref _sortOrder, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
    }
}
