using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 在网处置办理弹窗。
    /// </summary>
    public sealed class NetworkOnNetDisposalEditDialogViewModel : ViewModelBase
    {
        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private NetworkOnNetDisposalRecord _record;
        private bool _hasCommittedChanges;
        private string _disposalNo = string.Empty;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _approvalOpinion = "同意";
        private string _batchReason = NetworkTransferDomainValues.DisposalReasonExpired;
        private string _batchMethod = NetworkTransferDomainValues.DisposalMethodDelete;
        private string _uploadCategory = NetworkTransferDomainValues.AttachmentCategorySignedForm;

        public NetworkOnNetDisposalEditDialogViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService,
            NetworkOnNetDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _service = service;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _record = record;

            RefreshCandidatesCommand = new RelayCommand(async _ => await LoadCandidatesAsync(), _ => CanEditHeader);
            AddSelectedCommand = new RelayCommand(_ => AddSelected(), _ => CanEditHeader && AvailableAssets.Any(a => a.IsSelected));
            RemoveItemCommand = new RelayCommand(item =>
            {
                if (item is NetworkOnNetDisposalItemRow row) Items.Remove(row);
                CommandManager.InvalidateRequerySuggested();
            }, item => CanEditHeader && item is NetworkOnNetDisposalItemRow);
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmUploadCommand = new RelayCommand(async _ => await ConfirmUploadAsync(), _ => CanConfirmUpload);
            UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
            DeleteAttachmentCommand = new RelayCommand(async item =>
            {
                if (item is not SystemAttachment att) return;
                var (ok, msg) = await _service.DeleteAttachmentAsync(att.Id, RequireUser());
                if (!ok) { _dialogService.ShowError(msg); return; }
                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
            }, item => item is SystemAttachment && CanUploadAttachment);
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
        public ObservableCollection<NetworkOnNetDisposalItemRow> Items { get; } = new();
        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public bool CanEditHeader =>
            _record.Status == NetworkOnNetDisposalRecord.StatusDraft
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanSubmit => CanEditHeader && Items.Count > 0;
        public bool CanApprove =>
            _record.Status == NetworkOnNetDisposalRecord.StatusSubmitted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanConfirmUpload =>
            _record.Status == NetworkOnNetDisposalRecord.StatusApproved
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanUploadAttachment =>
            _record.Status is NetworkOnNetDisposalRecord.StatusApproved or NetworkOnNetDisposalRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanComplete =>
            _record.Status == NetworkOnNetDisposalRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public string DisposalNo { get => _disposalNo; set => SetProperty(ref _disposalNo, value); }
        public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
        public string Remark { get => _remark; set => SetProperty(ref _remark, value); }
        public string ApprovalOpinion { get => _approvalOpinion; set => SetProperty(ref _approvalOpinion, value); }
        public string BatchReason { get => _batchReason; set => SetProperty(ref _batchReason, value); }
        public string BatchMethod { get => _batchMethod; set => SetProperty(ref _batchMethod, value); }
        public string UploadCategory { get => _uploadCategory; set => SetProperty(ref _uploadCategory, value); }

        public RelayCommand RefreshCandidatesCommand { get; }
        public RelayCommand AddSelectedCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
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
                    var latest = await _service.GetDisposalByIdAsync(_record.Id);
                    if (latest != null) _record = latest;
                }
                else if (string.IsNullOrWhiteSpace(_record.DisposalNo))
                    _record.DisposalNo = await _service.GenerateNextDisposalNoAsync();

                BindFromRecord();
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
            Items.Clear();
            foreach (var item in _record.Items.OrderBy(i => i.SortOrder))
            {
                Items.Add(new NetworkOnNetDisposalItemRow
                {
                    OnNetAssetId = item.OnNetAssetId,
                    AssetNo = item.AssetNo,
                    AssetKind = item.AssetKind,
                    AssetName = item.AssetName,
                    ServerPath = item.ServerPath,
                    DisposalReason = item.DisposalReason,
                    DispositionMethod = item.DispositionMethod
                });
            }
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(CanEditHeader));
        }

        private async Task LoadCandidatesAsync()
        {
            AvailableAssets.Clear();
            var list = await _service.GetSelectableDisposalAssetsAsync(_record.Id > 0 ? _record.Id : null);
            HashSet<int> selected = Items.Select(i => i.OnNetAssetId).ToHashSet();
            foreach (var asset in list.Where(a => !selected.Contains(a.Id)))
                AvailableAssets.Add(new NetworkOnNetAssetCandidate(asset));
        }

        private void AddSelected()
        {
            foreach (var candidate in AvailableAssets.Where(a => a.IsSelected).ToList())
            {
                Items.Add(new NetworkOnNetDisposalItemRow
                {
                    OnNetAssetId = candidate.Asset.Id,
                    AssetNo = candidate.Asset.AssetNo,
                    AssetKind = candidate.Asset.AssetKind,
                    AssetName = candidate.Asset.AssetName,
                    ServerPath = candidate.Asset.ServerPath,
                    DisposalReason = BatchReason,
                    DispositionMethod = BatchMethod
                });
                AvailableAssets.Remove(candidate);
            }
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
                BindFromRecord();
                await LoadCandidatesAsync();
                _dialogService.ShowMessage("草稿已保存。");
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task SubmitAsync()
        {
            try
            {
                await SaveDraftAsync();
                await _service.SubmitDisposalAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("已提交。");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task ApproveAsync()
        {
            try
            {
                await _service.ApproveDisposalAsync(_record.Id, ApprovalOpinion, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("审批已通过。");
                await ReloadAsync();
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task ConfirmUploadAsync()
        {
            try
            {
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

        private async Task ReloadAttachmentsAsync()
        {
            Attachments.Clear();
            if (string.IsNullOrWhiteSpace(_record.DisposalNo)) return;
            foreach (var item in await _service.GetAttachmentsAsync(
                         NetworkTransferDomainValues.DisposalAttachmentBusinessType, _record.DisposalNo))
                Attachments.Add(item);
        }

        private async Task ReloadAsync()
        {
            var latest = await _service.GetDisposalByIdAsync(_record.Id);
            if (latest == null) return;
            _record = latest;
            BindFromRecord();
            await ReloadAttachmentsAsync();
        }

        private User RequireUser() =>
            _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
    }

    public sealed class NetworkOnNetDisposalItemRow : ViewModelBase
    {
        private string _disposalReason = string.Empty;
        private string _dispositionMethod = string.Empty;
        public int OnNetAssetId { get; set; }
        public string AssetNo { get; set; } = string.Empty;
        public string AssetKind { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string ServerPath { get; set; } = string.Empty;
        public string DisposalReason { get => _disposalReason; set => SetProperty(ref _disposalReason, value); }
        public string DispositionMethod { get => _dispositionMethod; set => SetProperty(ref _dispositionMethod, value); }
    }
}
