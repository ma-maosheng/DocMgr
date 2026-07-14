using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘归还登记工作台：待归还介质发起归还 → 右侧就地登记/打印/上传/办结。
    /// </summary>
    public partial class HardDiskMediaReturnRegistrationPageViewModel : ViewModelBase
    {
        private const string ReturnStageAll = "全部";
        private const string ReturnStageRegistered = "已登记归还信息";
        private const string ReturnStagePendingComplete = "待办结";
        private const string ReturnStageSignedUploaded = "已上传签字件";
        private const string ReturnStageCompleted = "已办结";

        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private bool _isPageInitialized;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = ReturnStageAll;
        private string _selectedApplicationType = ReturnStageAll;
        private HardDiskMediaApplication? _selectedApplication;
        private HardDiskMediaReturnCandidate? _selectedCandidate;
        private bool _isLeftPanelExpanded = true;

        public HardDiskMediaReturnRegistrationPageViewModel(
            IHardDiskMediaService hardDiskMediaService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            StartReturnCommand = new RelayCommand(async _ => await StartReturnAsync(), _ => CanStartReturn());
            OpenReturnCommand = new RelayCommand(async _ => await OpenReturnAsync(), _ => CanOpenReturn());
            DeleteCommand = new RelayCommand(async _ => await DeleteApplicationAsync(), _ => CanDeleteSelectedApplication());
            ToggleLeftPanelCommand = new RelayCommand(_ => IsLeftPanelExpanded = !IsLeftPanelExpanded);

            RecommendTargetLocationCommand = new RelayCommand(async _ => await RecommendTargetLocationAsync(), _ => CanRecommendTargetLocation);
            ShowTargetLocationSnapshotCommand = new RelayCommand(async _ => await ShowTargetLocationSnapshotAsync(), _ => CanShowTargetLocationSnapshot);
            SaveDraftCommand = new RelayCommand(async _ => await SaveAsync(HardDiskMediaApplication.StatusDraft), _ => IsRegistrationEditable);
            SubmitCommand = new RelayCommand(async _ => await SaveAsync(HardDiskMediaApplication.StatusSubmitted), _ => IsRegistrationEditable);
            PrintAbnormalReportCommand = new RelayCommand(async _ => await PrintAbnormalReportAsync(), _ => CanPrintAbnormalReport);
            UploadAbnormalReportCommand = new RelayCommand(async _ => await UploadAbnormalReportAsync(), _ => CanManageAbnormalReportAttachments);
            ViewAbnormalReportCommand = new RelayCommand(async attachment => await ViewAbnormalReportAsync(attachment as SystemAttachment), attachment => attachment is SystemAttachment || SelectedAbnormalReportAttachment != null);
            DeleteAbnormalReportCommand = new RelayCommand(async attachment => await DeleteAbnormalReportAsync(attachment as SystemAttachment), attachment => (attachment is SystemAttachment || SelectedAbnormalReportAttachment != null) && CanDeleteAbnormalReportAttachment);
            PrintHandoverSheetCommand = new RelayCommand(async _ => await PrintHandoverSheetAsync(), _ => CanPrintHandoverSheet);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            ViewAttachmentCommand = new RelayCommand(async attachment => await ViewAttachmentAsync(attachment as SystemAttachment), attachment => attachment is SystemAttachment);
            DeleteAttachmentCommand = new RelayCommand(async attachment => await DeleteAttachmentAsync(attachment as SystemAttachment), attachment => attachment is SystemAttachment && CanDeleteAttachment);
            CancelEditCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
        }

        public ObservableCollection<HardDiskMediaApplication> Applications { get; } = new();

        public ObservableCollection<HardDiskMediaReturnCandidate> ReturnCandidates { get; } = new();

        public ObservableCollection<string> StatusOptions { get; } = new();

        public ObservableCollection<string> ApplicationTypeOptions { get; } = new();

        public string PageTitle => "硬盘归还登记工作台";

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set => SetProperty(ref _selectedStatus, value);
        }

        public string SelectedApplicationType
        {
            get => _selectedApplicationType;
            set => SetProperty(ref _selectedApplicationType, value);
        }

        public HardDiskMediaReturnCandidate? SelectedCandidate
        {
            get => _selectedCandidate;
            set
            {
                if (SetProperty(ref _selectedCandidate, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public HardDiskMediaApplication? SelectedApplication
        {
            get => _selectedApplication;
            set
            {
                if (SetProperty(ref _selectedApplication, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
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

        public RelayCommand SearchCommand { get; }

        public RelayCommand RefreshCommand { get; }

        public RelayCommand StartReturnCommand { get; }

        public RelayCommand OpenReturnCommand { get; }

        public RelayCommand DeleteCommand { get; }

        public RelayCommand ToggleLeftPanelCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isPageInitialized)
            {
                return;
            }

            await LoadFilterOptionsAsync();
            await SearchAsync();
            _isPageInitialized = true;
        }

        private async Task LoadFilterOptionsAsync()
        {
            StatusOptions.Clear();
            StatusOptions.Add(ReturnStageAll);
            StatusOptions.Add(ReturnStageRegistered);
            StatusOptions.Add(ReturnStagePendingComplete);
            StatusOptions.Add(ReturnStageSignedUploaded);
            StatusOptions.Add(ReturnStageCompleted);
            SelectedStatus = ReturnStageAll;

            HardDiskMediaApplicationViewModelHelper.ResetReturnRegistrationKindOptions(ApplicationTypeOptions);
            SelectedApplicationType = ApplicationTypeOptions.FirstOrDefault() ?? ReturnStageAll;

            await Task.CompletedTask;
        }

        private async Task SearchAsync()
        {
            try
            {
                int? selectedId = SelectedApplication?.Id ?? _editingApplication?.Id;
                var items = await _hardDiskMediaService.SearchApplicationsAsync(SearchKeyword, null, null);

                Applications.Clear();
                foreach (var item in items.Where(item => HardDiskMediaApplicationViewModelHelper.IsReturnRegistrationType(item.ApplicationType))
                                          .Where(MatchesSelectedRegistrationKind)
                                          .Where(MatchesSelectedReturnStage))
                {
                    Applications.Add(item);
                }

                await LoadReturnCandidatesAsync();

                SelectedApplication = selectedId.HasValue
                    ? Applications.FirstOrDefault(item => item.Id == selectedId.Value)
                    : Applications.FirstOrDefault();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task RefreshListsKeepingEditorAsync()
        {
            int? editingId = _editingApplication?.Id;
            await SearchAsync();
            if (editingId is > 0)
            {
                SelectedApplication = Applications.FirstOrDefault(item => item.Id == editingId.Value) ?? SelectedApplication;
                if (SelectedApplication != null)
                {
                    SynchronizeEditingApplication(SelectedApplication);
                }
            }
        }

        private async Task LoadReturnCandidatesAsync()
        {
            string? selectedKey = SelectedCandidate == null ? null : BuildCandidateKey(SelectedCandidate);
            var candidates = await _hardDiskMediaService.GetReturnRegistrationCandidatesAsync();
            ReturnCandidates.Clear();
            foreach (var candidate in candidates
                         .OrderBy(item => item.ExpectedReturnDate ?? DateTime.MaxValue)
                         .ThenBy(item => item.DiskCode, StringComparer.Ordinal))
            {
                ReturnCandidates.Add(candidate);
            }

            SelectedCandidate = selectedKey == null
                ? ReturnCandidates.FirstOrDefault()
                : ReturnCandidates.FirstOrDefault(item => string.Equals(BuildCandidateKey(item), selectedKey, StringComparison.Ordinal))
                  ?? ReturnCandidates.FirstOrDefault();
        }

        private bool MatchesSelectedRegistrationKind(HardDiskMediaApplication application)
        {
            return HardDiskMediaReturnDomainValues.MatchesRegistrationKindFilter(
                SelectedApplicationType,
                application.ApplicationType,
                application.InspectionResult);
        }

        private bool MatchesSelectedReturnStage(HardDiskMediaApplication application)
        {
            if (SelectedStatus == ReturnStageAll)
            {
                return true;
            }

            return SelectedStatus switch
            {
                ReturnStageRegistered => application.ApplicationStatus == HardDiskMediaApplication.StatusDraft ||
                                         (application.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted && application.PrintCount <= 0) ||
                                         application.ApplicationStatus == HardDiskMediaApplication.StatusPendingUpload,
                ReturnStagePendingComplete => application.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted && application.PrintCount > 0,
                ReturnStageSignedUploaded => application.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded,
                ReturnStageCompleted => application.ApplicationStatus == HardDiskMediaApplication.StatusCompleted,
                _ => true
            };
        }

        private static string ResolveReturnStageText(HardDiskMediaApplication? application)
        {
            if (application == null)
            {
                return "(无)";
            }

            return application.ApplicationStatus switch
            {
                HardDiskMediaApplication.StatusDraft => ReturnStageRegistered,
                HardDiskMediaApplication.StatusSubmitted when application.PrintCount > 0 => ReturnStagePendingComplete,
                HardDiskMediaApplication.StatusSubmitted => ReturnStageRegistered,
                HardDiskMediaApplication.StatusSignedUploaded => ReturnStageSignedUploaded,
                HardDiskMediaApplication.StatusCompleted => ReturnStageCompleted,
                _ => application.ApplicationStatus
            };
        }

        private async Task RefreshAsync()
        {
            SearchKeyword = string.Empty;
            SelectedStatus = ReturnStageAll;
            SelectedApplicationType = ReturnStageAll;
            await SearchAsync();
        }

        private async Task StartReturnAsync()
        {
            if (SelectedCandidate == null)
            {
                return;
            }

            var existingReturn = await _hardDiskMediaService.GetActiveReturnRegistrationByMediumIdAsync(SelectedCandidate.MediumId);
            if (existingReturn != null)
            {
                var editable = HardDiskMediaApplicationViewModelHelper.CloneApplication(existingReturn);
                await LoadEditorSessionAsync(editable);
                SelectedApplication = Applications.FirstOrDefault(item => item.Id == existingReturn.Id) ?? SelectedApplication;
                _dialogService.ShowMessage(
                    $"该硬盘已有未办结归还登记单 [{existingReturn.ApplicationNo}]，已为您打开续办。",
                    "提示");
                return;
            }

            var draft = new HardDiskMediaApplication
            {
                ApplicationType = HardDiskMediaApplication.TypeReturnBlankRegistration,
                MediumId = SelectedCandidate.MediumId,
                SourceApplicationId = SelectedCandidate.SourceApplicationId,
                SourceOutboundRecordId = SelectedCandidate.SourceOutboundRecordId,
                ApplicantName = SelectedCandidate.ApplicantName,
                ApplicantDept = SelectedCandidate.ApplicantDept,
                CurrentLocation = SelectedCandidate.BorrowedLocation,
                ExpectedReturnDate = SelectedCandidate.ExpectedReturnDate,
                ApplyTime = DateTime.Today
            };

            await LoadEditorSessionAsync(draft);
        }

        private async Task OpenReturnAsync()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            var editable = HardDiskMediaApplicationViewModelHelper.CloneApplication(SelectedApplication);
            await LoadEditorSessionAsync(editable);
        }

        private async Task DeleteApplicationAsync()
        {
            HardDiskMediaApplication? target = _editingApplication?.Id > 0
                ? _editingApplication
                : SelectedApplication;
            if (target == null || target.Id <= 0)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定要删除登记单 [{target.ApplicationNo}] 吗？", "提示"))
            {
                return;
            }

            try
            {
                await _hardDiskMediaService.DeleteApplicationAsync(target.Id);
                CancelEdit();
                await SearchAsync();
                _dialogService.ShowMessage("删除成功。");
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private bool CanStartReturn()
        {
            return IsCurrentUserArchiveAdmin() && SelectedCandidate != null;
        }

        private bool CanOpenReturn()
        {
            return SelectedApplication != null && IsCurrentUserArchiveAdmin();
        }

        private bool CanDeleteSelectedApplication()
        {
            HardDiskMediaApplication? target = _editingApplication?.Id > 0
                ? _editingApplication
                : SelectedApplication;
            return target != null
                   && IsCurrentUserArchiveAdmin()
                   && target.ApplicationStatus != HardDiskMediaApplication.StatusCompleted;
        }

        private bool IsCurrentUserArchiveAdmin()
        {
            return HardDiskMediaApplicationViewModelHelper.IsArchiveRoomMediaAdmin(_userContextService.CurrentUser);
        }

        private static string BuildCandidateKey(HardDiskMediaReturnCandidate candidate)
        {
            return $"{candidate.MediumId}|{candidate.SourceApplicationId}|{candidate.SourceOutboundRecordId}";
        }
    }
}
