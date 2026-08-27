using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 历史存档离库处置办理弹窗。
/// </summary>
public sealed class HistoryArchiveDisposalEditDialogViewModel : ViewModelBase
{
    private readonly IHistoryArchiveDisposalService _service;
    private readonly IDialogService _dialogService;
    private readonly IUserContextService _userContextService;
    private HistoryArchiveDisposalRecord _record;
    private bool _hasCommittedChanges;
    private bool _suppressReviewSignerPersist;
    private string _disposalNo = string.Empty;
    private string _materialKindDisplay = string.Empty;
    private string _dispositionMethod = HistoryArchiveDisposalDomainValues.MethodDestroy;
    private string _transferTarget = string.Empty;
    private string _otherRemark = string.Empty;
    private string _reason = string.Empty;
    private string _remark = string.Empty;
    private string _archiveRoomHead = string.Empty;
    private DateTime? _archiveRoomHeadDate;
    private string _archiveDeputyPresident = string.Empty;
    private DateTime? _archiveDeputyPresidentDate;
    private string _uploadCategory = HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm;
    private HistoryArchiveDisposalBoxCandidateRow? _selectedCandidate;
    private HistoryArchiveDisposalItemRow? _selectedItem;

    public HistoryArchiveDisposalEditDialogViewModel(
        IHistoryArchiveDisposalService service,
        IDialogService dialogService,
        IUserContextService userContextService,
        HistoryArchiveDisposalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _service = service;
        _dialogService = dialogService;
        _userContextService = userContextService;
        _record = record;

        RefreshCandidatesCommand = new RelayCommand(async _ => await LoadCandidatesAsync(), _ => CanEditHeader);
        AddSelectedCommand = new RelayCommand(_ => AddSelected(), _ => CanAddSelected);
        AddAllCommand = new RelayCommand(_ => AddAll(), _ => CanAddAll);
        ClearItemsCommand = new RelayCommand(_ => ClearItems(), _ => CanClearItems);
        RemoveItemCommand = new RelayCommand(
            item => RemoveItem(item as HistoryArchiveDisposalItemRow),
            item => CanEditHeader && item is HistoryArchiveDisposalItemRow);
        SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader && Items.Count > 0);
        SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
        PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
        ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
        ConfirmUploadCommand = new RelayCommand(async _ => await ConfirmUploadAsync(), _ => CanConfirmUpload);
        UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
        DeleteAttachmentCommand = new RelayCommand(async item =>
        {
            if (item is not SystemAttachment att)
            {
                return;
            }

            var (ok, msg) = await _service.DeleteAttachmentAsync(att.Id, RequireUser());
            if (!ok)
            {
                _dialogService.ShowError(msg);
                return;
            }

            _hasCommittedChanges = true;
            await ReloadAttachmentsAsync();
        }, item => item is SystemAttachment && CanUploadAttachment);
        ViewAttachmentCommand = new RelayCommand(item =>
        {
            if (item is SystemAttachment att)
            {
                _dialogService.ShowSystemAttachmentView(att);
            }
        }, item => item is SystemAttachment);
        CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        _ = InitializeAsync();
    }

    public event Action<bool?>? RequestClose;
    public bool HasCommittedChanges => _hasCommittedChanges;
    public string WindowTitle =>
        $"资料离库处置 · {(string.IsNullOrWhiteSpace(DisposalNo) ? "待编单" : DisposalNo)} · {StatusDisplay}";
    public string StatusDisplay => HistoryArchiveDisposalDomainValues.ToStatusDisplay(_record.Status);

    public ObservableCollection<string> MaterialKindDisplayOptions { get; } =
        new(HistoryArchiveDisposalDomainValues.MaterialKindDisplayOptions);
    public ObservableCollection<string> MethodOptions { get; } =
        new(HistoryArchiveDisposalDomainValues.DispositionMethodOptions);
    public ObservableCollection<string> UploadCategoryOptions { get; } =
        new(HistoryArchiveDisposalDomainValues.AttachmentCategoryOptions);
    public ObservableCollection<HistoryArchiveDisposalBoxCandidateRow> AvailableBoxes { get; } = new();
    public ObservableCollection<HistoryArchiveDisposalItemRow> Items { get; } = new();
    public ObservableCollection<SystemAttachment> Attachments { get; } = new();

    public HistoryArchiveDisposalBoxCandidateRow? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (SetProperty(ref _selectedCandidate, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public HistoryArchiveDisposalItemRow? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string AvailableBoxesTitle => $"候选在柜档案盒（{AvailableBoxes.Count}）";
    public string SelectedItemsTitle => $"已选明细（{Items.Count}）";
    public string MixedGroupPreview
    {
        get
        {
            var related = AvailableBoxes
                .Where(item => item.IsSelected && item.IsMixedPlacement)
                .SelectMany(item => HistoryArchiveBoxCodeSupport.SplitBoxCodes(item.RelatedBoxCodesText))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return related.Count == 0
                ? string.Empty
                : "将自动纳入关联混放盒：" + string.Join("、", related);
        }
    }

    public bool CanEditHeader =>
        _record.Status == HistoryArchiveDisposalRecord.StatusDraft
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
    public bool CanEditMaterialKind => CanEditHeader && Items.Count == 0;
    public bool CanSubmit => CanEditHeader && Items.Count > 0;
    public bool CanAddSelected =>
        CanEditHeader
        && HistoryArchiveDisposalDomainValues.IsValidMaterialKind(MaterialKindDisplay)
        && AvailableBoxes.Any(item => item.IsSelected);
    public bool CanAddAll =>
        CanEditHeader
        && HistoryArchiveDisposalDomainValues.IsValidMaterialKind(MaterialKindDisplay)
        && AvailableBoxes.Count > 0;
    public bool CanClearItems => CanEditHeader && Items.Count > 0;
    public bool CanPrint =>
        _record.Id > 0
        && _record.Status is not HistoryArchiveDisposalRecord.StatusDraft
            and not HistoryArchiveDisposalRecord.StatusWithdrawn
            and not HistoryArchiveDisposalRecord.StatusForceWithdrawn;
    public bool CanApprove =>
        _record.Status == HistoryArchiveDisposalRecord.StatusSubmitted
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
    public bool CanConfirmUpload =>
        _record.Status == HistoryArchiveDisposalRecord.StatusApproved
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
    public bool CanUploadAttachment =>
        _record.Status is HistoryArchiveDisposalRecord.StatusApproved
            or HistoryArchiveDisposalRecord.StatusSignedUploaded
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
    public bool CanComplete =>
        _record.Status == HistoryArchiveDisposalRecord.StatusSignedUploaded
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
    public bool CanEditReviewSigners =>
        _record.Status is HistoryArchiveDisposalRecord.StatusApproved
            or HistoryArchiveDisposalRecord.StatusSignedUploaded
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

    public bool ShowTransferTarget => HistoryArchiveDisposalDomainValues.RequiresTransferTarget(DispositionMethod);
    public bool ShowOtherRemark => HistoryArchiveDisposalDomainValues.RequiresOtherRemark(DispositionMethod);

    public string DisposalNo { get => _disposalNo; set => SetProperty(ref _disposalNo, value); }

    public string MaterialKindDisplay
    {
        get => _materialKindDisplay;
        set
        {
            string normalized = HistoryArchiveDisposalDomainValues.ToMaterialKindDisplay(
                HistoryArchiveDisposalDomainValues.NormalizeMaterialKind(value));
            if (!SetProperty(ref _materialKindDisplay, normalized))
            {
                return;
            }

            if (CanEditHeader)
            {
                _ = LoadCandidatesAsync();
            }
        }
    }

    public string DispositionMethod
    {
        get => _dispositionMethod;
        set
        {
            if (!SetProperty(ref _dispositionMethod, value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowTransferTarget));
            OnPropertyChanged(nameof(ShowOtherRemark));
        }
    }

    public string TransferTarget { get => _transferTarget; set => SetProperty(ref _transferTarget, value); }
    public string OtherRemark { get => _otherRemark; set => SetProperty(ref _otherRemark, value); }
    public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
    public string Remark { get => _remark; set => SetProperty(ref _remark, value); }

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

    public string ArchiveRoomHeadDateDisplay => FormatDate(_archiveRoomHeadDate);

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

    public string ArchiveDeputyPresidentDateDisplay => FormatDate(_archiveDeputyPresidentDate);
    public string UploadCategory { get => _uploadCategory; set => SetProperty(ref _uploadCategory, value); }

    public RelayCommand RefreshCandidatesCommand { get; }
    public RelayCommand AddSelectedCommand { get; }
    public RelayCommand AddAllCommand { get; }
    public RelayCommand ClearItemsCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand SaveDraftCommand { get; }
    public RelayCommand SubmitCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand ApproveCommand { get; }
    public RelayCommand ConfirmUploadCommand { get; }
    public RelayCommand UploadAttachmentCommand { get; }
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
                var latest = await _service.GetRecordByIdAsync(_record.Id);
                if (latest != null)
                {
                    _record = latest;
                }
            }
            else if (string.IsNullOrWhiteSpace(_record.DisposalNo))
            {
                _record.DisposalNo = await _service.GenerateNextDisposalNoAsync();
            }

            BindFromRecord();
            if (CanEditHeader)
            {
                await LoadCandidatesAsync();
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
        DisposalNo = _record.DisposalNo;
        _materialKindDisplay = HistoryArchiveDisposalDomainValues.ToMaterialKindDisplay(_record.MaterialKind);
        DispositionMethod = string.IsNullOrWhiteSpace(_record.DispositionMethod)
            ? HistoryArchiveDisposalDomainValues.MethodDestroy
            : _record.DispositionMethod;
        TransferTarget = _record.TransferTarget;
        OtherRemark = _record.OtherRemark;
        Reason = _record.Reason;
        Remark = _record.Remark;
        _suppressReviewSignerPersist = true;
        try
        {
            _archiveRoomHead = _record.ArchiveRoomHead;
            _archiveRoomHeadDate = _record.ArchiveRoomHeadDate;
            _archiveDeputyPresident = _record.ArchiveDeputyPresident;
            _archiveDeputyPresidentDate = _record.ArchiveDeputyPresidentDate;
            RebuildItemsFromRecord();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(MaterialKindDisplay));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditMaterialKind));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmUpload));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanEditReviewSigners));
            OnPropertyChanged(nameof(CanAddSelected));
            OnPropertyChanged(nameof(CanAddAll));
            OnPropertyChanged(nameof(CanClearItems));
            OnPropertyChanged(nameof(ArchiveRoomHead));
            OnPropertyChanged(nameof(ArchiveRoomHeadDateDisplay));
            OnPropertyChanged(nameof(ArchiveDeputyPresident));
            OnPropertyChanged(nameof(ArchiveDeputyPresidentDateDisplay));
            NotifyItemListsChanged();
        }
        finally
        {
            _suppressReviewSignerPersist = false;
        }
    }

    private void RebuildItemsFromRecord()
    {
        string? keepSelected = SelectedItem?.BoxCode;
        Items.Clear();
        foreach (var item in _record.Items.OrderBy(row => row.SortOrder))
        {
            Items.Add(new HistoryArchiveDisposalItemRow(item));
        }

        SelectedItem = keepSelected == null
            ? null
            : Items.FirstOrDefault(row =>
                string.Equals(row.BoxCode, keepSelected, StringComparison.OrdinalIgnoreCase));
    }

    private async Task LoadCandidatesAsync()
    {
        if (!HistoryArchiveDisposalDomainValues.IsValidMaterialKind(MaterialKindDisplay))
        {
            ClearAvailableBoxes();
            NotifyItemListsChanged();
            return;
        }

        var list = await _service.GetSelectableBoxesAsync(
            MaterialKindDisplay,
            _record.Id > 0 ? _record.Id : null);
        HashSet<string> selectedCodes = Items
            .Select(row => row.BoxCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? keepSelected = SelectedCandidate?.BoxCode;
        ClearAvailableBoxes();
        foreach (var candidate in list.Where(item => item.IsSelectable && !selectedCodes.Contains(item.BoxCode)))
        {
            AttachAvailable(new HistoryArchiveDisposalBoxCandidateRow(candidate));
        }

        SelectedCandidate = keepSelected == null
            ? null
            : AvailableBoxes.FirstOrDefault(item =>
                string.Equals(item.BoxCode, keepSelected, StringComparison.OrdinalIgnoreCase));
        NotifyItemListsChanged();
    }

    private void AddSelected()
    {
        AddCandidates(AvailableBoxes.Where(item => item.IsSelected).ToList(), notifyAutoAdded: true);
    }

    private void AddAll()
    {
        AddCandidates(AvailableBoxes.ToList(), notifyAutoAdded: false);
    }

    private void AddCandidates(IReadOnlyList<HistoryArchiveDisposalBoxCandidateRow> picked, bool notifyAutoAdded)
    {
        if (picked.Count == 0)
        {
            return;
        }

        Dictionary<string, HistoryArchiveDisposalBoxCandidateRow> byCode = AvailableBoxes
            .ToDictionary(item => item.BoxCode, StringComparer.OrdinalIgnoreCase);
        var toAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var autoAdded = new List<string>();
        foreach (var row in picked)
        {
            toAdd.Add(row.BoxCode);
            foreach (string related in HistoryArchiveBoxCodeSupport.SplitBoxCodes(row.RelatedBoxCodesText))
            {
                if (toAdd.Add(related) && !string.Equals(related, row.BoxCode, StringComparison.OrdinalIgnoreCase))
                {
                    autoAdded.Add(related);
                }
            }
        }

        HashSet<string> existing = Items.Select(item => item.BoxCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string code in toAdd.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            if (!existing.Add(code) || !byCode.TryGetValue(code, out HistoryArchiveDisposalBoxCandidateRow? candidate))
            {
                continue;
            }

            Items.Add(new HistoryArchiveDisposalItemRow(candidate.Candidate));
            DetachAvailable(candidate);
        }

        if (notifyAutoAdded && autoAdded.Count > 0)
        {
            _dialogService.ShowMessage("已自动纳入关联混放盒：" + string.Join("、", autoAdded.Distinct(StringComparer.OrdinalIgnoreCase)));
        }

        SelectedCandidate = AvailableBoxes.FirstOrDefault(item => item.IsSelected);
        NotifyItemListsChanged();
    }

    private void ClearItems()
    {
        if (Items.Count == 0)
        {
            return;
        }

        if (!_dialogService.ShowConfirm($"确认清空已选明细（共 {Items.Count} 条）？档案盒将回到候选列表。"))
        {
            return;
        }

        Items.Clear();
        SelectedItem = null;
        _ = LoadCandidatesAsync();
    }

    private void RemoveItem(HistoryArchiveDisposalItemRow? row)
    {
        if (row == null)
        {
            return;
        }

        HashSet<string> group = HistoryArchiveBoxCodeSupport.SplitBoxCodes(row.RelatedBoxCodes)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        group.Add(row.BoxCode);
        List<HistoryArchiveDisposalItemRow> removing = Items
            .Where(item => group.Contains(item.BoxCode))
            .ToList();
        foreach (var item in removing)
        {
            Items.Remove(item);
        }

        if (ReferenceEquals(SelectedItem, row) || (SelectedItem != null && group.Contains(SelectedItem.BoxCode)))
        {
            SelectedItem = null;
        }

        _ = LoadCandidatesAsync();
    }

    private void NotifyItemListsChanged()
    {
        OnPropertyChanged(nameof(AvailableBoxesTitle));
        OnPropertyChanged(nameof(SelectedItemsTitle));
        OnPropertyChanged(nameof(MixedGroupPreview));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CanAddSelected));
        OnPropertyChanged(nameof(CanAddAll));
        OnPropertyChanged(nameof(CanClearItems));
        OnPropertyChanged(nameof(CanEditMaterialKind));
        CommandManager.InvalidateRequerySuggested();
    }

    private void AttachAvailable(HistoryArchiveDisposalBoxCandidateRow row)
    {
        row.SelectionChanged += OnCandidateSelectionChanged;
        AvailableBoxes.Add(row);
    }

    private void DetachAvailable(HistoryArchiveDisposalBoxCandidateRow row)
    {
        row.SelectionChanged -= OnCandidateSelectionChanged;
        AvailableBoxes.Remove(row);
    }

    private void ClearAvailableBoxes()
    {
        foreach (var row in AvailableBoxes)
        {
            row.SelectionChanged -= OnCandidateSelectionChanged;
        }

        AvailableBoxes.Clear();
    }

    private void OnCandidateSelectionChanged()
    {
        OnPropertyChanged(nameof(CanAddSelected));
        OnPropertyChanged(nameof(CanAddAll));
        OnPropertyChanged(nameof(MixedGroupPreview));
        CommandManager.InvalidateRequerySuggested();
    }

    private List<HistoryArchiveDisposalItem> BuildItems() =>
        Items.Select((row, index) => row.ToItem(index + 1)).ToList();

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
        var draft = new HistoryArchiveDisposalRecord
        {
            Id = _record.Id,
            DisposalNo = DisposalNo,
            MaterialKind = MaterialKindDisplay,
            DispositionMethod = DispositionMethod,
            TransferTarget = TransferTarget,
            OtherRemark = OtherRemark,
            Reason = Reason,
            Remark = Remark
        };
        _record = _record.Id > 0
            ? await _service.UpdateDraftAsync(draft, BuildItems(), RequireUser())
            : await _service.CreateDraftAsync(draft, BuildItems(), RequireUser());
        _hasCommittedChanges = true;
        BindFromRecord();
        await LoadCandidatesAsync();
    }

    private async Task SubmitAsync()
    {
        try
        {
            IReadOnlyList<HistoryArchiveDisposalBoxCandidate> selectable =
                await _service.GetSelectableBoxesAsync(
                    MaterialKindDisplay,
                    _record.Id > 0 ? _record.Id : null);
            Dictionary<string, HistoryArchiveDisposalBoxCandidate> byCode = selectable
                .Where(item => item.IsSelectable)
                .ToDictionary(item => item.BoxCode, StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<string> validationErrors = HistoryArchiveDisposalValidationSupport.ValidateForSubmit(
                MaterialKindDisplay,
                DispositionMethod,
                TransferTarget,
                OtherRemark,
                Reason,
                BuildItems(),
                byCode);
            if (validationErrors.Count > 0)
            {
                _dialogService.ShowError(
                    "提交前校验未通过：" + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, validationErrors));
                return;
            }

            await PersistDraftAsync();
            await _service.SubmitAsync(_record.Id, RequireUser());
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
            HistoryArchiveDisposalPrintData data = await _service.BuildPrintDataAsync(_record.Id);
            FlowDocument document = HistoryArchiveDisposalPrintDocumentFactory.Create(data);
            var previewWindow = new PrintPreviewWindow(document)
            {
                Owner = Application.Current.MainWindow
            };
            await _service.RecordPrintAsync(_record.Id);
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
            if (!_dialogService.ShowConfirm("确认审批通过？将自动填写资料室负责人、分管资料副院长的姓名与日期。"))
            {
                return;
            }

            await _service.ApproveAsync(_record.Id, RequireUser());
            _hasCommittedChanges = true;
            _dialogService.ShowMessage("审批已通过，审核审批姓名已填写，可按实际签字人修改。");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "—";

    private async Task PersistReviewSignersAsync()
    {
        if (_suppressReviewSignerPersist || !CanEditReviewSigners || _record.Id <= 0)
        {
            return;
        }

        try
        {
            await _service.UpdateReviewSignersAsync(
                _record.Id, _archiveRoomHead, _archiveDeputyPresident, RequireUser());
            _hasCommittedChanges = true;
            _record.ArchiveRoomHead = _archiveRoomHead.Trim();
            _record.ArchiveDeputyPresident = _archiveDeputyPresident.Trim();
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
            await _service.ConfirmReadyForUploadAsync(_record.Id, RequireUser());
            _hasCommittedChanges = true;
            _dialogService.ShowMessage("已确认可上传签批单。");
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

            if (!_dialogService.ShowConfirm("办结前请确认：对应档案盒已从档案柜撤出实物。是否继续办结？"))
            {
                return;
            }

            IReadOnlyList<string> validationErrors = HistoryArchiveDisposalValidationSupport.ValidateForComplete(
                MaterialKindDisplay,
                DispositionMethod,
                TransferTarget,
                OtherRemark,
                Reason,
                BuildItems(),
                ArchiveRoomHead,
                _archiveRoomHeadDate,
                ArchiveDeputyPresident,
                _archiveDeputyPresidentDate,
                Attachments.ToList(),
                physicalRemovalConfirmed: true);
            if (validationErrors.Count > 0)
            {
                _dialogService.ShowError(
                    "办结前信息完整性校验未通过：" + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, validationErrors));
                return;
            }

            await _service.CompleteAsync(_record.Id, RequireUser(), physicalRemovalConfirmed: true);
            _hasCommittedChanges = true;
            _dialogService.ShowMessage("处置单已办结，台账已标为已离库并从档案柜撤盒。");
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
            var (ok, message, _) = await _service.UploadAttachmentAsync(
                _record.Id,
                UploadCategory,
                Path.GetFileName(path),
                Path.GetExtension(path),
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

    private async Task ReloadAttachmentsAsync()
    {
        Attachments.Clear();
        if (string.IsNullOrWhiteSpace(_record.DisposalNo))
        {
            return;
        }

        foreach (var item in await _service.GetAttachmentsAsync(_record.DisposalNo))
        {
            Attachments.Add(item);
        }
    }

    private async Task ReloadAsync()
    {
        var latest = await _service.GetRecordByIdAsync(_record.Id);
        if (latest == null)
        {
            return;
        }

        _record = latest;
        BindFromRecord();
        if (CanEditHeader)
        {
            await LoadCandidatesAsync();
        }

        await ReloadAttachmentsAsync();
    }

    private User RequireUser() =>
        _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
}
