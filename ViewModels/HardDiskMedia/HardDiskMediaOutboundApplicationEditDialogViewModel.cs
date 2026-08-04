using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Services.HardDiskMedia;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 介质出库申请编辑弹窗 ViewModel。
    /// </summary>
    public class HardDiskMediaOutboundApplicationEditDialogViewModel : ViewModelBase
    {
        public const int MaxSelectableMediumCount = 5;

        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly HardDiskMediaApplication _sourceApplication;
        private bool _isInitialized;
        private bool _isUpdatingMediumSelection;
        private string _applicationNo = string.Empty;
        private HardDiskMediaApplicantOption? _selectedApplicant;
        private string _applicationType = HardDiskMediaApplication.TypeOutboundTemporary;
        private string _applicantDept = string.Empty;
        private DateTime _applyTime;
        private string _reason = string.Empty;
        private string _currentLocation = string.Empty;
        private string _targetLocation = string.Empty;
        private DateTime? _expectedReturnDate;
        private bool _hasCommittedChanges;

        public HardDiskMediaOutboundApplicationEditDialogViewModel(
            IHardDiskMediaService hardDiskMediaService,
            IDialogService dialogService,
            IUserContextService userContextService,
            HardDiskMediaApplication applicationToEdit)
        {
            ArgumentNullException.ThrowIfNull(applicationToEdit);

            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _sourceApplication = applicationToEdit;

            SaveDraftCommand = new RelayCommand(async _ => await SaveAsync(HardDiskMediaApplication.StatusDraft), _ => CanSaveApplicationDraft);
            SubmitCommand = new RelayCommand(async _ => await SaveAsync(HardDiskMediaApplication.StatusSubmitted), _ => CanSubmitApplication);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrintApplication);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string WindowTitle =>
            $"硬盘借出 · {(string.IsNullOrWhiteSpace(ApplicationNo) ? "待编单" : ApplicationNo)} · {StatusDisplay}";

        public string StatusDisplay => _sourceApplication.StatusStr;

        public string HeaderGuidanceText =>
            $"{ApplicationTypeGuidanceText} {MediumSelectionGuidanceText}";

        public ObservableCollection<HardDiskMediaOutboundMediumOption> MediumOptions { get; } = new();

        public ObservableCollection<HardDiskMediaApplicantOption> ApplicantOptions { get; } = new();

        public ObservableCollection<string> ApplicationTypeOptions { get; } = new();

        public string SaveButtonText => "保存草稿";

        public string SubmitButtonText => "提交申请";

        public string PrintButtonText => "打印申请";

        public bool CanSaveApplicationDraft => ResolveApplicationFormActions().CanSaveDraft;

        public bool CanSubmitApplication => ResolveApplicationFormActions().CanSubmitApplication;

        public bool CanPrintApplication => ResolveApplicationFormActions().CanPrintApplication;

        public string ApplicationTypeGuidanceText =>
            "临时出库归还期限为1个月，可提前填写预计归还日期；长期出库不设归还期限；永久出库表示硬盘另有他用无需归还。";

        public bool CanEditExpectedReturnDate =>
            HardDiskMediaOutboundReturnSupport.RequiresExpectedReturnDate(ApplicationType);

        public bool ShowExpectedReturnDateAsDash =>
            HardDiskMediaOutboundReturnSupport.IsNonReturnableOutboundType(ApplicationType);

        public string ExpectedReturnDateDisplay =>
            HardDiskMediaOutboundReturnSupport.FormatExpectedReturnDateDisplay(ApplicationType, ExpectedReturnDate);

        public string ExpectedReturnDateHint
        {
            get
            {
                if (!CanEditExpectedReturnDate)
                {
                    return string.Empty;
                }

                DateTime deadline = HardDiskMediaOutboundReturnSupport.CalculateReturnDeadline(ApplyTime);
                return $"归还期限至 {deadline:yyyy-MM-dd}，可填写不晚于该日期的预计归还日期。";
            }
        }

        public string MediumSelectionGuidanceText =>
            $"关联介质最多选择 {MaxSelectableMediumCount} 块硬盘，每块硬盘对应一份申请单，归还时可分别办理。";

        public string MediumSelectionSummary =>
            GetSelectedMedia().Count == 0
                ? "未选择"
                : $"已选 {GetSelectedMedia().Count} 块";

        public bool AllowsMultipleMediumSelection => _sourceApplication.Id == 0;

        public string ApplicationNo
        {
            get => _applicationNo;
            set
            {
                if (SetProperty(ref _applicationNo, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        public HardDiskMediaApplicantOption? SelectedApplicant
        {
            get => _selectedApplicant;
            set
            {
                if (SetProperty(ref _selectedApplicant, value))
                {
                    ApplicantDept = value?.ApplicantDept ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(TargetLocation))
                    {
                        TargetLocation = ApplicantDept;
                    }
                }
            }
        }

        public string ApplicationType
        {
            get => _applicationType;
            set
            {
                if (SetProperty(ref _applicationType, value))
                {
                    SyncExpectedReturnDate(preferExistingValue: false);
                    NotifyExpectedReturnDatePresentationChanged();
                }
            }
        }

        public string ApplicantDept
        {
            get => _applicantDept;
            set => SetProperty(ref _applicantDept, value);
        }

        public DateTime ApplyTime
        {
            get => _applyTime;
            set
            {
                if (SetProperty(ref _applyTime, value))
                {
                    SyncExpectedReturnDate(preferExistingValue: true);
                    NotifyExpectedReturnDatePresentationChanged();
                }
            }
        }

        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        public string CurrentLocation
        {
            get => _currentLocation;
            set => SetProperty(ref _currentLocation, value);
        }

        public string TargetLocation
        {
            get => _targetLocation;
            set => SetProperty(ref _targetLocation, value);
        }

        public DateTime? ExpectedReturnDate
        {
            get => _expectedReturnDate;
            set
            {
                if (SetProperty(ref _expectedReturnDate, value))
                {
                    OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
                }
            }
        }

        public string ReasonFieldLabel => "申请原因 *";

        public bool HasCommittedChanges
        {
            get => _hasCommittedChanges;
            private set => SetProperty(ref _hasCommittedChanges, value);
        }

        public ICommand SaveDraftCommand { get; }

        public ICommand SubmitCommand { get; }

        public ICommand PrintCommand { get; }

        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadOptionsAsync();
            await LoadApplicationAsync();
            RefreshApplicationActionCommandStates();
            _isInitialized = true;
        }

        private async Task LoadOptionsAsync()
        {
            var media = await _hardDiskMediaService.GetSelectableMediaAsync();
            var applications = await _hardDiskMediaService.SearchApplicationsAsync(null, null, null);

            var lockedMediumIds = applications
                .Where(item => item.Id != _sourceApplication.Id)
                .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusDraft ||
                               item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted ||
                               item.ApplicationStatus == HardDiskMediaApplication.StatusPendingUpload ||
                               item.ApplicationStatus == HardDiskMediaApplication.StatusPendingProcess)
                .Where(item => HardDiskMediaApplicationViewModelHelper.IsSelectableOutboundApplicationType(item.ApplicationType))
                .Select(item => item.MediumId)
                .ToHashSet();

            foreach (var option in MediumOptions)
            {
                option.PropertyChanged -= OnMediumOptionPropertyChanged;
            }

            MediumOptions.Clear();
            foreach (var item in media
                         .Where(m => string.Equals(m.Ledger?.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
                         .Where(m => m.RegisterLock == null
                                     || (string.Equals(m.RegisterLock.BusinessType, HardDiskRegisterLock.BusinessTypeOutboundApplication, StringComparison.Ordinal)
                                         && m.RegisterLock.BusinessRecordId == _sourceApplication.Id))
                         .Where(m => !lockedMediumIds.Contains(m.Id) || m.Id == _sourceApplication.MediumId)
                         .OrderBy(m => m.DiskCode))
            {
                string storageLocation = item.Ledger?.StorageLocation?.Trim() ?? string.Empty;
                string slotCode = ArchiveSlotLocationSupport.BuildSlotKey(storageLocation);
                if (string.IsNullOrWhiteSpace(slotCode))
                {
                    slotCode = storageLocation;
                }

                var option = new HardDiskMediaOutboundMediumOption
                {
                    Id = item.Id,
                    DiskCode = item.DiskCode?.Trim() ?? string.Empty,
                    SerialNumber = item.SerialNumber?.Trim() ?? string.Empty,
                    DiskType = item.DiskType?.Trim() ?? string.Empty,
                    Brand = item.Brand?.Trim() ?? string.Empty,
                    Capacity = item.Capacity?.Trim() ?? string.Empty,
                    InterfaceType = item.InterfaceType?.Trim() ?? string.Empty,
                    RegisterPerson = item.RegisterPerson?.Trim() ?? string.Empty,
                    RegisterDate = item.RegisterDate,
                    FactoryDate = item.FactoryDate,
                    RegistrationMethod = item.RegistrationMethod?.Trim() ?? string.Empty,
                    Remark = item.Remark?.Trim() ?? string.Empty,
                    CurrentLocation = storageLocation,
                    SlotCode = slotCode
                };
                option.PropertyChanged += OnMediumOptionPropertyChanged;
                MediumOptions.Add(option);
            }

            ApplicationTypeOptions.Clear();
            ApplicationTypeOptions.Add(HardDiskMediaApplication.TypeOutboundTemporary);
            ApplicationTypeOptions.Add(HardDiskMediaApplication.TypeOutboundLongTerm);
            ApplicationTypeOptions.Add(HardDiskMediaApplication.TypeOutboundPermanent);

            ApplicantOptions.Clear();
            ApplicantOptions.Add(new HardDiskMediaApplicantOption
            {
                ApplicantName = string.IsNullOrWhiteSpace(_sourceApplication.ApplicantName)
                    ? _userContextService.CurrentUser?.RealName ?? string.Empty
                    : _sourceApplication.ApplicantName,
                ApplicantDept = string.IsNullOrWhiteSpace(_sourceApplication.ApplicantDept)
                    ? _userContextService.CurrentUser?.Department ?? string.Empty
                    : _sourceApplication.ApplicantDept
            });
        }

        private async Task LoadApplicationAsync()
        {
            ApplicationNo = _sourceApplication.Id == 0
                ? await _hardDiskMediaService.GenerateNextApplicationNoAsync()
                : _sourceApplication.ApplicationNo;
            ApplicationType = _sourceApplication.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm ||
                              _sourceApplication.ApplicationType == HardDiskMediaApplication.TypeOutboundPermanent
                ? _sourceApplication.ApplicationType
                : HardDiskMediaApplication.TypeOutboundTemporary;

            SelectedApplicant = ApplicantOptions.FirstOrDefault();

            foreach (var option in MediumOptions)
            {
                option.IsSelected = option.Id == _sourceApplication.MediumId;
            }

            ApplicantDept = !string.IsNullOrWhiteSpace(_sourceApplication.ApplicantDept)
                ? _sourceApplication.ApplicantDept
                : SelectedApplicant?.ApplicantDept ?? string.Empty;
            ApplyTime = _sourceApplication.ApplyTime == default ? DateTime.Today : _sourceApplication.ApplyTime;
            Reason = _sourceApplication.Reason;
            TargetLocation = !string.IsNullOrWhiteSpace(_sourceApplication.TargetLocation)
                ? _sourceApplication.TargetLocation
                : ApplicantDept;
            UpdateSelectedMediumPresentation();
            ExpectedReturnDate = _sourceApplication.ExpectedReturnDate;
            SyncExpectedReturnDate(preferExistingValue: _sourceApplication.ExpectedReturnDate.HasValue);
            NotifyExpectedReturnDatePresentationChanged();
        }

        private async Task SaveAsync(int targetStatus)
        {
            if (!TryValidateForm(out var selectedMedia))
            {
                return;
            }

            try
            {
                if (_sourceApplication.Id == 0 && selectedMedia.Count > 1)
                {
                    await SaveBatchAsync(selectedMedia, targetStatus);
                    return;
                }

                var application = BuildApplicationForSave(targetStatus, selectedMedia[0]);
                await _hardDiskMediaService.SaveApplicationAsync(application, _userContextService.CurrentUser);
                SynchronizeSourceApplication(application);
                HasCommittedChanges = true;
                _dialogService.ShowMessage("申请已保存。");
                RequestClose?.Invoke(true);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PrintAsync()
        {
            if (!TryValidateForm(out var selectedMedia))
            {
                return;
            }

            if (_sourceApplication.Id == 0 && selectedMedia.Count > 1)
            {
                _dialogService.ShowMessage("多盘出库请先保存申请，再分别打开各申请单打印。");
                return;
            }

            try
            {
                int statusForPrint = _sourceApplication.ApplicationStatus;
                var application = BuildApplicationForSave(statusForPrint, selectedMedia[0]);

                await _hardDiskMediaService.SaveApplicationAsync(application, _userContextService.CurrentUser);
                SynchronizeSourceApplication(application);
                HasCommittedChanges = true;

                var data = await _hardDiskMediaService.BuildPrintDataAsync(application);
                var document = HardDiskMediaPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };

                await _hardDiskMediaService.MarkApplicationPrintedAsync(application);
                previewWindow.ShowDialog();
                RequestClose?.Invoke(true);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task SaveBatchAsync(IReadOnlyList<HardDiskMediaOutboundMediumOption> selectedMedia, int targetStatus)
        {
            string batchKey = ApplicationNo.Trim();
            HardDiskMediaApplication? primaryApplication = null;

            for (int index = 0; index < selectedMedia.Count; index++)
            {
                var medium = selectedMedia[index];
                var application = BuildApplicationForSave(targetStatus, medium);
                application.RelatedBatch = batchKey;

                if (index > 0)
                {
                    application.Id = 0;
                    application.ApplicationNo = string.Empty;
                }

                await _hardDiskMediaService.SaveApplicationAsync(application, _userContextService.CurrentUser);
                primaryApplication ??= application;
            }

            if (primaryApplication != null)
            {
                SynchronizeSourceApplication(primaryApplication);
            }

            HasCommittedChanges = true;
            _dialogService.ShowMessage($"已保存 {selectedMedia.Count} 份申请，每块硬盘对应一份申请单。");
            RequestClose?.Invoke(true);
        }

        private bool TryValidateForm(out IReadOnlyList<HardDiskMediaOutboundMediumOption> selectedMedia)
        {
            selectedMedia = GetSelectedMedia();

            if (selectedMedia.Count == 0)
            {
                _dialogService.ShowMessage("请至少选择一块关联介质。");
                selectedMedia = Array.Empty<HardDiskMediaOutboundMediumOption>();
                return false;
            }

            if (selectedMedia.Count > MaxSelectableMediumCount)
            {
                _dialogService.ShowMessage($"关联介质最多选择 {MaxSelectableMediumCount} 块硬盘。");
                return false;
            }

            if (_sourceApplication.Id > 0 && selectedMedia.Count > 1)
            {
                _dialogService.ShowMessage("编辑已有申请时仅可关联单块硬盘。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(ApplicationType))
            {
                _dialogService.ShowMessage("请选择申请类型。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Reason))
            {
                _dialogService.ShowMessage("请输入申请原因。");
                return false;
            }

            try
            {
                HardDiskMediaOutboundReturnSupport.ValidateExpectedReturnDate(ApplicationType, ApplyTime, ExpectedReturnDate);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowMessage(ex.Message);
                return false;
            }

            return true;
        }

        private HardDiskMediaApplication BuildApplicationForSave(int targetStatus, HardDiskMediaOutboundMediumOption medium)
        {
            return new HardDiskMediaApplication
            {
                Id = _sourceApplication.Id,
                ApplicationNo = ApplicationNo.Trim(),
                MediumId = medium.Id,
                SourceApplicationId = _sourceApplication.SourceApplicationId,
                ApplicationType = ApplicationType.Trim(),
                ApplicationStatus = targetStatus,
                ApplicantName = SelectedApplicant?.ApplicantName?.Trim() ?? string.Empty,
                ApplicantDept = ApplicantDept.Trim(),
                ApplyTime = ApplyTime,
                Reason = Reason.Trim(),
                TargetPersonOrUnit = string.Empty,
                CurrentLocation = string.IsNullOrWhiteSpace(medium.CurrentLocation) ? CurrentLocation.Trim() : medium.CurrentLocation.Trim(),
                TargetLocation = TargetLocation.Trim(),
                ExpectedReturnDate = HardDiskMediaOutboundReturnSupport.ResolveExpectedReturnDateForSave(
                    ApplicationType,
                    ApplyTime,
                    ExpectedReturnDate),
                RelatedBatch = _sourceApplication.RelatedBatch,
                RelatedArchiveTitle = string.Empty,
                Remark = _sourceApplication.Remark,
                PrintCount = _sourceApplication.PrintCount,
                PrintedTime = _sourceApplication.PrintedTime,
                SignedAttachmentUploaded = _sourceApplication.SignedAttachmentUploaded,
                SignedAttachmentUploadedTime = _sourceApplication.SignedAttachmentUploadedTime,
                SignedAttachmentUploader = _sourceApplication.SignedAttachmentUploader,
                ApprovedBy = _sourceApplication.ApprovedBy,
                ApprovedTime = _sourceApplication.ApprovedTime,
                ApprovalOpinion = _sourceApplication.ApprovalOpinion,
                ExecutedBy = _sourceApplication.ExecutedBy,
                ExecutedTime = _sourceApplication.ExecutedTime
            };
        }

        private void SynchronizeSourceApplication(HardDiskMediaApplication savedApplication)
        {
            _sourceApplication.Id = savedApplication.Id;
            _sourceApplication.ApplicationNo = savedApplication.ApplicationNo;
            _sourceApplication.MediumId = savedApplication.MediumId;
            _sourceApplication.SourceApplicationId = savedApplication.SourceApplicationId;
            _sourceApplication.ApplicationType = savedApplication.ApplicationType;
            _sourceApplication.ApplicationStatus = savedApplication.ApplicationStatus;
            _sourceApplication.ApplicantName = savedApplication.ApplicantName;
            _sourceApplication.ApplicantDept = savedApplication.ApplicantDept;
            _sourceApplication.ApplyTime = savedApplication.ApplyTime;
            _sourceApplication.Reason = savedApplication.Reason;
            _sourceApplication.CurrentLocation = savedApplication.CurrentLocation;
            _sourceApplication.TargetLocation = savedApplication.TargetLocation;
            _sourceApplication.ExpectedReturnDate = savedApplication.ExpectedReturnDate;
            _sourceApplication.RelatedBatch = savedApplication.RelatedBatch;
        }

        private void SyncExpectedReturnDate(bool preferExistingValue)
        {
            if (HardDiskMediaOutboundReturnSupport.IsNonReturnableOutboundType(ApplicationType))
            {
                ExpectedReturnDate = null;
                return;
            }

            if (preferExistingValue && _expectedReturnDate.HasValue)
            {
                ExpectedReturnDate = HardDiskMediaOutboundReturnSupport.ClampExpectedReturnDate(ApplyTime, _expectedReturnDate);
                return;
            }

            ExpectedReturnDate = HardDiskMediaOutboundReturnSupport.CalculateDefaultExpectedReturnDate(ApplyTime, ApplicationType);
        }

        private void NotifyExpectedReturnDatePresentationChanged()
        {
            OnPropertyChanged(nameof(CanEditExpectedReturnDate));
            OnPropertyChanged(nameof(ShowExpectedReturnDateAsDash));
            OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
            OnPropertyChanged(nameof(ExpectedReturnDateHint));
        }

        private IReadOnlyList<HardDiskMediaOutboundMediumOption> GetSelectedMedia()
        {
            return MediumOptions.Where(item => item.IsSelected).ToList();
        }

        private void OnMediumOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HardDiskMediaOutboundMediumOption.IsSelected) || _isUpdatingMediumSelection)
            {
                return;
            }

            if (sender is not HardDiskMediaOutboundMediumOption changedOption || !changedOption.IsSelected)
            {
                UpdateSelectedMediumPresentation();
                return;
            }

            if (_sourceApplication.Id > 0)
            {
                _isUpdatingMediumSelection = true;
                foreach (var option in MediumOptions.Where(item => item != changedOption && item.IsSelected))
                {
                    option.IsSelected = false;
                }

                _isUpdatingMediumSelection = false;
                UpdateSelectedMediumPresentation();
                return;
            }

            int selectedCount = MediumOptions.Count(item => item.IsSelected);
            if (selectedCount <= MaxSelectableMediumCount)
            {
                UpdateSelectedMediumPresentation();
                return;
            }

            _isUpdatingMediumSelection = true;
            changedOption.IsSelected = false;
            _isUpdatingMediumSelection = false;
            _dialogService.ShowMessage($"关联介质最多选择 {MaxSelectableMediumCount} 块硬盘。");
            UpdateSelectedMediumPresentation();
        }

        private void UpdateSelectedMediumPresentation()
        {
            var selectedMedia = GetSelectedMedia();
            CurrentLocation = selectedMedia.Count switch
            {
                0 => string.Empty,
                1 => selectedMedia[0].CurrentLocation,
                _ => string.Join("；", selectedMedia.Select(item => item.CurrentLocation).Distinct(StringComparer.Ordinal))
            };

            OnPropertyChanged(nameof(MediumSelectionSummary));
        }

        private ApplicationFormActionSupport.ActionState ResolveApplicationFormActions()
        {
            bool isDraft = _sourceApplication.ApplicationStatus == HardDiskMediaApplication.StatusDraft;
            return ApplicationFormActionSupport.Resolve(_sourceApplication.Id, isDraft);
        }

        private void RefreshApplicationActionCommandStates()
        {
            OnPropertyChanged(nameof(CanSaveApplicationDraft));
            OnPropertyChanged(nameof(CanSubmitApplication));
            OnPropertyChanged(nameof(CanPrintApplication));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
