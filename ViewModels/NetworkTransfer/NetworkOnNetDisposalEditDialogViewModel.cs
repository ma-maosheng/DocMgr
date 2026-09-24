using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.Services.NetworkTransfer;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 在网处置办理弹窗。
    /// </summary>
    public sealed partial class NetworkOnNetDisposalEditDialogViewModel : ViewModelBase
    {
        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IUserService _userService;
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private NetworkOnNetDisposalRecord _record;
        private bool _hasCommittedChanges;
        private bool _suppressReviewSignerPersist;
        private string _disposalNo = string.Empty;
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
        private string _batchReason = NetworkTransferDomainValues.DisposalReasonExpired;
        private string _batchMethod = NetworkTransferDomainValues.DisposalMethodDelete;
        private string _uploadCategory = NetworkTransferDomainValues.AttachmentCategorySignedForm;
        private NetworkOnNetAssetCandidate? _selectedAsset;
        private NetworkOnNetDisposalItemRow? _selectedItem;

        public NetworkOnNetDisposalEditDialogViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService,
            IUserService userService,
            IApprovalWorkflowService approvalWorkflowService,
            NetworkOnNetDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _service = service;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _userService = userService;
            _approvalWorkflowService = approvalWorkflowService;
            _record = record;

            RefreshCandidatesCommand = new RelayCommand(async _ => await LoadCandidatesAsync(), _ => CanEditHeader);
            ViewAssetDetailCommand = new RelayCommand(async item => await ViewAssetDetailAsync(item), item =>
                NetworkOnNetAssetDetailTextSupport.Resolve(item, ResolveCurrentAsset()) != null);
            AddSelectedCommand = new RelayCommand(_ => AddSelected(), _ => CanAddSelected);
            RemoveItemCommand = new RelayCommand(item => RemoveItem(item as NetworkOnNetDisposalItemRow),
                item => CanEditHeader && item is NetworkOnNetDisposalItemRow);
            ApplyBatchToItemsCommand = new RelayCommand(_ => ApplyBatchToItems(), _ => CanEditHeader);
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
                async _ => await UploadAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            CaptureSignedFormAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategorySignedForm),
                _ => CanUploadMandatoryAttachment);
            UploadOtherAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            CaptureOtherAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategoryOther),
                _ => CanUploadOtherAttachment);
            DeleteAttachmentCommand = new RelayCommand(async item =>
            {
                if (item is not SystemAttachment att) return;
                var (ok, msg) = await _service.DeleteAttachmentAsync(att.Id, RequireUser());
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
        public string WindowTitle => $"在网处置 · {(string.IsNullOrWhiteSpace(DisposalNo) ? "待编单" : DisposalNo)} · {StatusDisplay}";
        public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(_record.Status);

        public ObservableCollection<string> ReasonOptions { get; } = new(NetworkTransferDomainValues.DisposalReasonOptions);
        public ObservableCollection<string> MethodOptions { get; } = new(NetworkTransferDomainValues.DisposalMethodOptions);
        public ObservableCollection<string> UploadCategoryOptions { get; } = new(NetworkTransferDomainValues.AttachmentCategoryOptions);
        public ObservableCollection<NetworkOnNetAssetCandidate> AvailableAssets { get; } = new();
        public NetworkOnNetAssetCandidate? SelectedAsset
        {
            get => _selectedAsset;
            set { if (SetProperty(ref _selectedAsset, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        public NetworkOnNetDisposalItemRow? SelectedItem
        {
            get => _selectedItem;
            set { if (SetProperty(ref _selectedItem, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        public ObservableCollection<NetworkOnNetDisposalItemRow> Items { get; } = new();
        public string AvailableAssetsTitle => $"候选在网对象（{AvailableAssets.Count}）";
        public string SelectedItemsTitle => $"已选明细（{Items.Count}）";
        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<SystemAttachment> SignedFormAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> OtherAttachments { get; } = new();

        public bool CanEditHeader =>
            _record.Status == NetworkOnNetDisposalRecord.StatusDraft
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanSubmit => CanEditHeader && Items.Count > 0;
        /// <summary>草稿且至少勾选一条候选时，可加入明细。</summary>
        public bool CanAddSelected => CanEditHeader && AvailableAssets.Any(item => item.IsSelected);
        /// <summary>已提交及之后（非撤回）可打印签批单。</summary>
        public bool CanPrint =>
            _record.Id > 0
            && _record.Status is not NetworkOnNetDisposalRecord.StatusDraft
                and not NetworkOnNetDisposalRecord.StatusWithdrawn
                and not NetworkOnNetDisposalRecord.StatusForceWithdrawn;
        public bool CanApprove =>
            _record.Status == NetworkOnNetDisposalRecord.StatusSubmitted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanConfirmUpload =>
            _record.Status == NetworkOnNetDisposalRecord.StatusApproved
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        /// <summary>办结后资料管理员可增补「其他附件」。</summary>
        public bool CanSupplementOtherAttachments =>
            ApprovalWorkflowButtonSupport.CanSupplementOtherAttachments(
                _record.Status == NetworkOnNetDisposalRecord.StatusCompleted,
                ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

        public bool CanUploadAttachment =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
            && (CanSupplementOtherAttachments
                || _record.Status is NetworkOnNetDisposalRecord.StatusApproved
                    or NetworkOnNetDisposalRecord.StatusSignedUploaded);

        /// <summary>签批单：仅确认可上传后、办结前可传。</summary>
        public bool CanUploadMandatoryAttachment =>
            CanUploadAttachment && !CanSupplementOtherAttachments;

        /// <summary>其他附件：确认可上传后及办结后均可增补。</summary>
        public bool CanUploadOtherAttachment => CanUploadAttachment;

        public string UploadAttachmentHintText => CanSupplementOtherAttachments
            ? "办结后仅可增补「其他附件」；不可删除已有附件。"
            : "请在「确认可上传」后分区上传签批单；办结前仍可继续补传。";

        public string ApproveHintText => CanApprove
            ? "请按线下签批结果执行审批通过；通过后点击「确认可上传」。"
            : "仅「已提交」状态可审批通过。";

        public string ConfirmUploadHintText => CanConfirmUpload
            ? "确认后可分区上传签批单。"
            : "请先执行「审批通过」。";

        public string CompleteHintText => CanComplete
            ? "确认办结后更新在网台账终态。"
            : "请先上传签批单后再确认办结。";

        public bool CanComplete =>
            _record.Status == NetworkOnNetDisposalRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        /// <summary>已审批后可改审核审批人姓名。</summary>
        public bool CanEditReviewSigners =>
            _record.Status is NetworkOnNetDisposalRecord.StatusApproved or NetworkOnNetDisposalRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        /// <summary>签字卡可编辑：审批通过前或已审批后改签。</summary>
        public bool CanEditSigners => CanEditReviewSigners || CanApprove;

        public DateTime? SignatureDateMin =>
            ApprovalSignatureDateSupport.ResolveMinDate(_record.FirstPrintedAt, _record.LastPrintedAt, _record.PrintCount);

        public string DisposalNo { get => _disposalNo; set => SetProperty(ref _disposalNo, value); }
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

        /// <summary>资料室负责人姓名（审批通过后自动填写，可改）。</summary>
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

        /// <summary>分管资料副院长姓名（审批通过后自动填写，可改）。</summary>
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

        public string BatchReason { get => _batchReason; set => SetProperty(ref _batchReason, value); }
        public string BatchMethod { get => _batchMethod; set => SetProperty(ref _batchMethod, value); }
        public string UploadCategory { get => _uploadCategory; set => SetProperty(ref _uploadCategory, value); }

        public RelayCommand RefreshCandidatesCommand { get; }
        public RelayCommand ViewAssetDetailCommand { get; }
        public RelayCommand AddSelectedCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand ApplyBatchToItemsCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        /// <summary>打开签批单打印预览。</summary>
        public RelayCommand PrintCommand { get; }
        /// <summary>提交后审批通过，并自动填写审核审批姓名与日期。</summary>
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
                    var latest = await _service.GetDisposalByIdAsync(_record.Id);
                    if (latest != null) _record = latest;
                }
                else if (string.IsNullOrWhiteSpace(_record.DisposalNo))
                    _record.DisposalNo = await _service.GenerateNextDisposalNoAsync();

                await BindFromRecordAsync();
                await ReloadSignerEnableFlagsAsync();
                if (CanEditHeader) await LoadCandidatesAsync();
                await ReloadAttachmentsAsync();
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private void BindFromRecord()
        {
            DisposalNo = _record.DisposalNo;
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
                OnPropertyChanged(nameof(CanEditHeader));
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
                OnPropertyChanged(nameof(CanAddSelected));

                if (CanSupplementOtherAttachments)
                {
                    UploadCategory = NetworkTransferDomainValues.AttachmentCategoryOther;
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
            int? keepSelectedId = SelectedItem?.OnNetAssetId;
            Items.Clear();
            foreach (var item in _record.Items.OrderBy(i => i.SortOrder))
                Items.Add(NetworkOnNetDisposalItemRow.FromPersisted(item));
            SelectedItem = keepSelectedId is int id
                ? Items.FirstOrDefault(row => row.OnNetAssetId == id)
                : null;
        }

        private async Task BindFromRecordAsync()
        {
            BindFromRecord();
            await EnrichSelectedItemsAsync();
        }

        private async Task EnrichSelectedItemsAsync()
        {
            List<int> ids = Items.Select(row => row.OnNetAssetId).Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                return;

            IReadOnlyList<NetworkOnNetAsset> assets = await _service.GetOnNetAssetsByIdsAsync(ids);
            Dictionary<int, NetworkOnNetAsset> assetsById = assets.ToDictionary(item => item.Id);
            foreach (var row in Items)
            {
                if (assetsById.TryGetValue(row.OnNetAssetId, out NetworkOnNetAsset? asset))
                    row.ReplaceAsset(asset);
            }
        }

        private async Task LoadCandidatesAsync()
        {
            var list = await _service.GetSelectableDisposalAssetsAsync(_record.Id > 0 ? _record.Id : null);
            HashSet<int> selectedIds = Items.Select(row => row.OnNetAssetId).ToHashSet();
            int? keepSelectedId = SelectedAsset?.Asset.Id;
            ClearAvailableAssets();
            foreach (var asset in list.Where(item => !selectedIds.Contains(item.Id)))
                AttachAvailableAsset(new NetworkOnNetAssetCandidate(asset));
            SelectedAsset = keepSelectedId is int id
                ? AvailableAssets.FirstOrDefault(item => item.Asset.Id == id)
                : null;
            NotifyItemListsChanged();
        }

        private async Task ViewAssetDetailAsync(object? parameter)
        {
            NetworkOnNetAsset? asset = NetworkOnNetAssetDetailTextSupport.Resolve(parameter, ResolveCurrentAsset());
            if (asset == null)
            {
                _dialogService.ShowMessage("请先选择一条在网对象。");
                return;
            }

            try
            {
                await NetworkOnNetAssetDetailTextSupport.ShowAsync(_service, _dialogService, asset);
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private NetworkOnNetAsset? ResolveCurrentAsset() =>
            SelectedAsset?.Asset ?? SelectedItem?.Asset;

        private void AddSelected()
        {
            List<NetworkOnNetAssetCandidate> picked = AvailableAssets.Where(item => item.IsSelected).ToList();
            if (picked.Count == 0)
                return;

            HashSet<int> existingIds = Items.Select(row => row.OnNetAssetId).ToHashSet();
            foreach (var candidate in picked)
            {
                if (!existingIds.Add(candidate.Asset.Id))
                    continue;

                Items.Add(NetworkOnNetDisposalItemRow.FromCandidate(candidate, BatchReason, BatchMethod));
                DetachAvailableAsset(candidate);
            }

            SelectedAsset = AvailableAssets.FirstOrDefault(item => item.IsSelected);
            NotifyItemListsChanged();
        }

        private void RemoveItem(NetworkOnNetDisposalItemRow? row)
        {
            if (row == null || !Items.Remove(row))
                return;

            RestoreToAvailable(row);
            if (ReferenceEquals(SelectedItem, row))
                SelectedItem = null;
            NotifyItemListsChanged();
        }

        private void RestoreToAvailable(NetworkOnNetDisposalItemRow row)
        {
            if (AvailableAssets.Any(item => item.Asset.Id == row.OnNetAssetId))
                return;

            AttachAvailableAsset(row.ToCandidate(), insertAtFront: true);
        }

        private void ApplyBatchToItems()
        {
            if (Items.Count == 0)
            {
                _dialogService.ShowMessage("请先勾选在网对象并点「加入明细」，再赋值原因和方式。");
                return;
            }

            string reason = BatchReason?.Trim() ?? string.Empty;
            string method = BatchMethod?.Trim() ?? string.Empty;
            if (!NetworkTransferDomainValues.DisposalReasonOptions.Contains(reason, StringComparer.Ordinal)
                || !NetworkTransferDomainValues.DisposalMethodOptions.Contains(method, StringComparer.Ordinal))
            {
                _dialogService.ShowMessage("请选择有效的处置原因和处置方式。");
                return;
            }

            foreach (var row in Items)
                row.ApplyDisposition(reason, method);

            _dialogService.ShowMessage($"已将原因「{reason}」、方式「{method}」写入 {Items.Count} 条已选明细。");
        }

        private void NotifyItemListsChanged()
        {
            OnPropertyChanged(nameof(AvailableAssetsTitle));
            OnPropertyChanged(nameof(SelectedItemsTitle));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanAddSelected));
            CommandManager.InvalidateRequerySuggested();
        }

        private void AttachAvailableAsset(NetworkOnNetAssetCandidate candidate, bool insertAtFront = false)
        {
            candidate.SelectionChanged += OnCandidateSelectionChanged;
            if (AvailableAssets.Contains(candidate))
                return;

            if (insertAtFront)
                AvailableAssets.Insert(0, candidate);
            else
                AvailableAssets.Add(candidate);
        }

        private void DetachAvailableAsset(NetworkOnNetAssetCandidate candidate)
        {
            candidate.SelectionChanged -= OnCandidateSelectionChanged;
            AvailableAssets.Remove(candidate);
        }

        private void ClearAvailableAssets()
        {
            foreach (var candidate in AvailableAssets)
                candidate.SelectionChanged -= OnCandidateSelectionChanged;
            AvailableAssets.Clear();
        }

        private void OnCandidateSelectionChanged()
        {
            OnPropertyChanged(nameof(CanAddSelected));
            CommandManager.InvalidateRequerySuggested();
        }

        private List<NetworkOnNetDisposalItem> BuildItems() =>
            Items.Select((row, index) => new NetworkOnNetDisposalItem
            {
                SortOrder = index + 1,
                OnNetAssetId = row.OnNetAssetId,
                AssetNo = row.AssetNo,
                AssetKind = row.AssetKind,
                AssetName = row.AssetName,
                ServerPath = row.ServerPath,
                DisposalReason = row.DisposalReason,
                DispositionMethod = row.DispositionMethod,
                CreatedAt = DateTime.Now
            }).ToList();

        private async Task SaveDraftAsync()
        {
            try
            {
                await PersistDraftAsync();
                _dialogService.ShowMessage("草稿已保存。");
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task PersistDraftAsync()
        {
            var draft = new NetworkOnNetDisposalRecord
            {
                Id = _record.Id,
                DisposalNo = DisposalNo,
                Reason = Reason,
                Remark = Remark
            };
            _record = _record.Id > 0
                ? await _service.UpdateDisposalDraftAsync(draft, BuildItems(), RequireUser())
                : await _service.CreateDisposalDraftAsync(draft, BuildItems(), RequireUser());
            _hasCommittedChanges = true;
            await BindFromRecordAsync();
            await LoadCandidatesAsync();
        }

        private async Task SubmitAsync()
        {
            try
            {
                List<NetworkOnNetDisposalItem> items = BuildItems();
                IReadOnlyList<NetworkOnNetAsset> selectable = await _service.GetSelectableDisposalAssetsAsync(
                    _record.Id > 0 ? _record.Id : null);
                IReadOnlyList<string> validationErrors = NetworkOnNetDisposalValidationSupport.ValidateForSubmit(
                    Reason,
                    items,
                    selectable.Select(item => item.Id).ToHashSet());
                if (validationErrors.Count > 0)
                {
                    _dialogService.ShowError(
                        "提交前校验未通过：" + Environment.NewLine + Environment.NewLine
                        + string.Join(Environment.NewLine, validationErrors));
                    return;
                }

                await PersistDraftAsync();
                await _service.SubmitDisposalAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                await ReloadAsync();
                _dialogService.ShowMessage("已提交，可打印签批单。");
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task PrintAsync()
        {
            try
            {
                await PersistReviewSignersAsync();
                NetworkOnNetDisposalPrintData data = await _service.BuildDisposalPrintDataAsync(_record.Id);
                FlowDocument document = NetworkOnNetDisposalPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };
                await _service.RecordDisposalPrintAsync(_record.Id);
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

                await _service.ApproveDisposalAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("审批已通过，审核审批姓名已填写，可按实际签字人修改。");
                await ReloadAsync();
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task PersistReviewSignersAsync()
        {
            if (_suppressReviewSignerPersist || !CanEditReviewSigners || _record.Id <= 0)
                return;

            try
            {
                await _service.UpdateDisposalReviewSignersAsync(
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
                    BusinessType = ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal,
                    FieldValues = ApprovalChainApplySupport.BuildNetworkOnNetDisposalFieldValues(_record)
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
                await _service.ConfirmDisposalReadyForUploadAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("已确认可上传签批单。");
                await ReloadAsync();
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task CompleteAsync()
        {
            try
            {
                await PersistReviewSignersAsync();
                await ReloadAttachmentsAsync();

                IReadOnlyList<string> validationErrors = NetworkOnNetDisposalValidationSupport.ValidateForComplete(
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
                    Attachments.ToList());
                if (validationErrors.Count > 0)
                {
                    _dialogService.ShowError(
                        "办结前信息完整性校验未通过：" + Environment.NewLine + Environment.NewLine
                        + string.Join(Environment.NewLine, validationErrors));
                    return;
                }

                await _service.CompleteDisposalAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("处置单已办结。");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task UploadAttachmentAsync()
        {
            try
            {
                if (CanSupplementOtherAttachments)
                {
                    UploadCategory = NetworkTransferDomainValues.AttachmentCategoryOther;
                }

                string? path = _dialogService.OpenFileDialog("所有文件|*.*", "选择附件");
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
                byte[] content = await File.ReadAllBytesAsync(path);
                var (ok, message, _) = await _service.UploadAttachmentAsync(
                    NetworkTransferDomainValues.DisposalAttachmentBusinessType,
                    _record.Id, _record.DisposalNo, UploadCategory,
                    Path.GetFileName(path), Path.GetExtension(path), content.LongLength, content, RequireUser());
                if (!ok) { _dialogService.ShowError(message); return; }
                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
                await ReloadAsync();
                _dialogService.ShowMessage(message);
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task CaptureFromDocumentCameraAsync()
        {
            try
            {
                if (CanSupplementOtherAttachments)
                {
                    UploadCategory = NetworkTransferDomainValues.AttachmentCategoryOther;
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

                string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(DisposalNo, UploadCategory, "网处");
                var (ok, message, _) = await _service.UploadAttachmentAsync(
                    NetworkTransferDomainValues.DisposalAttachmentBusinessType,
                    _record.Id, _record.DisposalNo, UploadCategory,
                    fileName, ".jpg", captured.JpegContent.LongLength, captured.JpegContent, RequireUser());
                if (!ok) { _dialogService.ShowError(message); return; }
                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
                await ReloadAsync();
                _dialogService.ShowMessage(message);
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task ReloadAttachmentsAsync()
        {
            IEnumerable<SystemAttachment> list = string.IsNullOrWhiteSpace(_record.DisposalNo)
                ? Array.Empty<SystemAttachment>()
                : await _service.GetAttachmentsAsync(
                    NetworkTransferDomainValues.DisposalAttachmentBusinessType,
                    _record.DisposalNo);
            var policy = ApprovalAttachmentPolicySupport.Get(ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal);
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
            var latest = await _service.GetDisposalByIdAsync(_record.Id);
            if (latest == null) return;
            _record = latest;
            await BindFromRecordAsync();
            await ReloadSignerEnableFlagsAsync();
            if (CanEditHeader)
                await LoadCandidatesAsync();
            await ReloadAttachmentsAsync();
        }

        private User RequireUser() =>
            _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
    }
}
