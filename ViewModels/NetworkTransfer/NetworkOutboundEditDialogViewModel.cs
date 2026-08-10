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
    /// 出网申请办理弹窗。仅可选加工产出；目的地=立档时不跟踪中间过程介质。
    /// </summary>
    public sealed class NetworkOutboundEditDialogViewModel : ViewModelBase
    {
        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly NetworkTransferWorkspaceMode _mode;
        private NetworkOutboundRecord _record;
        private bool _hasCommittedChanges;
        private string _outboundNo = string.Empty;
        private string _destinationKind = string.Empty;
        private string _projectName = string.Empty;
        private string _year = string.Empty;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _prodLeader = string.Empty;
        private DateTime? _prodDate = DateTime.Today;
        private string _rndLeader = string.Empty;
        private DateTime? _rndDate = DateTime.Today;
        private string _deputyLeader = string.Empty;
        private DateTime? _deputyDate = DateTime.Today;
        private string _deliverer = string.Empty;
        private DateTime? _deliverDate = DateTime.Today;
        private string _administrator = string.Empty;
        private DateTime? _adminDate = DateTime.Today;
        private string _deptLeader = string.Empty;
        private DateTime? _deptDate = DateTime.Today;
        private string _uploadCategory = NetworkTransferDomainValues.AttachmentCategorySignedForm;

        public NetworkOutboundEditDialogViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService,
            NetworkOutboundRecord record,
            NetworkTransferWorkspaceMode mode)
        {
            ArgumentNullException.ThrowIfNull(record);
            _service = service;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _record = record;
            _mode = mode;

            RefreshCandidatesCommand = new RelayCommand(async _ => await LoadCandidatesAsync(), _ => CanEditHeader);
            AddSelectedCommand = new RelayCommand(_ => AddSelected(), _ => CanEditHeader && AvailableAssets.Any(a => a.IsSelected));
            RemoveItemCommand = new RelayCommand(item =>
            {
                if (item is NetworkOutboundItem row) Items.Remove(row);
                CommandManager.InvalidateRequerySuggested();
            }, item => CanEditHeader && item is NetworkOutboundItem);
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmHandoverCommand = new RelayCommand(async _ => await ConfirmHandoverAsync(), _ => CanConfirmHandover);
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
        public string WindowTitle => $"出网申请 · {(string.IsNullOrWhiteSpace(OutboundNo) ? "待编单" : OutboundNo)} · {StatusDisplay}";
        public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(_record.Status);
        public string BannerText =>
            "出网仅可选「加工产出」且状态为在网的台账对象。目的地为资料室立档时不登记中间过程介质，办结后自动生成建档草稿。";

        public ObservableCollection<string> DestinationKindOptions { get; } = new(NetworkTransferDomainValues.DestinationKindOptions);
        public ObservableCollection<string> UploadCategoryOptions { get; } = new(NetworkTransferDomainValues.AttachmentCategoryOptions);
        public ObservableCollection<NetworkOnNetAssetCandidate> AvailableAssets { get; } = new();
        public ObservableCollection<NetworkOutboundItem> Items { get; } = new();
        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public bool CanEditHeader =>
            _mode == NetworkTransferWorkspaceMode.Application
            && _record.Status == NetworkOutboundRecord.StatusDraft
            && (ArchiveRegisterBusinessRules.CanSubmitApplication(_userContextService.CurrentUser)
                || ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

        public bool CanSubmit => CanEditHeader && Items.Count > 0;
        public bool CanApprove =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkOutboundRecord.StatusSubmitted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanConfirmHandover =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkOutboundRecord.StatusApproved
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanUploadAttachment =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status is NetworkOutboundRecord.StatusApproved or NetworkOutboundRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool CanComplete =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkOutboundRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        public bool ShowApprovalPanel =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status >= NetworkOutboundRecord.StatusSubmitted
            && _record.Status != NetworkOutboundRecord.StatusWithdrawn
            && _record.Status != NetworkOutboundRecord.StatusForceWithdrawn;

        public string OutboundNo { get => _outboundNo; set => SetProperty(ref _outboundNo, value); }
        public string DestinationKind { get => _destinationKind; set => SetProperty(ref _destinationKind, value); }
        public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }
        public string Year { get => _year; set => SetProperty(ref _year, value); }
        public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
        public string Remark { get => _remark; set => SetProperty(ref _remark, value); }
        public string ProdLeader { get => _prodLeader; set => SetProperty(ref _prodLeader, value); }
        public DateTime? ProdDate { get => _prodDate; set => SetProperty(ref _prodDate, value); }
        public string RndLeader { get => _rndLeader; set => SetProperty(ref _rndLeader, value); }
        public DateTime? RndDate { get => _rndDate; set => SetProperty(ref _rndDate, value); }
        public string DeputyLeader { get => _deputyLeader; set => SetProperty(ref _deputyLeader, value); }
        public DateTime? DeputyDate { get => _deputyDate; set => SetProperty(ref _deputyDate, value); }
        public string Deliverer { get => _deliverer; set => SetProperty(ref _deliverer, value); }
        public DateTime? DeliverDate { get => _deliverDate; set => SetProperty(ref _deliverDate, value); }
        public string Administrator { get => _administrator; set => SetProperty(ref _administrator, value); }
        public DateTime? AdminDate { get => _adminDate; set => SetProperty(ref _adminDate, value); }
        public string DeptLeader { get => _deptLeader; set => SetProperty(ref _deptLeader, value); }
        public DateTime? DeptDate { get => _deptDate; set => SetProperty(ref _deptDate, value); }
        public string UploadCategory { get => _uploadCategory; set => SetProperty(ref _uploadCategory, value); }

        public RelayCommand RefreshCandidatesCommand { get; }
        public RelayCommand AddSelectedCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmHandoverCommand { get; }
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
                    var latest = await _service.GetOutboundByIdAsync(_record.Id);
                    if (latest != null) _record = latest;
                }
                else if (string.IsNullOrWhiteSpace(_record.OutboundNo))
                {
                    _record.OutboundNo = await _service.GenerateNextOutboundNoAsync();
                }

                BindFromRecord();
                if (CanEditHeader) await LoadCandidatesAsync();
                await ReloadAttachmentsAsync();
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private void BindFromRecord()
        {
            OutboundNo = _record.OutboundNo;
            DestinationKind = _record.DestinationKind;
            ProjectName = _record.ProjectName;
            Year = _record.Year;
            Reason = _record.Reason;
            Remark = _record.Remark;
            ProdLeader = _record.ProdLeader;
            ProdDate = _record.ProdDate ?? DateTime.Today;
            RndLeader = _record.RndLeader;
            RndDate = _record.RndDate ?? DateTime.Today;
            DeputyLeader = _record.DeputyLeader;
            DeputyDate = _record.DeputyDate ?? DateTime.Today;
            Deliverer = string.IsNullOrWhiteSpace(_record.Deliverer) ? _record.ApplicantName : _record.Deliverer;
            DeliverDate = _record.DeliverDate ?? DateTime.Today;
            Administrator = string.IsNullOrWhiteSpace(_record.Administrator)
                ? _userContextService.CurrentUser?.RealName ?? string.Empty
                : _record.Administrator;
            AdminDate = _record.AdminDate ?? DateTime.Today;
            DeptLeader = _record.DeptLeader;
            DeptDate = _record.DeptDate ?? DateTime.Today;
            Items.Clear();
            foreach (var item in _record.Items.OrderBy(i => i.SortOrder))
                Items.Add(item);
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(ShowApprovalPanel));
        }

        private async Task LoadCandidatesAsync()
        {
            AvailableAssets.Clear();
            var list = await _service.GetSelectableOutboundAssetsAsync(_record.Id > 0 ? _record.Id : null);
            HashSet<int> selected = Items.Select(i => i.OnNetAssetId).ToHashSet();
            foreach (var asset in list.Where(a => !selected.Contains(a.Id)))
                AvailableAssets.Add(new NetworkOnNetAssetCandidate(asset));
        }

        private void AddSelected()
        {
            foreach (var candidate in AvailableAssets.Where(a => a.IsSelected).ToList())
            {
                Items.Add(new NetworkOutboundItem
                {
                    SortOrder = Items.Count + 1,
                    OnNetAssetId = candidate.Asset.Id,
                    AssetNo = candidate.Asset.AssetNo,
                    AssetKind = candidate.Asset.AssetKind,
                    AssetName = candidate.Asset.AssetName,
                    ServerPath = candidate.Asset.ServerPath,
                    ConfidentialLevel = candidate.Asset.ConfidentialLevel,
                    DataSizeText = candidate.Asset.DataSizeText,
                    ProjectName = candidate.Asset.ProjectName,
                    Year = candidate.Asset.Year,
                    CreatedAt = DateTime.Now
                });
                AvailableAssets.Remove(candidate);
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private NetworkOutboundRecord BuildDraft() => new()
        {
            Id = _record.Id,
            OutboundNo = OutboundNo,
            DestinationKind = DestinationKind,
            ProjectName = ProjectName,
            Year = Year,
            Reason = Reason,
            Remark = Remark
        };

        private async Task SaveDraftAsync()
        {
            try
            {
                var ids = Items.Select(i => i.OnNetAssetId).ToList();
                _record = _record.Id > 0
                    ? await _service.UpdateOutboundDraftAsync(BuildDraft(), ids, RequireUser())
                    : await _service.CreateOutboundDraftAsync(BuildDraft(), ids, RequireUser());
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
                await _service.SubmitOutboundAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("已提交审批。");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task ApproveAsync()
        {
            try
            {
                await _service.ApproveOutboundAsync(new NetworkOutboundRecord
                {
                    Id = _record.Id,
                    ProdLeader = ProdLeader,
                    ProdDate = ProdDate,
                    RndLeader = RndLeader,
                    RndDate = RndDate,
                    DeputyLeader = DeputyLeader,
                    DeputyDate = DeputyDate
                }, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("审批已通过。");
                await ReloadAsync();
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task ConfirmHandoverAsync()
        {
            try
            {
                await _service.ConfirmOutboundHandoverAsync(new NetworkOutboundRecord
                {
                    Id = _record.Id,
                    Deliverer = Deliverer,
                    DeliverDate = DeliverDate,
                    Administrator = Administrator,
                    AdminDate = AdminDate,
                    DeptLeader = DeptLeader,
                    DeptDate = DeptDate
                }, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("出网交接已确认。");
                await ReloadAsync();
            }
            catch (Exception ex) { _dialogService.ShowError(ex.Message); }
        }

        private async Task CompleteAsync()
        {
            try
            {
                await _service.CompleteOutboundAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                string tip = "出网单已办结。";
                var latest = await _service.GetOutboundByIdAsync(_record.Id);
                if (latest != null && !string.IsNullOrWhiteSpace(latest.TargetRegisterFormNo))
                    tip += $" 已生成建档草稿【{latest.TargetRegisterFormNo}】。";
                _dialogService.ShowMessage(tip);
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
                    NetworkTransferDomainValues.OutboundAttachmentBusinessType,
                    _record.Id, _record.OutboundNo, UploadCategory,
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
            if (string.IsNullOrWhiteSpace(_record.OutboundNo)) return;
            foreach (var item in await _service.GetAttachmentsAsync(
                         NetworkTransferDomainValues.OutboundAttachmentBusinessType, _record.OutboundNo))
                Attachments.Add(item);
        }

        private async Task ReloadAsync()
        {
            var latest = await _service.GetOutboundByIdAsync(_record.Id);
            if (latest == null) return;
            _record = latest;
            BindFromRecord();
            await ReloadAttachmentsAsync();
        }

        private User RequireUser() =>
            _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
    }

    public sealed class NetworkOnNetAssetCandidate : ViewModelBase
    {
        private bool _isSelected;
        public NetworkOnNetAssetCandidate(NetworkOnNetAsset asset) => Asset = asset;
        public NetworkOnNetAsset Asset { get; }
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
        public string Display => $"{Asset.AssetNo} | {Asset.AssetName} | {Asset.ServerPath}";
    }
}
