using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 历史存档离库处置办理弹窗。
/// </summary>
public sealed partial class HistoryArchiveDisposalEditDialogViewModel : ViewModelBase
{
    private readonly IHistoryArchiveDisposalService _service;
    private readonly IDialogService _dialogService;
    private readonly IUserContextService _userContextService;
    private readonly IUserService _userService;
    private readonly IApprovalWorkflowService _approvalWorkflowService;
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
    private string _uploadCategory = HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm;
    private HistoryArchiveDisposalBoxCandidateRow? _selectedCandidate;
    private HistoryArchiveDisposalItemRow? _selectedItem;

    public HistoryArchiveDisposalEditDialogViewModel(
        IHistoryArchiveDisposalService service,
        IDialogService dialogService,
        IUserContextService userContextService,
        IUserService userService,
        IApprovalWorkflowService approvalWorkflowService,
        HistoryArchiveDisposalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _service = service;
        _dialogService = dialogService;
        _userContextService = userContextService;
        _userService = userService;
        _approvalWorkflowService = approvalWorkflowService;
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
        CaptureFromDocumentCameraCommand = new RelayCommand(
            async _ => await CaptureFromDocumentCameraAsync(),
            _ => CanUploadAttachment);
        UploadSignedFormAttachmentCommand = new RelayCommand(
            async _ => await UploadAttachmentByCategoryAsync(HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm),
            _ => CanUploadMandatoryAttachment);
        CaptureSignedFormAttachmentCommand = new RelayCommand(
            async _ => await CaptureAttachmentByCategoryAsync(HistoryArchiveDisposalDomainValues.AttachmentCategorySignedForm),
            _ => CanUploadMandatoryAttachment);
        UploadScenePhotoAttachmentCommand = new RelayCommand(
            async _ => await UploadAttachmentByCategoryAsync(HistoryArchiveDisposalDomainValues.AttachmentCategoryScenePhoto),
            _ => CanUploadMandatoryAttachment && RequiresScenePhoto);
        CaptureScenePhotoAttachmentCommand = new RelayCommand(
            async _ => await CaptureAttachmentByCategoryAsync(HistoryArchiveDisposalDomainValues.AttachmentCategoryScenePhoto),
            _ => CanUploadMandatoryAttachment && RequiresScenePhoto);
        UploadOtherAttachmentCommand = new RelayCommand(
            async _ => await UploadAttachmentByCategoryAsync(HistoryArchiveDisposalDomainValues.AttachmentCategoryOther),
            _ => CanUploadOtherAttachment);
        CaptureOtherAttachmentCommand = new RelayCommand(
            async _ => await CaptureAttachmentByCategoryAsync(HistoryArchiveDisposalDomainValues.AttachmentCategoryOther),
            _ => CanUploadOtherAttachment);
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
        }, item => item is SystemAttachment && CanUploadMandatoryAttachment);
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
    public ObservableCollection<SystemAttachment> SignedFormAttachments { get; } = new();
    public ObservableCollection<SystemAttachment> ScenePhotoAttachments { get; } = new();
    public ObservableCollection<SystemAttachment> OtherAttachments { get; } = new();

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

    /// <summary>办结后资料管理员可增补「其他附件」。</summary>
    public bool CanSupplementOtherAttachments =>
        ApprovalWorkflowButtonSupport.CanSupplementOtherAttachments(
            _record.Status == HistoryArchiveDisposalRecord.StatusCompleted,
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

    public bool CanUploadAttachment =>
        ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
        && (CanSupplementOtherAttachments
            || _record.Status is HistoryArchiveDisposalRecord.StatusApproved
                or HistoryArchiveDisposalRecord.StatusSignedUploaded);

    /// <summary>签批单/处置资料照片：仅确认可上传后、办结前可传。</summary>
    public bool CanUploadMandatoryAttachment =>
        CanUploadAttachment && !CanSupplementOtherAttachments;

    /// <summary>其他附件：确认可上传后及办结后均可增补。</summary>
    public bool CanUploadOtherAttachment => CanUploadAttachment;

    /// <summary>当前处置方式是否须上传处置资料照片。</summary>
    public bool RequiresScenePhoto =>
        HistoryArchiveDisposalDomainValues.RequiresScenePhoto(DispositionMethod);

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
        ? "确认办结后更新历史台账并撤柜。"
        : "请先上传签批单（销毁须处置资料照片）后再确认办结。";

    public bool CanComplete =>
        _record.Status == HistoryArchiveDisposalRecord.StatusSignedUploaded
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
    public bool CanEditReviewSigners =>
        _record.Status is HistoryArchiveDisposalRecord.StatusApproved
            or HistoryArchiveDisposalRecord.StatusSignedUploaded
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

    /// <summary>签字卡可编辑：审批通过前或已审批后改签。</summary>
    public bool CanEditSigners => CanEditReviewSigners || CanApprove;

    public DateTime? SignatureDateMin =>
        ApprovalSignatureDateSupport.ResolveMinDate(_record.FirstPrintedAt, _record.LastPrintedAt, _record.PrintCount);

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
            OnPropertyChanged(nameof(RequiresScenePhoto));
        }
    }

    public string TransferTarget { get => _transferTarget; set => SetProperty(ref _transferTarget, value); }
    public string OtherRemark { get => _otherRemark; set => SetProperty(ref _otherRemark, value); }
    public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
    public string Remark { get => _remark; set => SetProperty(ref _remark, value); }

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
            await ReloadSignerEnableFlagsAsync();
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
            RebuildItemsFromRecord();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(MaterialKindDisplay));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditMaterialKind));
            OnPropertyChanged(nameof(CanPrint));
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
            OnPropertyChanged(nameof(CanEditReviewSigners));
            OnPropertyChanged(nameof(CanEditSigners));
            OnPropertyChanged(nameof(SignatureDateMin));
            OnPropertyChanged(nameof(CanAddSelected));
            OnPropertyChanged(nameof(CanAddAll));
            OnPropertyChanged(nameof(CanClearItems));

            if (CanSupplementOtherAttachments)
            {
                UploadCategory = HistoryArchiveDisposalDomainValues.AttachmentCategoryOther;
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
            NotifyItemListsChanged();
            NotifyShellAliasPropertiesChanged();
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
            if (!_dialogService.ShowConfirm("确认审批通过？将自动填写资料室签字、分管资料院长签字的姓名与日期。"))
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

    private async Task PersistReviewSignersAsync()
    {
        if (_suppressReviewSignerPersist || !CanEditReviewSigners || _record.Id <= 0)
        {
            return;
        }

        try
        {
            await _service.UpdateReviewSignersAsync(
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
                BusinessType = ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal,
                FieldValues = ApprovalChainApplySupport.BuildHistoryDisposalFieldValues(_record)
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
                _enableDeptHead,
                DeptHead,
                _deptHeadDate,
                _enableArchiveRoomHead,
                ArchiveRoomHead,
                _archiveRoomHeadDate,
                _enableProductionHead,
                ProductionHead,
                _productionHeadDate,
                _enableArchiveDeputyPresident,
                ArchiveDeputyPresident,
                _archiveDeputyPresidentDate,
                _enableProductionVicePresident,
                ProductionVicePresident,
                _productionVicePresidentDate,
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
            if (CanSupplementOtherAttachments)
            {
                UploadCategory = HistoryArchiveDisposalDomainValues.AttachmentCategoryOther;
            }

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

    private async Task CaptureFromDocumentCameraAsync()
    {
        try
        {
            if (CanSupplementOtherAttachments)
            {
                UploadCategory = HistoryArchiveDisposalDomainValues.AttachmentCategoryOther;
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

            string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(DisposalNo, UploadCategory, "史离处");
            var (ok, message, _) = await _service.UploadAttachmentAsync(
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
        IEnumerable<SystemAttachment> list = string.IsNullOrWhiteSpace(_record.DisposalNo)
            ? Array.Empty<SystemAttachment>()
            : await _service.GetAttachmentsAsync(_record.DisposalNo);
        var policy = ApprovalAttachmentPolicySupport.Get(ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal);
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

    private async Task ReloadAsync()
    {
        var latest = await _service.GetRecordByIdAsync(_record.Id);
        if (latest == null)
        {
            return;
        }

        _record = latest;
        BindFromRecord();
        await ReloadSignerEnableFlagsAsync();
        if (CanEditHeader)
        {
            await LoadCandidatesAsync();
        }

        await ReloadAttachmentsAsync();
    }

    private User RequireUser() =>
        _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
}
