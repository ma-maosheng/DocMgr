using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘盘库登记办理弹窗 ViewModel（B 流：确认可上传）。
    /// </summary>
    public sealed partial class HardDiskInventoryRegisterEditDialogViewModel : ViewModelBase
    {
        private readonly IHardDiskInventoryRegisterService _registerService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IUserService _userService;
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private readonly List<HardDiskInventoryRegisterCandidateViewModel> _mediaPool = new();
        private HardDiskInventoryRegisterRecord _record;
        private bool _hasCommittedChanges;
        private bool _suppressReviewSignerPersist;
        private string _registerNo = string.Empty;
        private string _registerKind = HardDiskInventoryRegisterDomainValues.KindDamage;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _filterKeyword = string.Empty;
        private string _uploadCategory = HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm;
        private HardDiskInventoryRegisterItemViewModel? _selectedItem;
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
        private bool _enableDeptHead;
        private bool _enableArchiveRoomHead = true;
        private bool _enableProductionHead;
        private bool _enableArchiveDeputyPresident = true;
        private bool _enableProductionVicePresident;
        private bool _damagedDiskRelocationConfirmed;

        public HardDiskInventoryRegisterEditDialogViewModel(
            IHardDiskInventoryRegisterService registerService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IUserService userService,
            IApprovalWorkflowService approvalWorkflowService,
            HardDiskInventoryRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _registerService = registerService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _userService = userService;
            _approvalWorkflowService = approvalWorkflowService;
            _record = record;

            MoveToRegisterCommand = new RelayCommand(_ => MoveToRegister(), _ => CanEditHeader && AvailableDisks.Any(item => item.IsSelected));
            MoveToAvailableCommand = new RelayCommand(_ => MoveToAvailable(), _ => CanEditHeader && Items.Any(item => item.IsSelected));
            RecommendTargetLocationCommand = new RelayCommand<HardDiskInventoryRegisterItemViewModel>(
                async item => await RecommendTargetLocationAsync(item),
                item => CanEditHeader && RequiresTargetLocation && item != null);
            ShowTargetLocationPreviewCommand = new RelayCommand<HardDiskInventoryRegisterItemViewModel>(
                async item => await ShowTargetLocationPreviewAsync(item),
                item => CanEditHeader
                    && RequiresTargetLocation
                    && item != null
                    && !string.IsNullOrWhiteSpace(item.TargetStorageLocation));
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmUploadCommand = new RelayCommand(async _ => await ConfirmUploadAsync(), _ => CanConfirmUpload);
            UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
            CaptureFromDocumentCameraCommand = new RelayCommand(
                async _ => await CaptureFromDocumentCameraAsync(),
                _ => CanUploadAttachment);
            UploadSignedFormAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            CaptureSignedFormAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskInventoryRegisterDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            UploadOtherAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskInventoryRegisterDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            CaptureOtherAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskInventoryRegisterDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            DeleteAttachmentCommand = new RelayCommand(async item =>
            {
                if (item is not SystemAttachment att) return;
                var (ok, msg) = await _registerService.DeleteAttachmentAsync(att.Id, RequireCurrentUser());
                if (!ok) { _dialogService.ShowError(msg); return; }
                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
                await ReloadAsync();
            }, item => item is SystemAttachment && CanUploadMandatoryAttachment);
            ViewAttachmentCommand = new RelayCommand(item =>
            {
                if (item is SystemAttachment att) _dialogService.ShowSystemAttachmentView(att);
            }, item => item is SystemAttachment);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"硬盘盘库登记 · {(string.IsNullOrWhiteSpace(RegisterNo) ? "待编单" : RegisterNo)} · {StatusDisplay}";

        public string StatusDisplay => HardDiskInventoryRegisterDomainValues.ToStatusDisplay(_record.Status);

        public ObservableCollection<string> RegisterKindOptions { get; } = new(HardDiskInventoryRegisterDomainValues.RegisterKindOptions);

        public ObservableCollection<string> UploadCategoryOptions { get; } = new(HardDiskInventoryRegisterDomainValues.AttachmentCategoryOptions);

        public ObservableCollection<HardDiskInventoryRegisterCandidateViewModel> AvailableDisks { get; } = new();

        public ObservableCollection<HardDiskInventoryRegisterItemViewModel> Items { get; } = new();

        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> DamagedLocationOptions { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<SystemAttachment> SignedFormAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> OtherAttachments { get; } = new();

        public string AvailableDisksTitle => $"可选库内盘（{AvailableDisks.Count}）";

        public string RegisterDisksTitle => $"待登记硬盘（{Items.Count}）";

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

        public string RegisterNo
        {
            get => _registerNo;
            set => SetProperty(ref _registerNo, value);
        }

        public string RegisterKind
        {
            get => _registerKind;
            set
            {
                if (SetProperty(ref _registerKind, value))
                {
                    OnPropertyChanged(nameof(RequiresTargetLocation));
                    OnPropertyChanged(nameof(IsTargetLocationColumnReadOnly));
                    OnPropertyChanged(nameof(CanEditTargetLocation));
                    OnPropertyChanged(nameof(TargetLocationHint));
                    OnPropertyChanged(nameof(ShowDamagedDiskRelocationConfirm));
                    OnPropertyChanged(nameof(CanEditDamagedDiskRelocationConfirm));
                    OnPropertyChanged(nameof(CompleteHintText));
                    OnPropertyChanged(nameof(ConfirmUploadHintText));
                    SyncTargetStorageLocationsForRegisterKind();
                    RefreshAvailableDisks();
                    RaiseCommandStates();
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

        public string UploadCategory
        {
            get => _uploadCategory;
            set => SetProperty(ref _uploadCategory, value);
        }

        public HardDiskInventoryRegisterItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public bool RequiresTargetLocation =>
            HardDiskInventoryRegisterDomainValues.RequiresDamagedTargetLocation(RegisterKind);

        /// <summary>盘失登记或只读态下，目标档口列不可编辑。</summary>
        public bool IsTargetLocationColumnReadOnly => !RequiresTargetLocation || !CanEditHeader;

        /// <summary>损坏类登记且草稿可编时，目标档口下拉可选。</summary>
        public bool CanEditTargetLocation => RequiresTargetLocation && CanEditHeader;

        public string TargetLocationHint => RequiresTargetLocation
            ? "请为每块盘从下拉中选择损坏硬盘专用档口（可用行内「推荐」「预览」）。确认可上传附件信息时将同步台账档口与状态。"
            : "盘失登记无需归位档口，办结后清空存放位置。";

        public bool CanOperate =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanEditHeader =>
            CanOperate && _record.Status == HardDiskInventoryRegisterRecord.StatusDraft;

        public bool CanSubmit => CanEditHeader;

        public bool CanPrint =>
            _record.Id > 0
            && _record.Status is not HardDiskInventoryRegisterRecord.StatusDraft
                and not HardDiskInventoryRegisterRecord.StatusWithdrawn
                and not HardDiskInventoryRegisterRecord.StatusForceWithdrawn;

        public bool CanApprove =>
            CanOperate && _record.Status == HardDiskInventoryRegisterRecord.StatusSubmitted;

        public bool CanConfirmUpload =>
            CanOperate && _record.Status == HardDiskInventoryRegisterRecord.StatusApproved;

        /// <summary>办结后资料管理员可增补「其他附件」。</summary>
        public bool CanSupplementOtherAttachments =>
            ApprovalWorkflowButtonSupport.CanSupplementOtherAttachments(
                _record.Status == HardDiskInventoryRegisterRecord.StatusCompleted,
                CanOperate);

        public bool CanUploadAttachment =>
            CanOperate
            && (CanSupplementOtherAttachments
                || _record.Status is HardDiskInventoryRegisterRecord.StatusApproved
                    or HardDiskInventoryRegisterRecord.StatusSignedUploaded);

        /// <summary>签批单：仅确认可上传后、办结前可传。</summary>
        public bool CanUploadMandatoryAttachment =>
            CanUploadAttachment && !CanSupplementOtherAttachments;

        /// <summary>其他附件：确认可上传后及办结后均可增补。</summary>
        public bool CanUploadOtherAttachment => CanUploadAttachment;

        public string UploadAttachmentHintText => CanSupplementOtherAttachments
            ? "办结后仅可增补「其他附件」；不可删除已有附件。"
            : "请在「确认可上传附件信息」后分区上传签批单；办结前仍可继续补传。";

        public string ApproveHintText => CanApprove
            ? "请按线下签批结果执行审批通过；通过后点击「确认可上传附件信息」。"
            : "仅「已提交」状态可审批通过。";

        public string ConfirmUploadHintText => CanConfirmUpload
            ? (ShowDamagedDiskRelocationConfirm
                ? "损坏登记须先勾选「已完成损坏硬盘迁档」，再确认可上传附件信息（将同步台账档口）。"
                : "确认后可分区上传签批单。")
            : "请先执行「审批通过」。";

        public string CompleteHintText => CanComplete
            ? "确认办结后更新硬盘台账状态/档口并写入流转流水。"
            : "请先上传签批单后再确认办结。";

        /// <summary>损坏登记：审批通过后展示「已完成损坏硬盘迁档」勾选。</summary>
        public bool ShowDamagedDiskRelocationConfirm =>
            HardDiskInventoryRegisterDomainValues.RequiresDamagedDiskRelocationConfirm(RegisterKind);

        /// <summary>仅「已审批、待确认可上传」阶段可勾选；确认可上传附件信息后锁定。</summary>
        public bool CanEditDamagedDiskRelocationConfirm =>
            ShowDamagedDiskRelocationConfirm
            && CanOperate
            && _record.Status == HardDiskInventoryRegisterRecord.StatusApproved;

        public bool DamagedDiskRelocationConfirmed
        {
            get => _damagedDiskRelocationConfirmed;
            set
            {
                if (!CanEditDamagedDiskRelocationConfirm)
                {
                    return;
                }

                SetProperty(ref _damagedDiskRelocationConfirmed, value);
            }
        }

        public bool CanComplete =>
            CanOperate && _record.Status == HardDiskInventoryRegisterRecord.StatusSignedUploaded;

        /// <summary>已审批后可改审核审批人姓名。</summary>
        public bool CanEditReviewSigners =>
            CanOperate
            && _record.Status is HardDiskInventoryRegisterRecord.StatusApproved
                or HardDiskInventoryRegisterRecord.StatusSignedUploaded;

        /// <summary>签字卡可编辑：审批通过前或已审批后改签。</summary>
        public bool CanEditSigners => CanEditReviewSigners || CanApprove;

        public DateTime? SignatureDateMin =>
            ApprovalSignatureDateSupport.ResolveMinDate(_record.FirstPrintedAt, _record.LastPrintedAt, _record.PrintCount);

        public bool ShowDeptHead => _enableDeptHead;
        public bool ShowArchiveRoomHead => _enableArchiveRoomHead;
        public bool ShowProductionHead => _enableProductionHead;
        public bool ShowArchiveDeputyPresident => _enableArchiveDeputyPresident;
        public bool ShowProductionVicePresident => _enableProductionVicePresident;
        public bool ShowReviewSignerSection => ShowDeptHead || ShowArchiveRoomHead || ShowProductionHead;
        public bool ShowApproveSignerSection => ShowArchiveDeputyPresident || ShowProductionVicePresident;

        public string DeptHead
        {
            get => _deptHead;
            set
            {
                if (!SetProperty(ref _deptHead, value ?? string.Empty))
                    return;
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
                    return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string ArchiveRoomHead
        {
            get => _archiveRoomHead;
            set
            {
                if (!SetProperty(ref _archiveRoomHead, value ?? string.Empty))
                    return;
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
                    return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string ProductionHead
        {
            get => _productionHead;
            set
            {
                if (!SetProperty(ref _productionHead, value ?? string.Empty))
                    return;
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
                    return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string ArchiveDeputyPresident
        {
            get => _archiveDeputyPresident;
            set
            {
                if (!SetProperty(ref _archiveDeputyPresident, value ?? string.Empty))
                    return;
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
                    return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string ProductionVicePresident
        {
            get => _productionVicePresident;
            set
            {
                if (!SetProperty(ref _productionVicePresident, value ?? string.Empty))
                    return;
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
                    return;
                _ = PersistReviewSignersAsync();
            }
        }

        public RelayCommand MoveToRegisterCommand { get; }
        public RelayCommand MoveToAvailableCommand { get; }
        public RelayCommand<HardDiskInventoryRegisterItemViewModel> RecommendTargetLocationCommand { get; }
        public RelayCommand<HardDiskInventoryRegisterItemViewModel> ShowTargetLocationPreviewCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand PrintCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmUploadCommand { get; }
        public RelayCommand UploadAttachmentCommand { get; }
        public RelayCommand CaptureFromDocumentCameraCommand { get; }
        public RelayCommand UploadSignedFormAttachmentCommand { get; }
        public RelayCommand CaptureSignedFormAttachmentCommand { get; }
        public RelayCommand UploadOtherAttachmentCommand { get; }
        public RelayCommand CaptureOtherAttachmentCommand { get; }
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
                    var latest = await _registerService.GetRecordByIdAsync(_record.Id);
                    if (latest != null) _record = latest;
                }
                else if (string.IsNullOrWhiteSpace(_record.RegisterNo))
                {
                    _record.RegisterNo = await _registerService.GenerateNextRegisterNoAsync();
                }

                await EnsureDamagedLocationOptionsAsync();
                await BindFromRecordAsync();
                EnsurePersistedDamagedLocationsInOptions();
                await ReloadSignerEnableFlagsAsync();
                if (CanEditHeader)
                {
                    await ReloadMediaPoolAsync();
                    RefreshAvailableDisks();
                }

                await ReloadAttachmentsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void BindFromRecord()
        {
            RegisterNo = _record.RegisterNo;
            RegisterKind = string.IsNullOrWhiteSpace(_record.RegisterKind)
                ? HardDiskInventoryRegisterDomainValues.KindDamage
                : _record.RegisterKind;
            Reason = _record.Reason;
            Remark = _record.Remark;
            _damagedDiskRelocationConfirmed = _record.DamagedDiskRelocationConfirmed;
            OnPropertyChanged(nameof(DamagedDiskRelocationConfirmed));
            _suppressReviewSignerPersist = true;
            try
            {
                _deptHead = _record.DeptHead;
                _deptHeadDate = _record.DeptHeadDate;
                _archiveRoomHead = _record.ArchiveRoomHead;
                _archiveRoomHeadDate = _record.ArchiveRoomHeadDate;
                _productionHead = _record.ProductionHead;
                _productionHeadDate = _record.ProductionHeadDate;
                _archiveDeputyPresident = _record.ArchiveDeputyPresident;
                _archiveDeputyPresidentDate = _record.ArchiveDeputyPresidentDate;
                _productionVicePresident = _record.ProductionVicePresident;
                _productionVicePresidentDate = _record.ProductionVicePresidentDate;

                Items.Clear();
                foreach (var item in _record.Items.OrderBy(detail => detail.SortOrder))
                {
                    Items.Add(HardDiskInventoryRegisterItemViewModel.FromItem(item));
                }

                SyncTargetStorageLocationsForRegisterKind();

                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(CanEditHeader));
                OnPropertyChanged(nameof(CanSubmit));
                OnPropertyChanged(nameof(CanPrint));
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
                OnPropertyChanged(nameof(ShowDamagedDiskRelocationConfirm));
                OnPropertyChanged(nameof(CanEditDamagedDiskRelocationConfirm));
                OnPropertyChanged(nameof(CanEditReviewSigners));
                OnPropertyChanged(nameof(CanEditSigners));
                OnPropertyChanged(nameof(SignatureDateMin));
                OnPropertyChanged(nameof(RequiresTargetLocation));
                OnPropertyChanged(nameof(IsTargetLocationColumnReadOnly));
                OnPropertyChanged(nameof(CanEditTargetLocation));
                OnPropertyChanged(nameof(TargetLocationHint));
                OnPropertyChanged(nameof(AvailableDisksTitle));
                OnPropertyChanged(nameof(RegisterDisksTitle));

                if (CanSupplementOtherAttachments)
                {
                    UploadCategory = HardDiskInventoryRegisterDomainValues.AttachmentCategoryOther;
                }

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
                NotifyShellAliasPropertiesChanged();
                RaiseCommandStates();
            }
            finally
            {
                _suppressReviewSignerPersist = false;
            }
        }

        private Task BindFromRecordAsync()
        {
            BindFromRecord();
            return Task.CompletedTask;
        }

        private async Task ReloadMediaPoolAsync()
        {
            int? currentId = _record.Id > 0 ? _record.Id : null;
            var media = await _registerService.GetSelectableMediaAsync(currentId);
            _mediaPool.Clear();
            foreach (var medium in media)
            {
                _mediaPool.Add(HardDiskInventoryRegisterCandidateViewModel.FromMedium(medium));
            }
        }

        private void RefreshAvailableDisks()
        {
            HashSet<int> selectedIds = Items.Select(item => item.MediumId).ToHashSet();
            string keyword = FilterKeyword?.Trim() ?? string.Empty;
            string kind = RegisterKind?.Trim() ?? string.Empty;

            AvailableDisks.Clear();
            foreach (var candidate in _mediaPool.Where(item => !selectedIds.Contains(item.MediumId)))
            {
                string status = candidate.MediaStatus?.Trim() ?? string.Empty;
                bool compatible = string.Equals(kind, HardDiskInventoryRegisterDomainValues.KindDamage, StringComparison.Ordinal)
                    ? string.Equals(status, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal)
                    : string.Equals(kind, HardDiskInventoryRegisterDomainValues.KindRelocateDamaged, StringComparison.Ordinal)
                        ? string.Equals(status, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal)
                        : string.Equals(status, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal)
                          || string.Equals(status, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal);

                if (!compatible)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(keyword)
                    && !candidate.DiskCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !candidate.SerialNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !candidate.StorageLocation.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AvailableDisks.Add(candidate);
            }

            OnPropertyChanged(nameof(AvailableDisksTitle));
            OnPropertyChanged(nameof(RegisterDisksTitle));
            OnPropertyChanged(nameof(CanSubmit));
            RaiseCommandStates();
        }

        private void MoveToRegister()
        {
            var selected = AvailableDisks.Where(item => item.IsSelected).ToList();
            foreach (var candidate in selected)
            {
                if (Items.Any(item => item.MediumId == candidate.MediumId))
                {
                    continue;
                }

                Items.Add(HardDiskInventoryRegisterItemViewModel.FromCandidate(candidate));
            }

            RefreshAvailableDisks();
        }

        private void MoveToAvailable()
        {
            var selected = Items.Where(item => item.IsSelected).ToList();
            foreach (var item in selected)
            {
                Items.Remove(item);
            }

            RefreshAvailableDisks();
        }

        /// <summary>
        /// 切换登记类型时同步目标档口：盘失登记清空；损坏类登记保留已填档口。
        /// </summary>
        private void SyncTargetStorageLocationsForRegisterKind()
        {
            if (RequiresTargetLocation)
            {
                return;
            }

            foreach (var item in Items)
            {
                if (!string.IsNullOrWhiteSpace(item.TargetStorageLocation))
                {
                    item.TargetStorageLocation = string.Empty;
                }
            }
        }

        private async Task RecommendTargetLocationAsync(HardDiskInventoryRegisterItemViewModel? item)
        {
            if (item == null)
            {
                return;
            }

            SelectedItem = item;
            await EnsureDamagedLocationOptionsAsync();
            EnsurePersistedDamagedLocationsInOptions();
            if (DamagedLocationOptions.Count == 0)
            {
                _dialogService.ShowMessage("未找到损坏硬盘专用档口，请先在磁盘柜开柜界面完成设置。", "推荐档口");
                return;
            }

            var preferred = DamagedLocationOptions
                .Where(option =>
                {
                    int capacity = option.SlotCapacity > 0
                        ? option.SlotCapacity
                        : CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
                    return option.ExistingMediumCount < capacity;
                })
                .OrderBy(option => option.ExistingMediumCount)
                .ThenBy(option => option.Location, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (preferred == null)
            {
                _dialogService.ShowMessage("损坏硬盘专用档口均已满，请先腾出容量或新增专用档口。", "推荐档口");
                return;
            }

            item.TargetStorageLocation = preferred.Location;
            RaiseCommandStates();
            _dialogService.ShowMessage($"已推荐档口：{preferred.DisplayText}", "推荐档口");
        }

        private async Task ShowTargetLocationPreviewAsync(HardDiskInventoryRegisterItemViewModel? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.TargetStorageLocation))
            {
                _dialogService.ShowMessage("请先填写目标档口后再预览。", "档口预览");
                return;
            }

            SelectedItem = item;
            if (!TryParseCabinetLocation(item.TargetStorageLocation, out string cabinetName, out CabinetFace face, out string slotCode))
            {
                _dialogService.ShowMessage("目标档口无法解析，请核对格式（如：柜名A-1-2）后再预览。", "档口预览");
                return;
            }

            var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                .FirstOrDefault(c =>
                    c.Type == CabinetType.MagneticDisk
                    && string.Equals(c.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                _dialogService.ShowMessage($"未找到柜号 [{cabinetName}] 对应的防磁磁盘柜。", "档口预览");
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

        private async Task EnsureDamagedLocationOptionsAsync()
        {
            var options = await _registerService.GetDamagedTargetLocationOptionsAsync();
            DamagedLocationOptions.Clear();
            foreach (var option in options)
            {
                DamagedLocationOptions.Add(option);
            }
        }

        /// <summary>
        /// 将明细中已保存、但当前选项列表未包含的目标档口补入下拉，避免只读/重开后显示为空。
        /// </summary>
        private void EnsurePersistedDamagedLocationsInOptions()
        {
            foreach (var item in Items)
            {
                string location = item.TargetStorageLocation?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(location))
                {
                    continue;
                }

                if (DamagedLocationOptions.Any(option =>
                        string.Equals(option.Location, location, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                DamagedLocationOptions.Insert(0, new HardDiskMediaReturnTargetLocationOption
                {
                    Location = location,
                    ExistingMediumCount = 0
                });
            }
        }

        private static bool TryParseCabinetLocation(string? location, out string cabinetName, out CabinetFace face, out string slotCode)
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

        private async Task SaveDraftAsync()
        {
            try
            {
                await PersistDraftAsync();
                _dialogService.ShowMessage("草稿已保存。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PersistDraftAsync()
        {
            EnsureReasonFilled();
            var user = RequireCurrentUser();
            var payload = BuildDraftPayload();
            var itemDrafts = BuildItemDrafts();

            _record = _record.Id <= 0
                ? await _registerService.CreateDraftAsync(payload, itemDrafts, user)
                : await _registerService.UpdateDraftAsync(payload, itemDrafts, user);

            _hasCommittedChanges = true;
            await BindFromRecordAsync();
            EnsurePersistedDamagedLocationsInOptions();
            if (CanEditHeader)
            {
                await ReloadMediaPoolAsync();
                RefreshAvailableDisks();
            }
        }

        private async Task SubmitAsync()
        {
            try
            {
                if (Items.Count == 0)
                {
                    _dialogService.ShowError("请至少选择一块硬盘后再提交。");
                    return;
                }

                await PersistDraftAsync();
                await _registerService.SubmitAsync(_record.Id, RequireCurrentUser());
                _hasCommittedChanges = true;
                await ReloadAsync();
                _dialogService.ShowMessage("已提交，可打印签批单。");
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
                await PersistReviewSignersAsync();
                HardDiskInventoryRegisterPrintData data = await _registerService.BuildPrintDataAsync(_record.Id);
                FlowDocument document = HardDiskInventoryRegisterPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };
                await _registerService.RecordPrintAsync(_record.Id);
                previewWindow.ShowDialog();
                await ReloadAsync();
                _hasCommittedChanges = true;
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
                if (!_dialogService.ShowConfirm("确认审批通过？将自动填写资料室签字、分管资料院长签字的姓名与日期。"))
                    return;

                await _registerService.ApproveAsync(_record.Id, "同意", RequireCurrentUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("审批已通过，审核审批姓名已填写，可按实际签字人修改。");
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PersistReviewSignersAsync()
        {
            if (_suppressReviewSignerPersist || !CanEditReviewSigners || _record.Id <= 0)
                return;

            try
            {
                await _registerService.UpdateReviewSignersAsync(
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

        private async Task ReloadSignerEnableFlagsAsync()
        {
            var users = _userService.GetAllUsers();
            var chain = await _approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister,
                    ApplicantDept = _record.ApplicantDept,
                    FieldValues = ApprovalChainApplySupport.BuildHardDiskInventoryRegisterFieldValues(_record)
                },
                users);
            _enableDeptHead = chain.DeptHead.IsEnabled;
            _enableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled;
            _enableProductionHead = chain.ProductionHead.IsEnabled;
            _enableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled;
            _enableProductionVicePresident = chain.ProductionVicePresident.IsEnabled;
            OnPropertyChanged(nameof(ShowDeptHead));
            OnPropertyChanged(nameof(ShowArchiveRoomHead));
            OnPropertyChanged(nameof(ShowProductionHead));
            OnPropertyChanged(nameof(ShowArchiveDeputyPresident));
            OnPropertyChanged(nameof(ShowProductionVicePresident));
            OnPropertyChanged(nameof(ShowReviewSignerSection));
            OnPropertyChanged(nameof(ShowApproveSignerSection));
        }

        private async Task ConfirmUploadAsync()
        {
            try
            {
                if (ShowDamagedDiskRelocationConfirm && !DamagedDiskRelocationConfirmed)
                {
                    _dialogService.ShowError("请勾选确认：已完成损坏硬盘迁档。");
                    return;
                }

                await PersistReviewSignersAsync();
                await _registerService.ConfirmReadyForUploadAsync(
                    _record.Id,
                    RequireCurrentUser(),
                    DamagedDiskRelocationConfirmed);
                _hasCommittedChanges = true;
                _dialogService.ShowMessage(
                    ShowDamagedDiskRelocationConfirm
                        ? "已确认可上传附件信息，损坏硬盘档口与状态已同步至台账。"
                        : "已确认可上传附件信息。");
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
                await PersistReviewSignersAsync();
                await ReloadAttachmentsAsync();

                if (!_dialogService.ShowConfirm("确认办结？办结后将更新硬盘台账状态/档口并写入流转流水。", "确认办结"))
                {
                    return;
                }

                await _registerService.CompleteAsync(_record.Id, RequireCurrentUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("盘库登记已办结。");
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
                if (CanSupplementOtherAttachments)
                {
                    UploadCategory = HardDiskInventoryRegisterDomainValues.AttachmentCategoryOther;
                }

                string? path = _dialogService.OpenFileDialog("所有文件|*.*", "选择附件");
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
                byte[] content = await File.ReadAllBytesAsync(path);
                var (ok, message, _) = await _registerService.UploadAttachmentAsync(
                    _record.Id,
                    UploadCategory,
                    Path.GetFileName(path),
                    Path.GetExtension(path),
                    content.LongLength,
                    content,
                    RequireCurrentUser());
                if (!ok) { _dialogService.ShowError(message); return; }
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

        private async Task CaptureFromDocumentCameraAsync()
        {
            try
            {
                if (CanSupplementOtherAttachments)
                {
                    UploadCategory = HardDiskInventoryRegisterDomainValues.AttachmentCategoryOther;
                }

                if (string.IsNullOrWhiteSpace(UploadCategory))
                {
                    _dialogService.ShowMessage("请先选择附件分类。");
                    return;
                }

                DocumentCameraCaptureResult? captured = DocumentCameraAttachmentCaptureSupport.Capture(_dialogService);
                if (captured == null)
                {
                    return;
                }

                string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(RegisterNo, UploadCategory, "盘库登");
                var (ok, message, _) = await _registerService.UploadAttachmentAsync(
                    _record.Id,
                    UploadCategory,
                    fileName,
                    ".jpg",
                    captured.JpegContent.LongLength,
                    captured.JpegContent,
                    RequireCurrentUser());
                if (!ok) { _dialogService.ShowError(message); return; }
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

        private async Task ReloadAttachmentsAsync()
        {
            IEnumerable<SystemAttachment> list = string.IsNullOrWhiteSpace(_record.RegisterNo)
                ? Array.Empty<SystemAttachment>()
                : await _registerService.GetAttachmentsAsync(_record.RegisterNo);
            var policy = ApprovalAttachmentPolicySupport.Get(ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister);
            ApprovalAttachmentPolicySupport.Partition(
                policy,
                list,
                Attachments,
                SignedFormAttachments,
                photos: null,
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

        private async Task ReloadAsync()
        {
            var latest = await _registerService.GetRecordByIdAsync(_record.Id);
            if (latest == null) return;
            _record = latest;
            await BindFromRecordAsync();
            EnsurePersistedDamagedLocationsInOptions();
            await ReloadSignerEnableFlagsAsync();
            if (CanEditHeader)
            {
                await ReloadMediaPoolAsync();
                RefreshAvailableDisks();
            }
            else
            {
                AvailableDisks.Clear();
                OnPropertyChanged(nameof(AvailableDisksTitle));
            }

            await ReloadAttachmentsAsync();
        }

        /// <summary>
        /// 登记说明未填时，默认用当前登记类型，避免提交被硬拦。
        /// </summary>
        private void EnsureReasonFilled()
        {
            if (!string.IsNullOrWhiteSpace(Reason))
            {
                return;
            }

            string kind = RegisterKind?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(kind))
            {
                throw new InvalidOperationException("请选择登记类型，或填写登记说明。");
            }

            Reason = kind;
        }

        private HardDiskInventoryRegisterRecord BuildDraftPayload()
        {
            return new HardDiskInventoryRegisterRecord
            {
                Id = _record.Id,
                RegisterNo = RegisterNo,
                RegisterKind = RegisterKind,
                Reason = Reason,
                Remark = Remark
            };
        }

        private List<HardDiskInventoryRegisterItemDraft> BuildItemDrafts()
        {
            return Items
                .Select(item => new HardDiskInventoryRegisterItemDraft
                {
                    MediumId = item.MediumId,
                    TargetStorageLocation = RequiresTargetLocation ? item.TargetStorageLocation : string.Empty
                })
                .ToList();
        }

        private User RequireCurrentUser()
        {
            return _userContextService.CurrentUser
                ?? throw new InvalidOperationException("当前用户无效，请重新登录。");
        }

        private void RaiseCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>库内可选硬盘行。</summary>
    public sealed class HardDiskInventoryRegisterCandidateViewModel : ViewModelBase
    {
        private bool _isSelected;

        public static HardDiskInventoryRegisterCandidateViewModel FromMedium(HardDiskMedium medium)
        {
            ArgumentNullException.ThrowIfNull(medium);
            return new HardDiskInventoryRegisterCandidateViewModel
            {
                MediumId = medium.Id,
                DiskCode = medium.DiskCode?.Trim() ?? string.Empty,
                SerialNumber = medium.SerialNumber?.Trim() ?? string.Empty,
                DiskType = medium.DiskType?.Trim() ?? string.Empty,
                Brand = medium.Brand?.Trim() ?? string.Empty,
                Capacity = medium.Capacity?.Trim() ?? string.Empty,
                FactoryDate = medium.FactoryDate,
                MediaStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty,
                StorageLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty
            };
        }

        public int MediumId { get; init; }
        public string DiskCode { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string DiskType { get; init; } = string.Empty;
        public string Brand { get; init; } = string.Empty;
        public string Capacity { get; init; } = string.Empty;
        public DateTime? FactoryDate { get; init; }
        public string FactoryDateText => FactoryDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        public string MediaStatus { get; init; } = string.Empty;
        public string StorageLocation { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    /// <summary>待登记明细行。</summary>
    public sealed class HardDiskInventoryRegisterItemViewModel : ViewModelBase
    {
        private bool _isSelected;
        private string _targetStorageLocation = string.Empty;

        public static HardDiskInventoryRegisterItemViewModel FromCandidate(HardDiskInventoryRegisterCandidateViewModel candidate)
        {
            return new HardDiskInventoryRegisterItemViewModel
            {
                MediumId = candidate.MediumId,
                DiskCode = candidate.DiskCode,
                SerialNumber = candidate.SerialNumber,
                DiskType = candidate.DiskType,
                Capacity = candidate.Capacity,
                FactoryDate = candidate.FactoryDate,
                BeforeMediaStatus = candidate.MediaStatus,
                BeforeStorageLocation = candidate.StorageLocation
            };
        }

        public static HardDiskInventoryRegisterItemViewModel FromItem(HardDiskInventoryRegisterItem item)
        {
            var medium = item.Medium;
            return new HardDiskInventoryRegisterItemViewModel
            {
                MediumId = item.MediumId,
                DiskCode = item.DiskCode,
                SerialNumber = item.SerialNumber,
                DiskType = medium?.DiskType?.Trim() ?? string.Empty,
                Capacity = medium?.Capacity?.Trim() ?? string.Empty,
                FactoryDate = medium?.FactoryDate,
                BeforeMediaStatus = item.BeforeMediaStatus,
                BeforeStorageLocation = item.BeforeStorageLocation,
                TargetStorageLocation = item.TargetStorageLocation
            };
        }

        public int MediumId { get; init; }
        public string DiskCode { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string DiskType { get; init; } = string.Empty;
        public string Capacity { get; init; } = string.Empty;
        public DateTime? FactoryDate { get; init; }
        public string FactoryDateText => FactoryDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        public string BeforeMediaStatus { get; init; } = string.Empty;
        public string BeforeStorageLocation { get; init; } = string.Empty;

        public string TargetStorageLocation
        {
            get => _targetStorageLocation;
            set
            {
                if (SetProperty(ref _targetStorageLocation, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
