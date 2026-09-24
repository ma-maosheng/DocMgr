using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料离库处置办理弹窗 ViewModel（草稿编辑 + 审批办结工作台，布局对齐硬盘离库处置）。
    /// </summary>
    public sealed partial class ArchiveDisposalEditDialogViewModel : ViewModelBase
    {
        private const string AllFilterText = "全部";

        private readonly IArchiveDisposalService _disposalService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly ICabinetService _cabinetService;
        private readonly IUserService _userService;
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private readonly List<ArchiveDisposalCandidateRow> _candidatePool = new();
        private YearlyArchiveDisposalRecord _record;
        private bool _hasCommittedChanges;
        private bool _isApplyingFilters;
        private bool _suppressBatchMethodApply;
        private string _disposalNo = string.Empty;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _approvalOpinion = "同意";
        private string _batchDispositionMethod = string.Empty;
        private string _uploadCategory = ArchiveDisposalDomainValues.AttachmentCategorySignedForm;
        private SystemAttachment? _selectedAttachment;
        private bool _physicalRemovalConfirmed;
        private bool _formatRetainedConfirmed;
        private bool _showPhysicalRemovalConfirm;
        private bool _showFormatRetainConfirm;
        private string _filterKeyword = string.Empty;
        private string _filterSourceRegisterKind = AllFilterText;
        private string _filterMediumKind = AllFilterText;
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
        private bool _enableProductionHead = true;
        private bool _enableArchiveDeputyPresident = true;
        private bool _enableProductionVicePresident = true;

        public ArchiveDisposalEditDialogViewModel(
            IArchiveDisposalService disposalService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IHardDiskMediaService hardDiskMediaService,
            ICabinetService cabinetService,
            IUserService userService,
            IApprovalWorkflowService approvalWorkflowService,
            YearlyArchiveDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _disposalService = disposalService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _hardDiskMediaService = hardDiskMediaService;
            _cabinetService = cabinetService;
            _userService = userService;
            _approvalWorkflowService = approvalWorkflowService;
            _record = record;

            MoveToDisposalCommand = new RelayCommand(_ => MoveToDisposal(), _ => CanEditHeader && AvailableItems.Any(i => i.IsSelected));
            MoveToAvailableCommand = new RelayCommand(_ => MoveToAvailable(), _ => CanEditHeader && Items.Any(i => i.IsSelected));
            ApplyDispositionMethodCommand = new RelayCommand(
                _ => ApplyDispositionMethodToSelected(BatchDispositionMethod),
                _ => CanEditHeader
                    && Items.Any(i => i.IsSelected)
                    && !string.IsNullOrWhiteSpace(BatchDispositionMethod));
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters(), _ => CanEditHeader);
            RecommendBlankSlotCommand = new RelayCommand<ArchiveDisposalItemRow>(
                async item => await RecommendBlankSlotAsync(item),
                item => CanEditBlankSlots
                        && item is { IsFormatRetain: true });
            ShowBlankSlotSnapshotCommand = new RelayCommand<ArchiveDisposalItemRow>(
                async item => await ShowBlankSlotSnapshotAsync(item),
                item => item is { IsFormatRetain: true }
                        && !string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation));
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmUploadCommand = new RelayCommand(async _ => await ConfirmUploadAsync(), _ => CanConfirmUpload);
            UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
            CaptureFromDocumentCameraCommand = new RelayCommand(
                async _ => await CaptureFromDocumentCameraAsync(),
                _ => CanUploadAttachment);
            UploadSignedFormAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(ArchiveDisposalDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            CaptureSignedFormAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(ArchiveDisposalDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            UploadScenePhotoAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(ArchiveDisposalDomainValues.AttachmentCategoryScenePhoto),
                _ => CanUploadMandatoryAttachment && RequiresScenePhoto);
            CaptureScenePhotoAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(ArchiveDisposalDomainValues.AttachmentCategoryScenePhoto),
                _ => CanUploadMandatoryAttachment && RequiresScenePhoto);
            UploadOtherAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(ArchiveDisposalDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            CaptureOtherAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(ArchiveDisposalDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            DeleteAttachmentCommand = new RelayCommand(
                async item => await DeleteAttachmentAsync(item as SystemAttachment),
                item => item is SystemAttachment && CanUploadMandatoryAttachment);
            ViewAttachmentCommand = new RelayCommand(
                async item => await ViewAttachmentAsync(item as SystemAttachment),
                item => item is SystemAttachment);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdraw);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"{(IsSimulated ? "模拟" : "电子")}资料离库处置 · {(string.IsNullOrWhiteSpace(DisposalNo) ? "待编单" : DisposalNo)} · {StatusDisplay}";

        public string StatusDisplay => ArchiveDisposalDomainValues.ToStatusDisplay(_record.Status);

        public bool IsSimulated =>
            string.Equals(_record.MediaKind?.Trim(), ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);

        public bool ShowMediumKindFilter => !IsSimulated;

        public string BannerText =>
            "流程：保存草稿 → 提交 → 打印签批单并线下签字 → 审批 → 确认可上传 → 上传签批单（销毁须资料照片）→ 办结。办结释档空盒/空袋前须确认物理移除；拟销硬盘低格留盘须确认已低格并从下拉选择目标空盘档口。";

        public ObservableCollection<ArchiveDisposalCandidateRow> AvailableItems { get; } = new();

        public ObservableCollection<ArchiveDisposalItemRow> Items { get; } = new();

        /// <summary>低格留盘可选的空白硬盘专用档口。</summary>
        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> BlankLocationOptions { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<SystemAttachment> SignedFormAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> ScenePhotoAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> OtherAttachments { get; } = new();

        public ObservableCollection<string> DispositionMethodOptions { get; } = new();

        public ObservableCollection<string> UploadCategoryOptions { get; } = new(ArchiveDisposalDomainValues.AttachmentCategoryOptions);

        public ObservableCollection<string> SourceRegisterKindFilterOptions { get; } = new();

        public ObservableCollection<string> MediumKindFilterOptions { get; } = new();

        public string AvailableItemsTitle => $"可选盘库资料（{AvailableItems.Count}）";

        public string DisposalItemsTitle => $"待处置明细（{Items.Count}）";

        public string FilterKeyword
        {
            get => _filterKeyword;
            set
            {
                if (SetProperty(ref _filterKeyword, value))
                {
                    RefreshAvailableItems();
                }
            }
        }

        public string FilterSourceRegisterKind
        {
            get => _filterSourceRegisterKind;
            set
            {
                if (SetProperty(ref _filterSourceRegisterKind, value))
                {
                    RefreshAvailableItems();
                }
            }
        }

        public string FilterMediumKind
        {
            get => _filterMediumKind;
            set
            {
                if (SetProperty(ref _filterMediumKind, value))
                {
                    RefreshAvailableItems();
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

        /// <summary>默认部门审核（只读展示）。</summary>
        public string DefaultDeptHeadDisplay => EmptyAsDash(_defaultDeptHead);

        /// <summary>默认资料室负责人（只读展示）。</summary>
        public string DefaultArchiveRoomHeadDisplay => EmptyAsDash(_defaultArchiveRoomHead);

        /// <summary>默认生产科负责人（只读展示）。</summary>
        public string DefaultProductionHeadDisplay => EmptyAsDash(_defaultProductionHead);

        /// <summary>默认分管资料室副院长（只读展示）。</summary>
        public string DefaultArchiveDeputyPresidentDisplay => EmptyAsDash(_defaultArchiveDeputyPresident);

        /// <summary>默认分管生产副院长（只读展示）。</summary>
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
            || (_record.Status is YearlyArchiveDisposalRecord.StatusApproved
                    or YearlyArchiveDisposalRecord.StatusSignedUploaded
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

        public string BatchDispositionMethod
        {
            get => _batchDispositionMethod;
            set
            {
                if (!SetProperty(ref _batchDispositionMethod, value))
                {
                    return;
                }

                RefreshCommandStates();
                if (!_suppressBatchMethodApply)
                {
                    ApplyDispositionMethodToSelected(value);
                }
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

        public bool PhysicalRemovalConfirmed
        {
            get => _physicalRemovalConfirmed;
            set => SetProperty(ref _physicalRemovalConfirmed, value);
        }

        public bool FormatRetainedConfirmed
        {
            get => _formatRetainedConfirmed;
            set => SetProperty(ref _formatRetainedConfirmed, value);
        }

        public bool ShowPhysicalRemovalConfirm
        {
            get => _showPhysicalRemovalConfirm;
            private set => SetProperty(ref _showPhysicalRemovalConfirm, value);
        }

        public bool ShowFormatRetainConfirm
        {
            get => _showFormatRetainConfirm;
            private set => SetProperty(ref _showFormatRetainConfirm, value);
        }

        public bool CanOperate =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanEditHeader =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusDraft;

        /// <summary>办结前可编辑低格留盘目标空盘档口（不做预占用）。</summary>
        public bool CanEditBlankSlots =>
            CanOperate
            && _record.Status == YearlyArchiveDisposalRecord.StatusSignedUploaded
            && Items.Any(item => ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod));

        public bool CanInteractItemsGrid => CanEditHeader || CanEditBlankSlots;

        public bool ShowBlankSlotColumn =>
            Items.Any(item => ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod));

        public bool CanSubmit =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusDraft && _record.Id > 0 && Items.Count > 0;

        public bool CanApprove =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusSubmitted;

        public bool CanConfirmUpload =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusApproved;

        /// <summary>办结后资料管理员可增补「其他附件」。</summary>
        public bool CanSupplementOtherAttachments =>
            ApprovalWorkflowButtonSupport.CanSupplementOtherAttachments(
                _record.Status == YearlyArchiveDisposalRecord.StatusCompleted,
                CanOperate);

        public bool CanUploadAttachment =>
            CanOperate
            && (_record.Status == YearlyArchiveDisposalRecord.StatusApproved
                || _record.Status == YearlyArchiveDisposalRecord.StatusSignedUploaded
                || CanSupplementOtherAttachments);

        /// <summary>签批单/处置资料照片：仅确认可上传后、办结前可传。</summary>
        public bool CanUploadMandatoryAttachment =>
            CanUploadAttachment && !CanSupplementOtherAttachments;

        /// <summary>其他附件：确认可上传后及办结后均可增补。</summary>
        public bool CanUploadOtherAttachment => CanUploadAttachment;

        /// <summary>当前明细是否含须上传处置资料照片的处置方式。</summary>
        public bool RequiresScenePhoto =>
            ArchiveDisposalDomainValues.RequiresScenePhoto(Items.Select(item => item.DispositionMethod));

        public string UploadAttachmentHintText => CanSupplementOtherAttachments
            ? "办结后仅可增补「其他附件」；不可删除已有附件。"
            : "请在「确认可上传」后分区上传签批单与处置资料照片；办结前仍可继续补传。";

        public string ApproveHintText => CanApprove
            ? "请按线下签批结果执行审批通过；通过后点击「确认可上传」。"
            : "仅「已提交」状态可审批通过。";

        public string ConfirmUploadHintText => CanConfirmUpload
            ? "确认后可分区上传签批单与处置资料照片。"
            : "请先执行「审批通过」。";

        public string CompleteHintText => CanComplete
            ? "确认办结前请核对实物撤柜/低格确认与必备附件。"
            : "请先上传签批单（销毁须处置资料照片）后再确认办结。";

        public bool CanComplete =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusSignedUploaded;

        public bool CanPrint =>
            CanOperate
            && _record.Id > 0
            && _record.Status is not YearlyArchiveDisposalRecord.StatusDraft
                and not YearlyArchiveDisposalRecord.StatusWithdrawn
                and not YearlyArchiveDisposalRecord.StatusForceWithdrawn;

        public bool CanWithdraw =>
            CanOperate
            && _record.Id > 0
            && _record.Status is not YearlyArchiveDisposalRecord.StatusCompleted
                and not YearlyArchiveDisposalRecord.StatusWithdrawn
                and not YearlyArchiveDisposalRecord.StatusForceWithdrawn;

        public RelayCommand MoveToDisposalCommand { get; }
        public RelayCommand MoveToAvailableCommand { get; }
        public RelayCommand ApplyDispositionMethodCommand { get; }
        public RelayCommand ClearFiltersCommand { get; }
        public RelayCommand<ArchiveDisposalItemRow> RecommendBlankSlotCommand { get; }
        public RelayCommand<ArchiveDisposalItemRow> ShowBlankSlotSnapshotCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmUploadCommand { get; }
        public RelayCommand UploadAttachmentCommand { get; }
        public RelayCommand CaptureFromDocumentCameraCommand { get; }
        public RelayCommand UploadSignedFormAttachmentCommand { get; }
        public RelayCommand CaptureSignedFormAttachmentCommand { get; }
        public RelayCommand UploadScenePhotoAttachmentCommand { get; }
        public RelayCommand CaptureScenePhotoAttachmentCommand { get; }
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
            try
            {
                BindFromRecord(_record);
                await EnsureBlankLocationOptionsAsync();
                EnsurePersistedBlankLocationsInOptions();
                await ReloadDefaultApproversAsync();
                if (_record.Id <= 0 && string.IsNullOrWhiteSpace(_record.DisposalNo))
                {
                    DisposalNo = await _disposalService.GenerateNextDisposalNoAsync();
                    _record.DisposalNo = DisposalNo;
                }
                else
                {
                    DisposalNo = _record.DisposalNo?.Trim() ?? string.Empty;
                }

                await ReloadCandidatePoolAsync();
                if (_record.Id > 0)
                {
                    await ReloadAttachmentsAsync();
                    await RefreshCompleteHintsAsync();
                }

                RefreshCommandStates();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ReloadDefaultApproversAsync()
        {
            var users = _userService.GetAllUsers();
            var chain = await _approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal,
                    FieldValues = ApprovalChainApplySupport.BuildYearlyDisposalFieldValues(
                        _record.MediaKind,
                        Items.Select(item => item.ToEntity()))
                },
                users);
            var approvers = chain.MatchedRuleId == null
                ? ArchiveDisposalDefaultApproverSupport.Resolve(users)
                : ArchiveDisposalDefaultApproverSupport.FromChain(chain);

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

        private static string EmptyAsDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

        private static string PreferNonEmpty(string? primary, string? fallback) =>
            !string.IsNullOrWhiteSpace(primary) ? primary.Trim() : (fallback?.Trim() ?? string.Empty);

        private void BindFromRecord(YearlyArchiveDisposalRecord record)
        {
            _record = record;
            DisposalNo = record.DisposalNo?.Trim() ?? DisposalNo;
            Reason = record.Reason;
            Remark = record.Remark;
            ApprovalOpinion = string.IsNullOrWhiteSpace(record.ApprovalOpinion) ? "同意" : record.ApprovalOpinion;
            PhysicalRemovalConfirmed = record.PhysicalRemovalConfirmed;
            FormatRetainedConfirmed = record.FormatRetainedConfirmed;

            Items.Clear();
            foreach (var item in record.Items.OrderBy(i => i.SortOrder))
            {
                Items.Add(ArchiveDisposalItemRow.FromEntity(item));
            }

            RefreshDispositionMethodOptions();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(ApplicantName));
            OnPropertyChanged(nameof(ApplicantDept));
            OnPropertyChanged(nameof(ShowMediumKindFilter));
            OnPropertyChanged(nameof(AvailableItemsTitle));
            OnPropertyChanged(nameof(DisposalItemsTitle));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditBlankSlots));
            OnPropertyChanged(nameof(CanInteractItemsGrid));
            OnPropertyChanged(nameof(ShowBlankSlotColumn));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmUpload));
            OnPropertyChanged(nameof(CanSupplementOtherAttachments));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanUploadMandatoryAttachment));
            OnPropertyChanged(nameof(CanUploadOtherAttachment));
            OnPropertyChanged(nameof(RequiresScenePhoto));
            OnPropertyChanged(nameof(UploadAttachmentHintText));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(ConfirmUploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanWithdraw));
            OnPropertyChanged(nameof(CanEditSigners));
            OnPropertyChanged(nameof(SignatureDateMin));
            NotifyShellAliasPropertiesChanged();

            if (CanSupplementOtherAttachments)
            {
                UploadCategory = ArchiveDisposalDomainValues.AttachmentCategoryOther;
            }
        }

        private async Task ReloadCandidatePoolAsync()
        {
            var selectable = await _disposalService.GetSelectableItemsAsync(
                _record.MediaKind,
                _record.Id > 0 ? _record.Id : null);

            _candidatePool.Clear();
            foreach (var item in selectable.OrderBy(i => i.DisplayTitle, StringComparer.Ordinal))
            {
                _candidatePool.Add(new ArchiveDisposalCandidateRow(item));
            }

            RebuildFilterOptions();
            RefreshAvailableItems();
        }

        private void RebuildFilterOptions()
        {
            _isApplyingFilters = true;
            try
            {
                string previousKind = FilterSourceRegisterKind;
                string previousMedium = FilterMediumKind;

                SourceRegisterKindFilterOptions.Clear();
                SourceRegisterKindFilterOptions.Add(AllFilterText);
                foreach (var value in _candidatePool
                    .Select(item => item.SourceRegisterKind)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal))
                {
                    SourceRegisterKindFilterOptions.Add(value);
                }

                MediumKindFilterOptions.Clear();
                MediumKindFilterOptions.Add(AllFilterText);
                foreach (var value in _candidatePool
                    .Select(item => item.MediumKind)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal))
                {
                    MediumKindFilterOptions.Add(value);
                }

                _filterSourceRegisterKind = SourceRegisterKindFilterOptions.Contains(previousKind)
                    ? previousKind
                    : AllFilterText;
                _filterMediumKind = MediumKindFilterOptions.Contains(previousMedium)
                    ? previousMedium
                    : AllFilterText;
                OnPropertyChanged(nameof(FilterSourceRegisterKind));
                OnPropertyChanged(nameof(FilterMediumKind));
            }
            finally
            {
                _isApplyingFilters = false;
            }
        }

        private void RefreshAvailableItems()
        {
            if (_isApplyingFilters)
            {
                return;
            }

            HashSet<string> selectedKeys = Items.Select(i => i.SelectionKey).ToHashSet(StringComparer.Ordinal);
            IEnumerable<ArchiveDisposalCandidateRow> query = _candidatePool
                .Where(item => !selectedKeys.Contains(item.SelectionKey));

            if (!string.IsNullOrWhiteSpace(FilterKeyword))
            {
                string keyword = FilterKeyword.Trim();
                query = query.Where(item =>
                    item.DisplayTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.BeforeStorageLocation.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.ContainerCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.ElectronicArchiveNo.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.MediumCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.MaterialName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.ItemName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.FormNo.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(FilterSourceRegisterKind, AllFilterText, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(FilterSourceRegisterKind))
            {
                query = query.Where(item =>
                    string.Equals(item.SourceRegisterKind, FilterSourceRegisterKind, StringComparison.Ordinal));
            }

            if (ShowMediumKindFilter
                && !string.Equals(FilterMediumKind, AllFilterText, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(FilterMediumKind))
            {
                query = query.Where(item =>
                    string.Equals(item.MediumKind, FilterMediumKind, StringComparison.Ordinal));
            }

            AvailableItems.Clear();
            foreach (var item in query.OrderBy(row => row.DisplayTitle, StringComparer.Ordinal))
            {
                item.IsSelected = false;
                AvailableItems.Add(item);
            }

            OnPropertyChanged(nameof(AvailableItemsTitle));
            OnPropertyChanged(nameof(DisposalItemsTitle));
            RefreshCommandStates();
        }

        private void ClearFilters()
        {
            _isApplyingFilters = true;
            try
            {
                _filterKeyword = string.Empty;
                _filterSourceRegisterKind = AllFilterText;
                _filterMediumKind = AllFilterText;
                OnPropertyChanged(nameof(FilterKeyword));
                OnPropertyChanged(nameof(FilterSourceRegisterKind));
                OnPropertyChanged(nameof(FilterMediumKind));
            }
            finally
            {
                _isApplyingFilters = false;
            }

            RefreshAvailableItems();
        }

        /// <summary>为低格留盘明细推荐空白硬盘专用档口（不做预占用）。</summary>
        private async Task RecommendBlankSlotAsync(ArchiveDisposalItemRow? item)
        {
            if (item == null || !CanEditBlankSlots || !item.IsFormatRetain)
            {
                return;
            }

            try
            {
                await EnsureBlankLocationOptionsAsync();
                EnsurePersistedBlankLocationsInOptions();
                if (BlankLocationOptions.Count == 0)
                {
                    _dialogService.ShowMessage("未找到空白硬盘专用档口，请先在磁盘柜开柜界面完成设置。", "推荐档口");
                    return;
                }

                string? recommended = await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync();
                string slot = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(recommended);
                if (string.IsNullOrWhiteSpace(slot))
                {
                    _dialogService.ShowMessage("当前未找到可用的空白硬盘专用档口。", "推荐档口");
                    return;
                }

                var matched = BlankLocationOptions.FirstOrDefault(option =>
                    string.Equals(
                        HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(option.Location),
                        slot,
                        StringComparison.OrdinalIgnoreCase));
                if (matched == null)
                {
                    matched = new HardDiskMediaReturnTargetLocationOption
                    {
                        Location = slot,
                        ExistingMediumCount = 0
                    };
                    BlankLocationOptions.Insert(0, matched);
                }

                item.TargetBlankSlotLocation = matched.Location;
                RefreshCommandStates();
                _dialogService.ShowMessage($"已推荐档口：{matched.DisplayText}", "推荐档口");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task EnsureBlankLocationOptionsAsync()
        {
            var options = await _hardDiskMediaService.GetOrderedBlankDedicatedSlotLocationOptionsAsync();
            BlankLocationOptions.Clear();
            foreach (var option in options)
            {
                BlankLocationOptions.Add(option);
            }
        }

        /// <summary>
        /// 将明细中已保存、但当前选项列表未包含的空盘档口补入下拉，避免只读/重开后显示为空。
        /// </summary>
        private void EnsurePersistedBlankLocationsInOptions()
        {
            foreach (var item in Items)
            {
                if (!item.IsFormatRetain)
                {
                    continue;
                }

                string location = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.TargetBlankSlotLocation);
                if (string.IsNullOrWhiteSpace(location))
                {
                    continue;
                }

                if (BlankLocationOptions.Any(option =>
                        string.Equals(
                            HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(option.Location),
                            location,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    // 统一为选项中的 Location 写法，保证 ComboBox SelectedValue 能命中。
                    var matched = BlankLocationOptions.First(option =>
                        string.Equals(
                            HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(option.Location),
                            location,
                            StringComparison.OrdinalIgnoreCase));
                    if (!string.Equals(item.TargetBlankSlotLocation, matched.Location, StringComparison.Ordinal))
                    {
                        item.TargetBlankSlotLocation = matched.Location;
                    }

                    continue;
                }

                BlankLocationOptions.Insert(0, new HardDiskMediaReturnTargetLocationOption
                {
                    Location = location,
                    ExistingMediumCount = 0
                });
                item.TargetBlankSlotLocation = location;
            }
        }

        /// <summary>打开低格留盘目标空盘档口的占用快照。</summary>
        private async Task ShowBlankSlotSnapshotAsync(ArchiveDisposalItemRow? item)
        {
            if (item == null || !item.IsFormatRetain)
            {
                return;
            }

            try
            {
                string location = item.TargetBlankSlotLocation?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(location))
                {
                    _dialogService.ShowMessage("请先选择或推荐空盘档口后再查看快照。", "档口快照");
                    return;
                }

                if (!TryParseCabinetLocation(location, out string cabinetName, out CabinetFace face, out string slotCode))
                {
                    _dialogService.ShowMessage("空盘档口无法解析，请核对格式（如：柜名A-1-2）后再查看快照。", "档口快照");
                    return;
                }

                var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                    .FirstOrDefault(c =>
                        c.Type == CabinetType.MagneticDisk
                        && string.Equals(c.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
                if (cabinet == null)
                {
                    _dialogService.ShowMessage($"未找到柜号 [{cabinetName}] 对应的防磁磁盘柜。", "档口快照");
                    return;
                }

                _dialogService.ShowCabinetOpenDialog(new CabinetOpenRequest
                {
                    CabinetId = cabinet.Id,
                    CabinetName = cabinet.Name,
                    CabinetType = cabinet.Type,
                    Face = face,
                    LayerCount = cabinet.LayerCount,
                    ColumnCount = cabinet.ColumnCount,
                    TargetSlotCode = slotCode,
                    WidthCm = cabinet.Width,
                    HeightCm = cabinet.Height,
                    DepthCm = cabinet.Depth
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private static bool TryParseCabinetLocation(
            string? location,
            out string cabinetName,
            out CabinetFace face,
            out string slotCode)
        {
            cabinetName = string.Empty;
            face = CabinetFace.A;
            slotCode = string.Empty;

            if (!HardDiskBlankSlotLocationSupport.TryParseLocationCode(
                    location,
                    out string parsedCabinet,
                    out string faceCode,
                    out int row,
                    out int column))
            {
                return false;
            }

            cabinetName = parsedCabinet;
            face = string.Equals(faceCode, "B", StringComparison.OrdinalIgnoreCase) ? CabinetFace.B : CabinetFace.A;
            slotCode = $"{row}-{column}";
            return !string.IsNullOrWhiteSpace(cabinetName);
        }

        private void RefreshDispositionMethodOptions()
        {
            _suppressBatchMethodApply = true;
            try
            {
                DispositionMethodOptions.Clear();
                var methods = Items
                    .SelectMany(item => ArchiveDisposalDomainValues.ResolveAllowedMethods(
                        _record.MediaKind,
                        item.DisposalReason,
                        item.MediumKind))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var method in methods)
                {
                    DispositionMethodOptions.Add(method);
                }

                if (DispositionMethodOptions.Count > 0
                    && !DispositionMethodOptions.Contains(BatchDispositionMethod, StringComparer.Ordinal))
                {
                    _batchDispositionMethod = DispositionMethodOptions[0];
                    OnPropertyChanged(nameof(BatchDispositionMethod));
                }
            }
            finally
            {
                _suppressBatchMethodApply = false;
            }
        }

        private void MoveToDisposal()
        {
            var selected = AvailableItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var row in selected)
            {
                if (Items.Any(item => string.Equals(item.SelectionKey, row.SelectionKey, StringComparison.Ordinal)))
                {
                    continue;
                }

                Items.Add(ArchiveDisposalItemRow.FromSelectable(row.Source));
            }

            RenumberItems();
            RefreshDispositionMethodOptions();
            RefreshAvailableItems();
        }

        private void MoveToAvailable()
        {
            var selected = Items.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var row in selected)
            {
                Items.Remove(row);
            }

            RenumberItems();
            RefreshDispositionMethodOptions();
            RefreshAvailableItems();
        }

        private void ApplyDispositionMethodToSelected(string? method)
        {
            if (!CanEditHeader)
            {
                return;
            }

            string normalized = ArchiveDisposalDomainValues.NormalizeDispositionMethod(method);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            var selected = Items.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var distinctReasons = selected
                .Select(item => item.DisposalReason?.Trim() ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (distinctReasons.Count > 1)
            {
                _dialogService.ShowMessage(
                    "所选明细的「离库原因」不一致，请按同一原因分批勾选后再赋值。",
                    "无法赋值");
                return;
            }

            foreach (var item in selected)
            {
                var allowed = ArchiveDisposalDomainValues.ResolveAllowedMethods(
                    _record.MediaKind,
                    item.DisposalReason,
                    item.MediumKind);
                if (!allowed.Contains(normalized, StringComparer.Ordinal))
                {
                    _dialogService.ShowError($"「{item.DisplayTitle}」不允许处置方式「{normalized}」。");
                    return;
                }
            }

            foreach (var item in selected)
            {
                item.DispositionMethod = normalized;
            }
        }

        private void RenumberItems()
        {
            int sort = 1;
            foreach (var item in Items)
            {
                item.SortOrder = sort++;
                item.IsSelected = false;
            }

            OnPropertyChanged(nameof(DisposalItemsTitle));
        }

        private List<YearlyArchiveDisposalItem> BuildEntityItems()
        {
            return Items.Select(item => item.ToEntity()).ToList();
        }

        private async Task SaveDraftAsync()
        {
            try
            {
                var user = RequireUser();
                var draft = new YearlyArchiveDisposalRecord
                {
                    Id = _record.Id,
                    DisposalNo = DisposalNo,
                    MediaKind = _record.MediaKind,
                    Reason = Reason,
                    Remark = Remark
                };
                var items = BuildEntityItems();
                var saved = _record.Id > 0
                    ? await _disposalService.UpdateDraftAsync(draft, items, user)
                    : await _disposalService.CreateDraftAsync(draft, items, user);

                _hasCommittedChanges = true;
                BindFromRecord(saved);
                EnsurePersistedBlankLocationsInOptions();
                await ReloadCandidatePoolAsync();
                _dialogService.ShowMessage("草稿已保存。");
                RefreshCommandStates();
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

                if (!_dialogService.ShowConfirm("确认提交该离库处置单？提交后将锁定关联介质。"))
                {
                    return;
                }

                await _disposalService.SubmitAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
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

                await _disposalService.ApproveAsync(_record.Id, ApprovalOpinion, RequireUser());
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
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
                await _disposalService.ConfirmReadyForUploadAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                _dialogService.ShowMessage("已确认可上传签批单。");
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
                await RefreshCompleteHintsAsync();
                if (ShowPhysicalRemovalConfirm && !PhysicalRemovalConfirmed)
                {
                    _dialogService.ShowError("请勾选确认：已完成处置后空档案盒/介质袋的物理移除。");
                    return;
                }

                if (ShowFormatRetainConfirm && !FormatRetainedConfirmed)
                {
                    _dialogService.ShowError("请勾选确认：拟销硬盘已完成低级格式化。");
                    return;
                }

                if (ShowFormatRetainConfirm)
                {
                    var missingSlot = Items
                        .Where(item => ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod))
                        .FirstOrDefault(item => string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation));
                    if (missingSlot != null)
                    {
                        _dialogService.ShowError($"「{missingSlot.DisplayTitle}」为低格留盘，请先填写目标空盘档口。");
                        return;
                    }
                }

                if (!_dialogService.ShowConfirm("确认办结？办结后将写入正式清账结果并释档空盒/空袋。"))
                {
                    return;
                }

                await PersistReviewSignersAsync();

                var blankSlots = Items
                    .Where(item => ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod) && item.Id > 0)
                    .ToDictionary(
                        item => item.Id,
                        item => item.TargetBlankSlotLocation?.Trim() ?? string.Empty);

                await _disposalService.CompleteAsync(
                    _record.Id,
                    RequireUser(),
                    PhysicalRemovalConfirmed,
                    FormatRetainedConfirmed,
                    blankSlots);
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                _dialogService.ShowMessage("已办结。");
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

                await _disposalService.WithdrawAsync(_record.Id, null, RequireUser());
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                _dialogService.ShowMessage("已撤回作废。");
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
                FlowDocument document = ArchiveDisposalPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document);
                previewWindow.ShowDialog();
                await _disposalService.RecordPrintAsync(_record.Id);
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
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
                    UploadCategory = ArchiveDisposalDomainValues.AttachmentCategoryOther;
                }
                else if (_record.Status == YearlyArchiveDisposalRecord.StatusApproved
                    && !string.Equals(UploadCategory, ArchiveDisposalDomainValues.AttachmentCategoryOther, StringComparison.Ordinal))
                {
                    _dialogService.ShowMessage("请先点击「确认可上传」，再上传签批单或处置资料照片。");
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
                var (ok, message, _) = await _disposalService.UploadAttachmentAsync(
                    _record.Id,
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
                    UploadCategory = ArchiveDisposalDomainValues.AttachmentCategoryOther;
                }
                else if (_record.Status == YearlyArchiveDisposalRecord.StatusApproved
                    && !string.Equals(UploadCategory, ArchiveDisposalDomainValues.AttachmentCategoryOther, StringComparison.Ordinal))
                {
                    _dialogService.ShowMessage("请先点击「确认可上传」，再上传签批单或处置资料照片。");
                    return;
                }

                DocumentCameraCaptureResult? captured = DocumentCameraAttachmentCaptureSupport.Capture(_dialogService);
                if (captured == null)
                {
                    return;
                }

                string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(DisposalNo, UploadCategory, "资离处");
                var (ok, message, _) = await _disposalService.UploadAttachmentAsync(
                    _record.Id,
                    UploadCategory,
                    fileName,
                    ".jpg",
                    captured.JpegContent.LongLength,
                    captured.JpegContent,
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
                if (!_dialogService.ShowConfirm($"确认删除附件【{attachment.FileName}】？"))
                {
                    return;
                }

                var (ok, message) = await _disposalService.DeleteAttachmentAsync(attachment.Id, RequireUser());
                if (!ok)
                {
                    _dialogService.ShowError(message);
                    return;
                }

                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
                await ReloadRecordAsync();
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
                var latest = await _disposalService.GetAttachmentByIdAsync(attachment.Id);
                if (latest == null)
                {
                    _dialogService.ShowError("附件不存在。");
                    return;
                }

                _dialogService.ShowSystemAttachmentView(latest);
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
                BindFromRecord(latest);
                EnsurePersistedBlankLocationsInOptions();
                await ReloadCandidatePoolAsync();
                await RefreshCompleteHintsAsync();
            }

            RefreshCommandStates();
        }

        private async Task ReloadAttachmentsAsync()
        {
            IEnumerable<SystemAttachment> list = string.IsNullOrWhiteSpace(_record.DisposalNo)
                ? Array.Empty<SystemAttachment>()
                : await _disposalService.GetAttachmentsAsync(_record.DisposalNo);
            var policy = ApprovalAttachmentPolicySupport.Get(ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal);
            ApprovalAttachmentPolicySupport.Partition(
                policy,
                list,
                Attachments,
                SignedFormAttachments,
                ScenePhotoAttachments,
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

        private async Task RefreshCompleteHintsAsync()
        {
            if (_record.Id <= 0)
            {
                ShowPhysicalRemovalConfirm = false;
                ShowFormatRetainConfirm = false;
                return;
            }

            ShowPhysicalRemovalConfirm = await _disposalService.RequiresPhysicalRemovalConfirmationAsync(_record.Id);
            ShowFormatRetainConfirm = await _disposalService.RequiresFormatRetainConfirmationAsync(_record.Id);
            OnPropertyChanged(nameof(CanEditBlankSlots));
            OnPropertyChanged(nameof(CanInteractItemsGrid));
            OnPropertyChanged(nameof(ShowBlankSlotColumn));
        }

        private bool HasUnsavedHeaderChanges()
        {
            static string BuildKey(int mediumId, string? mediumKind, int filingFactId) =>
                mediumId > 0
                    ? $"M:{mediumKind?.Trim()}:{mediumId}"
                    : $"F:{filingFactId}";

            return !string.Equals(_record.Reason ?? string.Empty, Reason ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(_record.Remark ?? string.Empty, Remark ?? string.Empty, StringComparison.Ordinal)
                || !_record.Items
                    .Select(item => BuildKey(item.MediumId, item.MediumKind, item.FilingFactId))
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .SequenceEqual(Items.Select(item => item.SelectionKey).OrderBy(key => key, StringComparer.Ordinal))
                || !_record.Items
                    .OrderBy(item => BuildKey(item.MediumId, item.MediumKind, item.FilingFactId), StringComparer.Ordinal)
                    .Select(item => $"{BuildKey(item.MediumId, item.MediumKind, item.FilingFactId)}|{item.DispositionMethod?.Trim()}")
                    .SequenceEqual(Items
                        .OrderBy(item => item.SelectionKey, StringComparer.Ordinal)
                        .Select(item => $"{item.SelectionKey}|{item.DispositionMethod}"));
        }

        private void RefreshCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditBlankSlots));
            OnPropertyChanged(nameof(CanInteractItemsGrid));
            OnPropertyChanged(nameof(ShowBlankSlotColumn));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmUpload));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanUploadMandatoryAttachment));
            OnPropertyChanged(nameof(CanUploadOtherAttachment));
            OnPropertyChanged(nameof(RequiresScenePhoto));
            OnPropertyChanged(nameof(UploadAttachmentHintText));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(ConfirmUploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanWithdraw));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(CanEditSigners));
            OnPropertyChanged(nameof(SignatureDateMin));
            NotifyShellAliasPropertiesChanged();
        }

        private async Task PersistReviewSignersAsync()
        {
            if (_suppressReviewSignerPersist
                || _record.Id <= 0
                || _record.Status is not (YearlyArchiveDisposalRecord.StatusApproved
                    or YearlyArchiveDisposalRecord.StatusSignedUploaded)
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
                    RequireUser());
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

        private Models.SystemSettings.User RequireUser()
        {
            return _userContextService.CurrentUser
                ?? throw new InvalidOperationException("当前用户无效。");
        }
    }

    /// <summary>
    /// 可选盘库资料行。
    /// </summary>
    public sealed class ArchiveDisposalCandidateRow : ViewModelBase
    {
        private bool _isSelected;

        public ArchiveDisposalCandidateRow(ArchiveDisposalSelectableItem source)
        {
            Source = source;
        }

        public ArchiveDisposalSelectableItem Source { get; }

        public string SelectionKey => Source.SelectionKey;

        public string DisplayTitle => Source.DisplayTitle;

        public string DisposalReason => Source.DisposalReason;

        public string SourceRegisterKind => Source.SourceRegisterKind;

        public string BeforeStorageLocation => Source.BeforeStorageLocation;

        public string ContainerCode => Source.ContainerCode;

        public string ElectronicArchiveNo => Source.ElectronicArchiveNo;

        public string MediumKind => Source.MediumKind;

        public string MediumCode => Source.MediumCode;

        public string MaterialName => Source.MaterialName;

        public string ItemName => Source.ItemName;

        public string FormNo => Source.FormNo;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    /// <summary>
    /// 待处置明细行。
    /// </summary>
    public sealed class ArchiveDisposalItemRow : ViewModelBase
    {
        private bool _isSelected;
        private string _dispositionMethod = string.Empty;
        private string _targetBlankSlotLocation = string.Empty;

        /// <summary>已持久化明细主键；新建未保存行为 0。</summary>
        public int Id { get; set; }

        public int SortOrder { get; set; }

        public int FilingFactId { get; set; }

        public int ContainerId { get; set; }

        public string ContainerCode { get; set; } = string.Empty;

        public string BeforeStorageLocation { get; set; } = string.Empty;

        public string SourceRegisterKind { get; set; } = string.Empty;

        public string DisposalReason { get; set; } = string.Empty;

        public string DispositionMethod
        {
            get => _dispositionMethod;
            set
            {
                if (SetProperty(ref _dispositionMethod, value))
                {
                    OnPropertyChanged(nameof(IsFormatRetain));
                }
            }
        }

        public string MaterialName { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string FormNo { get; set; } = string.Empty;

        public int InventoryLostCopyCount { get; set; }

        public int InventoryScrapCopyCount { get; set; }

        public string BeforeLifecycleStatus { get; set; } = string.Empty;

        public string MediumKind { get; set; } = string.Empty;

        public int MediumId { get; set; }

        public string MediumCode { get; set; } = string.Empty;

        public int ElectronicArchiveUnitId { get; set; }

        public string ElectronicArchiveNo { get; set; } = string.Empty;

        public string BeforeMediaStatus { get; set; } = string.Empty;

        public string TargetBlankSlotLocation
        {
            get => _targetBlankSlotLocation;
            set => SetProperty(ref _targetBlankSlotLocation, value);
        }

        public bool IsFormatRetain =>
            ArchiveDisposalDomainValues.IsFormatRetainMethod(DispositionMethod);

        public string DisplayTitle =>
            MediumId > 0
                ? $"{MediumKind} {MediumCode}（{ElectronicArchiveNo}）"
                : $"[{ContainerCode}] {(string.IsNullOrWhiteSpace(ItemName) ? MaterialName : ItemName)}";

        public string SelectionKey =>
            MediumId > 0 ? $"M:{MediumKind}:{MediumId}" : $"F:{FilingFactId}";

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public static ArchiveDisposalItemRow FromSelectable(ArchiveDisposalSelectableItem source)
        {
            string method = ArchiveDisposalDomainValues.ResolveDefaultMethod(
                source.MediaKind,
                source.DisposalReason,
                source.MediumKind);
            return new ArchiveDisposalItemRow
            {
                FilingFactId = source.FilingFactId,
                ContainerId = source.ContainerId,
                ContainerCode = source.ContainerCode,
                BeforeStorageLocation = source.BeforeStorageLocation,
                SourceRegisterKind = source.SourceRegisterKind,
                DisposalReason = source.DisposalReason,
                DispositionMethod = method,
                MaterialName = source.MaterialName,
                ItemName = source.ItemName,
                FormNo = source.FormNo,
                InventoryLostCopyCount = source.InventoryLostCopyCount,
                InventoryScrapCopyCount = source.InventoryScrapCopyCount,
                BeforeLifecycleStatus = source.BeforeLifecycleStatus,
                MediumKind = source.MediumKind,
                MediumId = source.MediumId,
                MediumCode = source.MediumCode,
                ElectronicArchiveUnitId = source.ElectronicArchiveUnitId,
                ElectronicArchiveNo = source.ElectronicArchiveNo,
                BeforeMediaStatus = source.BeforeMediaStatus
            };
        }

        public static ArchiveDisposalItemRow FromEntity(YearlyArchiveDisposalItem item)
        {
            return new ArchiveDisposalItemRow
            {
                Id = item.Id,
                SortOrder = item.SortOrder,
                FilingFactId = item.FilingFactId,
                ContainerId = item.ContainerId,
                ContainerCode = item.ContainerCode,
                BeforeStorageLocation = item.BeforeStorageLocation,
                SourceRegisterKind = item.SourceRegisterKind,
                DisposalReason = item.DisposalReason,
                DispositionMethod = ArchiveDisposalDomainValues.NormalizeDispositionMethod(item.DispositionMethod),
                MaterialName = item.MaterialName,
                ItemName = item.ItemName,
                FormNo = item.FormNo,
                InventoryLostCopyCount = item.InventoryLostCopyCount,
                InventoryScrapCopyCount = item.InventoryScrapCopyCount,
                BeforeLifecycleStatus = item.BeforeLifecycleStatus,
                MediumKind = item.MediumKind,
                MediumId = item.MediumId,
                MediumCode = item.MediumCode,
                ElectronicArchiveUnitId = item.ElectronicArchiveUnitId,
                ElectronicArchiveNo = item.ElectronicArchiveNo,
                BeforeMediaStatus = item.BeforeMediaStatus,
                TargetBlankSlotLocation = item.TargetBlankSlotLocation
            };
        }

        public YearlyArchiveDisposalItem ToEntity()
        {
            return new YearlyArchiveDisposalItem
            {
                Id = Id,
                SortOrder = SortOrder,
                FilingFactId = FilingFactId,
                ContainerId = ContainerId,
                ContainerCode = ContainerCode,
                BeforeStorageLocation = BeforeStorageLocation,
                SourceRegisterKind = SourceRegisterKind,
                DisposalReason = DisposalReason,
                DispositionMethod = ArchiveDisposalDomainValues.NormalizeDispositionMethod(DispositionMethod),
                MaterialName = MaterialName,
                ItemName = ItemName,
                FormNo = FormNo,
                InventoryLostCopyCount = InventoryLostCopyCount,
                InventoryScrapCopyCount = InventoryScrapCopyCount,
                BeforeLifecycleStatus = BeforeLifecycleStatus,
                MediumKind = MediumKind,
                MediumId = MediumId,
                MediumCode = MediumCode,
                ElectronicArchiveUnitId = ElectronicArchiveUnitId,
                ElectronicArchiveNo = ElectronicArchiveNo,
                BeforeMediaStatus = BeforeMediaStatus,
                TargetBlankSlotLocation = TargetBlankSlotLocation
            };
        }
    }
}
