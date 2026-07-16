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
    /// 资料归还工作台：发起归还（由已办结出库单生成）→ 登记 → 办结入库。
    /// </summary>
    public sealed class ArchiveReturnWorkbenchViewModel : ViewModelBase
    {
        private const string FilterAll = "全部";
        private const string OutboundFilterOverdue = "超期未还";

        private readonly IArchiveReturnService _returnService;
        private readonly IArchiveOutboundService _outboundService;
        private readonly IUserContextService _userContextService;
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
        private bool _isBusy;
        private bool _isAdmin;
        private bool _hasAbnormalReportUploaded;
        private bool _isLeftPanelExpanded = true;
        private bool _isActive = true;

        /// <summary>页面卸载时调用，阻止卸载后继续访问已释放的页面作用域服务。</summary>
        public void Deactivate() => _isActive = false;

        public ArchiveReturnWorkbenchViewModel(
            ArchiveReturnWorkspaceMode workspaceMode,
            IArchiveReturnService returnService,
            IArchiveOutboundService outboundService,
            IUserContextService userContextService,
            IDialogService dialogService)
        {
            _workspaceMode = workspaceMode;
            _returnService = returnService;
            _outboundService = outboundService;
            _userContextService = userContextService;
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
            PrintReceiptCommand = new RelayCommand(async _ => await PrintReceiptAsync(), _ => !IsBusy && CanPrintReceipt);
            PrintAbnormalReportCommand = new RelayCommand(async _ => await PrintAbnormalReportAsync(), _ => !IsBusy && CanPrintAbnormalReport);
            UploadAbnormalReportCommand = new RelayCommand(async _ => await UploadAbnormalReportAsync(), _ => !IsBusy && CanManageAbnormalReportAttachments);
            ViewAbnormalReportCommand = new RelayCommand(async _ => await ViewAbnormalReportAsync(), _ => SelectedAbnormalReportAttachment != null);
            DeleteAbnormalReportCommand = new RelayCommand(async _ => await DeleteAbnormalReportAsync(), _ => !IsBusy && CanManageAbnormalReportAttachments && SelectedAbnormalReportAttachment != null);
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
            ArchiveReturnWorkspaceMode.Approval => "资料归还审批",
            _ => "资料归还入库"
        };

        public string PageSubtitle => _workspaceMode switch
        {
            ArchiveReturnWorkspaceMode.Application => "由借出人发起归还申请并提交审批。",
            ArchiveReturnWorkspaceMode.Approval => "对已提交的归还申请进行审批。",
            _ => "办理实物交接、上传签批交接单并办结入库。"
        };

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

        public ObservableCollection<SystemAttachment> AbnormalReportAttachments { get; } = new();

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
        public RelayCommand PrintReceiptCommand { get; }
        public RelayCommand PrintAbnormalReportCommand { get; }
        public RelayCommand UploadAbnormalReportCommand { get; }
        public RelayCommand ViewAbnormalReportCommand { get; }
        public RelayCommand DeleteAbnormalReportCommand { get; }
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
                    OnPropertyChanged(nameof(CanPrintReceipt));
                    OnPropertyChanged(nameof(CanPrintAbnormalReport));
                    OnPropertyChanged(nameof(CanManageAbnormalReportAttachments));
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

        public bool HasAbnormalReportUploaded
        {
            get => _hasAbnormalReportUploaded;
            private set
            {
                if (SetProperty(ref _hasAbnormalReportUploaded, value))
                {
                    OnPropertyChanged(nameof(CanComplete));
                    OnPropertyChanged(nameof(CanPrintReceipt));
                    RefreshAbnormalFlowHint();
                }
            }
        }

        public SystemAttachment? SelectedAbnormalReportAttachment
        {
            get => _selectedAbnormalReportAttachment;
            set => SetProperty(ref _selectedAbnormalReportAttachment, value);
        }

        private SystemAttachment? _selectedAbnormalReportAttachment;

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
            _workspaceMode == ArchiveReturnWorkspaceMode.Approval
            && IsAdmin
            && EditingRecord is { Id: > 0, Status: YearlyArchiveReturnRecord.Submitted };

        public bool CanConfirmHandover =>
            _workspaceMode == ArchiveReturnWorkspaceMode.Handover
            && IsAdmin
            && EditingRecord is { Id: > 0, Status: YearlyArchiveReturnRecord.Approved }
            && (!HasAbnormalReturnItems || HasAbnormalReportUploaded);

        public bool ShowApplicationActions => _workspaceMode == ArchiveReturnWorkspaceMode.Application;

        public bool ShowApprovalActions => _workspaceMode == ArchiveReturnWorkspaceMode.Approval;

        public bool ShowHandoverActions => _workspaceMode == ArchiveReturnWorkspaceMode.Handover;

        public bool ShowOutboundCandidatesPanel => _workspaceMode == ArchiveReturnWorkspaceMode.Application;

        public bool HasAbnormalReturnItems =>
            EditingRecord != null
            && ArchiveReturnDomainValues.HasAbnormalReturnItems(EditItems.Select(item => item.Source));

        public bool ShowAbnormalReturnPanel =>
            IsEditing && HasAbnormalReturnItems;

        public bool CanPrintAbnormalReport =>
            ShowAbnormalReturnPanel && EditingRecord is { Id: > 0 }
            && EditingRecord.Status is YearlyArchiveReturnRecord.Draft
                or YearlyArchiveReturnRecord.Submitted
                or YearlyArchiveReturnRecord.Approved
                or YearlyArchiveReturnRecord.SignedUploaded;

        public bool CanManageAbnormalReportAttachments =>
            ShowAbnormalReturnPanel
            && EditingRecord is { Status: YearlyArchiveReturnRecord.Draft }
            && _workspaceMode == ArchiveReturnWorkspaceMode.Application;

        /// <summary>已实物交接后可办结入库。</summary>
        public bool CanComplete =>
            _workspaceMode == ArchiveReturnWorkspaceMode.Handover
            && IsAdmin
            && EditingRecord is { Status: YearlyArchiveReturnRecord.SignedUploaded }
            && (!HasAbnormalReturnItems || HasAbnormalReportUploaded);

        /// <summary>办结前可作废；审批后仅管理员可强制。</summary>
        public bool CanVoid => EditingRecord is { } record
            && record.Id > 0
            && record.Status is YearlyArchiveReturnRecord.Draft
                or YearlyArchiveReturnRecord.Submitted
                or YearlyArchiveReturnRecord.Approved
                or YearlyArchiveReturnRecord.SignedUploaded
            && (_workspaceMode != ArchiveReturnWorkspaceMode.Application
                || record.Status is YearlyArchiveReturnRecord.Draft or YearlyArchiveReturnRecord.Submitted);

        /// <summary>实物交接后可打印回执。</summary>
        public bool CanPrintReceipt => EditingRecord is { Id: > 0 } record
            && record.Status is YearlyArchiveReturnRecord.SignedUploaded or YearlyArchiveReturnRecord.Completed
            && (_workspaceMode == ArchiveReturnWorkspaceMode.Handover || record.Status == YearlyArchiveReturnRecord.Completed)
            && (!HasAbnormalReturnItems || HasAbnormalReportUploaded || record.Status == YearlyArchiveReturnRecord.Completed);

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
            if (IsAdmin)
            {
                var returnable = await _returnService.GetReturnableOutboundsAsync(SelectedYear);
                _allReturnableOutbounds.AddRange(returnable);
            }

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
            if (SelectedReturnStatus != FilterAll
                && !string.Equals(record.StatusStr, SelectedReturnStatus, StringComparison.Ordinal))
            {
                return false;
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

            if (!_dialogService.ShowConfirm($"确认审批通过归还单 {record.ReturnNo}？", "审批确认"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _returnService.ApproveReturnFlowAsync(record.Id, user);
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

            if (!_dialogService.ShowConfirm($"确认归还单 {record.ReturnNo} 已完成实物交接？", "实物交接确认"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _returnService.ConfirmHandoverFlowAsync(record.Id, user);
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

            if (!_dialogService.ShowConfirm($"确认办结归还单 {record.ReturnNo}？办结后将冲销出库提档对资料台账的影响，且不可撤销。", "办结确认"))
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

            if (!_dialogService.ShowConfirm($"确认作废归还单 {record.ReturnNo}？", "作废确认"))
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
            _ = LoadAttachmentsAsync(record.Id);
            NotifyEditCommandStates();
        }

        private void OnReturnCopyCountsChanged()
        {
            OnPropertyChanged(nameof(HasAbnormalReturnItems));
            OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrintReceipt));
            OnPropertyChanged(nameof(CanPrintAbnormalReport));
            OnPropertyChanged(nameof(CanManageAbnormalReportAttachments));
            RefreshAbnormalFlowHint();
        }

        private async Task LoadAttachmentsAsync(int recordId)
        {
            if (!_isActive)
            {
                return;
            }

            AbnormalReportAttachments.Clear();
            SelectedAbnormalReportAttachment = null;
            HasAbnormalReportUploaded = false;

            if (recordId <= 0)
            {
                RefreshAbnormalFlowHint();
                return;
            }

            var attachments = await _returnService.GetAttachmentsAsync(recordId);
            foreach (var attachment in attachments)
            {
                if (string.Equals(
                        attachment.FileCategory,
                        ArchiveReturnDomainValues.AttachmentKindSignedAbnormalReturnReport,
                        StringComparison.Ordinal))
                {
                    AbnormalReportAttachments.Add(attachment);
                }
            }

            HasAbnormalReportUploaded = AbnormalReportAttachments.Count > 0;
            RefreshAbnormalFlowHint();
        }

        private void RefreshAbnormalFlowHint()
        {
            if (!ShowAbnormalReturnPanel)
            {
                AbnormalFlowHint = string.Empty;
                return;
            }

            AbnormalFlowHint = HasAbnormalReportUploaded
                ? (IsEditable
                    ? "灭失情况表扫描件已上传，可登记后打印回执并办结入库。"
                    : "灭失情况表扫描件已上传，可打印回执并办结入库。")
                : (IsEditable
                    ? "本单存在灭失份数：请填写灭失具体情况，打印灭失情况表并完成线下签字后上传扫描件，再办理登记。"
                    : "本单存在灭失份数，登记信息已锁定。");
        }

        private void NotifyEditCommandStates()
        {
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(CanSaveDraft));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmHandover));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanVoid));
            OnPropertyChanged(nameof(CanPrintReceipt));
            OnPropertyChanged(nameof(CanPrintAbnormalReport));
            OnPropertyChanged(nameof(CanManageAbnormalReportAttachments));
            OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
            OnPropertyChanged(nameof(HasAbnormalReturnItems));
            OnPropertyChanged(nameof(ShowApplicationActions));
            OnPropertyChanged(nameof(ShowApprovalActions));
            OnPropertyChanged(nameof(ShowHandoverActions));
            OnPropertyChanged(nameof(ShowOutboundCandidatesPanel));
            OnPropertyChanged(nameof(CanStartReturn));
            RefreshAbnormalFlowHint();
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task PrintReceiptAsync()
        {
            if (EditingRecord is not { Id: > 0 } record)
            {
                return;
            }

            IsBusy = true;
            try
            {
                bool blankHandoverSignatures = true;
                var data = await _returnService.BuildReceiptPrintDataAsync(record.Id, blankHandoverSignatures);
                var document = ArchiveReturnPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };

                await _returnService.RecordPrintAsync(record.Id);
                previewWindow.ShowDialog();

                var reloaded = await _returnService.GetReturnAsync(record.Id);
                if (reloaded != null)
                {
                    LoadEditing(reloaded);
                }
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("归还回执打印生成失败：" + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task PrintAbnormalReportAsync()
        {
            if (EditingRecord is not { Id: > 0 } record)
            {
                return;
            }

            IsBusy = true;
            try
            {
                bool blankApprovalSignatures = true;
                var data = await _returnService.BuildAbnormalReportPrintDataAsync(record.Id, blankApprovalSignatures);
                var document = ArchiveReturnAbnormalReportPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };
                previewWindow.ShowDialog();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("灭失情况表打印失败：" + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task UploadAbnormalReportAsync()
        {
            if (EditingRecord is not { } record)
            {
                return;
            }

            if (record.Id <= 0)
            {
                _dialogService.ShowError("请先保存草稿后再上传灭失情况表扫描件。");
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = SystemAttachmentUploadSupport.OpenFileDialogFilter
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            try
            {
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

                var result = await _returnService.UploadAbnormalReportAttachmentFlowAsync(record.Id, attachment, user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                await LoadAttachmentsAsync(record.Id);
                _dialogService.ShowMessage(result.Message, "上传成功");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("上传灭失情况表扫描件失败：" + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ViewAbnormalReportAsync()
        {
            var attachment = SelectedAbnormalReportAttachment;
            if (attachment == null)
            {
                return;
            }

            try
            {
                var result = await _returnService.PrepareAttachmentViewFlowAsync(attachment);
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
                _dialogService.ShowError("查看报备件失败：" + ex.Message);
            }
        }

        private async Task DeleteAbnormalReportAsync()
        {
            if (EditingRecord is not { Id: > 0 } record)
            {
                return;
            }

            var attachment = SelectedAbnormalReportAttachment;
            if (attachment == null)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确认删除报备件「{attachment.FileName}」？", "删除确认"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _returnService.DeleteAbnormalReportAttachmentFlowAsync(record.Id, attachment, user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                await LoadAttachmentsAsync(record.Id);
                _dialogService.ShowMessage(result.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void CancelEdit()
        {
            EditItems.Clear();
            AbnormalReportAttachments.Clear();
            SelectedAbnormalReportAttachment = null;
            HasAbnormalReportUploaded = false;
            EditingRecord = null;
            EditHeader = string.Empty;
            AbnormalFlowHint = string.Empty;
            NotifyEditCommandStates();
        }
    }
}
