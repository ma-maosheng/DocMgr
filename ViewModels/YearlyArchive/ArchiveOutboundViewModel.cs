using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using DocMgr.Views.Shared;
using DocMgr.Views.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using Microsoft.Win32;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveOutboundViewModel : ViewModelBase
    {
        private readonly IArchiveOutboundService _outboundService;
        private readonly IArchiveOutboundWordExportService _outboundWordExportService;
        private readonly IArchiveFilingSearchService _searchService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private ArchiveOutboundWorkspaceMode _workspaceMode = ArchiveOutboundWorkspaceMode.Application;

        private YearlyArchiveOutboundRecord _record = new();
        private bool _isBusy;
        private bool _hasProofMaterialSelected;

        public ArchiveOutboundViewModel(
            IArchiveOutboundService outboundService,
            IArchiveOutboundWordExportService outboundWordExportService,
            IArchiveFilingSearchService searchService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _outboundService = outboundService;
            _outboundWordExportService = outboundWordExportService;
            _searchService = searchService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            Items = new ObservableCollection<YearlyArchiveOutboundItem>();

            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanSaveApplicationDraft);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmitApplication);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => _record.CanApplicantWithdraw);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrintApplication);
            RegisterItemsFromResultSetCommand = new RelayCommand(async _ => await RegisterItemsFromResultSetAsync(), _ => CanEditApplicationHeader);
            ExpandAllItemDetailsCommand = new RelayCommand(_ => SetAllItemDetailsExpanded(true), _ => HasContainerUnits);
            CollapseAllItemDetailsCommand = new RelayCommand(_ => SetAllItemDetailsExpanded(false), _ => HasContainerUnits);
            SaveApprovalCommand = new RelayCommand(async _ => await SaveApprovalAsync(), _ => CanSaveApproval);
            PrintApprovalCommand = new RelayCommand(async _ => await PrintApprovalAsync(), _ => CanPrintApproval);
            UploadSignedApprovalCommand = new RelayCommand(
                async _ => await UploadAttachmentAsync(ArchiveOutboundDomainValues.AttachmentKindSignedApprovalForm),
                _ => CanUploadSignedAttachment);
            UploadProofMaterialScanCommand = new RelayCommand(
                async _ => await UploadAttachmentAsync(ArchiveOutboundDomainValues.AttachmentKindProofMaterialScan),
                _ => CanManageProofMaterialAttachments);
            CompleteApprovalPhaseCommand = new RelayCommand(async _ => await CompleteApprovalPhaseAsync(), _ => CanCompleteApprovalPhase);
            ViewAttachmentCommand = new RelayCommand(async param => await ViewAttachmentAsync(param as SystemAttachment));
            DeleteAttachmentCommand = new RelayCommand(
                async param => await DeleteAttachmentAsync(param as SystemAttachment),
                param => CanDeleteAttachment(param as SystemAttachment));
            UploadHandoverCommand = new RelayCommand(
                async _ => await UploadAttachmentAsync(ArchiveOutboundDomainValues.AttachmentKindSignedHandoverForm),
                _ => CanManageHandoverAttachments);
            UploadMaterialPhotoCommand = new RelayCommand(
                async _ => await UploadAttachmentAsync(ArchiveOutboundDomainValues.AttachmentKindMaterialPhoto),
                _ => CanManageHandoverAttachments);
            PrintHandoverCommand = new RelayCommand(async _ => await PrintHandoverAsync(), _ => CanPrintHandover);
            CompleteHandoverCommand = new RelayCommand(async _ => await CompleteHandoverAsync(), _ => CanCompleteHandover);
            OpenBusinessAssistantCommand = new RelayCommand(_ => OpenBusinessAssistant(), _ => CanOpenBusinessAssistant);
            CloseCommand = new RelayCommand(_ => CloseWithCommit(false));
        }

        public event Action<bool?>? RequestClose;

        public ObservableCollection<YearlyArchiveOutboundItem> Items { get; }

        public ObservableCollection<ArchiveOutboundItemRowViewModel> ItemRows { get; } = new();

        public ObservableCollection<ArchiveOutboundContainerUnitViewModel> ContainerUnits { get; } = new();

        public int ContainerUnitCount => ContainerUnits.Count;

        public bool HasContainerUnits => ContainerUnits.Count > 0;

        public string ContainerUnitsSummary =>
            ContainerUnits.Count == 0
                ? "尚未登记拟领用资料"
                : $"共 {ContainerUnits.Count} 个盒/袋，{Items.Count} 条资料";

        public ObservableCollection<SystemAttachment> ProofMaterialAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> SignedApprovalAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> HandoverFormAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> MaterialPhotoAttachments { get; } = new();

        private string _handoverRemark = string.Empty;

        public string DestinationKind
        {
            get => Record.DestinationKind;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value)
                    ? ArchiveOutboundDomainValues.DestinationInternal
                    : value.Trim();

                if (string.Equals(Record.DestinationKind, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                if (!string.Equals(normalized, ArchiveOutboundDomainValues.DestinationExternal, StringComparison.Ordinal))
                {
                    Record.ExternalUnit = string.Empty;
                }

                Record.DestinationKind = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowExternalUnit));
                OnPropertyChanged(nameof(Record));
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
                    Record.ProofMaterialNote = ArchiveOutboundDomainValues.ProofMaterialNoneText;
                }
                else if (string.Equals(
                             Record.ProofMaterialNote?.Trim(),
                             ArchiveOutboundDomainValues.ProofMaterialNoneText,
                             StringComparison.Ordinal)
                         || string.IsNullOrWhiteSpace(Record.ProofMaterialNote))
                {
                    Record.ProofMaterialNote = string.Empty;
                }

                NotifyProofMaterialStateChanged();
            }
        }

        public string ProofMaterialName
        {
            get => HasProofMaterial ? Record.ProofMaterialNote?.Trim() ?? string.Empty : string.Empty;
            set
            {
                if (!HasProofMaterial)
                {
                    return;
                }

                string normalized = value?.Trim() ?? string.Empty;
                if (string.Equals(Record.ProofMaterialNote, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                Record.ProofMaterialNote = normalized;
                NotifyProofMaterialStateChanged();
            }
        }

        private void NotifyProofMaterialStateChanged()
        {
            OnPropertyChanged(nameof(HasProofMaterial));
            OnPropertyChanged(nameof(ProofMaterialName));
            OnPropertyChanged(nameof(ProofMaterialDisplay));
            OnPropertyChanged(nameof(RequiresProofMaterialScanUpload));
            OnPropertyChanged(nameof(ShowProofMaterialAttachmentSection));
            OnPropertyChanged(nameof(CanManageProofMaterialAttachments));
            OnPropertyChanged(nameof(Record));
            RefreshApprovalCommandStates();
        }

        public bool ShowExternalUnit =>
            string.Equals(DestinationKind, ArchiveOutboundDomainValues.DestinationExternal, StringComparison.Ordinal);

        public bool ShowApprovalApplicationInfo => ShowApprovalActions;

        public string ApplyDateDisplay =>
            Record.ApplyDate == default ? string.Empty : Record.ApplyDate.ToString("yyyy-MM-dd");

        public string DestinationDisplayText
        {
            get
            {
                if (ShowExternalUnit)
                {
                    string unit = Record.ExternalUnit?.Trim() ?? string.Empty;
                    return string.IsNullOrWhiteSpace(unit)
                        ? "外部（单位）"
                        : $"外部（单位）：{unit}";
                }

                return "本部门（内部）";
            }
        }

        public string ExpectedReturnDateDisplay =>
            Record.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "无";

        public string ProofMaterialDisplay
        {
            get
            {
                if (!ArchiveOutboundDomainValues.HasProofMaterial(Record.ProofMaterialNote))
                {
                    return ArchiveOutboundDomainValues.ProofMaterialNoneText;
                }

                return Record.ProofMaterialNote.Trim();
            }
        }

        public string ExpectedReturnDateHint =>
            "需归还的提档资料或需归还的库内硬盘，请在对应盒/袋的领用设置中填写预计归还日期。";

        public string HandoverRemark
        {
            get => _handoverRemark;
            set => SetProperty(ref _handoverRemark, value);
        }

        public string HandoverRemarkHint =>
            "请填写实物交接时的补充说明（可选），将打印在交接单「备注」栏，并在资料出库办结时保存。";

        public YearlyArchiveOutboundRecord Record
        {
            get => _record;
            private set
            {
                _record = value;
                OnPropertyChanged(nameof(Record));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }

        public string WindowTitle => $"资料借出 · {Record.OutboundNo} · {StatusDisplay}";

        /// <summary>审批工作台顶部流程说明文案。</summary>
        public string ApprovalWorkspaceBannerText =>
            "请先查看申请信息与拟领用资料明细，再按“审批通过→上传签字件→审批阶段确认办结→打印审批单”的顺序办理。";

        public string ApproveHintText => CanSaveApproval
            ? "后续：审批通过后，请上传签字件。"
            : "仅「已提交」且审批信息完整时可执行审批通过。";

        public string UploadHintText => CanUploadSignedAttachment
            ? "后续：上传签字件后，请点击「审批阶段确认办结」。"
            : "请先执行「审批通过」，再上传签字件。";

        public string CompleteHintText => CanCompleteApprovalPhase
            ? "确认办结后，可打印审批单。"
            : "请先审批通过后再确认办结。";

        public string PrintApprovalHintText => CanPrintApproval
            ? "可打印审批单供线下签字或归档留存。"
            : "请先完成「审批阶段确认办结」，再打印审批单。";

        public string StatusDisplay => Record.StatusStr;

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public RelayCommand SaveDraftCommand { get; }

        public RelayCommand SubmitCommand { get; }

        public RelayCommand WithdrawCommand { get; }

        public RelayCommand PrintCommand { get; }

        public RelayCommand SaveApprovalCommand { get; }

        public RelayCommand PrintApprovalCommand { get; }

        public RelayCommand UploadSignedApprovalCommand { get; }

        public RelayCommand UploadProofMaterialScanCommand { get; }

        public RelayCommand CompleteApprovalPhaseCommand { get; }

        public RelayCommand ViewAttachmentCommand { get; }

        public RelayCommand DeleteAttachmentCommand { get; }

        public RelayCommand UploadHandoverCommand { get; }

        public RelayCommand UploadMaterialPhotoCommand { get; }

        public RelayCommand PrintHandoverCommand { get; }

        public RelayCommand CompleteHandoverCommand { get; }

        public RelayCommand OpenBusinessAssistantCommand { get; }

        public RelayCommand CloseCommand { get; }

        public RelayCommand RegisterItemsFromResultSetCommand { get; }

        public RelayCommand ExpandAllItemDetailsCommand { get; }

        public RelayCommand CollapseAllItemDetailsCommand { get; }

        public void SetWorkspaceMode(ArchiveOutboundWorkspaceMode mode) => _workspaceMode = mode;

        public bool ShowApplicationActions => _workspaceMode == ArchiveOutboundWorkspaceMode.Application;

        public bool ShowApprovalActions => _workspaceMode == ArchiveOutboundWorkspaceMode.Approval;

        public bool ShowHandoverActions => _workspaceMode == ArchiveOutboundWorkspaceMode.Handover;

        public bool ShowHandoverWorkspaceContent =>
            _workspaceMode == ArchiveOutboundWorkspaceMode.Handover
            && (_record.IsSignedUploaded || _record.IsCompleted);

        public bool CanManageHandoverAttachments =>
            ShowHandoverWorkspaceContent && _record.IsSignedUploaded;

        public bool CanEditHandoverRemark => CanManageHandoverAttachments;

        public bool CanPrintHandover => ShowHandoverWorkspaceContent;

        public bool CanCompleteHandover =>
            CanManageHandoverAttachments && IsHandoverAttachmentsReadyForComplete();

        public bool CanOpenBusinessAssistant => ShowHandoverWorkspaceContent;

        /// <summary>是否可执行「审批通过」。</summary>
        public bool CanSaveApproval => ResolveApprovalButtonState().CanApprovePass;

        /// <summary>是否可打印审批单（确认办结后）。</summary>
        public bool CanPrintApproval => ResolveApprovalButtonState().CanPrintApprovalForm;

        /// <summary>是否可上传签字件（审批通过后）。</summary>
        public bool CanUploadSignedAttachment => ResolveApprovalButtonState().CanUploadSignedAttachment;

        /// <summary>是否可确认办结（审批通过后；具体校验在点击时执行）。</summary>
        public bool CanCompleteApprovalPhase => ResolveApprovalButtonState().CanConfirmComplete;

        /// <summary>是否可管理审批附件（与上传签字件同阶段）。</summary>
        public bool CanManageApprovalAttachments => CanUploadSignedAttachment;

        /// <summary>仅「已提交、未审批」阶段可编辑审批字段。</summary>
        public bool CanEditApprovalFields => CanSaveApproval;

        public bool ShowProofMaterialAttachmentSection => RequiresProofMaterialScanUpload;

        public bool RequiresProofMaterialScanUpload =>
            ArchiveOutboundDomainValues.RequiresProofMaterialScan(Record.ProofMaterialNote);

        public bool CanManageProofMaterialAttachments =>
            CanViewApprovalWorkspace()
            && RequiresProofMaterialScanUpload
            && (_record.IsSubmitted || _record.IsApproved);

        public bool CanEditApplicationHeader =>
            _workspaceMode == ArchiveOutboundWorkspaceMode.Application && _record.IsDraft;

        public bool CanSaveApplicationDraft =>
            _workspaceMode == ArchiveOutboundWorkspaceMode.Application
            && ResolveApplicationFormActions().CanSaveDraft;

        public bool CanSubmitApplication =>
            _workspaceMode == ArchiveOutboundWorkspaceMode.Application
            && ResolveApplicationFormActions().CanSubmitApplication;

        public bool CanPrintApplication =>
            _workspaceMode == ArchiveOutboundWorkspaceMode.Application
            && ResolveApplicationFormActions().CanPrintApplication;

        public bool IsItemsReadOnly => !CanEditApplicationHeader;

        public bool HasCommittedChanges { get; private set; }

        private void CloseWithCommit(bool committed)
        {
            HasCommittedChanges = committed;
            RequestClose?.Invoke(committed);
        }

        public async Task InitializeAsync(int? recordId, YearlyArchiveOutboundRecord? draftRecord = null)
        {
            HasCommittedChanges = false;

            if (recordId is int id && id > 0)
            {
                var record = await _outboundService.GetRecordAsync(id)
                    ?? throw new InvalidOperationException("未找到借出申请单。");

                ApplyRecord(record);
                await LoadAttachmentsAsync();
                await TryAutoFillDefaultApprovalInfoAsync();
                return;
            }

            if (draftRecord != null)
            {
                ApplyRecord(draftRecord);
                return;
            }

            throw new InvalidOperationException("无效的记录 Id。");
        }

        private async Task LoadAttachmentsAsync()
        {
            ProofMaterialAttachments.Clear();
            SignedApprovalAttachments.Clear();
            HandoverFormAttachments.Clear();
            MaterialPhotoAttachments.Clear();

            if (Record.Id <= 0)
            {
                return;
            }

            var attachments = await _outboundService.GetAttachmentsAsync(Record.Id);
            foreach (var attachment in attachments)
            {
                if (string.Equals(attachment.FileCategory, ArchiveOutboundDomainValues.AttachmentKindProofMaterialScan, StringComparison.Ordinal))
                {
                    ProofMaterialAttachments.Add(attachment);
                }
                else if (string.Equals(attachment.FileCategory, ArchiveOutboundDomainValues.AttachmentKindSignedApprovalForm, StringComparison.Ordinal))
                {
                    SignedApprovalAttachments.Add(attachment);
                }
                else if (string.Equals(attachment.FileCategory, ArchiveOutboundDomainValues.AttachmentKindSignedHandoverForm, StringComparison.Ordinal))
                {
                    HandoverFormAttachments.Add(attachment);
                }
                else if (string.Equals(attachment.FileCategory, ArchiveOutboundDomainValues.AttachmentKindMaterialPhoto, StringComparison.Ordinal))
                {
                    MaterialPhotoAttachments.Add(attachment);
                }
            }

            OnPropertyChanged(nameof(RequiresProofMaterialScanUpload));
            OnPropertyChanged(nameof(ShowProofMaterialAttachmentSection));
            OnPropertyChanged(nameof(CanManageProofMaterialAttachments));
            RefreshApprovalCommandStates();
            RefreshHandoverCommandStates();
        }

        private async Task RegisterItemsFromResultSetAsync()
        {
            if (!CanEditApplicationHeader)
            {
                return;
            }

            if (!await EnsureRecordPersistedForEditingAsync())
            {
                return;
            }

            int? resultSetId = _dialogService.ShowSearchResultSetPickDialog();
            if (resultSetId is not int id || id <= 0)
            {
                return;
            }

            var result = await _outboundService.AttachSearchResultSetAsync(Record.Id, id, RequireUser());
            if (!result.Success)
            {
                _dialogService.ShowError(result.Message);
                return;
            }

            await ReloadRecordAsync();
            OnPropertyChanged(nameof(ContainerUnits));
            _dialogService.ShowMessage(result.Message, "登记资料");
        }

        private async Task RemoveItemAsync(ArchiveOutboundItemRowViewModel row)
        {
            if (!CanEditApplicationHeader || Record.Id <= 0)
            {
                return;
            }

            string label = string.IsNullOrWhiteSpace(row.MaterialName) ? row.ItemName : row.MaterialName;
            bool isRevokeRegistration = row.IsFromSearchResultSetRegistration;
            string confirmTitle = isRevokeRegistration ? "撤销登记" : "删除资料";
            string confirmMessage = isRevokeRegistration
                ? $"确定撤销登记资料「{label}」吗？"
                : $"确定删除资料「{label}」吗？";
            if (!_dialogService.ShowConfirm(confirmMessage, confirmTitle))
            {
                return;
            }

            if (row.Source.Id <= 0)
            {
                Items.Remove(row.Source);
                ItemRows.Remove(row);
                RebuildContainerUnits();
                OnPropertyChanged(nameof(ContainerUnits));
                return;
            }

            var result = await _outboundService.RemoveApplicationItemAsync(Record.Id, row.Source.Id, RequireUser());
            if (!result.Success)
            {
                _dialogService.ShowError(result.Message);
                return;
            }

            await ReloadRecordAsync();
        }

        private async Task RevokeContainerUnitRegistrationAsync(ArchiveOutboundContainerUnitViewModel unit)
        {
            if (!CanEditApplicationHeader || Record.Id <= 0 || !unit.CanRevokeGroupRegistration)
            {
                return;
            }

            var rowsToRemove = unit.ItemRows
                .Where(row => row.IsFromSearchResultSetRegistration)
                .ToList();
            if (rowsToRemove.Count == 0)
            {
                return;
            }

            string confirmMessage =
                $"确定撤销「{unit.UnitTitle}」的整组登记吗？将移除该{unit.ContainerKindLabel}下 {rowsToRemove.Count} 条检索集登记资料。";
            if (!_dialogService.ShowConfirm(confirmMessage, "撤销整组登记"))
            {
                return;
            }

            foreach (var row in rowsToRemove.Where(row => row.Source.Id <= 0))
            {
                Items.Remove(row.Source);
                ItemRows.Remove(row);
            }

            var persistedIds = rowsToRemove
                .Where(row => row.Source.Id > 0)
                .Select(row => row.Source.Id)
                .ToList();
            if (persistedIds.Count == 0)
            {
                RebuildContainerUnits();
                OnPropertyChanged(nameof(ContainerUnits));
                return;
            }

            var result = await _outboundService.RemoveApplicationItemsAsync(Record.Id, persistedIds, RequireUser());
            if (!result.Success)
            {
                _dialogService.ShowError(result.Message);
                return;
            }

            await ReloadRecordAsync();
        }

        private async Task ViewDetailAsync(ArchiveOutboundItemRowViewModel row)
        {
            var item = row.Source;
            if (item.FilingFactId <= 0)
            {
                _dialogService.ShowMessage("该明细未关联立档记录，无法查看资料详情。", "提示");
                return;
            }

            try
            {
                var hit = await _searchService.GetSearchHitByFilingFactIdAsync(item.FilingFactId);
                if (hit == null || hit.RegisterRecordId <= 0)
                {
                    _dialogService.ShowMessage("无法定位该条立档记录对应的登记资料。", "提示");
                    return;
                }

                _dialogService.ShowArchiveDetailWindow(new ArchiveDetailOpenRequest(
                    hit.RegisterRecordId,
                    BuildDetailHighlightContext(hit, item),
                    item.MediaKind,
                    item.FilingFactId));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"打开资料详情失败：{ex.Message}");
            }
        }

        private static ArchiveDetailHighlightContext BuildDetailHighlightContext(
            FiledArchiveSearchHit hit,
            YearlyArchiveOutboundItem item)
        {
            var context = ArchiveDetailHighlightContext.FromHit(hit);
            if (!string.Equals(
                    item.SelectionScopeKind,
                    ArchiveSearchSelectionScopeKind.ContentEntry,
                    StringComparison.Ordinal)
                || item.ContentEntryId is not int contentEntryId
                || contentEntryId <= 0)
            {
                return context;
            }

            return new ArchiveDetailHighlightContext
            {
                MediaKind = context.MediaKind,
                RegisterMediaId = context.RegisterMediaId,
                MediaItemId = context.MediaItemId,
                ItemType = context.ItemType,
                ItemName = context.ItemName,
                ContainerCode = context.ContainerCode,
                ContentEntryKeyword = context.ContentEntryKeyword,
                ContentEntryKindFilter = context.ContentEntryKindFilter,
                MatchedContentEntryIds = new[] { contentEntryId }
            };
        }

        private void ApplyRecord(YearlyArchiveOutboundRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.ProofMaterialNote))
            {
                record.ProofMaterialNote = ArchiveOutboundDomainValues.ProofMaterialNoneText;
            }

            _hasProofMaterialSelected = ArchiveOutboundDomainValues.HasProofMaterial(record.ProofMaterialNote);
            Record = record;
            HandoverRemark = record.HandoverRemark ?? string.Empty;
            Items.Clear();
            ItemRows.Clear();
            ContainerUnits.Clear();

            foreach (var item in record.Items.OrderBy(i => i.SortOrder))
            {
                Items.Add(item);
                ItemRows.Add(new ArchiveOutboundItemRowViewModel(
                    item,
                    CanEditApplicationHeader,
                    _dialogService,
                    RemoveItemAsync,
                    ViewDetailAsync));
            }

            RebuildContainerUnits();

            OnPropertyChanged(nameof(CanEditApplicationHeader));
            OnPropertyChanged(nameof(CanSaveApplicationDraft));
            OnPropertyChanged(nameof(CanSubmitApplication));
            OnPropertyChanged(nameof(CanPrintApplication));
            OnPropertyChanged(nameof(IsItemsReadOnly));
            OnPropertyChanged(nameof(DestinationKind));
            OnPropertyChanged(nameof(ShowExternalUnit));
            OnPropertyChanged(nameof(ShowApprovalApplicationInfo));
            OnPropertyChanged(nameof(ApplyDateDisplay));
            OnPropertyChanged(nameof(DestinationDisplayText));
            OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
            OnPropertyChanged(nameof(ProofMaterialDisplay));
            OnPropertyChanged(nameof(HasProofMaterial));
            OnPropertyChanged(nameof(ProofMaterialName));
            OnPropertyChanged(nameof(ShowProofMaterialAttachmentSection));
            OnPropertyChanged(nameof(CanManageProofMaterialAttachments));
            RefreshApprovalCommandStates();
            RefreshHandoverCommandStates();
            OnPropertyChanged(nameof(ItemRows));
            OnPropertyChanged(nameof(ContainerUnits));
        }

        private void RebuildContainerUnits()
        {
            ContainerUnits.Clear();

            var rowLookup = ItemRows.ToDictionary(row => row.Source, row => row);
            int unitIndex = 1;
            foreach (var group in ArchiveOutboundContainerUnitSupport.GroupItems(Items))
            {
                var unitItems = group.ToList();
                var sample = unitItems[0];
                var rows = unitItems
                    .Select(item => rowLookup[item])
                    .ToList();

                ContainerUnits.Add(new ArchiveOutboundContainerUnitViewModel(
                    group.Key,
                    sample.MediaKind,
                    sample.ContainerCode,
                    sample.CurrentStorageLocation,
                    rows,
                    CanEditApplicationHeader,
                    _dialogService,
                    unitIndex++,
                    OnContainerUnitSharedDiskStateChanged,
                    RevokeContainerUnitRegistrationAsync));
            }

            RefreshSharedDiskState();
            foreach (var unit in ContainerUnits)
            {
                unit.ItemDetailsPanel.RefreshItems(unit.ItemRows);
            }
            OnPropertyChanged(nameof(ContainerUnitCount));
            OnPropertyChanged(nameof(HasContainerUnits));
            OnPropertyChanged(nameof(ContainerUnitsSummary));
        }

        private void SyncContainerUnitsToItems()
        {
            foreach (var unit in ContainerUnits)
            {
                unit.ApplyToItems();
            }

            SyncSharedDiskSettingsAcrossUnits();
            ArchiveOutboundReturnSupport.SyncRecordExpectedReturnDate(Record, Items);
            OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
        }

        private void OnContainerUnitSharedDiskStateChanged(ArchiveOutboundContainerUnitViewModel sourceUnit)
        {
            RefreshSharedDiskState(sourceUnit);
        }

        /// <summary>
        /// 根据各介质袋当前库内硬盘选用情况，同步共用归还设置并刷新提示文案。
        /// </summary>
        private void RefreshSharedDiskState(ArchiveOutboundContainerUnitViewModel? changedUnit = null)
        {
            SyncSharedDiskSettingsAcrossUnits(changedUnit);
            RefreshSharedDiskEditability();
            ArchiveOutboundReturnSupport.SyncRecordExpectedReturnDate(Record, Items);
            OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
        }

        private void SyncSharedDiskSettingsAcrossUnits(ArchiveOutboundContainerUnitViewModel? changedUnit = null)
        {
            var diskUnits = ContainerUnits
                .Where(unit => unit.RequisitionedMediumId is > 0 && unit.ShowBlankDiskFields)
                .GroupBy(unit => unit.RequisitionedMediumId!.Value)
                .Where(group => group.Count() > 1);

            foreach (var group in diskUnits)
            {
                var orderedUnits = group.OrderBy(unit => unit.UnitIndex).ToList();
                var primary = orderedUnits[0];

                if (changedUnit != null
                    && changedUnit.RequisitionedMediumId == group.Key
                    && !ReferenceEquals(changedUnit, primary))
                {
                    changedUnit.ApplySharedDiskSettingsFromPeer(
                        primary.RequisitionedDiskNeedReturn,
                        primary.ExpectedReturnDate);
                }

                foreach (var unit in orderedUnits.Skip(1))
                {
                    unit.ApplySharedDiskSettingsFromPeer(
                        primary.RequisitionedDiskNeedReturn,
                        primary.ExpectedReturnDate);
                }
            }
        }

        private void RefreshSharedDiskEditability()
        {
            foreach (var unit in ContainerUnits)
            {
                unit.SetSharedDiskPresentation(false, string.Empty);
            }

            foreach (var group in ContainerUnits
                         .Where(unit => unit.RequisitionedMediumId is > 0 && unit.ShowBlankDiskFields)
                         .GroupBy(unit => unit.RequisitionedMediumId!.Value)
                         .Where(g => g.Count() > 1))
            {
                var orderedUnits = group.OrderBy(unit => unit.UnitIndex).ToList();
                var primary = orderedUnits[0];
                string diskCode = primary.RequisitionedDiskCode?.Trim() ?? string.Empty;

                primary.SetSharedDiskPresentation(
                    readOnly: false,
                    hint: ArchiveOutboundSharedDiskSettingsSupport.BuildPrimarySharedDiskHint(
                        orderedUnits.Count,
                        diskCode));

                foreach (var unit in orderedUnits.Skip(1))
                {
                    unit.SetSharedDiskPresentation(
                        readOnly: true,
                        hint: ArchiveOutboundSharedDiskSettingsSupport.BuildPeerSharedDiskHint(
                            orderedUnits.Count,
                            diskCode,
                            primary.UnitTitle));
                }
            }
        }

        private void SetAllItemDetailsExpanded(bool expanded)
        {
            foreach (var unit in ContainerUnits)
            {
                unit.ItemDetailsPanel.SetExpanded(expanded);
            }
        }

        private ApprovalWorkflowButtonSupport.Phase ResolveApprovalPhase()
        {
            if (_record.IsSignedUploaded || _record.IsCompleted)
            {
                return ApprovalWorkflowButtonSupport.Phase.ApprovalCompleted;
            }

            if (_record.IsApproved)
            {
                return ApprovalWorkflowButtonSupport.Phase.ApprovalInProgress;
            }

            if (_record.IsSubmitted)
            {
                return ApprovalWorkflowButtonSupport.Phase.PendingApproval;
            }

            return ApprovalWorkflowButtonSupport.Phase.PendingApproval;
        }

        private ApprovalWorkflowButtonSupport.ButtonState ResolveApprovalButtonState()
        {
            if (!IsApprovalWorkspaceActive)
            {
                return new ApprovalWorkflowButtonSupport.ButtonState(false, false, false, false);
            }

            var user = _userContextService.CurrentUser;
            bool isOperatorAllowed = user != null && _outboundService.IsArchiveAdminUser(user);
            bool canExecuteApprovePass = _record.IsSubmitted && _record.HasApprovalInput;

            return ApprovalWorkflowButtonSupport.Resolve(
                ResolveApprovalPhase(),
                isOperatorAllowed,
                canExecuteApprovePass);
        }

        private bool IsApprovalWorkspaceActive =>
            _workspaceMode == ArchiveOutboundWorkspaceMode.Approval
            && (_record.IsSubmitted || _record.IsApproved || _record.IsSignedUploaded);

        private bool IsInMemoryApprovalComplete() =>
            !string.IsNullOrWhiteSpace(Record.DeptAuditor)
            && Record.DeptAuditDate.HasValue
            && !string.IsNullOrWhiteSpace(Record.ArchiveRoomHead)
            && Record.ArchiveRoomHeadDate.HasValue
            && !string.IsNullOrWhiteSpace(Record.ProductionHead)
            && Record.ProductionHeadDate.HasValue
            && !string.IsNullOrWhiteSpace(Record.VicePresident)
            && Record.VicePresidentDate.HasValue;

        private bool IsApprovalAttachmentsReadyForComplete()
        {
            if (SignedApprovalAttachments.Count == 0)
            {
                return false;
            }

            if (RequiresProofMaterialScanUpload && ProofMaterialAttachments.Count == 0)
            {
                return false;
            }

            return true;
        }

        private bool CanViewApprovalWorkspace() =>
            IsApprovalWorkspaceActive;

        private void RefreshApprovalCommandStates()
        {
            OnPropertyChanged(nameof(CanSaveApproval));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(CanEditApprovalFields));
            OnPropertyChanged(nameof(CanManageApprovalAttachments));
            OnPropertyChanged(nameof(CanCompleteApprovalPhase));
            OnPropertyChanged(nameof(CanPrintApproval));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(PrintApprovalHintText));
            OnPropertyChanged(nameof(RequiresProofMaterialScanUpload));
            OnPropertyChanged(nameof(ShowProofMaterialAttachmentSection));
            OnPropertyChanged(nameof(CanManageProofMaterialAttachments));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private bool CanManageProofMaterialAttachment(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return false;
            }

            return string.Equals(
                       attachment.FileCategory,
                       ArchiveOutboundDomainValues.AttachmentKindProofMaterialScan,
                       StringComparison.Ordinal)
                   && CanManageProofMaterialAttachments;
        }

        private bool CanEditApproval() => CanViewApprovalWorkspace();

        private bool IsHandoverAttachmentsReadyForComplete()
        {
            return HandoverFormAttachments.Count > 0 && MaterialPhotoAttachments.Count > 0;
        }

        private bool CanDeleteAttachment(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return false;
            }

            if (CanManageProofMaterialAttachment(attachment))
            {
                return true;
            }

            if (CanManageApprovalAttachments
                && string.Equals(
                    attachment.FileCategory,
                    ArchiveOutboundDomainValues.AttachmentKindSignedApprovalForm,
                    StringComparison.Ordinal))
            {
                return true;
            }

            return CanManageHandoverAttachments && IsHandoverAttachmentCategory(attachment.FileCategory);
        }

        private static bool IsApprovalAttachmentCategory(string? category) =>
            string.Equals(category, ArchiveOutboundDomainValues.AttachmentKindProofMaterialScan, StringComparison.Ordinal)
            || string.Equals(category, ArchiveOutboundDomainValues.AttachmentKindSignedApprovalForm, StringComparison.Ordinal);

        private static bool IsHandoverAttachmentCategory(string? category) =>
            string.Equals(category, ArchiveOutboundDomainValues.AttachmentKindSignedHandoverForm, StringComparison.Ordinal)
            || string.Equals(category, ArchiveOutboundDomainValues.AttachmentKindMaterialPhoto, StringComparison.Ordinal);

        private void RefreshHandoverCommandStates()
        {
            OnPropertyChanged(nameof(ShowHandoverWorkspaceContent));
            OnPropertyChanged(nameof(CanManageHandoverAttachments));
            OnPropertyChanged(nameof(CanEditHandoverRemark));
            OnPropertyChanged(nameof(CanPrintHandover));
            OnPropertyChanged(nameof(CanCompleteHandover));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private bool CanEditHandover() => CanManageHandoverAttachments;

        private User RequireUser()
        {
            return _userContextService.CurrentUser
                ?? throw new InvalidOperationException("请先登录。");
        }

        private async Task SaveDraftAsync()
        {
            if (!ValidateApplicationHeader(showMessage: true))
            {
                return;
            }

            SyncContainerUnitsToItems();

            try
            {
                IsBusy = true;
                var result = await _outboundService.SaveDraftFlowAsync(BuildSaveRequest(), RequireUser());
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                await ReloadRecordAsync();
                _dialogService.ShowMessage(result.Message, "保存成功");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SubmitAsync()
        {
            if (!ValidateApplicationHeader(showMessage: true))
            {
                return;
            }

            SyncContainerUnitsToItems();

            try
            {
                IsBusy = true;
                var saveResult = await _outboundService.SaveDraftFlowAsync(BuildSaveRequest(), RequireUser());
                if (!saveResult.Success)
                {
                    _dialogService.ShowError(saveResult.Message);
                    return;
                }

                await ReloadRecordAsync();
                SyncContainerUnitsToItems();

                var preview = await _outboundService.PreviewSubmitApplicationAsync(Record.Id, RequireUser());
                if (!preview.IsValid)
                {
                    _dialogService.ShowError("申请校验未通过：\n\n" + preview.ErrorMessage);
                    return;
                }

                _dialogService.ShowTextDetailDialog(preview.ExecutionSummary, "借出申请 · 拟执行逻辑");
                if (preview.HasLongTermSimulatedStockDepletionReminder)
                {
                    _dialogService.ShowTextDetailDialog(
                        preview.LongTermSimulatedStockDepletionReminder,
                        "借出申请 · 库内归零提醒");
                }

                if (!_dialogService.ShowConfirm("确认按以上逻辑提交申请？", "提交确认"))
                {
                    return;
                }

                var result = await _outboundService.SubmitApplicationFlowAsync(Record.Id, RequireUser());
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                await ReloadRecordAsync();
                _dialogService.ShowMessage(result.Message, "提交成功");
                CloseWithCommit(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"提交申请失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task WithdrawAsync()
        {
            var result = await _outboundService.WithdrawApplicationFlowAsync(Record.Id, "申请人撤回", RequireUser());
            if (!result.Success)
            {
                _dialogService.ShowError(result.Message);
                return;
            }

            await ReloadRecordAsync();
            _dialogService.ShowMessage(result.Message, "撤回成功");
            CloseWithCommit(true);
        }

        private async Task PrintAsync()
        {
            try
            {
                bool blankApproval = _workspaceMode == ArchiveOutboundWorkspaceMode.Application;
                var data = await _outboundService.BuildPrintDataAsync(Record.Id, blankApproval);
                var document = ArchiveOutboundPrintDocumentFactory.Create(data);
                var exportOptions = new PrintPreviewExportOptions
                {
                    ExportAsync = () => ExportOutboundWordAsync(data)
                };

                var previewWindow = new PrintPreviewWindow(document, exportOptions)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                await _outboundService.RecordPrintAsync(Record.Id);
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

        private Task ExportOutboundWordAsync(ArchiveOutboundPrintData data)
        {
            try
            {
                string defaultName = string.IsNullOrWhiteSpace(data.OutboundNo)
                    ? "年度资料出库申请审批单.docx"
                    : $"{data.OutboundNo}.docx";
                string? path = _dialogService.SaveFileDialog(
                    "Word 文档|*.docx",
                    "导出 Word",
                    defaultName);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return Task.CompletedTask;
                }

                if (!path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".docx";
                }

                _outboundWordExportService.ExportToFile(data, path);
                _dialogService.ShowMessage($"Word 文档已保存：\n{path}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出 Word 失败：" + ex.Message);
            }

            return Task.CompletedTask;
        }

        private async Task SaveApprovalAsync()
        {
            if (!IsInMemoryApprovalComplete())
            {
                _dialogService.ShowError("四级审批信息尚未完整，请补全后再审批通过。");
                return;
            }

            var result = await _outboundService.SaveApprovalFlowAsync(Record, RequireUser());
            if (!result.Success)
            {
                _dialogService.ShowError(result.Message);
                return;
            }

            await ReloadRecordAsync();
            _dialogService.ShowMessage("审批通过成功。下一步：请上传签字件。", "审批通过");
            RefreshApprovalCommandStates();
        }

        private async Task TryAutoFillDefaultApprovalInfoAsync()
        {
            if (!ShowApprovalActions || !_record.IsSubmitted || _record.IsApproved)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null || !_outboundService.IsArchiveAdminUser(user))
            {
                return;
            }

            try
            {
                await _outboundService.ApplyDefaultApprovalInfoAsync(Record, user);
                OnPropertyChanged(nameof(Record));
                RefreshApprovalCommandStates();
            }
            catch
            {
                // 自动回填失败不阻断页面打开，用户仍可手工录入审批信息。
            }
        }

        private async Task PrintApprovalAsync()
        {
            try
            {
                bool blankApproval = !_record.IsApproved && !_record.IsSignedUploaded && !_record.IsCompleted;
                var data = await _outboundService.BuildPrintDataFromRecordAsync(Record, blankApproval);
                var document = ArchiveOutboundPrintDocumentFactory.Create(data);
                var exportOptions = new PrintPreviewExportOptions
                {
                    ExportAsync = () => ExportOutboundWordAsync(data)
                };

                var previewWindow = new PrintPreviewWindow(document, exportOptions)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                await _outboundService.RecordPrintAsync(Record.Id);
                previewWindow.ShowDialog();
                await ReloadRecordAsync();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("审批单打印生成失败：" + ex.Message);
            }
        }

        private async Task CompleteApprovalPhaseAsync()
        {
            if (!CanCompleteApprovalPhase)
            {
                _dialogService.ShowError("请先审批通过后再确认办结。");
                return;
            }

            var validation = await _outboundService.ValidateApprovalPhaseAsync(Record);
            if (!validation.IsValid)
            {
                _dialogService.ShowError("审批信息验证未通过：\n\n" + validation.ErrorMessage);
                return;
            }

            var result = await _outboundService.CompleteApprovalPhaseFlowAsync(Record, RequireUser());
            if (!result.Success)
            {
                _dialogService.ShowError(result.Message);
                return;
            }

            HasCommittedChanges = true;
            await ReloadRecordAsync();
            _dialogService.ShowMessage("审批阶段确认办结成功。下一步：请打印审批单。", "审批阶段确认办结");
            RefreshApprovalCommandStates();
        }

        private async Task CompleteHandoverAsync()
        {
            if (!IsHandoverAttachmentsReadyForComplete())
            {
                _dialogService.ShowError("请先上传交接签字单和资料照片后再办结。");
                return;
            }

            var result = await _outboundService.CompletePhysicalOutboundFlowAsync(Record.Id, HandoverRemark, RequireUser());
            if (!result.Success)
            {
                _dialogService.ShowError(result.Message);
                return;
            }

            await ReloadRecordAsync();
            _dialogService.ShowMessage(result.Message, "资料出库");
            CloseWithCommit(true);
        }

        private async Task PrintHandoverAsync()
        {
            try
            {
                bool blankHandoverSignatures = true;
                var data = await _outboundService.BuildHandoverPrintDataAsync(Record.Id, HandoverRemark, blankHandoverSignatures);
                var document = ArchiveOutboundHandoverPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                await _outboundService.RecordPrintAsync(Record.Id);
                previewWindow.ShowDialog();
                await ReloadRecordAsync();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("交接单打印生成失败：" + ex.Message);
            }
        }

        private void OpenBusinessAssistant()
        {
            var checklist = ArchiveOutboundHandoverAssistantBuilder.Build(Record);
            var rows = checklist
                .Select(item => new ArchiveOutboundHandoverAssistantRowViewModel(item.Category, item.Text))
                .ToList();

            var assistantViewModel = new ArchiveOutboundHandoverAssistantViewModel(Record, rows);
            var window = new ArchiveOutboundHandoverAssistantWindow
            {
                DataContext = assistantViewModel,
                Owner = System.Windows.Application.Current.Windows
                    .OfType<ArchiveOutboundEditDialog>()
                    .FirstOrDefault()
                    ?? System.Windows.Application.Current.MainWindow
            };

            window.ShowDialog();
        }

        private async Task UploadAttachmentAsync(string attachmentKind)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图像/PDF|*.jpg;*.jpeg;*.png;*.pdf|所有文件|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            byte[] content = await File.ReadAllBytesAsync(dialog.FileName);
            string fileName = Path.GetFileName(dialog.FileName);
            string extension = Path.GetExtension(fileName);

            var attachment = new SystemAttachment
            {
                FileName = fileName,
                Extension = extension,
                FileSize = content.LongLength,
                FileContent = content
            };

            var result = await _outboundService.UploadAttachmentFlowAsync(Record.Id, attachmentKind, attachment, RequireUser());
            if (!result.Success)
            {
                _dialogService.ShowError(result.Message);
                return;
            }

            await LoadAttachmentsAsync();
            _dialogService.ShowMessage(result.Message, "上传成功");
        }

        private async Task ViewAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            try
            {
                var result = await _outboundService.PrepareAttachmentViewFlowAsync(attachment);
                if (!result.Success || result.Attachment?.FileContent == null)
                {
                    _dialogService.ShowMessage(result.Message);
                    return;
                }

                var full = result.Attachment;
                if (_dialogService.ShowConfirm("直接打开？\n【确定】打开 【取消】另存为"))
                {
                    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_" + full.FileName);
                    await File.WriteAllBytesAsync(path, full.FileContent);
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                else
                {
                    var dlg = new SaveFileDialog { FileName = full.FileName };
                    if (dlg.ShowDialog() == true)
                    {
                        await File.WriteAllBytesAsync(dlg.FileName, full.FileContent);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("查看附件失败：" + ex.Message);
            }
        }

        private async Task DeleteAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null || !CanDeleteAttachment(attachment))
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定删除“{attachment.FileName}”？"))
            {
                return;
            }

            try
            {
                var result = await _outboundService.DeleteAttachmentFlowAsync(Record.Id, attachment, RequireUser());
                if (!result.Success)
                {
                    _dialogService.ShowMessage(result.Message);
                    return;
                }

                ProofMaterialAttachments.Remove(attachment);
                SignedApprovalAttachments.Remove(attachment);
                HandoverFormAttachments.Remove(attachment);
                MaterialPhotoAttachments.Remove(attachment);
                RefreshApprovalCommandStates();
                RefreshHandoverCommandStates();
                _dialogService.ShowMessage(result.Message, "删除成功");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("删除失败：" + ex.Message);
            }
        }

        private bool ValidateApplicationHeader(bool showMessage)
        {
            if (ShowExternalUnit && string.IsNullOrWhiteSpace(Record.ExternalUnit))
            {
                if (showMessage)
                {
                    _dialogService.ShowError("资料去向为“外部（单位）”时，请填写外部单位。");
                }

                return false;
            }

            if (HasProofMaterial && string.IsNullOrWhiteSpace(ProofMaterialName))
            {
                if (showMessage)
                {
                    _dialogService.ShowError("已选择有证明材料，请填写证明材料名称。");
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// 登记资料等操作依赖持久化记录；未保存的新申请在用户主动操作时先落库为草稿。
        /// </summary>
        private async Task<bool> EnsureRecordPersistedForEditingAsync()
        {
            if (Record.Id > 0)
            {
                return true;
            }

            if (!ValidateApplicationHeader(showMessage: true))
            {
                return false;
            }

            SyncContainerUnitsToItems();

            try
            {
                IsBusy = true;
                var result = await _outboundService.SaveDraftFlowAsync(BuildSaveRequest(), RequireUser());
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return false;
                }

                await ReloadRecordAsync();
                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private SaveOutboundDraftRequest BuildSaveRequest()
        {
            SyncContainerUnitsToItems();
            if (!HasProofMaterial)
            {
                Record.ProofMaterialNote = ArchiveOutboundDomainValues.ProofMaterialNoneText;
            }
            else
            {
                Record.ProofMaterialNote = ProofMaterialName.Trim();
            }

            Record.Items = Items.ToList();
            return new SaveOutboundDraftRequest
            {
                Record = Record,
                Items = Items.ToList()
            };
        }

        private async Task ReloadRecordAsync()
        {
            var record = await _outboundService.GetRecordAsync(Record.Id);
            if (record != null)
            {
                ApplyRecord(record);
                await LoadAttachmentsAsync();
            }
        }

        private ApplicationFormActionSupport.ActionState ResolveApplicationFormActions() =>
            ApplicationFormActionSupport.Resolve(_record.Id, _record.IsDraft);
    }
}
