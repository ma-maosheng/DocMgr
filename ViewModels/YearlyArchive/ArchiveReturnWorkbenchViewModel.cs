using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using DocMgr.Views.Shared;
using DocMgr.Views.YearlyArchive;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料归还工作台：申请侧发起；审批入库侧完成审批 → 交接 → 办结。
    /// </summary>
    public sealed partial class ArchiveReturnWorkbenchViewModel : ViewModelBase
    {
        private const string FilterAll = "全部";
        private const string OutboundFilterOverdue = "超期未还";

        private readonly IArchiveReturnService _returnService;
        private readonly IArchiveOutboundService _outboundService;
        private readonly IUserContextService _userContextService;
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;
        private readonly List<YearlyArchiveOutboundRecord> _allReturnableOutbounds = new();
        private readonly List<YearlyArchiveReturnRecord> _allReturns = new();

        private int _selectedYear;
        private string _searchKeyword = string.Empty;
        private string _selectedReturnStatus = FilterAll;
        private string _selectedOutboundFilter = FilterAll;
        private YearlyArchiveOutboundRecord? _selectedOutbound;
        private YearlyArchiveReturnRecord? _selectedReturn;
        private YearlyArchiveReturnRecord? _editingRecord;
        private string _editHeader = string.Empty;
        private string _abnormalFlowHint = string.Empty;
        private string _workflowHintText = string.Empty;
        private bool _isBusy;
        private bool _isAdmin;
        private bool _isLeftPanelExpanded = true;
        private bool _isActive = true;

        /// <summary>页面卸载时调用，阻止卸载后继续访问已释放的页面作用域服务。</summary>
        public void Deactivate() => _isActive = false;

        public ArchiveReturnWorkbenchViewModel(
            ArchiveReturnWorkspaceMode workspaceMode,
            IArchiveReturnService returnService,
            IArchiveOutboundService outboundService,
            IUserContextService userContextService,
            IUserService userService,
            IDialogService dialogService)
        {
            _workspaceMode = workspaceMode;
            _returnService = returnService;
            _outboundService = outboundService;
            _userContextService = userContextService;
            _userService = userService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(_ => ApplyFilters(), _ => !IsBusy);
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
            ViewOutboundDetailCommand = new RelayCommand(async _ => await ViewOutboundDetailAsync(), _ => !IsBusy && SelectedOutbound != null);
            StartReturnCommand = new RelayCommand(async _ => await StartReturnAsync(), _ => !IsBusy && CanStartReturn);
            OpenReturnCommand = new RelayCommand(async _ => await OpenReturnAsync(), _ => !IsBusy && SelectedReturn != null);
            SaveDraftCommand = new RelayCommand(async _ => await SaveAsync(false), _ => !IsBusy && CanSaveDraft);
            RegisterCommand = new RelayCommand(async _ => await SaveAsync(true), _ => !IsBusy && CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => !IsBusy && CanApprove);
            ConfirmHandoverCommand = new RelayCommand(async _ => await ConfirmHandoverAsync(), _ => !IsBusy && CanConfirmHandover);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => !IsBusy && CanComplete);
            VoidCommand = new RelayCommand(async _ => await VoidAsync(), _ => !IsBusy && CanVoid);
            PrintSignedHandoverCommand = new RelayCommand(async _ => await PrintHandoverDocumentAsync(), _ => !IsBusy && CanPrintSignedHandoverOnApplication);
            PrintHandoverSheetCommand = new RelayCommand(async _ => await PrintHandoverDocumentAsync(), _ => !IsBusy && CanPrintHandoverSheet);
            UploadSignedAttachmentCommand = new RelayCommand(async _ => await UploadSignedAttachmentAsync(), _ => !IsBusy && CanUploadSignedAttachment);
            CaptureSignedAttachmentCommand = new RelayCommand(async _ => await CaptureSignedAttachmentAsync(), _ => !IsBusy && CanUploadSignedAttachment);
            ViewSignedAttachmentCommand = new RelayCommand(async _ => await ViewSignedAttachmentAsync(), _ => SelectedSignedAttachment != null);
            DeleteSignedAttachmentCommand = new RelayCommand(async _ => await DeleteSignedAttachmentAsync(), _ => !IsBusy && CanDeleteSignedAttachment && SelectedSignedAttachment != null);
            CancelEditCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
            ToggleLeftPanelCommand = new RelayCommand(_ => IsLeftPanelExpanded = !IsLeftPanelExpanded);
            AssignRehomeTargetCommand = new RelayCommand<ArchiveReturnItemEditRowViewModel>(
                async row => await AssignRehomeTargetAsync(row),
                row => !IsBusy && IsEditable && row?.NeedsRehome == true);

            EditItemDetailsPanel = new ItemDetailsListPresenter<ArchiveReturnItemEditRowViewModel>(
                "归还明细",
                summaryBuilder: ItemDetailsPanelSummarySupport.BuildReturnItemSummary);
            EditItems.CollectionChanged += (_, _) =>
                EditItemDetailsPanel.RefreshItems(EditItems, preserveExpanded: EditItemDetailsPanel.IsExpanded);
            EditItemDetailsPanel.RefreshItems(EditItems);
        }

        private readonly ArchiveReturnWorkspaceMode _workspaceMode;

        public ArchiveReturnWorkspaceMode WorkspaceMode => _workspaceMode;

        public string PageTitle => _workspaceMode switch
        {
            ArchiveReturnWorkspaceMode.Application => "资料归还申请",
            _ => "资料审批入库"
        };

        public string PageSubtitle => _workspaceMode switch
        {
            ArchiveReturnWorkspaceMode.Application => "选择待归还出库单，填写归还申请；无论资料是否完好，均须打印签批交接单并完成线下签字后提交（扫描件由资料室上传）。",
            _ => "审批、实物交接；由资料室资料管理员上传签批交接单并办结。"
        };

        /// <summary>资料室侧工作台（审批入库；含兼容旧 Handover 入口）。</summary>
        private bool IsAdminWorkbenchMode =>
            _workspaceMode is ArchiveReturnWorkspaceMode.Approval or ArchiveReturnWorkspaceMode.Handover;

        public ObservableCollection<int> Years { get; } = new();

        public ObservableCollection<string> ReturnStatusOptions { get; } = new()
        {
            FilterAll,
            ApplicationWorkflowStatus.TextDraft,
            ApplicationWorkflowStatus.TextSubmitted,
            ApplicationWorkflowStatus.TextApproved,
            ApplicationWorkflowStatus.TextSignedUploaded,
            ApplicationWorkflowStatus.TextCompleted,
            ApplicationWorkflowStatus.TextWithdrawn,
            ApplicationWorkflowStatus.TextForceWithdrawn
        };

        public ObservableCollection<string> OutboundFilterOptions { get; } = new()
        {
            FilterAll,
            OutboundFilterOverdue
        };

        public ObservableCollection<YearlyArchiveOutboundRecord> ReturnableOutbounds { get; } = new();

        public ObservableCollection<YearlyArchiveReturnRecord> Returns { get; } = new();

        public ObservableCollection<ArchiveReturnItemEditRowViewModel> EditItems { get; } = new();

        public ItemDetailsListPresenter<ArchiveReturnItemEditRowViewModel> EditItemDetailsPanel { get; }

        public ObservableCollection<SystemAttachment> SignedAttachments { get; } = new();

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        public string SelectedReturnStatus
        {
            get => _selectedReturnStatus;
            set => SetProperty(ref _selectedReturnStatus, value);
        }

        public string SelectedOutboundFilter
        {
            get => _selectedOutboundFilter;
            set => SetProperty(ref _selectedOutboundFilter, value);
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewOutboundDetailCommand { get; }
        public RelayCommand StartReturnCommand { get; }
        public RelayCommand OpenReturnCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand RegisterCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmHandoverCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand VoidCommand { get; }
        public RelayCommand PrintSignedHandoverCommand { get; }
        public RelayCommand PrintHandoverSheetCommand { get; }
        public RelayCommand UploadSignedAttachmentCommand { get; }
        public RelayCommand CaptureSignedAttachmentCommand { get; }
        public RelayCommand ViewSignedAttachmentCommand { get; }
        public RelayCommand DeleteSignedAttachmentCommand { get; }
        public RelayCommand CancelEditCommand { get; }
        public RelayCommand ToggleLeftPanelCommand { get; }
        public RelayCommand<ArchiveReturnItemEditRowViewModel> AssignRehomeTargetCommand { get; }

        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (SetProperty(ref _selectedYear, value) && value > 0)
                {
                    _ = RefreshAsync();
                }
            }
        }

        public YearlyArchiveOutboundRecord? SelectedOutbound
        {
            get => _selectedOutbound;
            set => SetProperty(ref _selectedOutbound, value);
        }

        public YearlyArchiveReturnRecord? SelectedReturn
        {
            get => _selectedReturn;
            set => SetProperty(ref _selectedReturn, value);
        }

        public YearlyArchiveReturnRecord? EditingRecord
        {
            get => _editingRecord;
            private set
            {
                if (SetProperty(ref _editingRecord, value))
                {
                    OnPropertyChanged(nameof(IsEditing));
                    OnPropertyChanged(nameof(IsEditable));
                    OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
                    OnPropertyChanged(nameof(CanComplete));
                    OnPropertyChanged(nameof(CanVoid));
                    OnPropertyChanged(nameof(CanPrintSignedHandoverOnApplication));
                    OnPropertyChanged(nameof(CanPrintHandoverSheet));
                    OnPropertyChanged(nameof(HasAbnormalReturnItems));
                }
            }
        }

        public string EditHeader
        {
            get => _editHeader;
            private set => SetProperty(ref _editHeader, value);
        }

        public string LossDescription
        {
            get => EditingRecord?.LossDescription ?? string.Empty;
            set
            {
                if (EditingRecord == null)
                {
                    return;
                }

                string normalized = value ?? string.Empty;
                if (!string.Equals(EditingRecord.LossDescription, normalized, StringComparison.Ordinal))
                {
                    EditingRecord.LossDescription = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public string AbnormalFlowHint
        {
            get => _abnormalFlowHint;
            private set => SetProperty(ref _abnormalFlowHint, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public bool IsLeftPanelExpanded
        {
            get => _isLeftPanelExpanded;
            set
            {
                if (SetProperty(ref _isLeftPanelExpanded, value))
                {
                    OnPropertyChanged(nameof(LeftPanelToggleToolTip));
                    OnPropertyChanged(nameof(LeftPanelToggleGlyph));
                }
            }
        }

        public string LeftPanelToggleToolTip => IsLeftPanelExpanded ? "收起左侧列表" : "展开左侧列表";

        public string LeftPanelToggleGlyph => IsLeftPanelExpanded ? "◀" : "▶";

        public bool IsAdmin
        {
            get => _isAdmin;
            private set => SetProperty(ref _isAdmin, value);
        }

        public bool IsEditing => EditingRecord != null;

        public bool IsEditable => EditingRecord is { Status: YearlyArchiveReturnRecord.Draft }
            && _workspaceMode == ArchiveReturnWorkspaceMode.Application;

        public bool CanStartReturn =>
            _workspaceMode == ArchiveReturnWorkspaceMode.Application
            && SelectedOutbound != null
            && _returnService.CanSubmitApplication(_userContextService.CurrentUser);

        public bool CanSaveDraft =>
            IsEditable && _returnService.CanSubmitApplication(_userContextService.CurrentUser);

        public bool CanSubmit =>
            IsEditable && _returnService.CanSubmitApplication(_userContextService.CurrentUser);

        public bool CanApprove =>
            IsAdminWorkbenchMode
            && IsAdmin
            && EditingRecord is { Id: > 0, Status: YearlyArchiveReturnRecord.Submitted };

        public bool CanConfirmHandover =>
            IsAdminWorkbenchMode
            && IsAdmin
            && EditingRecord is { Id: > 0, Status: YearlyArchiveReturnRecord.Approved };

        public bool CanUploadSignedAttachment =>
            IsAdminWorkbenchMode
            && IsAdmin
            && EditingRecord is { Id: > 0, Status: YearlyArchiveReturnRecord.SignedUploaded };

        public bool CanDeleteSignedAttachment =>
            IsAdminWorkbenchMode
            && IsAdmin
            && EditingRecord is { Id: > 0 } record
            && record.Status is not (
                YearlyArchiveReturnRecord.Completed
                or YearlyArchiveReturnRecord.WithdrawnVoid
                or YearlyArchiveReturnRecord.ForceVoided);

        public bool ShowApplicationActions => _workspaceMode == ArchiveReturnWorkspaceMode.Application;

        /// <summary>审批入库工作台：同时展示审批与交接办结操作区。</summary>
        public bool ShowApprovalActions => IsAdminWorkbenchMode;

        /// <summary>审批信息蓝框：仅在打开归还单后展示。</summary>
        public bool ShowApprovalEditorPanel => ShowApprovalActions && IsEditing;

        /// <summary>左侧「待归还出库单」：申请与审批入库页均展示（审批页仅查看/对照，不可发起）。</summary>
        public bool ShowOutboundCandidatesPanel => true;

        /// <summary>待归还出库单上的「发起归还」仅申请页可用。</summary>
        public bool ShowStartReturnButton => _workspaceMode == ArchiveReturnWorkspaceMode.Application;

        public bool HasAbnormalReturnItems =>
            EditingRecord != null
            && ArchiveReturnDomainValues.HasAbnormalReturnItems(EditItems.Select(item => item.Source));

        public bool ShowAbnormalReturnPanel =>
            IsEditing && HasAbnormalReturnItems;

        /// <summary>申请侧可打印签批交接单。</summary>
        public bool CanPrintSignedHandoverOnApplication =>
            ShowApplicationActions
            && EditingRecord is { Id: > 0 } record
            && record.Status is YearlyArchiveReturnRecord.Draft or YearlyArchiveReturnRecord.Submitted;

        /// <summary>审批侧可打印交接单。</summary>
        public bool CanPrintHandoverSheet =>
            ShowApprovalActions
            && EditingRecord is { Id: > 0 } record
            && record.Status is YearlyArchiveReturnRecord.Approved
                or YearlyArchiveReturnRecord.SignedUploaded
                or YearlyArchiveReturnRecord.Completed;

        /// <summary>已上传签批交接单且已打印后可确认办结。</summary>
        public bool CanComplete =>
            IsAdminWorkbenchMode
            && IsAdmin
            && EditingRecord is { Status: YearlyArchiveReturnRecord.SignedUploaded, PrintCount: > 0, SignedAttachmentUploaded: true };

        /// <summary>申请侧撤回作废；审批侧逾期强制作废。</summary>
        public string VoidActionText => _workspaceMode == ArchiveReturnWorkspaceMode.Application
            ? "撤回作废"
            : "强制作废";

        public string VoidActionToolTip => _workspaceMode == ArchiveReturnWorkspaceMode.Application
            ? "申请人撤回，状态变为「已作废（撤回）」"
            : "资料室管理员强制作废（须满足逾期时限），状态变为「已作废（强制）」";

        /// <summary>申请人仅草稿/已提交可撤回；管理员仅草稿/已提交且逾期可强制。</summary>
        public bool CanVoid
        {
            get
            {
                if (EditingRecord is not { Id: > 0 } record)
                {
                    return false;
                }

                if (record.Status is YearlyArchiveReturnRecord.Completed
                    or YearlyArchiveReturnRecord.WithdrawnVoid
                    or YearlyArchiveReturnRecord.ForceVoided)
                {
                    return false;
                }

                if (_workspaceMode == ArchiveReturnWorkspaceMode.Application)
                {
                    var user = _userContextService.CurrentUser;
                    return user != null
                           && record.RegisteredByUserId == user.Id
                           && record.Status is YearlyArchiveReturnRecord.Draft
                               or YearlyArchiveReturnRecord.Submitted;
                }

                return IsAdmin
                       && record.Status is YearlyArchiveReturnRecord.Draft
                           or YearlyArchiveReturnRecord.Submitted;
            }
        }

        public string WorkflowHintText
        {
            get => _workflowHintText;
            private set => SetProperty(ref _workflowHintText, value);
        }

        public string ApproveHintText
        {
            get
            {
                if (CanApprove)
                {
                    return HasAbnormalReturnItems
                        ? "本单存在灭失：签批交接单需借出时全部审核审批人（部门负责人、资料室负责人、生产科负责人、生产副院长）签字；请核对后点击“审批通过”。"
                        : "本单资料完好归还：签批交接单仅需部门负责人签字，无需资料室负责人及其他审批人；请核对后点击“审批通过”。";
                }

                return EditingRecord?.Status == YearlyArchiveReturnRecord.Approved
                    ? "审批已通过，请办理实物交接。"
                    : string.Empty;
            }
        }

        /// <summary>完好归还：仅展示部门负责人。</summary>
        public bool ShowIntactApprovalSigner => IsEditing && !HasAbnormalReturnItems;

        /// <summary>灭失归还：展示借出时全部四级审核审批人。</summary>
        public bool ShowLossApprovalSigners => IsEditing && HasAbnormalReturnItems;

        /// <summary>审核人字段标签。</summary>
        public string ReviewerFieldLabel => HasAbnormalReturnItems
            ? "部门负责人（借出时） *"
            : "部门负责人 *";

        /// <summary>资料室负责人字段标签（仅灭失时展示）。</summary>
        public string ApproverFieldLabel => "资料室负责人（借出时） *";

        public string ConfirmHandoverHintText
        {
            get
            {
                if (CanConfirmHandover)
                {
                    return "请确认交接双方与日期无误后办理实物交接；确认后请上传签批交接单。";
                }

                return EditingRecord?.Status == YearlyArchiveReturnRecord.SignedUploaded
                       && EditingRecord is { SignedAttachmentUploaded: false }
                    ? "后续：确认实物交接后，请上传签批交接单。"
                    : string.Empty;
            }
        }

        public string UploadHintText => CanUploadSignedAttachment
            ? "后续：上传签批交接单后，请打印交接单并点击“确认办结”。"
            : "请先确认实物交接，再上传签批交接单。";

        public string CompleteHintText => CanComplete
            ? "下一步：确认办结，完成资料收回入库。"
            : EditingRecord is { Status: YearlyArchiveReturnRecord.SignedUploaded, SignedAttachmentUploaded: true, PrintCount: <= 0 }
                ? "请先打印交接单，再确认办结。"
                : EditingRecord is { Status: YearlyArchiveReturnRecord.SignedUploaded, SignedAttachmentUploaded: false }
                    ? "请先上传签批交接单并打印交接单，再确认办结。"
                    : "请先完成实物交接并上传签批交接单，再确认办结。";

        public async Task InitializeAsync()
        {
            if (!_isActive)
            {
                return;
            }

            IsAdmin = _returnService.IsArchiveAdminUser(_userContextService.CurrentUser);

            int currentYear = DateTime.Now.Year;
            Years.Clear();
            for (int year = currentYear; year >= currentYear - 9; year--)
            {
                Years.Add(year);
            }

            _selectedYear = currentYear;
            OnPropertyChanged(nameof(SelectedYear));
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (!_isActive || IsBusy || SelectedYear <= 0)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            SearchKeyword = string.Empty;
            SelectedReturnStatus = FilterAll;
            SelectedOutboundFilter = FilterAll;

            IsBusy = true;
            try
            {
                await ReloadListsAsync(user);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReloadListsAsync(User user)
        {
            if (!_isActive)
            {
                return;
            }

            int? selectedOutboundId = SelectedOutbound?.Id;
            int? selectedReturnId = SelectedReturn?.Id;

            _allReturnableOutbounds.Clear();
            // 申请/审批入库页均加载待归还出库单，便于资料室对照借出情况。
            var returnable = await _returnService.GetReturnableOutboundsAsync(SelectedYear);
            _allReturnableOutbounds.AddRange(returnable);

            _allReturns.Clear();
            var returns = await _returnService.ListReturnsAsync(SelectedYear, user);
            _allReturns.AddRange(returns);

            ApplyFilters(selectedOutboundId, selectedReturnId);
        }

        private void ApplyFilters(int? preferredOutboundId = null, int? preferredReturnId = null)
        {
            ReturnableOutbounds.Clear();
            foreach (var record in _allReturnableOutbounds.Where(MatchesOutboundFilters))
            {
                ReturnableOutbounds.Add(record);
            }

            Returns.Clear();
            foreach (var record in _allReturns.Where(MatchesReturnFilters))
            {
                Returns.Add(record);
            }

            SelectedOutbound = preferredOutboundId.HasValue
                ? ReturnableOutbounds.FirstOrDefault(record => record.Id == preferredOutboundId.Value)
                : ReturnableOutbounds.FirstOrDefault();

            SelectedReturn = preferredReturnId.HasValue
                ? Returns.FirstOrDefault(record => record.Id == preferredReturnId.Value)
                : Returns.FirstOrDefault();
        }

        private bool MatchesOutboundFilters(YearlyArchiveOutboundRecord record)
        {
            if (SelectedOutboundFilter == OutboundFilterOverdue
                && !IsOverdueOutbound(record))
            {
                return false;
            }

            return MatchesKeyword(
                SearchKeyword,
                record.OutboundNo,
                record.MaterialSummary,
                record.ApplicantName,
                record.ApplicantDept,
                record.ProjectName);
        }

        private bool MatchesReturnFilters(YearlyArchiveReturnRecord record)
        {
            if (SelectedReturnStatus != FilterAll)
            {
                if (string.Equals(
                        SelectedReturnStatus,
                        ApplicationWorkflowStatus.TextSignedUploaded,
                        StringComparison.Ordinal))
                {
                    if (record.Status != YearlyArchiveReturnRecord.SignedUploaded)
                    {
                        return false;
                    }
                }
                else if (!string.Equals(
                             ApplicationWorkflowStatus.ToDisplay(record.Status),
                             SelectedReturnStatus,
                             StringComparison.Ordinal)
                         && !string.Equals(record.StatusStr, SelectedReturnStatus, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return MatchesKeyword(
                SearchKeyword,
                record.ReturnNo,
                record.SourceOutboundNo,
                record.BorrowerName,
                record.BorrowerDept,
                record.ProjectName,
                record.RegisteredByName,
                record.Reason,
                record.Remark);
        }

        private static bool IsOverdueOutbound(YearlyArchiveOutboundRecord record) =>
            ArchiveOutboundReturnSupport.IsOutboundOverdue(record, DateTime.Today);

        private static bool MatchesKeyword(string? keyword, params string?[] fields)
        {
            string trimmed = keyword?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                return true;
            }

            return fields.Any(field =>
                !string.IsNullOrWhiteSpace(field)
                && field.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        private async Task StartReturnAsync()
        {
            var outbound = SelectedOutbound;
            var user = _userContextService.CurrentUser;
            if (outbound == null || user == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var draft = await _returnService.CreateDraftFromOutboundAsync(outbound.Id, user);
                LoadEditing(draft);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ViewOutboundDetailAsync()
        {
            var outbound = SelectedOutbound;
            if (outbound == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var record = await _outboundService.GetRecordAsync(outbound.Id);
                if (record == null)
                {
                    _dialogService.ShowError("未找到该出库单，可能已被删除。");
                    return;
                }

                var viewModel = new ArchiveReturnOutboundDetailViewModel(record);
                var window = new ArchiveReturnOutboundDetailWindow
                {
                    Owner = Application.Current.MainWindow,
                    DataContext = viewModel
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("打开借出详情失败：" + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenReturnAsync()
        {
            var selected = SelectedReturn;
            if (selected == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var record = await _returnService.GetReturnAsync(selected.Id);
                if (record == null)
                {
                    _dialogService.ShowError("未找到该归还单，可能已被删除。");
                    return;
                }

                LoadEditing(record);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveAsync(bool submitForRegistration)
        {
            if (EditingRecord == null)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            if (submitForRegistration)
            {
                var locationChangedItems = EditItems
                    .Where(row => string.Equals(
                        row.Source.ContainerStatusKind,
                        ArchiveReturnContainerAssessment.StatusLocationChanged,
                        StringComparison.Ordinal))
                    .ToList();
                if (locationChangedItems.Count > 0)
                {
                    string preview = string.Join(
                        "\n",
                        locationChangedItems.Take(5).Select(row =>
                            $"· {row.ItemName}：{row.StorageLocation} → {row.CurrentStorageLocation}"));
                    if (!_dialogService.ShowConfirm(
                            $"有 {locationChangedItems.Count} 条明细借出后盒位已变更，登记后办结将归入当前盒位：\n{preview}\n\n是否继续登记？",
                            "盒位已变确认"))
                    {
                        return;
                    }
                }

                var blocked = EditItems.Where(row => row.NeedsRehome && row.Source.RehomeTargetBoxId is null or <= 0).ToList();
                if (blocked.Count > 0)
                {
                    _dialogService.ShowError($"有 {blocked.Count} 条明细原盒已失效，请先点击「指定目标盒」后再登记。");
                    return;
                }
            }

            var request = new SaveReturnRequest
            {
                Record = EditingRecord,
                Items = EditItems.Select(item => item.Source).ToList(),
                SubmitForRegistration = submitForRegistration
            };

            IsBusy = true;
            try
            {
                var result = await _returnService.SaveReturnFlowAsync(request, user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ReloadSavedRecordAsync(result.RecordId);
            }
            finally
            {
                IsBusy = false;
                await TryReloadListsAfterOperationAsync();
            }
        }

        private async Task AssignRehomeTargetAsync(ArchiveReturnItemEditRowViewModel? row)
        {
            if (row == null || EditingRecord == null || !IsEditable)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                // 未落库草稿先保存，以便写入 RehomeTargetBoxId
                if (EditingRecord.Id <= 0 || row.Source.Id <= 0)
                {
                    var saveResult = await _returnService.SaveReturnFlowAsync(
                        new SaveReturnRequest
                        {
                            Record = EditingRecord,
                            Items = EditItems.Select(item => item.Source).ToList(),
                            SubmitForRegistration = false
                        },
                        user);
                    if (!saveResult.Success)
                    {
                        _dialogService.ShowError(saveResult.Message);
                        return;
                    }

                    await ReloadSavedRecordAsync(saveResult.RecordId);
                    row = EditItems.FirstOrDefault(item => item.Source.FilingFactId == row.Source.FilingFactId
                        && item.Source.SourceOutboundItemId == row.Source.SourceOutboundItemId);
                    if (row == null || EditingRecord == null)
                    {
                        return;
                    }
                }

                var options = await _returnService.GetRehomeTargetOptionsAsync(row.Source.FilingFactId);
                if (options.Count == 0)
                {
                    _dialogService.ShowError("当前没有可用的在用档案盒，请先在迁档中新建空盒或并档后再归还。");
                    return;
                }

                var pickVm = new ArchiveReturnRehomeTargetPickViewModel(options);
                var dialog = new ArchiveReturnRehomeTargetPickDialog
                {
                    Owner = Application.Current.MainWindow,
                    DataContext = pickVm
                };
                if (dialog.ShowDialog() != true || pickVm.SelectedOption == null)
                {
                    return;
                }

                var result = await _returnService.AssignRehomeTargetBoxAsync(
                    EditingRecord.Id,
                    row.Source.Id,
                    pickVm.SelectedOption.BoxId,
                    user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ReloadSavedRecordAsync(result.RecordId);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("指定归还目标盒失败：" + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApproveAsync()
        {
            if (EditingRecord is not { } record || record.Id <= 0)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ReviewerName))
            {
                _dialogService.ShowMessage("请填写部门负责人。");
                return;
            }

            if (HasAbnormalReturnItems && string.IsNullOrWhiteSpace(ApproverName))
            {
                _dialogService.ShowMessage("存在灭失时请填写资料室负责人。");
                return;
            }

            if (HasAbnormalReturnItems && string.IsNullOrWhiteSpace(ProductionHeadName))
            {
                _dialogService.ShowMessage("存在灭失时请填写生产科负责人。");
                return;
            }

            if (HasAbnormalReturnItems && string.IsNullOrWhiteSpace(VicePresidentName))
            {
                _dialogService.ShowMessage("存在灭失时请填写生产副院长。");
                return;
            }

            if (!_dialogService.ShowConfirm($"确认审批通过归还单 {record.ReturnNo}？", "审批确认"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _returnService.ApproveReturnFlowAsync(record.Id, user, BuildApprovalInput());
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ReloadSavedRecordAsync(result.RecordId);
            }
            finally
            {
                IsBusy = false;
                await TryReloadListsAfterOperationAsync();
            }
        }

        private async Task ConfirmHandoverAsync()
        {
            if (EditingRecord is not { } record || record.Id <= 0)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(HandoverAdmin))
            {
                _dialogService.ShowMessage("请填写办理交接人（资料管理员）。");
                return;
            }

            if (!HandoverDate.HasValue)
            {
                _dialogService.ShowMessage("请填写办理交接日期。");
                return;
            }

            if (!_dialogService.ShowConfirm($"确认归还单 {record.ReturnNo} 已完成实物交接？", "实物交接确认"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _returnService.ConfirmHandoverFlowAsync(record.Id, user, BuildHandoverInput());
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ReloadSavedRecordAsync(result.RecordId);
            }
            finally
            {
                IsBusy = false;
                await TryReloadListsAfterOperationAsync();
            }
        }

        private async Task CompleteAsync()
        {
            if (EditingRecord is not { } record || record.Id <= 0)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            if (!CanComplete)
            {
                _dialogService.ShowMessage(
                    record.SignedAttachmentUploaded && record.PrintCount <= 0
                        ? "请先打印交接单，再确认办结。"
                        : "请先上传签批交接单并打印交接单，再确认办结。");
                return;
            }

            string confirmMessage =
                $"确认办结归还单 {record.ReturnNo}？办结后将冲销出库提档对资料台账的影响，且不可撤销。";
            if (HasAbnormalReturnItems)
            {
                confirmMessage +=
                    "\n\n若灭失导致档口内档案盒变为空盒，办结后请资料管理员对空档案盒进行物理处置（取走、合并或注销）。";
            }

            if (!_dialogService.ShowConfirm(confirmMessage, "办结确认"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _returnService.CompleteReturnFlowAsync(record.Id, user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ReloadSavedRecordAsync(result.RecordId);
            }
            finally
            {
                IsBusy = false;
                await TryReloadListsAfterOperationAsync();
            }
        }

        private async Task VoidAsync()
        {
            if (EditingRecord is not { } record || record.Id <= 0)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确认{VoidActionText}归还单 {record.ReturnNo}？", $"{VoidActionText}确认"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _returnService.VoidReturnFlowAsync(record.Id, null, user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                CancelEdit();
            }
            finally
            {
                IsBusy = false;
                await TryReloadListsAfterOperationAsync();
            }
        }

        private async Task ReloadSavedRecordAsync(int recordId)
        {
            if (!_isActive)
            {
                return;
            }

            int targetId = recordId > 0 ? recordId : EditingRecord?.Id ?? 0;
            if (targetId <= 0)
            {
                return;
            }

            var reloaded = await _returnService.GetReturnAsync(targetId);
            if (reloaded != null)
            {
                LoadEditing(reloaded);
            }
        }

        private async Task TryReloadListsAfterOperationAsync()
        {
            if (!_isActive)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null || SelectedYear <= 0)
            {
                return;
            }

            await ReloadListsAsync(user);
        }

        private void LoadEditing(YearlyArchiveReturnRecord record)
        {
            EditItems.Clear();
            foreach (var item in record.Items)
            {
                var row = new ArchiveReturnItemEditRowViewModel(item);
                row.ReturnCopyCountsChanged += (_, _) => OnReturnCopyCountsChanged();
                EditItems.Add(row);
            }

            EditingRecord = record;
            EditHeader = $"归还单 {record.ReturnNo}　源出库单 {record.SourceOutboundNo}　借出人 {record.BorrowerName}　状态 {record.StatusStr}";
            OnPropertyChanged(nameof(LossDescription));
            _ = LoadApprovalFormFieldsAsync(record);
            _ = LoadAttachmentsAsync(record.Id);
            NotifyEditCommandStates();
        }

        private void OnReturnCopyCountsChanged()
        {
            OnPropertyChanged(nameof(HasAbnormalReturnItems));
            OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
            OnPropertyChanged(nameof(ShowIntactApprovalSigner));
            OnPropertyChanged(nameof(ShowLossApprovalSigners));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrintHandoverSheet));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(ReviewerFieldLabel));
            OnPropertyChanged(nameof(ApproverFieldLabel));
            RefreshAbnormalFlowHint();
            RefreshWorkflowHint();

            if (IsAdminWorkbenchMode && EditingRecord is { } editingRecord)
            {
                _ = SyncApprovalSignersForLossStateAsync(editingRecord);
            }
        }

        private async Task LoadAttachmentsAsync(int recordId)
        {
            if (!_isActive)
            {
                return;
            }

            SignedAttachments.Clear();
            SelectedSignedAttachment = null;

            if (recordId <= 0)
            {
                RefreshAbnormalFlowHint();
                RefreshWorkflowHint();
                return;
            }

            var attachments = await _returnService.GetAttachmentsAsync(recordId);
            foreach (var attachment in attachments)
            {
                if (string.Equals(
                        attachment.FileCategory,
                        ArchiveReturnDomainValues.AttachmentKindSignedHandover,
                        StringComparison.Ordinal))
                {
                    SignedAttachments.Add(attachment);
                }
            }

            RefreshAbnormalFlowHint();
            RefreshWorkflowHint();
        }

        private void RefreshAbnormalFlowHint()
        {
            if (!ShowAbnormalReturnPanel)
            {
                AbnormalFlowHint = string.Empty;
                return;
            }

            // 灭失说明写入签批交接单，不再单独上传灭失情况表。
            AbnormalFlowHint = ShowApplicationActions
                ? (IsEditable
                    ? "本单存在灭失份数：请填写灭失具体情况（将写入签批交接单），打印并完成线下签字后提交；签批交接单扫描件由资料室资料管理员上传。"
                    : "本单存在灭失份数：请打印签批交接单完成线下签字；签批交接单扫描件由资料室资料管理员上传。")
                : "本单存在灭失份数：灭失说明已体现在签批交接单中，请核对明细与四级签字人后办理审批与交接。";
        }

        private void NotifyEditCommandStates()
        {
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(CanSaveDraft));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmHandover));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(CanDeleteSignedAttachment));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanVoid));
            OnPropertyChanged(nameof(CanPrintSignedHandoverOnApplication));
            OnPropertyChanged(nameof(CanPrintHandoverSheet));
            OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
            OnPropertyChanged(nameof(HasAbnormalReturnItems));
            OnPropertyChanged(nameof(ShowIntactApprovalSigner));
            OnPropertyChanged(nameof(ShowLossApprovalSigners));
            OnPropertyChanged(nameof(ShowApplicationActions));
            OnPropertyChanged(nameof(ShowApprovalActions));
            OnPropertyChanged(nameof(ShowApprovalEditorPanel));
            OnPropertyChanged(nameof(ShowOutboundCandidatesPanel));
            OnPropertyChanged(nameof(ShowStartReturnButton));
            OnPropertyChanged(nameof(CanStartReturn));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(ReviewerFieldLabel));
            OnPropertyChanged(nameof(ApproverFieldLabel));
            OnPropertyChanged(nameof(ConfirmHandoverHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            RefreshAbnormalFlowHint();
            RefreshWorkflowHint();
            CommandManager.InvalidateRequerySuggested();
        }

        private void CancelEdit()
        {
            EditItems.Clear();
            SignedAttachments.Clear();
            SelectedSignedAttachment = null;
            EditingRecord = null;
            EditHeader = string.Empty;
            AbnormalFlowHint = string.Empty;
            WorkflowHintText = string.Empty;
            NotifyEditCommandStates();
        }
    }
}
