using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 模拟资料盘库登记办理弹窗 ViewModel（B 流 · DisposalUnlockUpload，无照片）。
    /// </summary>
    public sealed partial class SimulatedArchiveInventoryRegisterEditDialogViewModel : ViewModelBase
    {
        private readonly IArchiveInventoryRegisterService _registerService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IUserService _userService;
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private readonly List<SimulatedInventoryCandidateRow> _candidatePool = new();
        private const string SlotFilterAll = "全部";

        private YearlyArchiveInventoryRegisterRecord _record;
        private bool _hasCommittedChanges;
        private bool _suppressReviewSignerPersist;
        private string _registerNo = string.Empty;
        private string _registerKind = ArchiveInventoryRegisterDomainValues.KindLost;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _filterKeyword = string.Empty;
        private string _selectedSlot = SlotFilterAll;
        private string _uploadCategory = ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm;
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
        private SimulatedInventoryItemRow? _selectedItem;

        public SimulatedArchiveInventoryRegisterEditDialogViewModel(
            IArchiveInventoryRegisterService registerService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IUserService userService,
            IApprovalWorkflowService approvalWorkflowService,
            YearlyArchiveInventoryRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _registerService = registerService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _userService = userService;
            _approvalWorkflowService = approvalWorkflowService;
            _record = record;

            MoveToRegisterCommand = new RelayCommand(
                _ => MoveToRegister(),
                _ => CanEditHeader && AvailableCandidates.Any(item => item.IsSelected));
            MoveToAvailableCommand = new RelayCommand(
                _ => MoveToAvailable(),
                _ => CanEditHeader && Items.Any(item => item.IsSelected));
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmUploadCommand = new RelayCommand(async _ => await ConfirmUploadAsync(), _ => CanConfirmUpload);
            UploadSignedFormAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            CaptureSignedFormAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            UploadOtherAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(ArchiveInventoryRegisterDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            CaptureOtherAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(ArchiveInventoryRegisterDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            DeleteAttachmentCommand = new RelayCommand(async item =>
            {
                if (item is not SystemAttachment att) return;
                var (ok, msg) = await _registerService.DeleteAttachmentAsync(att.Id, RequireUser());
                if (!ok) { _dialogService.ShowError(msg); return; }
                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
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
            $"模拟资料盘库登记 · {(string.IsNullOrWhiteSpace(RegisterNo) ? "待编单" : RegisterNo)} · {StatusDisplay}";

        public string StatusDisplay => ArchiveInventoryRegisterDomainValues.ToStatusDisplay(_record.Status);

        public string BannerText =>
            "模拟资料盘库登记：选择在库子项并填写登记类型/说明。流程：保存草稿 → 提交 → 打印签批单并线下签字 → 审批通过 → 确认可上传附件信息 → 分区上传签批单 → 确认办结。";

        public ObservableCollection<string> RegisterKindOptions { get; } = new();

        public ObservableCollection<string> SlotOptions { get; } = new();

        public ObservableCollection<SimulatedInventoryCandidateRow> AvailableCandidates { get; } = new();

        public ObservableCollection<SimulatedInventoryItemRow> Items { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<SystemAttachment> SignedFormAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> OtherAttachments { get; } = new();

        public string AvailableTitle => $"可选在库子项（{AvailableCandidates.Count}）";

        public string RegisterTitle => $"待登记子项（{Items.Count}）";

        public bool CanEditRegisterKind => CanEditHeader;

        /// <summary>拟销登记时「丢失份数」显示为「-」（此项为空，按可用份数全额拟销）。</summary>
        public bool IsScrapRegisterKind =>
            string.Equals(RegisterKind?.Trim(), ArchiveInventoryRegisterDomainValues.KindScrap, StringComparison.Ordinal);

        public string FilterKeyword
        {
            get => _filterKeyword;
            set
            {
                if (SetProperty(ref _filterKeyword, value))
                    RefreshAvailableCandidates();
            }
        }

        public string SelectedSlot
        {
            get => _selectedSlot;
            set
            {
                if (SetProperty(ref _selectedSlot, value ?? SlotFilterAll))
                    RefreshAvailableCandidates();
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
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(IsScrapRegisterKind));
                    SyncLostCopyDisplayMode();
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

        public SimulatedInventoryItemRow? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public bool CanEditHeader =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
            && _record.Status == YearlyArchiveInventoryRegisterRecord.StatusDraft;

        public bool CanSubmit => CanEditHeader;

        public bool CanPrint =>
            _record.Id > 0
            && _record.Status is not YearlyArchiveInventoryRegisterRecord.StatusDraft
                and not YearlyArchiveInventoryRegisterRecord.StatusWithdrawn
                and not YearlyArchiveInventoryRegisterRecord.StatusForceWithdrawn;

        public bool CanApprove =>
            _record.Status == YearlyArchiveInventoryRegisterRecord.StatusSubmitted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanConfirmUpload =>
            _record.Status == YearlyArchiveInventoryRegisterRecord.StatusApproved
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanSupplementOtherAttachments =>
            ApprovalWorkflowButtonSupport.CanSupplementOtherAttachments(
                _record.Status == YearlyArchiveInventoryRegisterRecord.StatusCompleted,
                ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

        public bool CanUploadAttachment =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
            && (CanSupplementOtherAttachments
                || _record.Status is YearlyArchiveInventoryRegisterRecord.StatusApproved
                    or YearlyArchiveInventoryRegisterRecord.StatusSignedUploaded);

        public bool CanUploadMandatoryAttachment =>
            CanUploadAttachment && !CanSupplementOtherAttachments;

        public bool CanUploadOtherAttachment => CanUploadAttachment;

        public string UploadAttachmentHintText => CanSupplementOtherAttachments
            ? "办结后仅可增补「其他附件」；不可删除已有附件。"
            : "请在「确认可上传附件信息」后分区上传签批单；办结前仍可继续补传。";

        public string ApproveHintText => CanApprove
            ? "请按线下签批结果执行审批通过；通过后点击「确认可上传附件信息」。"
            : "仅「已提交」状态可审批通过。";

        public string ConfirmUploadHintText => CanConfirmUpload
            ? "确认后可分区上传签批单。"
            : "请先执行「审批通过」。";

        public string CompleteHintText => CanComplete
            ? "确认办结后即时写入盘库台账效应。"
            : "请先上传签批单后再确认办结。";

        public bool CanComplete =>
            _record.Status == YearlyArchiveInventoryRegisterRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanEditReviewSigners =>
            _record.Status is YearlyArchiveInventoryRegisterRecord.StatusApproved
                or YearlyArchiveInventoryRegisterRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

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
                if (!SetProperty(ref _deptHead, value ?? string.Empty)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? DeptHeadDate
        {
            get => _deptHeadDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _deptHeadDate, clamped)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string ArchiveRoomHead
        {
            get => _archiveRoomHead;
            set
            {
                if (!SetProperty(ref _archiveRoomHead, value ?? string.Empty)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? ArchiveRoomHeadDate
        {
            get => _archiveRoomHeadDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _archiveRoomHeadDate, clamped)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string ProductionHead
        {
            get => _productionHead;
            set
            {
                if (!SetProperty(ref _productionHead, value ?? string.Empty)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? ProductionHeadDate
        {
            get => _productionHeadDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _productionHeadDate, clamped)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string ArchiveDeputyPresident
        {
            get => _archiveDeputyPresident;
            set
            {
                if (!SetProperty(ref _archiveDeputyPresident, value ?? string.Empty)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? ArchiveDeputyPresidentDate
        {
            get => _archiveDeputyPresidentDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _archiveDeputyPresidentDate, clamped)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string ProductionVicePresident
        {
            get => _productionVicePresident;
            set
            {
                if (!SetProperty(ref _productionVicePresident, value ?? string.Empty)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public DateTime? ProductionVicePresidentDate
        {
            get => _productionVicePresidentDate;
            set
            {
                var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
                if (!SetProperty(ref _productionVicePresidentDate, clamped)) return;
                _ = PersistReviewSignersAsync();
            }
        }

        public string UploadCategory
        {
            get => _uploadCategory;
            set => SetProperty(ref _uploadCategory, value);
        }

        public RelayCommand MoveToRegisterCommand { get; }
        public RelayCommand MoveToAvailableCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand PrintCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmUploadCommand { get; }
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
                RegisterKindOptions.Clear();
                foreach (var kind in ArchiveInventoryRegisterDomainValues.SimulatedRegisterKindOptions)
                    RegisterKindOptions.Add(kind);

                if (_record.Id > 0)
                {
                    var latest = await _registerService.GetRecordByIdAsync(_record.Id);
                    if (latest != null) _record = latest;
                }
                else if (string.IsNullOrWhiteSpace(_record.RegisterNo))
                {
                    _record.RegisterNo = await _registerService.GenerateNextRegisterNoAsync();
                }

                BindFromRecord();
                await ReloadSignerEnableFlagsAsync();
                if (CanEditHeader)
                    await ReloadCandidatePoolAsync();
                RebuildItemsFromRecord();
                RefreshAvailableCandidates();
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
                ? ArchiveInventoryRegisterDomainValues.KindLost
                : _record.RegisterKind.Trim();
            Reason = _record.Reason;
            Remark = _record.Remark;
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

                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(CanEditHeader));
                OnPropertyChanged(nameof(CanEditRegisterKind));
                OnPropertyChanged(nameof(IsScrapRegisterKind));
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
                OnPropertyChanged(nameof(CanEditReviewSigners));
                OnPropertyChanged(nameof(CanEditSigners));
                OnPropertyChanged(nameof(SignatureDateMin));

                if (CanSupplementOtherAttachments)
                    UploadCategory = ArchiveInventoryRegisterDomainValues.AttachmentCategoryOther;

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

        private void RebuildItemsFromRecord()
        {
            Items.Clear();
            foreach (var item in _record.Items.OrderBy(detail => detail.SortOrder))
                Items.Add(SimulatedInventoryItemRow.FromItem(item, IsScrapRegisterKind));
            if (IsScrapRegisterKind)
                SyncLostCopyDisplayMode();
            OnPropertyChanged(nameof(RegisterTitle));
        }

        private async Task ReloadCandidatePoolAsync()
        {
            int? currentId = _record.Id > 0 ? _record.Id : null;
            _candidatePool.Clear();
            var facts = await _registerService.GetSelectableSimulatedFilingFactsAsync(currentId);
            foreach (var fact in facts)
                _candidatePool.Add(SimulatedInventoryCandidateRow.FromFact(fact));
            RebuildSlotOptions();
        }

        private void RebuildSlotOptions()
        {
            string previous = SelectedSlot;
            SlotOptions.Clear();
            SlotOptions.Add(SlotFilterAll);
            foreach (string slot in _candidatePool
                .Select(ResolveSlotKey)
                .Where(slot => !string.IsNullOrWhiteSpace(slot))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(slot => slot, StringComparer.OrdinalIgnoreCase))
            {
                SlotOptions.Add(slot);
            }

            _selectedSlot = SlotOptions.Contains(previous, StringComparer.OrdinalIgnoreCase)
                ? previous
                : SlotFilterAll;
            OnPropertyChanged(nameof(SelectedSlot));
        }

        private static string ResolveSlotKey(SimulatedInventoryCandidateRow candidate)
        {
            string location = candidate.StorageLocation?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(location))
                return string.Empty;
            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(location);
            return string.IsNullOrWhiteSpace(slotKey) ? location : slotKey;
        }

        private void RefreshAvailableCandidates()
        {
            string keyword = FilterKeyword?.Trim() ?? string.Empty;
            string selectedSlot = SelectedSlot?.Trim() ?? SlotFilterAll;
            bool filterBySlot = !string.Equals(selectedSlot, SlotFilterAll, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(selectedSlot);
            HashSet<string> selectedKeys = Items.Select(item => item.SelectionKey).ToHashSet(StringComparer.Ordinal);

            AvailableCandidates.Clear();
            foreach (var candidate in _candidatePool.Where(item => !selectedKeys.Contains(item.SelectionKey)))
            {
                if (filterBySlot && !MatchesSelectedSlot(candidate.StorageLocation, selectedSlot))
                    continue;
                if (!string.IsNullOrWhiteSpace(keyword) && !candidate.MatchesKeyword(keyword))
                    continue;
                AvailableCandidates.Add(candidate);
            }

            OnPropertyChanged(nameof(AvailableTitle));
            OnPropertyChanged(nameof(RegisterTitle));
            OnPropertyChanged(nameof(CanSubmit));
            RaiseCommandStates();
        }

        private static bool MatchesSelectedSlot(string? storageLocation, string selectedSlot)
        {
            if (ArchiveSlotLocationSupport.IsSameSlot(storageLocation, selectedSlot))
                return true;
            return string.Equals(storageLocation?.Trim(), selectedSlot.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void MoveToRegister()
        {
            foreach (var candidate in AvailableCandidates.Where(item => item.IsSelected).ToList())
            {
                if (Items.Any(item => string.Equals(item.SelectionKey, candidate.SelectionKey, StringComparison.Ordinal)))
                    continue;
                Items.Add(SimulatedInventoryItemRow.FromCandidate(candidate, IsScrapRegisterKind));
            }

            RefreshAvailableCandidates();
        }

        private void MoveToAvailable()
        {
            foreach (var item in Items.Where(row => row.IsSelected).ToList())
                Items.Remove(item);
            RefreshAvailableCandidates();
        }

        private async Task SaveDraftAsync()
        {
            if (!CanEditHeader) return;
            try
            {
                await PersistDraftAsync();
                _dialogService.ShowMessage("草稿已保存。", "盘库登记");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PersistDraftAsync()
        {
            EnsureReasonFilled();
            var drafts = BuildDrafts();
            var header = BuildHeader();
            _record = _record.Id <= 0
                ? await _registerService.CreateDraftAsync(header, drafts, RequireUser())
                : await _registerService.UpdateDraftAsync(header, drafts, RequireUser());
            _hasCommittedChanges = true;
            BindFromRecord();
            RebuildItemsFromRecord();
            if (CanEditHeader)
            {
                await ReloadCandidatePoolAsync();
                RefreshAvailableCandidates();
            }
        }

        private async Task SubmitAsync()
        {
            try
            {
                EnsureReasonFilled();
                _ = BuildDrafts();
                await PersistDraftAsync();
                await _registerService.SubmitAsync(_record.Id, RequireUser());
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
                ArchiveInventoryRegisterPrintData data = await _registerService.BuildPrintDataAsync(_record.Id);
                FlowDocument document = ArchiveInventoryRegisterPrintDocumentFactory.Create(data);
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
                if (!_dialogService.ShowConfirm("确认审批通过？将按审批链自动填写启用节点的签字姓名与日期。"))
                    return;

                await _registerService.ApproveAsync(_record.Id, "同意", RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("审批已通过，审核审批姓名已填写，可按实际签字人修改。");
                await ReloadAsync();
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
                await PersistReviewSignersAsync();
                await _registerService.ConfirmReadyForUploadAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("已确认可上传附件信息。");
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
                if (!SignedFormAttachments.Any())
                {
                    _dialogService.ShowError("办结前须上传签批单附件。");
                    return;
                }

                if (!_dialogService.ShowConfirm("确认办结？办结后即时写入台账，不可再改。", "确认办结"))
                    return;

                await _registerService.CompleteAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("盘库登记已确认办结。", "确认办结");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void EnsureReasonFilled()
        {
            if (!string.IsNullOrWhiteSpace(Reason))
                return;

            string kind = RegisterKind?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(kind))
                throw new InvalidOperationException("请选择登记类型，或填写登记说明。");

            Reason = kind;
        }

        private YearlyArchiveInventoryRegisterRecord BuildHeader() =>
            new()
            {
                Id = _record.Id,
                RegisterNo = RegisterNo?.Trim() ?? string.Empty,
                MediaKind = ArchiveInventoryRegisterDomainValues.MediaKindSimulated,
                RegisterKind = RegisterKind?.Trim() ?? string.Empty,
                Reason = Reason?.Trim() ?? string.Empty,
                Remark = Remark?.Trim() ?? string.Empty,
                Status = YearlyArchiveInventoryRegisterRecord.StatusDraft
            };

        private List<ArchiveInventoryRegisterItemDraft> BuildDrafts()
        {
            if (Items.Count == 0)
                throw new InvalidOperationException("请至少选择一个资料子项。");

            bool isScrap = IsScrapRegisterKind;
            var drafts = new List<ArchiveInventoryRegisterItemDraft>();
            foreach (var item in Items)
            {
                int registerCopyCount = isScrap ? item.AvailableCopyCount : item.LostCopyCount;
                if (registerCopyCount <= 0)
                {
                    throw new InvalidOperationException(
                        isScrap
                            ? $"【{item.DisplayName}】可用份数须大于 0，无法拟销登记。"
                            : $"【{item.DisplayName}】丢失份数须大于 0。");
                }

                if (registerCopyCount > item.AvailableCopyCount)
                {
                    throw new InvalidOperationException(
                        $"【{item.DisplayName}】丢失份数 {registerCopyCount} 不能大于可用份数 {item.AvailableCopyCount}。");
                }

                drafts.Add(new ArchiveInventoryRegisterItemDraft
                {
                    FilingFactId = item.FilingFactId,
                    LostCopyCount = registerCopyCount
                });
            }

            return drafts;
        }

        private void SyncLostCopyDisplayMode()
        {
            bool showAsEmpty = IsScrapRegisterKind;
            foreach (var item in Items)
                item.ShowLostCopyAsEmpty = showAsEmpty;
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

        private async Task ReloadSignerEnableFlagsAsync()
        {
            var users = _userService.GetAllUsers();
            var chain = await _approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister,
                    FieldValues = ApprovalChainApplySupport.BuildYearlyArchiveInventoryRegisterFieldValues(_record)
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

        private async Task ReloadAttachmentsAsync()
        {
            IEnumerable<SystemAttachment> list = string.IsNullOrWhiteSpace(_record.RegisterNo)
                ? Array.Empty<SystemAttachment>()
                : await _registerService.GetAttachmentsAsync(_record.RegisterNo);
            var policy = ApprovalAttachmentPolicySupport.Get(ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister);
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

        private async Task UploadAttachmentAsync()
        {
            try
            {
                if (CanSupplementOtherAttachments)
                    UploadCategory = ArchiveInventoryRegisterDomainValues.AttachmentCategoryOther;

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
                    RequireUser());
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
                    UploadCategory = ArchiveInventoryRegisterDomainValues.AttachmentCategoryOther;

                if (string.IsNullOrWhiteSpace(UploadCategory))
                {
                    _dialogService.ShowMessage("请先选择附件分类。");
                    return;
                }

                DocumentCameraCaptureResult? captured = DocumentCameraAttachmentCaptureSupport.Capture(_dialogService);
                if (captured == null)
                    return;

                string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(RegisterNo, UploadCategory, "盘库");
                var (ok, message, _) = await _registerService.UploadAttachmentAsync(
                    _record.Id,
                    UploadCategory,
                    fileName,
                    ".jpg",
                    captured.JpegContent.LongLength,
                    captured.JpegContent,
                    RequireUser());
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

        private async Task ReloadAsync()
        {
            var latest = await _registerService.GetRecordByIdAsync(_record.Id);
            if (latest == null) return;
            _record = latest;
            BindFromRecord();
            await ReloadSignerEnableFlagsAsync();
            RebuildItemsFromRecord();
            if (CanEditHeader)
            {
                await ReloadCandidatePoolAsync();
                RefreshAvailableCandidates();
            }
            else
            {
                AvailableCandidates.Clear();
                OnPropertyChanged(nameof(AvailableTitle));
            }

            await ReloadAttachmentsAsync();
        }

        private void RaiseCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditRegisterKind));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanComplete));
        }

        private User RequireUser() =>
            _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
    }

    /// <summary>模拟盘库可选在库子项行。</summary>
    public sealed class SimulatedInventoryCandidateRow : ViewModelBase
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int FilingFactId { get; init; }
        public string ProjectName { get; init; } = string.Empty;
        public string Year { get; init; } = string.Empty;
        public string MaterialName { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string ContainerCode { get; init; } = string.Empty;
        public string StorageLocation { get; init; } = string.Empty;
        public int AvailableCopyCount { get; init; }
        public string SelectionKey => $"F:{FilingFactId}";

        public string DisplayName =>
            string.IsNullOrWhiteSpace(ItemName) ? MaterialName : $"{MaterialName}/{ItemName}";

        public bool MatchesKeyword(string keyword)
        {
            return ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || Year.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || MaterialName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ItemName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ContainerCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || StorageLocation.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        public static SimulatedInventoryCandidateRow FromFact(ArchiveInventorySelectableSimulatedFact fact)
        {
            return new SimulatedInventoryCandidateRow
            {
                FilingFactId = fact.FilingFactId,
                ProjectName = fact.ProjectName?.Trim() ?? string.Empty,
                Year = fact.Year?.Trim() ?? string.Empty,
                MaterialName = fact.MaterialName?.Trim() ?? string.Empty,
                ItemName = fact.ItemName?.Trim() ?? string.Empty,
                ContainerCode = fact.ContainerCode?.Trim() ?? string.Empty,
                StorageLocation = fact.StorageLocation?.Trim() ?? string.Empty,
                AvailableCopyCount = Math.Max(0, fact.AvailableCopyCount)
            };
        }
    }

    /// <summary>模拟盘库待登记子项行。</summary>
    public sealed class SimulatedInventoryItemRow : ViewModelBase
    {
        private bool _isSelected;
        private int _lostCopyCount = 1;
        private bool _showLostCopyAsEmpty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int FilingFactId { get; init; }
        public string ProjectName { get; init; } = string.Empty;
        public string Year { get; init; } = string.Empty;
        public string MaterialName { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string ContainerCode { get; init; } = string.Empty;
        public string StorageLocation { get; init; } = string.Empty;
        public int AvailableCopyCount { get; init; }

        public bool ShowLostCopyAsEmpty
        {
            get => _showLostCopyAsEmpty;
            set
            {
                if (SetProperty(ref _showLostCopyAsEmpty, value))
                {
                    OnPropertyChanged(nameof(LostCopyCountDisplay));
                    OnPropertyChanged(nameof(IsLostCopyCountEditable));
                }
            }
        }

        public bool IsLostCopyCountEditable => !ShowLostCopyAsEmpty;

        public int LostCopyCount
        {
            get => _lostCopyCount;
            set
            {
                if (SetProperty(ref _lostCopyCount, Math.Max(0, value)))
                    OnPropertyChanged(nameof(LostCopyCountDisplay));
            }
        }

        public string LostCopyCountDisplay
        {
            get => ShowLostCopyAsEmpty ? "-" : LostCopyCount.ToString();
            set
            {
                if (ShowLostCopyAsEmpty) return;
                string trimmed = value?.Trim() ?? string.Empty;
                if (int.TryParse(trimmed, out int parsed))
                    LostCopyCount = parsed;
            }
        }

        public string SelectionKey => $"F:{FilingFactId}";

        public string DisplayName =>
            string.IsNullOrWhiteSpace(ItemName) ? MaterialName : $"{MaterialName}/{ItemName}";

        public static SimulatedInventoryItemRow FromCandidate(
            SimulatedInventoryCandidateRow candidate,
            bool showLostCopyAsEmpty = false)
        {
            return new SimulatedInventoryItemRow
            {
                FilingFactId = candidate.FilingFactId,
                ProjectName = candidate.ProjectName,
                Year = candidate.Year,
                MaterialName = candidate.MaterialName,
                ItemName = candidate.ItemName,
                ContainerCode = candidate.ContainerCode,
                StorageLocation = candidate.StorageLocation,
                AvailableCopyCount = candidate.AvailableCopyCount,
                LostCopyCount = 1,
                ShowLostCopyAsEmpty = showLostCopyAsEmpty
            };
        }

        public static SimulatedInventoryItemRow FromItem(
            YearlyArchiveInventoryRegisterItem item,
            bool showLostCopyAsEmpty = false)
        {
            return new SimulatedInventoryItemRow
            {
                FilingFactId = item.FilingFactId,
                ProjectName = item.ProjectName?.Trim() ?? string.Empty,
                Year = item.Year?.Trim() ?? string.Empty,
                MaterialName = item.MaterialName?.Trim() ?? string.Empty,
                ItemName = item.ItemName?.Trim() ?? string.Empty,
                ContainerCode = item.ContainerCode?.Trim() ?? string.Empty,
                StorageLocation = item.BeforeStorageLocation?.Trim() ?? string.Empty,
                AvailableCopyCount = item.BeforeAvailableCopyCount,
                LostCopyCount = Math.Max(1, item.LostCopyCount),
                ShowLostCopyAsEmpty = showLostCopyAsEmpty
            };
        }
    }
}
