using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料离库处置办理弹窗 ViewModel。
    /// </summary>
    public sealed class ArchiveDisposalEditDialogViewModel : ViewModelBase
    {
        private readonly IArchiveDisposalService _disposalService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private YearlyArchiveDisposalRecord _record;
        private bool _hasCommittedChanges;
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

        public ArchiveDisposalEditDialogViewModel(
            IArchiveDisposalService disposalService,
            IDialogService dialogService,
            IUserContextService userContextService,
            YearlyArchiveDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _disposalService = disposalService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _record = record;

            MoveToDisposalCommand = new RelayCommand(_ => MoveToDisposal(), _ => CanEditHeader && AvailableItems.Any(i => i.IsSelected));
            MoveToAvailableCommand = new RelayCommand(_ => MoveToAvailable(), _ => CanEditHeader && Items.Any(i => i.IsSelected));
            ApplyDispositionMethodCommand = new RelayCommand(
                _ => ApplyDispositionMethod(),
                _ => CanEditHeader && Items.Any(i => i.IsSelected) && !string.IsNullOrWhiteSpace(BatchDispositionMethod));
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmUploadCommand = new RelayCommand(async _ => await ConfirmUploadAsync(), _ => CanConfirmUpload);
            UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
            DeleteAttachmentCommand = new RelayCommand(
                async item => await DeleteAttachmentAsync(item as SystemAttachment),
                item => item is SystemAttachment && CanUploadAttachment);
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

        public string BannerText =>
            "流程：保存草稿 → 提交 → 打印签批单并线下签字 → 审批 → 确认可上传 → 上传签批单（销毁须现场照片）→ 办结。办结释档空盒/空袋前须确认物理移除；拟销硬盘低格留存须确认已低格。";

        public ObservableCollection<ArchiveDisposalCandidateRow> AvailableItems { get; } = new();

        public ObservableCollection<ArchiveDisposalItemRow> Items { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<string> DispositionMethodOptions { get; } = new();

        public ObservableCollection<string> UploadCategoryOptions { get; } = new(ArchiveDisposalDomainValues.AttachmentCategoryOptions);

        public string AvailableTitle => $"可选盘库资料（{AvailableItems.Count}）";

        public string DisposalTitle => $"待处置明细（{Items.Count}）";

        public string DisposalNo
        {
            get => _disposalNo;
            set => SetProperty(ref _disposalNo, value);
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
            set => SetProperty(ref _batchDispositionMethod, value);
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

        public bool CanSubmit =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusDraft && _record.Id > 0 && Items.Count > 0;

        public bool CanApprove =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusSubmitted;

        public bool CanConfirmUpload =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusApproved;

        public bool CanUploadAttachment =>
            CanOperate
            && (_record.Status == YearlyArchiveDisposalRecord.StatusApproved
                || _record.Status == YearlyArchiveDisposalRecord.StatusSignedUploaded);

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
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmUploadCommand { get; }
        public RelayCommand UploadAttachmentCommand { get; }
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
                await ReloadSelectableAsync();
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

        private void BindFromRecord(YearlyArchiveDisposalRecord record)
        {
            _record = record;
            DisposalNo = record.DisposalNo;
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
            OnPropertyChanged(nameof(AvailableTitle));
            OnPropertyChanged(nameof(DisposalTitle));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmUpload));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanWithdraw));
        }

        private async Task ReloadSelectableAsync()
        {
            var selectable = await _disposalService.GetSelectableItemsAsync(
                _record.MediaKind,
                _record.Id > 0 ? _record.Id : null);

            HashSet<string> selectedKeys = Items.Select(i => i.SelectionKey).ToHashSet(StringComparer.Ordinal);
            AvailableItems.Clear();
            foreach (var item in selectable.Where(i => !selectedKeys.Contains(i.SelectionKey)))
            {
                AvailableItems.Add(new ArchiveDisposalCandidateRow(item));
            }

            OnPropertyChanged(nameof(AvailableTitle));
        }

        private void RefreshDispositionMethodOptions()
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
                BatchDispositionMethod = DispositionMethodOptions[0];
            }
        }

        private void MoveToDisposal()
        {
            var selected = AvailableItems.Where(i => i.IsSelected).ToList();
            foreach (var row in selected)
            {
                AvailableItems.Remove(row);
                Items.Add(ArchiveDisposalItemRow.FromSelectable(row.Source));
            }

            RenumberItems();
            RefreshDispositionMethodOptions();
            OnPropertyChanged(nameof(AvailableTitle));
            OnPropertyChanged(nameof(DisposalTitle));
            CommandManager.InvalidateRequerySuggested();
        }

        private void MoveToAvailable()
        {
            var selected = Items.Where(i => i.IsSelected).ToList();
            foreach (var row in selected)
            {
                Items.Remove(row);
                AvailableItems.Add(new ArchiveDisposalCandidateRow(row.ToSelectable(_record.MediaKind)));
            }

            RenumberItems();
            RefreshDispositionMethodOptions();
            OnPropertyChanged(nameof(AvailableTitle));
            OnPropertyChanged(nameof(DisposalTitle));
            CommandManager.InvalidateRequerySuggested();
        }

        private void ApplyDispositionMethod()
        {
            string method = BatchDispositionMethod?.Trim() ?? string.Empty;
            foreach (var item in Items.Where(i => i.IsSelected))
            {
                var allowed = ArchiveDisposalDomainValues.ResolveAllowedMethods(
                    _record.MediaKind,
                    item.DisposalReason,
                    item.MediumKind);
                if (!allowed.Contains(method, StringComparer.Ordinal))
                {
                    _dialogService.ShowError($"「{item.DisplayTitle}」不允许处置方式「{method}」。");
                    return;
                }

                item.DispositionMethod = method;
            }
        }

        private void RenumberItems()
        {
            int sort = 1;
            foreach (var item in Items)
            {
                item.SortOrder = sort++;
            }
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
                await ReloadSelectableAsync();
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
                if (_record.Id <= 0)
                {
                    await SaveDraftAsync();
                    if (_record.Id <= 0)
                    {
                        return;
                    }
                }
                else if (CanEditHeader)
                {
                    await SaveDraftAsync();
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

                if (!_dialogService.ShowConfirm("确认办结本处置单？办结后将写入正式清账结果。"))
                {
                    return;
                }

                await _disposalService.CompleteAsync(
                    _record.Id,
                    RequireUser(),
                    PhysicalRemovalConfirmed,
                    FormatRetainedConfirmed);
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
                if (!_dialogService.ShowConfirm($"确认撤回作废【{DisposalNo}】？"))
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
                if (_record.Status == YearlyArchiveDisposalRecord.StatusApproved
                    && !string.Equals(UploadCategory, ArchiveDisposalDomainValues.AttachmentCategoryOther, StringComparison.Ordinal))
                {
                    _dialogService.ShowMessage("请先点击「确认可上传」，再上传签批单或处置现场照片。");
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
                await ReloadSelectableAsync();
                await RefreshCompleteHintsAsync();
            }

            RefreshCommandStates();
        }

        private async Task ReloadAttachmentsAsync()
        {
            if (string.IsNullOrWhiteSpace(_record.DisposalNo))
            {
                Attachments.Clear();
                return;
            }

            var list = await _disposalService.GetAttachmentsAsync(_record.DisposalNo);
            Attachments.Clear();
            foreach (var item in list)
            {
                Attachments.Add(item);
            }
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
        }

        private void RefreshCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmUpload));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanWithdraw));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
        }

        private Models.SystemSettings.User RequireUser()
        {
            return _userContextService.CurrentUser
                ?? throw new InvalidOperationException("当前用户无效。");
        }
    }

    public sealed class ArchiveDisposalCandidateRow : ViewModelBase
    {
        private bool _isSelected;

        public ArchiveDisposalCandidateRow(ArchiveDisposalSelectableItem source)
        {
            Source = source;
        }

        public ArchiveDisposalSelectableItem Source { get; }

        public string DisplayTitle => Source.DisplayTitle;

        public string DisposalReason => Source.DisposalReason;

        public string SourceRegisterKind => Source.SourceRegisterKind;

        public string BeforeStorageLocation => Source.BeforeStorageLocation;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public sealed class ArchiveDisposalItemRow : ViewModelBase
    {
        private bool _isSelected;
        private string _dispositionMethod = string.Empty;

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
            set => SetProperty(ref _dispositionMethod, value);
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

        public string TargetBlankSlotLocation { get; set; } = string.Empty;

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
                SortOrder = item.SortOrder,
                FilingFactId = item.FilingFactId,
                ContainerId = item.ContainerId,
                ContainerCode = item.ContainerCode,
                BeforeStorageLocation = item.BeforeStorageLocation,
                SourceRegisterKind = item.SourceRegisterKind,
                DisposalReason = item.DisposalReason,
                DispositionMethod = item.DispositionMethod,
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
                SortOrder = SortOrder,
                FilingFactId = FilingFactId,
                ContainerId = ContainerId,
                ContainerCode = ContainerCode,
                BeforeStorageLocation = BeforeStorageLocation,
                SourceRegisterKind = SourceRegisterKind,
                DisposalReason = DisposalReason,
                DispositionMethod = DispositionMethod,
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

        public ArchiveDisposalSelectableItem ToSelectable(string mediaKind)
        {
            return new ArchiveDisposalSelectableItem
            {
                MediaKind = mediaKind,
                FilingFactId = FilingFactId,
                ContainerId = ContainerId,
                ContainerCode = ContainerCode,
                BeforeStorageLocation = BeforeStorageLocation,
                SourceRegisterKind = SourceRegisterKind,
                DisposalReason = DisposalReason,
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
                BeforeMediaStatus = BeforeMediaStatus
            };
        }
    }
}
