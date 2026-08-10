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
    /// 入网申请办理弹窗（草稿编辑 + 审批交接办结）。
    /// </summary>
    public sealed class NetworkInboundEditDialogViewModel : ViewModelBase
    {
        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly NetworkTransferWorkspaceMode _mode;
        private NetworkInboundRecord _record;
        private bool _hasCommittedChanges;
        private string _inboundNo = string.Empty;
        private string _sourceKind = string.Empty;
        private string _projectName = string.Empty;
        private string _year = string.Empty;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _sourceResultSetNo = string.Empty;
        private int? _sourceResultSetId;
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
        private SystemAttachment? _selectedAttachment;
        private string _manualAssetKind = NetworkTransferDomainValues.AssetKindJobData;
        private string _manualAssetName = string.Empty;
        private string _manualServerPath = string.Empty;

        public NetworkInboundEditDialogViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService,
            NetworkInboundRecord record,
            NetworkTransferWorkspaceMode mode)
        {
            ArgumentNullException.ThrowIfNull(record);
            _service = service;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _record = record;
            _mode = mode;

            ImportSearchResultSetCommand = new RelayCommand(async _ => await ImportSearchResultSetAsync(), _ => CanEditHeader && IsArchivedSource);
            AddManualItemCommand = new RelayCommand(_ => AddManualItem(), _ => CanEditHeader && !IsArchivedSource);
            RemoveItemCommand = new RelayCommand(item => RemoveItem(item as NetworkInboundItem), item => CanEditHeader && item is NetworkInboundItem);
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmHandoverCommand = new RelayCommand(async _ => await ConfirmHandoverAsync(), _ => CanConfirmHandover);
            UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
            DeleteAttachmentCommand = new RelayCommand(async item => await DeleteAttachmentAsync(item as SystemAttachment), item => item is SystemAttachment && CanUploadAttachment);
            ViewAttachmentCommand = new RelayCommand(item =>
            {
                if (item is SystemAttachment attachment)
                {
                    _dialogService.ShowSystemAttachmentView(attachment);
                }
            }, item => item is SystemAttachment);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"入网申请 · {(string.IsNullOrWhiteSpace(InboundNo) ? "待编单" : InboundNo)} · {StatusDisplay}";

        public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(_record.Status);

        public string BannerText =>
            "已立档资料入网：明细唯一来自电子资料检索结果集，不跟踪中间过程介质。外部离线等其他来源可手工录入明细。流程：草稿→提交→审批签字→确认入网交接→上传签批单→办结（写入在网台账）。";

        public ObservableCollection<string> SourceKindOptions { get; } = new(NetworkTransferDomainValues.SourceKindOptions);

        public ObservableCollection<string> AssetKindOptions { get; } = new(NetworkTransferDomainValues.AssetKindOptions);

        public ObservableCollection<string> UploadCategoryOptions { get; } = new(NetworkTransferDomainValues.AttachmentCategoryOptions);

        public ObservableCollection<NetworkInboundItem> Items { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public bool IsArchivedSource =>
            NetworkTransferDomainValues.IsArchivedElectronicSearchSource(SourceKind);

        public bool CanEditHeader =>
            _mode == NetworkTransferWorkspaceMode.Application
            && _record.Status == NetworkInboundRecord.StatusDraft
            && (ArchiveRegisterBusinessRules.CanSubmitApplication(_userContextService.CurrentUser)
                || ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

        public bool CanSubmit => CanEditHeader && Items.Count > 0;

        public bool CanApprove =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkInboundRecord.StatusSubmitted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanConfirmHandover =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkInboundRecord.StatusApproved
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanUploadAttachment =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status is NetworkInboundRecord.StatusApproved or NetworkInboundRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanComplete =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkInboundRecord.StatusSignedUploaded
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool ShowApprovalPanel =>
            _mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status >= NetworkInboundRecord.StatusSubmitted
            && _record.Status != NetworkInboundRecord.StatusWithdrawn
            && _record.Status != NetworkInboundRecord.StatusForceWithdrawn;

        public string InboundNo
        {
            get => _inboundNo;
            set => SetProperty(ref _inboundNo, value);
        }

        public string SourceKind
        {
            get => _sourceKind;
            set
            {
                if (SetProperty(ref _sourceKind, value))
                {
                    OnPropertyChanged(nameof(IsArchivedSource));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        public string Year
        {
            get => _year;
            set => SetProperty(ref _year, value);
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

        public int? SourceResultSetId
        {
            get => _sourceResultSetId;
            set => SetProperty(ref _sourceResultSetId, value);
        }

        public string SourceResultSetNo
        {
            get => _sourceResultSetNo;
            set => SetProperty(ref _sourceResultSetNo, value);
        }

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

        public string ManualAssetKind
        {
            get => _manualAssetKind;
            set => SetProperty(ref _manualAssetKind, value);
        }

        public string ManualAssetName
        {
            get => _manualAssetName;
            set => SetProperty(ref _manualAssetName, value);
        }

        public string ManualServerPath
        {
            get => _manualServerPath;
            set => SetProperty(ref _manualServerPath, value);
        }

        public RelayCommand ImportSearchResultSetCommand { get; }
        public RelayCommand AddManualItemCommand { get; }
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
                    var latest = await _service.GetInboundByIdAsync(_record.Id);
                    if (latest != null)
                    {
                        _record = latest;
                    }
                }
                else if (string.IsNullOrWhiteSpace(_record.InboundNo))
                {
                    _record.InboundNo = await _service.GenerateNextInboundNoAsync();
                }

                BindFromRecord();
                await ReloadAttachmentsAsync();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void BindFromRecord()
        {
            InboundNo = _record.InboundNo;
            SourceKind = _record.SourceKind;
            ProjectName = _record.ProjectName;
            Year = _record.Year;
            Reason = _record.Reason;
            Remark = _record.Remark;
            SourceResultSetId = _record.SourceResultSetId;
            SourceResultSetNo = _record.SourceResultSetNo;
            ProdLeader = _record.ProdLeader;
            ProdDate = _record.ProdDate ?? DateTime.Today;
            RndLeader = _record.RndLeader;
            RndDate = _record.RndDate ?? DateTime.Today;
            DeputyLeader = _record.DeputyLeader;
            DeputyDate = _record.DeputyDate ?? DateTime.Today;
            Deliverer = string.IsNullOrWhiteSpace(_record.Deliverer)
                ? _record.ApplicantName
                : _record.Deliverer;
            DeliverDate = _record.DeliverDate ?? DateTime.Today;
            Administrator = string.IsNullOrWhiteSpace(_record.Administrator)
                ? _userContextService.CurrentUser?.RealName ?? string.Empty
                : _record.Administrator;
            AdminDate = _record.AdminDate ?? DateTime.Today;
            DeptLeader = _record.DeptLeader;
            DeptDate = _record.DeptDate ?? DateTime.Today;

            Items.Clear();
            foreach (var item in _record.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            {
                Items.Add(CloneItem(item));
            }

            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(ShowApprovalPanel));
            OnPropertyChanged(nameof(IsArchivedSource));
        }

        private static NetworkInboundItem CloneItem(NetworkInboundItem item) => new()
        {
            Id = item.Id,
            SortOrder = item.SortOrder,
            AssetKind = item.AssetKind,
            AssetName = item.AssetName,
            ConfidentialLevel = item.ConfidentialLevel,
            DataSizeText = item.DataSizeText,
            TargetServerPath = item.TargetServerPath,
            SourceKind = item.SourceKind,
            SourceResultSetItemId = item.SourceResultSetItemId,
            SourceFilingFactId = item.SourceFilingFactId,
            FormNo = item.FormNo,
            MaterialName = item.MaterialName,
            ItemName = item.ItemName,
            ContainerCode = item.ContainerCode,
            StorageLocation = item.StorageLocation,
            OnNetAssetId = item.OnNetAssetId,
            CreatedAt = item.CreatedAt
        };

        private NetworkInboundRecord BuildDraftSnapshot()
        {
            return new NetworkInboundRecord
            {
                Id = _record.Id,
                InboundNo = InboundNo,
                SourceKind = SourceKind,
                ProjectName = ProjectName,
                Year = Year,
                Reason = Reason,
                Remark = Remark,
                SourceResultSetId = SourceResultSetId,
                SourceResultSetNo = SourceResultSetNo
            };
        }

        private async Task ImportSearchResultSetAsync()
        {
            try
            {
                if (!SourceResultSetId.HasValue || SourceResultSetId.Value <= 0)
                {
                    _dialogService.ShowError("请先填写电子资料检索结果集 Id。");
                    return;
                }

                var imported = await _service.BuildInboundItemsFromElectronicSearchAsync(SourceResultSetId.Value, null);
                Items.Clear();
                foreach (var item in imported)
                {
                    Items.Add(item);
                }

                if (string.IsNullOrWhiteSpace(SourceResultSetNo))
                {
                    SourceResultSetNo = $"RS-{SourceResultSetId.Value}";
                }

                CommandManager.InvalidateRequerySuggested();
                _dialogService.ShowMessage($"已导入 {Items.Count} 条电子检索明细。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void AddManualItem()
        {
            if (string.IsNullOrWhiteSpace(ManualAssetName))
            {
                _dialogService.ShowError("请填写资料名称。");
                return;
            }

            Items.Add(new NetworkInboundItem
            {
                SortOrder = Items.Count + 1,
                AssetKind = ManualAssetKind,
                AssetName = ManualAssetName.Trim(),
                TargetServerPath = ManualServerPath?.Trim() ?? string.Empty,
                SourceKind = SourceKind,
                CreatedAt = DateTime.Now
            });
            ManualAssetName = string.Empty;
            ManualServerPath = string.Empty;
            CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveItem(NetworkInboundItem? item)
        {
            if (item == null)
            {
                return;
            }

            Items.Remove(item);
            int sort = 1;
            foreach (var row in Items)
            {
                row.SortOrder = sort++;
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private async Task SaveDraftAsync()
        {
            try
            {
                var user = RequireUser();
                var draft = BuildDraftSnapshot();
                _record = _record.Id > 0
                    ? await _service.UpdateInboundDraftAsync(draft, Items.ToList(), user)
                    : await _service.CreateInboundDraftAsync(draft, Items.ToList(), user);
                _hasCommittedChanges = true;
                BindFromRecord();
                _dialogService.ShowMessage("草稿已保存。");
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
                await SaveDraftAsync();
                await _service.SubmitInboundAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("已提交审批。");
                RequestClose?.Invoke(true);
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
                // 审批前允许资料室补录服务器路径
                if (_record.Status == NetworkInboundRecord.StatusSubmitted)
                {
                    await PersistItemPathsIfNeededAsync();
                }

                await _service.ApproveInboundAsync(new NetworkInboundRecord
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
                await ReloadRecordAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ConfirmHandoverAsync()
        {
            try
            {
                await PersistItemPathsIfNeededAsync();
                await _service.ConfirmInboundHandoverAsync(new NetworkInboundRecord
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
                _dialogService.ShowMessage("入网交接已确认。");
                await ReloadRecordAsync();
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
                await _service.CompleteInboundAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("入网单已办结，已写入在网台账。");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PersistItemPathsIfNeededAsync()
        {
            if (_record.Status is NetworkInboundRecord.StatusSubmitted or NetworkInboundRecord.StatusApproved)
            {
                await _service.UpdateInboundItemPathsAsync(_record.Id, Items.ToList(), RequireUser());
                await ReloadRecordAsync();
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
                string fileName = Path.GetFileName(path);
                string extension = Path.GetExtension(path);
                var (ok, message, _) = await _service.UploadAttachmentAsync(
                    NetworkTransferDomainValues.InboundAttachmentBusinessType,
                    _record.Id,
                    _record.InboundNo,
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
                var (ok, message) = await _service.DeleteAttachmentAsync(attachment.Id, RequireUser());
                if (!ok)
                {
                    _dialogService.ShowError(message);
                    return;
                }

                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ReloadAttachmentsAsync()
        {
            Attachments.Clear();
            if (string.IsNullOrWhiteSpace(_record.InboundNo))
            {
                return;
            }

            var list = await _service.GetAttachmentsAsync(
                NetworkTransferDomainValues.InboundAttachmentBusinessType,
                _record.InboundNo);
            foreach (var item in list)
            {
                Attachments.Add(item);
            }
        }

        private async Task ReloadRecordAsync()
        {
            var latest = await _service.GetInboundByIdAsync(_record.Id);
            if (latest != null)
            {
                _record = latest;
                BindFromRecord();
                await ReloadAttachmentsAsync();
            }
        }

        private User RequireUser() =>
            _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。");
    }
}
