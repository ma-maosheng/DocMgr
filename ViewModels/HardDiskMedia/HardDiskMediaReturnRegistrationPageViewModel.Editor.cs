using DocMgr.Models.HardDiskMedia;
using DocMgr.Services.HardDiskMedia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘归还登记工作台：右侧就地编辑会话。
    /// </summary>
    public partial class HardDiskMediaReturnRegistrationPageViewModel
    {
        private HardDiskMediaApplication? _editingApplication;
        private bool _isEditorSessionReady;
        private string _applicationNo = string.Empty;
        private string _sourceApplicationNo = string.Empty;
        private HardDiskMediaReturnMediumOption? _selectedMedium;
        private HardDiskMediaApplicantOption? _selectedApplicant;
        private string _applicationType = HardDiskMediaApplication.TypeReturnBlankRegistration;
        private string _applicantName = string.Empty;
        private string _applicantDept = string.Empty;
        private DateTime _applyTime;
        private string _reason = string.Empty;
        private string _currentLocation = string.Empty;
        private string _targetLocation = string.Empty;
        private HardDiskMediaReturnTargetLocationOption? _selectedReturnTargetLocationOption;
        private DateTime? _expectedReturnDate;
        private string _inspectionResult = string.Empty;
        private string _formatConfirmation = string.Empty;
        private string _remark = string.Empty;
        private SystemAttachment? _selectedAttachment;
        private SystemAttachment? _selectedAbnormalReportAttachment;
        private bool _hasAbnormalReportUploaded;
        private string _abnormalFlowHint = string.Empty;
        private bool _preferRecommendedTargetLocationForCurrentKind;
        private readonly List<HardDiskMediaReturnCandidate> _returnCandidates = new();

        public ObservableCollection<HardDiskMediaReturnMediumOption> MediumOptions { get; } = new();

        public ObservableCollection<HardDiskMediaApplicantOption> ApplicantOptions { get; } = new();

        public ObservableCollection<string> InspectionResultOptions { get; } = new();

        public ObservableCollection<string> FormatConfirmationOptions { get; } = new();

        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> ReturnTargetLocationOptions { get; } = new();
        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<SystemAttachment> AbnormalReportAttachments { get; } = new();

        public string SaveButtonText => "保存登记";

        public string SubmitButtonText => "登记归还信息";

        public string ReturnLocationLabel => InspectionResult switch
        {
            HardDiskMediaReturnDomainValues.RegistrationKindLossRegistration => "归还位置（挂失）",
            var value when HardDiskMediaReturnDomainValues.IsDamagedReturnInspection(value) => "归还位置（损坏硬盘档口）",
            _ => "归还位置（空白硬盘档口）"
        };

        public string TargetLocationLabel => InspectionResult switch
        {
            var value when HardDiskMediaReturnDomainValues.IsDamagedReturnInspection(value) => "损坏入柜位置",
            HardDiskMediaReturnDomainValues.RegistrationKindLossRegistration => "挂失结果",
            _ => "归位位置（空白硬盘档口）"
        };

        public string ReasonFieldLabel => IsSpecialSituationInspectionResult ? "特殊情况说明 *" : "特殊情况说明";

        public bool IsMediumSelectionEnabled => IsRegistrationEditable && SelectedApplicant != null;
        public bool IsRegistrationEditable =>
            _editingApplication != null &&
            (_editingApplication.Id == 0 ||
             _editingApplication.ApplicationStatus == HardDiskMediaApplication.StatusDraft ||
             _editingApplication.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted ||
             _editingApplication.ApplicationStatus == HardDiskMediaApplication.StatusPendingUpload);
        public bool CanUploadSignedAttachment => false;

        public bool HasAbnormalReturnItems =>
            IsEditing && (
                IsSpecialSituationInspectionResult ||
                HardDiskMediaReturnDomainValues.IsAbnormalReturn(_editingApplication));

        public bool ShowAbnormalReturnPanel => IsEditing && HasAbnormalReturnItems;

        public bool ShowNormalReturnReasonField => IsEditing && !ShowAbnormalReturnPanel;

        public bool HasAbnormalReportUploaded => _hasAbnormalReportUploaded;

        public string AbnormalFlowHint => _abnormalFlowHint;

        public bool CanPrintAbnormalReport =>
            ShowAbnormalReturnPanel && IsRegistrationEditable;

        public bool CanManageAbnormalReportAttachments =>
            ShowAbnormalReturnPanel && IsRegistrationEditable;

        public bool CanPrintHandoverSheet =>
            _editingApplication is { Id: > 0, ApplicationStatus: HardDiskMediaApplication.StatusSubmitted };

        public bool CanComplete =>
            _editingApplication is { Id: > 0, ApplicationStatus: HardDiskMediaApplication.StatusSubmitted, PrintCount: > 0 }
            && (!HasAbnormalReturnItems || HasAbnormalReportUploaded);

        public bool CanDeleteAttachment => false;

        public bool CanDeleteAbnormalReportAttachment => CanManageAbnormalReportAttachments;

        public string RegisterHintText
        {
            get
            {
                if (!IsRegistrationEditable)
                {
                    return "当前状态不允许重新登记归还信息。";
                }

                if (ShowAbnormalReturnPanel)
                {
                    return HasAbnormalReportUploaded
                        ? "非正常归还情况表扫描件已上传，可登记归还信息。"
                        : "请先填写具体情况、打印情况表并完成线下签字后上传扫描件，再办理登记。";
                }

                return "下一步：登记归还信息后，请打印交接单。";
            }
        }

        public string PrintHintText => CanPrintHandoverSheet
            ? (ShowAbnormalReturnPanel
                ? "下一步：打印交接单后，请确认办结。"
                : "下一步：打印交接单后，请确认办结。")
            : "请先完成“登记归还信息”，再打印交接单。";

        public string UploadHintText => string.Empty;

        public string CompleteHintText => CanComplete
            ? "下一步：确认办结，完成介质收回入库。"
            : (_editingApplication is { PrintCount: <= 0 }
                ? "请先打印交接单，再确认办结。"
                : "请先完成登记与打印交接单，再确认办结。");

        public bool UseTargetLocationOptionList =>
            !IsLossInspectionScenario && ReturnTargetLocationOptions.Count > 0;

        public bool IsTargetLocationRequired => !IsLossInspectionScenario;

        public bool IsFormatConfirmationEditable =>
            IsRegistrationEditable && GetAllowedFormatConfirmationOptions(InspectionResult).Count > 1;

        public bool CanRecommendTargetLocation => SelectedMedium != null && !IsLossInspectionScenario;

        public bool CanShowTargetLocationSnapshot =>
            !IsLossInspectionScenario &&
            TryParseCabinetLocation(TargetLocation, out _, out _, out _);

        public Visibility TargetLocationSnapshotButtonVisibility =>
            IsLossInspectionScenario ? Visibility.Collapsed : Visibility.Visible;

        private bool IsDamagedInspectionScenario => HardDiskMediaReturnDomainValues.IsDamagedReturnInspection(InspectionResult);
        private bool IsLossInspectionScenario => HardDiskMediaReturnDomainValues.IsLossRegistrationInspection(InspectionResult);
        private bool IsSpecialSituationInspectionResult =>
            IsDamagedInspectionScenario || IsLossInspectionScenario;

        public Visibility TargetLocationSectionVisibility => IsTargetLocationRequired ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TargetLocationSelectionVisibility => IsTargetLocationRequired && UseTargetLocationOptionList ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TargetLocationTextVisibility => IsTargetLocationRequired && !UseTargetLocationOptionList ? Visibility.Visible : Visibility.Collapsed;

        public string TargetLocationHintText => InspectionResult switch
        {
            var value when HardDiskMediaReturnDomainValues.IsDamagedReturnInspection(value) =>
                "损坏归还按损坏硬盘专用档口推荐，可使用“推荐档口”和“档口快照”辅助选择。",
            HardDiskMediaReturnDomainValues.RegistrationKindLossRegistration => "挂失登记无需归位档口。",
            _ => "正常归还按空白硬盘专用档口推荐归位。"
        };

        public string ApplicationNo
        {
            get => _applicationNo;
            set => SetProperty(ref _applicationNo, value);
        }

        public string SourceApplicationNo
        {
            get => _sourceApplicationNo;
            set => SetProperty(ref _sourceApplicationNo, value);
        }

        public HardDiskMediaReturnMediumOption? SelectedMedium
        {
            get => _selectedMedium;
            set
            {
                if (SetProperty(ref _selectedMedium, value))
                {
                    ApplySelectedMedium(value);
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
                    ApplicantName = value?.ApplicantName ?? string.Empty;
                    ApplicantDept = value?.ApplicantDept ?? string.Empty;
                    OnPropertyChanged(nameof(IsMediumSelectionEnabled));
                    if (_isEditorSessionReady)
                    {
                        ReloadMediumOptionsForReturnRegistration();
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
                    OnPropertyChanged(nameof(TargetLocationLabel));
                    OnPropertyChanged(nameof(ReasonFieldLabel));
                    NotifyTargetLocationSelectionChanged();
                }
            }
        }

        public string ApplicantName
        {
            get => _applicantName;
            set => SetProperty(ref _applicantName, value);
        }

        public string ApplicantDept
        {
            get => _applicantDept;
            set => SetProperty(ref _applicantDept, value);
        }

        public DateTime ApplyTime
        {
            get => _applyTime;
            set => SetProperty(ref _applyTime, value);
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
            set
            {
                if (SetProperty(ref _targetLocation, value))
                {
                    NotifyTargetLocationSelectionChanged();
                }
            }
        }

        public HardDiskMediaReturnTargetLocationOption? SelectedReturnTargetLocationOption
        {
            get => _selectedReturnTargetLocationOption;
            set
            {
                if (SetProperty(ref _selectedReturnTargetLocationOption, value) && value != null)
                {
                    TargetLocation = value.Location;
                }
            }
        }

        public DateTime? ExpectedReturnDate
        {
            get => _expectedReturnDate;
            set => SetProperty(ref _expectedReturnDate, value);
        }

        public string Remark
        {
            get => _remark;
            set => SetProperty(ref _remark, value);
        }

        public string InspectionResult
        {
            get => _inspectionResult;
            set
            {
                if (SetProperty(ref _inspectionResult, value))
                {
                    SyncApplicationTypeFromInspectionResult();
                    OnPropertyChanged(nameof(ReturnLocationLabel));
                    OnPropertyChanged(nameof(TargetLocationLabel));
                    OnPropertyChanged(nameof(ReasonFieldLabel));
                    OnPropertyChanged(nameof(HasAbnormalReturnItems));
                    OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
                    OnPropertyChanged(nameof(ShowNormalReturnReasonField));
                    OnPropertyChanged(nameof(CanPrintAbnormalReport));
                    OnPropertyChanged(nameof(CanManageAbnormalReportAttachments));
                    RebuildFormatConfirmationOptions(forceSelection: true);
                    _preferRecommendedTargetLocationForCurrentKind = true;
                    RefreshReturnTargetLocationAsync();
                    NotifyTargetLocationSelectionChanged();
                    RefreshAbnormalFlowHint();
                    OnPropertyChanged(nameof(RegisterHintText));
                    OnPropertyChanged(nameof(TargetLocationHintText));
                }
            }
        }

        public string FormatConfirmation
        {
            get => _formatConfirmation;
            set => SetProperty(ref _formatConfirmation, value);
        }

        public SystemAttachment? SelectedAttachment
        {
            get => _selectedAttachment;
            set => SetProperty(ref _selectedAttachment, value);
        }

        public SystemAttachment? SelectedAbnormalReportAttachment
        {
            get => _selectedAbnormalReportAttachment;
            set => SetProperty(ref _selectedAbnormalReportAttachment, value);
        }

        public ICommand SaveDraftCommand { get; private set; } = null!;

        public ICommand RecommendTargetLocationCommand { get; private set; } = null!;

        public ICommand ShowTargetLocationSnapshotCommand { get; private set; } = null!;

        public ICommand SubmitCommand { get; private set; } = null!;

        public ICommand PrintAbnormalReportCommand { get; private set; } = null!;

        public ICommand UploadAbnormalReportCommand { get; private set; } = null!;

        public ICommand ViewAbnormalReportCommand { get; private set; } = null!;

        public ICommand DeleteAbnormalReportCommand { get; private set; } = null!;

        public ICommand PrintHandoverSheetCommand { get; private set; } = null!;

        public ICommand CompleteCommand { get; private set; } = null!;

        public ICommand ViewAttachmentCommand { get; private set; } = null!;

        public ICommand DeleteAttachmentCommand { get; private set; } = null!;

        public ICommand CancelEditCommand { get; private set; } = null!;


        public bool IsEditing => _editingApplication != null;

        public string EditHeader
        {
            get
            {
                if (_editingApplication == null)
                {
                    return string.Empty;
                }

                string diskPart = SelectedMedium == null
                    ? string.Empty
                    : $" · {SelectedMedium.DisplayText}";
                string stage = ResolveReturnStageText(_editingApplication);
                return _editingApplication.Id == 0
                    ? $"新建归还登记{diskPart}"
                    : $"登记单 {_editingApplication.ApplicationNo}{diskPart} · {stage}";
            }
        }

        public string WorkflowHintText
        {
            get
            {
                if (_editingApplication == null)
                {
                    return string.Empty;
                }

                if (IsRegistrationEditable)
                {
                    return RegisterHintText;
                }

                if (CanPrintHandoverSheet)
                {
                    return PrintHintText;
                }

                if (CanComplete)
                {
                    return CompleteHintText;
                }

                return _editingApplication.ApplicationStatus == HardDiskMediaApplication.StatusCompleted
                    ? "本单已办结入库。"
                    : "请按流程提示继续办理。";
            }
        }

        /// <summary>
        /// 将指定归还单载入右侧编辑区。
        /// </summary>
        public async Task LoadEditorSessionAsync(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);
            _editingApplication = application;
            _isEditorSessionReady = false;
            await LoadEditorOptionsAsync();
            await LoadEditorApplicationAsync();
            await LoadEditorAttachmentsAsync();
            _isEditorSessionReady = true;
            NotifyEditorStateChanged();
        }

        /// <summary>
        /// 清空右侧编辑区。
        /// </summary>
        public void CancelEdit()
        {
            _editingApplication = null;
            _isEditorSessionReady = false;
            ApplicationNo = string.Empty;
            SourceApplicationNo = string.Empty;
            SelectedApplicant = null;
            SelectedMedium = null;
            MediumOptions.Clear();
            ApplicantOptions.Clear();
            ReturnTargetLocationOptions.Clear();
            Attachments.Clear();
            AbnormalReportAttachments.Clear();
            SelectedAttachment = null;
            SelectedAbnormalReportAttachment = null;
            _hasAbnormalReportUploaded = false;
            _abnormalFlowHint = string.Empty;
            Reason = string.Empty;
            CurrentLocation = string.Empty;
            TargetLocation = string.Empty;
            InspectionResult = string.Empty;
            FormatConfirmation = string.Empty;
            Remark = string.Empty;
            NotifyEditorStateChanged();
        }

        private async Task LoadEditorOptionsAsync()
        {
            _returnCandidates.Clear();
            _returnCandidates.AddRange(await _hardDiskMediaService.GetReturnRegistrationCandidatesAsync());

            ApplicantOptions.Clear();
            foreach (var option in _returnCandidates
                         .GroupBy(item => new { item.ApplicantName, item.ApplicantDept })
                         .Select(group => new HardDiskMediaApplicantOption
                         {
                             ApplicantName = group.Key.ApplicantName,
                             ApplicantDept = group.Key.ApplicantDept
                         })
                         .OrderBy(item => item.ApplicantName)
                         .ThenBy(item => item.ApplicantDept))
            {
                ApplicantOptions.Add(option);
            }

            InspectionResultOptions.Clear();
            foreach (string option in HardDiskMediaReturnDomainValues.RegistrationKindFilterOptions)
            {
                InspectionResultOptions.Add(option);
            }

            FormatConfirmationOptions.Clear();
            FormatConfirmationOptions.Add("已格式化");
            FormatConfirmationOptions.Add("未格式化");
            FormatConfirmationOptions.Add("不适用");
        }

        private async Task LoadEditorApplicationAsync()
        {
            if (_editingApplication == null)
            {
                return;
            }

            await ReloadEditingApplicationFromDatabaseAsync();

            ApplicationNo = _editingApplication.Id == 0
                ? await _hardDiskMediaService.GenerateNextReturnRegistrationNoAsync()
                : _editingApplication.ApplicationNo;

            SelectedApplicant = ResolveInitialApplicant();
            if (SelectedApplicant != null)
            {
                ReloadMediumOptionsForReturnRegistration();
            }
            else
            {
                MediumOptions.Clear();
                SourceApplicationNo = string.Empty;
            }

            ApplyTime = _editingApplication.ApplyTime == default ? DateTime.Today : _editingApplication.ApplyTime;
            ApplicantName = _editingApplication.ApplicantName;
            ApplicantDept = _editingApplication.ApplicantDept;
            CurrentLocation = _editingApplication.CurrentLocation;
            Reason = _editingApplication.Reason;
            ExpectedReturnDate = _editingApplication.ExpectedReturnDate;
            InspectionResult = ResolveInitialInspectionResult();
            SyncApplicationTypeFromInspectionResult();
            FormatConfirmation = ResolveInitialFormatConfirmation();
            RebuildFormatConfirmationOptions(forceSelection: false);
            Remark = _editingApplication.Remark;

            SourceApplicationNo = await _hardDiskMediaService.ResolveReturnSourceApplicationNoAsync(
                _editingApplication.SourceApplicationId,
                _editingApplication.SourceOutboundRecordId);

            if (_editingApplication.Id == 0)
            {
                _preferRecommendedTargetLocationForCurrentKind = true;
            }

            await UpdateReturnTargetLocationAsync();
        }

        private async Task<bool> SaveAsync(string targetStatus)
        {
            if (_editingApplication == null || !IsRegistrationEditable)
            {
                _dialogService.ShowMessage("当前登记单已办结或已作废，不允许编辑。");
                return false;
            }

            if (SelectedMedium == null)
            {
                _dialogService.ShowMessage("请选择关联介质。");
                return false;
            }

            if (IsSpecialSituationInspectionResult && string.IsNullOrWhiteSpace(Reason))
            {
                string message = InspectionResult switch
                {
                    var value when HardDiskMediaReturnDomainValues.IsDamagedReturnInspection(value) =>
                        "损坏归还时，请填写特殊情况说明。",
                    HardDiskMediaReturnDomainValues.RegistrationKindLossRegistration =>
                        "挂失登记时，请填写特殊情况说明。",
                    _ => "请填写特殊情况说明。"
                };
                _dialogService.ShowMessage(message);
                return false;
            }

            if (string.IsNullOrWhiteSpace(InspectionResult))
            {
                _dialogService.ShowMessage("请选择查验结果。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(FormatConfirmation))
            {
                _dialogService.ShowMessage("请选择格式化确认。");
                return false;
            }

            string effectiveApplicationType = ResolveApplicationTypeByInspectionResult();

            if (!GetAllowedInspectionResultOptions().Contains(InspectionResult, StringComparer.Ordinal))
            {
                _dialogService.ShowMessage("当前登记类型下，查验结果取值无效，请重新选择。");
                return false;
            }

            if (!GetAllowedFormatConfirmationOptions(InspectionResult).Contains(FormatConfirmation, StringComparer.Ordinal))
            {
                _dialogService.ShowMessage("当前登记场景下，格式化确认取值无效，请重新选择。");
                return false;
            }

            if (targetStatus == HardDiskMediaApplication.StatusSubmitted && ShowAbnormalReturnPanel)
            {
                if (_editingApplication.Id <= 0)
                {
                    _dialogService.ShowMessage("非正常归还需先保存草稿并上传情况表扫描件后再登记。");
                    return false;
                }

                if (!HasAbnormalReportUploaded)
                {
                    _dialogService.ShowMessage("非正常归还需上传情况表扫描件后再登记。");
                    return false;
                }
            }

            try
            {
                var application = new HardDiskMediaApplication
                {
                    Id = _editingApplication.Id,
                    ApplicationNo = ApplicationNo.Trim(),
                    MediumId = SelectedMedium.Id,
                    SourceApplicationId = SelectedMedium.SourceApplicationId,
                    SourceOutboundRecordId = SelectedMedium.SourceOutboundRecordId,
                    ApplicationType = effectiveApplicationType,
                    ApplicationStatus = targetStatus,
                    ApplicantName = ApplicantName.Trim(),
                    ApplicantDept = ApplicantDept.Trim(),
                    ApplyTime = ApplyTime,
                    Reason = Reason.Trim(),
                    TargetPersonOrUnit = ApplicantName.Trim(),
                    CurrentLocation = string.IsNullOrWhiteSpace(CurrentLocation) ? SelectedMedium.CurrentLocation : CurrentLocation.Trim(),
                    TargetLocation = ResolveTargetLocationForSave(effectiveApplicationType),
                    ExpectedReturnDate = ExpectedReturnDate,
                    InspectionResult = InspectionResult.Trim(),
                    FormatConfirmation = FormatConfirmation.Trim(),
                    RelatedBatch = string.Empty,
                    RelatedArchiveTitle = string.Empty,
                    Remark = Remark.Trim(),
                    PrintCount = _editingApplication.PrintCount,
                    PrintedTime = _editingApplication.PrintedTime,
                    SignedAttachmentUploaded = _editingApplication.SignedAttachmentUploaded,
                    SignedAttachmentUploadedTime = _editingApplication.SignedAttachmentUploadedTime,
                    SignedAttachmentUploader = _editingApplication.SignedAttachmentUploader,
                    ApprovedBy = _editingApplication.ApprovedBy,
                    ApprovedTime = _editingApplication.ApprovedTime,
                    ApprovalOpinion = _editingApplication.ApprovalOpinion,
                    ExecutedBy = _editingApplication.ExecutedBy,
                    ExecutedTime = _editingApplication.ExecutedTime
                };

                await _hardDiskMediaService.SaveApplicationAsync(application, _userContextService.CurrentUser);
                SynchronizeEditingApplication(application);
                string nextStepMessage = targetStatus == HardDiskMediaApplication.StatusSubmitted
                    ? (ShowAbnormalReturnPanel
                        ? "归还信息登记成功。下一步：请打印交接单。"
                        : "归还信息登记成功。下一步：请打印交接单。")
                    : "登记已保存。";
                _dialogService.ShowMessage(nextStepMessage);
                await RefreshListsKeepingEditorAsync();
                NotifyEditorStateChanged();
                return true;
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }

            return false;
        }

        private async Task<bool> EnsureDraftSavedForAbnormalFlowAsync()
        {
            if (_editingApplication is { Id: > 0 })
            {
                return true;
            }

            return await SaveAsync(HardDiskMediaApplication.StatusDraft);
        }

        private async Task PrintAbnormalReportAsync()
        {
            if (!CanPrintAbnormalReport)
            {
                _dialogService.ShowMessage("当前状态不允许打印非正常归还情况表。");
                return;
            }

            if (!await EnsureDraftSavedForAbnormalFlowAsync())
            {
                return;
            }

            try
            {
                var data = await _hardDiskMediaService.BuildAbnormalReturnReportPrintDataAsync(_editingApplication!, blankReturnerSignature: true);
                var document = HardDiskMediaAbnormalReturnReportPrintDocumentFactory.Create(data);
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
        }

        private async Task UploadAbnormalReportAsync()
        {
            if (!CanManageAbnormalReportAttachments)
            {
                _dialogService.ShowMessage("当前状态不允许上传非正常归还情况表扫描件。");
                return;
            }

            if (_editingApplication is not { Id: > 0 })
            {
                if (!await EnsureDraftSavedForAbnormalFlowAsync())
                {
                    return;
                }
            }

            var filePath = _dialogService.OpenFileDialog("所有文件|*.*", "选择非正常归还情况表扫描件");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileContent = await File.ReadAllBytesAsync(filePath);
                var result = await _hardDiskMediaService.UploadAbnormalReturnReportAsync(
                    _editingApplication,
                    _userContextService.CurrentUser,
                    fileInfo.Name,
                    fileInfo.Extension,
                    fileInfo.Length,
                    fileContent);
                if (!result.Success)
                {
                    _dialogService.ShowMessage(result.Message);
                    return;
                }

                await RefreshEditingApplicationAsync();
                await LoadEditorAttachmentsAsync();
                await RefreshListsKeepingEditorAsync();
                NotifyEditorStateChanged();
                _dialogService.ShowMessage("非正常归还情况表扫描件上传成功。下一步：请登记归还信息。");
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"读取扫描件失败：{ex.Message}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"上传扫描件失败：{ex.Message}");
            }
        }

        private async Task ViewAbnormalReportAsync(SystemAttachment? attachment)
        {
            await ViewAttachmentAsync(attachment ?? SelectedAbnormalReportAttachment);
        }

        private async Task DeleteAbnormalReportAsync(SystemAttachment? attachment)
        {
            attachment ??= SelectedAbnormalReportAttachment;
            if (attachment == null)
            {
                return;
            }

            if (!CanDeleteAbnormalReportAttachment)
            {
                _dialogService.ShowMessage("当前状态不允许删除扫描件。");
                return;
            }

            if (!_dialogService.ShowConfirm($"确定删除附件“{attachment.FileName}”吗？", "提示"))
            {
                return;
            }

            var result = await _hardDiskMediaService.DeleteAbnormalReturnReportAsync(attachment);
            _dialogService.ShowMessage(result.Message);
            if (!result.Success)
            {
                return;
            }

            await RefreshEditingApplicationAsync();
            await LoadEditorAttachmentsAsync();
            await RefreshListsKeepingEditorAsync();
            NotifyEditorStateChanged();
        }

        private async Task PrintHandoverSheetAsync()
        {
            if (!CanPrintHandoverSheet)
            {
                _dialogService.ShowMessage("请先完成“登记归还信息”，再打印交接单。");
                return;
            }

            try
            {
                var data = await _hardDiskMediaService.BuildPrintDataAsync(_editingApplication!);
                var document = HardDiskMediaPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };

                await _hardDiskMediaService.MarkApplicationPrintedAsync(_editingApplication!);
                previewWindow.ShowDialog();
                                await RefreshEditingApplicationAsync();
                await RefreshListsKeepingEditorAsync();
                NotifyEditorStateChanged();
                _dialogService.ShowMessage("交接单打印完成。下一步：请确认办结。");
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task CompleteAsync()
        {
            if (!CanComplete)
            {
                _dialogService.ShowMessage(ShowAbnormalReturnPanel && !HasAbnormalReportUploaded
                    ? "非正常归还需上传情况表扫描件并打印交接单后再确认办结。"
                    : "请先打印交接单，再确认办结。");
                return;
            }

            try
            {
                var completeResult = await _hardDiskMediaService.CompleteApplicationAsync(_editingApplication!, _userContextService.CurrentUser);
                if (!completeResult.Success)
                {
                    _dialogService.ShowMessage(completeResult.Message);
                    return;
                }

                                await RefreshEditingApplicationAsync();
                await LoadEditorAttachmentsAsync();
                await RefreshListsKeepingEditorAsync();
                NotifyEditorStateChanged();
                _dialogService.ShowMessage("已办结入库。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"确认办结失败：{ex.Message}");
            }
        }

        private async Task ViewAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            var result = await _hardDiskMediaService.PrepareApplicationAttachmentViewAsync(attachment);
            if (!result.Success || result.Attachment?.FileContent == null)
            {
                _dialogService.ShowMessage(result.Message);
                return;
            }

            var fullAttachment = result.Attachment;
            if (_dialogService.ShowConfirm("直接打开附件？\n【确定】打开 【取消】另存为", "附件操作"))
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fullAttachment.FileName}");
                await File.WriteAllBytesAsync(tempPath, fullAttachment.FileContent);
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                return;
            }

            string? savePath = _dialogService.SaveFileDialog("所有文件|*.*", "另存附件", fullAttachment.FileName);
            if (!string.IsNullOrWhiteSpace(savePath))
            {
                await File.WriteAllBytesAsync(savePath, fullAttachment.FileContent);
            }
        }

        private async Task DeleteAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定删除附件“{attachment.FileName}”吗？", "提示"))
            {
                return;
            }

            var result = await _hardDiskMediaService.DeleteApplicationAttachmentAsync(attachment);
            _dialogService.ShowMessage(result.Message);
            if (!result.Success)
            {
                return;
            }

                        await RefreshEditingApplicationAsync();
            await LoadEditorAttachmentsAsync();
            await RefreshListsKeepingEditorAsync();
            NotifyEditorStateChanged();
        }

        private async Task RefreshEditingApplicationAsync()
        {
            await ReloadEditingApplicationFromDatabaseAsync();

            OnPropertyChanged(nameof(IsRegistrationEditable));
            OnPropertyChanged(nameof(IsMediumSelectionEnabled));
            OnPropertyChanged(nameof(IsFormatConfirmationEditable));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(HasAbnormalReturnItems));
            OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
            OnPropertyChanged(nameof(ShowNormalReturnReasonField));
            OnPropertyChanged(nameof(HasAbnormalReportUploaded));
            OnPropertyChanged(nameof(AbnormalFlowHint));
            OnPropertyChanged(nameof(CanPrintAbnormalReport));
            OnPropertyChanged(nameof(CanManageAbnormalReportAttachments));
            OnPropertyChanged(nameof(CanDeleteAbnormalReportAttachment));
            OnPropertyChanged(nameof(CanPrintHandoverSheet));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanDeleteAttachment));
            OnPropertyChanged(nameof(RegisterHintText));
            OnPropertyChanged(nameof(PrintHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task LoadEditorAttachmentsAsync()
        {
            int? selectedAbnormalAttachmentId = SelectedAbnormalReportAttachment?.Id;
            Attachments.Clear();
            AbnormalReportAttachments.Clear();
            _hasAbnormalReportUploaded = false;

            if (_editingApplication == null || string.IsNullOrWhiteSpace(_editingApplication.ApplicationNo))
            {
                SelectedAttachment = null;
                SelectedAbnormalReportAttachment = null;
                RefreshAbnormalFlowHint();
                return;
            }

            var attachments = await _hardDiskMediaService.GetApplicationAttachmentsAsync(_editingApplication.ApplicationNo);
            foreach (var attachment in attachments)
            {
                if (string.Equals(
                        attachment.FileCategory,
                        HardDiskMediaReturnDomainValues.AttachmentKindSignedAbnormalReturnReport,
                        StringComparison.Ordinal))
                {
                    AbnormalReportAttachments.Add(attachment);
                }
                else
                {
                    Attachments.Add(attachment);
                }
            }

            _hasAbnormalReportUploaded = AbnormalReportAttachments.Count > 0
                || await _hardDiskMediaService.HasUploadedAbnormalReturnReportAsync(
                    _editingApplication.Id,
                    _editingApplication.ApplicationNo);

            SelectedAbnormalReportAttachment = selectedAbnormalAttachmentId.HasValue
                ? AbnormalReportAttachments.FirstOrDefault(item => item.Id == selectedAbnormalAttachmentId.Value)
                : AbnormalReportAttachments.FirstOrDefault();
            SelectedAttachment = Attachments.FirstOrDefault();
            RefreshAbnormalFlowHint();
        }

        private void RefreshAbnormalFlowHint()
        {
            if (!ShowAbnormalReturnPanel)
            {
                _abnormalFlowHint = string.Empty;
            }
            else
            {
                _abnormalFlowHint = HasAbnormalReportUploaded
                    ? (IsRegistrationEditable
                        ? "非正常归还情况表扫描件已上传，可登记后打印交接单并办结入库。"
                        : "非正常归还情况表扫描件已上传，可打印交接单并办结入库。")
                    : (IsRegistrationEditable
                        ? "本单为非正常归还：请填写具体情况，打印情况表并完成线下签字后上传扫描件，再办理登记。"
                        : "本单为非正常归还，登记信息已锁定。");
            }

            OnPropertyChanged(nameof(AbnormalFlowHint));
            OnPropertyChanged(nameof(HasAbnormalReportUploaded));
        }

        private string ResolveInitialInspectionResult()
        {
            return HardDiskMediaReturnDomainValues.ResolveInspectionResultDisplay(
                _editingApplication?.ApplicationType,
                _editingApplication?.InspectionResult);
        }

        private void SyncApplicationTypeFromInspectionResult()
        {
            string resolvedType = ResolveApplicationTypeByInspectionResult();
            if (string.Equals(_applicationType, resolvedType, StringComparison.Ordinal))
            {
                return;
            }

            _applicationType = resolvedType;
            OnPropertyChanged(nameof(ApplicationType));
            OnPropertyChanged(nameof(TargetLocationLabel));
        }

        private void SynchronizeEditingApplication(HardDiskMediaApplication savedApplication)
        {
            _editingApplication ??= new HardDiskMediaApplication();
            _editingApplication.Id = savedApplication.Id;
            _editingApplication.ApplicationNo = savedApplication.ApplicationNo;
            _editingApplication.MediumId = savedApplication.MediumId;
            _editingApplication.SourceApplicationId = savedApplication.SourceApplicationId;
            _editingApplication.SourceOutboundRecordId = savedApplication.SourceOutboundRecordId;
            _editingApplication.ApplicationType = savedApplication.ApplicationType;
            _editingApplication.ApplicationStatus = savedApplication.ApplicationStatus;
            _editingApplication.ApplicantName = savedApplication.ApplicantName;
            _editingApplication.ApplicantDept = savedApplication.ApplicantDept;
            _editingApplication.ApplyTime = savedApplication.ApplyTime;
            _editingApplication.Reason = savedApplication.Reason;
            _editingApplication.CurrentLocation = savedApplication.CurrentLocation;
            _editingApplication.TargetLocation = savedApplication.TargetLocation;
            _editingApplication.ExpectedReturnDate = savedApplication.ExpectedReturnDate;
            _editingApplication.InspectionResult = savedApplication.InspectionResult;
            _editingApplication.FormatConfirmation = savedApplication.FormatConfirmation;
            _editingApplication.Remark = savedApplication.Remark;
            _editingApplication.PrintCount = savedApplication.PrintCount;
            _editingApplication.PrintedTime = savedApplication.PrintedTime;
            _editingApplication.SignedAttachmentUploaded = savedApplication.SignedAttachmentUploaded;
            _editingApplication.SignedAttachmentUploadedTime = savedApplication.SignedAttachmentUploadedTime;
            _editingApplication.SignedAttachmentUploader = savedApplication.SignedAttachmentUploader;
            _editingApplication.ApprovedBy = savedApplication.ApprovedBy;
            _editingApplication.ApprovedTime = savedApplication.ApprovedTime;
            _editingApplication.ApprovalOpinion = savedApplication.ApprovalOpinion;
            _editingApplication.ExecutedBy = savedApplication.ExecutedBy;
            _editingApplication.ExecutedTime = savedApplication.ExecutedTime;
            _editingApplication.TargetPersonOrUnit = savedApplication.TargetPersonOrUnit;
            _editingApplication.RelatedBatch = savedApplication.RelatedBatch;
            _editingApplication.RelatedArchiveTitle = savedApplication.RelatedArchiveTitle;

            OnPropertyChanged(nameof(IsRegistrationEditable));
            OnPropertyChanged(nameof(IsMediumSelectionEnabled));
            OnPropertyChanged(nameof(IsFormatConfirmationEditable));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(HasAbnormalReturnItems));
            OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
            OnPropertyChanged(nameof(ShowNormalReturnReasonField));
            OnPropertyChanged(nameof(HasAbnormalReportUploaded));
            OnPropertyChanged(nameof(AbnormalFlowHint));
            OnPropertyChanged(nameof(CanPrintAbnormalReport));
            OnPropertyChanged(nameof(CanManageAbnormalReportAttachments));
            OnPropertyChanged(nameof(CanDeleteAbnormalReportAttachment));
            OnPropertyChanged(nameof(CanPrintHandoverSheet));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanDeleteAttachment));
            OnPropertyChanged(nameof(RegisterHintText));
            OnPropertyChanged(nameof(PrintHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            CommandManager.InvalidateRequerySuggested();
        }

        private string ResolveInitialFormatConfirmation()
        {
            if (_editingApplication != null && !string.IsNullOrWhiteSpace(_editingApplication.FormatConfirmation))
            {
                return _editingApplication.FormatConfirmation.Trim();
            }

            string inspection = HardDiskMediaReturnDomainValues.ResolveInspectionResultDisplay(
                _editingApplication?.ApplicationType,
                _editingApplication?.InspectionResult);

            return RequiresFormatConfirmationNotApplicable(inspection) ? "不适用" : "已格式化";
        }

        private void RebuildFormatConfirmationOptions(bool forceSelection)
        {
            var allowedFormatOptions = GetAllowedFormatConfirmationOptions(InspectionResult);
            FormatConfirmationOptions.Clear();
            foreach (string option in allowedFormatOptions)
            {
                FormatConfirmationOptions.Add(option);
            }

            string suggestedFormatConfirmation = ResolveSuggestedFormatConfirmation(InspectionResult, FormatConfirmation);
            if (forceSelection ||
                !allowedFormatOptions.Contains(FormatConfirmation, StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(FormatConfirmation))
            {
                _formatConfirmation = suggestedFormatConfirmation;
                OnPropertyChanged(nameof(FormatConfirmation));
            }

            OnPropertyChanged(nameof(IsFormatConfirmationEditable));
        }

        private static List<string> GetAllowedInspectionResultOptions()
        {
            return HardDiskMediaReturnDomainValues.RegistrationKindFilterOptions.ToList();
        }

        private static string ResolveSuggestedFormatConfirmation(string inspectionResult, string currentFormatConfirmation)
        {
            if (RequiresFormatConfirmationNotApplicable(inspectionResult))
            {
                return "不适用";
            }

            if (!string.IsNullOrWhiteSpace(currentFormatConfirmation) &&
                !string.Equals(currentFormatConfirmation, "不适用", StringComparison.Ordinal))
            {
                return currentFormatConfirmation.Trim();
            }

            return "已格式化";
        }

        private static List<string> GetAllowedFormatConfirmationOptions(string inspectionResult)
        {
            if (RequiresFormatConfirmationNotApplicable(inspectionResult))
            {
                return ["不适用"];
            }

            return ["已格式化", "未格式化"];
        }

        private static bool RequiresFormatConfirmationNotApplicable(string inspectionResult)
        {
            return HardDiskMediaReturnDomainValues.IsLossRegistrationInspection(inspectionResult) ||
                   HardDiskMediaReturnDomainValues.IsDamagedReturnInspection(inspectionResult);
        }

        private void RebuildInspectionAndFormatOptions(bool forceFormatSelection)
        {
            InspectionResultOptions.Clear();
            foreach (string option in GetAllowedInspectionResultOptions())
            {
                InspectionResultOptions.Add(option);
            }

            RebuildFormatConfirmationOptions(forceFormatSelection);
        }

        private static string ResolveSuggestedInspectionResult(string currentInspectionResult)
        {
            if (!string.IsNullOrWhiteSpace(currentInspectionResult) &&
                GetAllowedInspectionResultOptions().Contains(currentInspectionResult.Trim(), StringComparer.Ordinal))
            {
                return currentInspectionResult.Trim();
            }

            return HardDiskMediaReturnDomainValues.RegistrationKindNormalReturn;
        }

        private HardDiskMediaApplicantOption? ResolveInitialApplicant()
        {
            if (_editingApplication == null)
            {
                return null;
            }

            if (_editingApplication.Id == 0
                && !_editingApplication.SourceApplicationId.HasValue
                && !_editingApplication.SourceOutboundRecordId.HasValue)
            {
                return null;
            }

            string applicantName = !string.IsNullOrWhiteSpace(_editingApplication.ApplicantName)
                ? _editingApplication.ApplicantName.Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(applicantName))
            {
                if (_editingApplication.SourceApplicationId.HasValue)
                {
                    var sourceCandidate = _returnCandidates.FirstOrDefault(item =>
                        item.SourceApplicationId == _editingApplication.SourceApplicationId.Value);
                    applicantName = sourceCandidate?.ApplicantName ?? string.Empty;
                }
                else if (_editingApplication.SourceOutboundRecordId.HasValue)
                {
                    var sourceCandidate = _returnCandidates.FirstOrDefault(item =>
                        item.SourceOutboundRecordId == _editingApplication.SourceOutboundRecordId.Value);
                    applicantName = sourceCandidate?.ApplicantName ?? string.Empty;
                }
            }

            var resolved = ApplicantOptions.FirstOrDefault(item => string.Equals(item.ApplicantName, applicantName, StringComparison.OrdinalIgnoreCase));
            if (resolved != null)
            {
                return resolved;
            }

            if (!string.IsNullOrWhiteSpace(applicantName))
            {
                var fallback = new HardDiskMediaApplicantOption
                {
                    ApplicantName = applicantName,
                    ApplicantDept = _editingApplication.ApplicantDept?.Trim() ?? string.Empty
                };
                ApplicantOptions.Insert(0, fallback);
                return fallback;
            }

            return ApplicantOptions.FirstOrDefault();
        }

        private void ReloadMediumOptionsForReturnRegistration()
        {
            if (_editingApplication == null)
            {
                MediumOptions.Clear();
                SelectedMedium = null;
                return;
            }

            int? selectedSourceApplicationId = SelectedMedium?.SourceApplicationId ?? _editingApplication.SourceApplicationId;
            int? selectedSourceOutboundRecordId = SelectedMedium?.SourceOutboundRecordId ?? _editingApplication.SourceOutboundRecordId;
            int selectedMediumId = SelectedMedium?.Id ?? _editingApplication.MediumId;

            MediumOptions.Clear();
            if (_editingApplication.Id == 0)
            {
                SourceApplicationNo = string.Empty;
            }

            if (SelectedApplicant == null)
            {
                SelectedMedium = null;
                if (_editingApplication.Id == 0)
                {
                    CurrentLocation = string.Empty;
                    TargetLocation = string.Empty;
                    ExpectedReturnDate = null;
                }
                return;
            }

            foreach (var item in _returnCandidates
                         .Where(candidate => string.Equals(candidate.ApplicantName, SelectedApplicant.ApplicantName, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(candidate => candidate.DiskCode))
            {
                MediumOptions.Add(new HardDiskMediaReturnMediumOption
                {
                    Id = item.MediumId,
                    SourceApplicationId = item.SourceApplicationId,
                    SourceOutboundRecordId = item.SourceOutboundRecordId,
                    SourceApplicationNo = item.SourceApplicationNo,
                    ApplicantName = item.ApplicantName,
                    ApplicantDept = item.ApplicantDept,
                    CurrentLocation = item.BorrowedLocation,
                    OriginalLocation = item.OriginalLocation,
                    ExpectedReturnDate = item.ExpectedReturnDate,
                    DisplayText = $"{item.DiskCode} / {item.Capacity} / {item.SerialNumber} / 原位:{item.OriginalLocation}"
                });
            }

            if (_editingApplication.Id > 0 &&
                selectedMediumId > 0 &&
                MediumOptions.All(item => item.Id != selectedMediumId))
            {
                MediumOptions.Insert(0, new HardDiskMediaReturnMediumOption
                {
                    Id = _editingApplication.MediumId,
                    SourceApplicationId = _editingApplication.SourceApplicationId,
                    SourceOutboundRecordId = _editingApplication.SourceOutboundRecordId,
                    SourceApplicationNo = SourceApplicationNo,
                    ApplicantName = _editingApplication.ApplicantName,
                    ApplicantDept = _editingApplication.ApplicantDept,
                    CurrentLocation = _editingApplication.CurrentLocation,
                    OriginalLocation = _editingApplication.TargetLocation,
                    ExpectedReturnDate = _editingApplication.ExpectedReturnDate,
                    DisplayText = $"{_editingApplication.ApplicationNo} / 已登记记录"
                });
            }

            SelectedMedium = selectedSourceOutboundRecordId is > 0
                ? MediumOptions.FirstOrDefault(item => item.SourceOutboundRecordId == selectedSourceOutboundRecordId.Value)
                : selectedSourceApplicationId is > 0
                    ? MediumOptions.FirstOrDefault(item => item.SourceApplicationId == selectedSourceApplicationId.Value)
                    : MediumOptions.FirstOrDefault(item => item.Id == selectedMediumId);

            SelectedMedium ??= MediumOptions.FirstOrDefault();
        }

        private void ApplySelectedMedium(HardDiskMediaReturnMediumOption? value)
        {
            bool preservePersistedValues = !_isEditorSessionReady && _editingApplication is { Id: > 0 };
            CurrentLocation = preservePersistedValues && !string.IsNullOrWhiteSpace(_editingApplication?.CurrentLocation)
                ? _editingApplication!.CurrentLocation
                : value?.CurrentLocation ?? string.Empty;
            SourceApplicationNo = !string.IsNullOrWhiteSpace(value?.SourceApplicationNo)
                ? value.SourceApplicationNo
                : SourceApplicationNo;

            ApplicantName = preservePersistedValues && !string.IsNullOrWhiteSpace(_editingApplication?.ApplicantName)
                ? _editingApplication!.ApplicantName
                : value?.ApplicantName ?? SelectedApplicant?.ApplicantName ?? string.Empty;
            ApplicantDept = preservePersistedValues && !string.IsNullOrWhiteSpace(_editingApplication?.ApplicantDept)
                ? _editingApplication!.ApplicantDept
                : value?.ApplicantDept ?? SelectedApplicant?.ApplicantDept ?? string.Empty;
            ExpectedReturnDate = preservePersistedValues && _editingApplication?.ExpectedReturnDate != null
                ? _editingApplication.ExpectedReturnDate
                : value?.ExpectedReturnDate;
            RefreshReturnTargetLocationAsync();
        }

        private async Task ReloadEditingApplicationFromDatabaseAsync()
        {
            if (_editingApplication == null)
            {
                return;
            }

            if (_editingApplication.Id <= 0 && string.IsNullOrWhiteSpace(_editingApplication.ApplicationNo))
            {
                return;
            }

            var applications = await _hardDiskMediaService.SearchApplicationsAsync(_editingApplication.ApplicationNo, null, null);
            var refreshed = applications.FirstOrDefault(item => item.Id == _editingApplication.Id)
                ?? applications.FirstOrDefault(item => string.Equals(item.ApplicationNo, _editingApplication.ApplicationNo, StringComparison.OrdinalIgnoreCase));
            if (refreshed == null)
            {
                return;
            }

            SynchronizeEditingApplication(refreshed);
        }

        private async Task UpdateReturnTargetLocationAsync()
        {
            bool preferRecommended = _preferRecommendedTargetLocationForCurrentKind;
            _preferRecommendedTargetLocationForCurrentKind = false;

            ReturnTargetLocationOptions.Clear();
            SelectedReturnTargetLocationOption = null;
            NotifyTargetLocationSelectionChanged();

            if (SelectedMedium == null)
            {
                TargetLocation = string.Empty;
                return;
            }

            if (IsLossInspectionScenario)
            {
                TargetLocation = HardDiskMediaReturnDomainValues.LossReturnTargetLocationDisplay;
                return;
            }

            if (IsDamagedInspectionScenario)
            {
                await LoadDedicatedReturnTargetLocationOptionsAsync(
                    HardDiskMediaApplication.TypeReturnDamagedRegistration,
                    autoSelectRecommended: true,
                    preferRecommendedForCurrentKind: preferRecommended);
                return;
            }

            await LoadDedicatedReturnTargetLocationOptionsAsync(
                HardDiskMediaApplication.TypeReturnBlankRegistration,
                autoSelectRecommended: true,
                preferRecommendedForCurrentKind: preferRecommended);
        }

        private async Task LoadDedicatedReturnTargetLocationOptionsAsync(
            string applicationType,
            bool autoSelectRecommended,
            bool preferRecommendedForCurrentKind)
        {
            if (SelectedMedium == null)
            {
                TargetLocation = string.Empty;
                return;
            }

            var options = await _hardDiskMediaService.GetReturnTargetLocationOptionsAsync(
                applicationType,
                SelectedMedium.Id,
                SelectedMedium.SourceApplicationId,
                SelectedMedium.SourceOutboundRecordId);

            foreach (var option in options)
            {
                ReturnTargetLocationOptions.Add(option);
            }

            string persistedLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(_editingApplication?.TargetLocation);
            string currentLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(TargetLocation);
            bool shouldHonorPersisted = _editingApplication is { Id: > 0 } && !preferRecommendedForCurrentKind;

            if (shouldHonorPersisted
                && !string.IsNullOrWhiteSpace(persistedLocation)
                && ReturnTargetLocationOptions.All(item => !string.Equals(item.Location, persistedLocation, StringComparison.OrdinalIgnoreCase)))
            {
                ReturnTargetLocationOptions.Insert(0, new HardDiskMediaReturnTargetLocationOption
                {
                    Location = persistedLocation,
                    ExistingMediumCount = 0
                });
            }

            string? recommended = applicationType == HardDiskMediaApplication.TypeReturnBlankRegistration
                ? await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync()
                : ReturnTargetLocationOptions.FirstOrDefault()?.Location;

            if (autoSelectRecommended)
            {
                if (preferRecommendedForCurrentKind || _editingApplication is not { Id: > 0 })
                {
                    SelectedReturnTargetLocationOption =
                        ReturnTargetLocationOptions.FirstOrDefault(item => string.Equals(item.Location, recommended, StringComparison.OrdinalIgnoreCase))
                        ?? ReturnTargetLocationOptions.FirstOrDefault();
                }
                else
                {
                    SelectedReturnTargetLocationOption =
                        ReturnTargetLocationOptions.FirstOrDefault(item => string.Equals(item.Location, persistedLocation, StringComparison.OrdinalIgnoreCase))
                        ?? ReturnTargetLocationOptions.FirstOrDefault(item => string.Equals(item.Location, recommended, StringComparison.OrdinalIgnoreCase))
                        ?? ReturnTargetLocationOptions.FirstOrDefault(item => string.Equals(item.Location, currentLocation, StringComparison.OrdinalIgnoreCase))
                        ?? ReturnTargetLocationOptions.FirstOrDefault();
                }
            }
            else
            {
                SelectedReturnTargetLocationOption =
                    ReturnTargetLocationOptions.FirstOrDefault(item => string.Equals(item.Location, persistedLocation, StringComparison.OrdinalIgnoreCase))
                    ?? ReturnTargetLocationOptions.FirstOrDefault(item => string.Equals(item.Location, currentLocation, StringComparison.OrdinalIgnoreCase))
                    ?? ReturnTargetLocationOptions.FirstOrDefault();
            }

            TargetLocation = SelectedReturnTargetLocationOption?.Location ?? string.Empty;
        }

        private async void RefreshReturnTargetLocationAsync()
        {
            try
            {
                await UpdateReturnTargetLocationAsync();
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

        private void NotifyTargetLocationSelectionChanged()
        {
            OnPropertyChanged(nameof(IsTargetLocationRequired));
            OnPropertyChanged(nameof(TargetLocationSectionVisibility));
            OnPropertyChanged(nameof(UseTargetLocationOptionList));
            OnPropertyChanged(nameof(TargetLocationSelectionVisibility));
            OnPropertyChanged(nameof(TargetLocationTextVisibility));
            OnPropertyChanged(nameof(CanRecommendTargetLocation));
            OnPropertyChanged(nameof(CanShowTargetLocationSnapshot));
            OnPropertyChanged(nameof(TargetLocationSnapshotButtonVisibility));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task RecommendTargetLocationAsync()
        {
            if (!CanRecommendTargetLocation)
            {
                return;
            }

            await UpdateReturnTargetLocationAsync();
            if (SelectedReturnTargetLocationOption == null)
            {
                string slotType = IsDamagedInspectionScenario
                    ? "损坏硬盘专用档口"
                    : "空白硬盘专用档口";
                _dialogService.ShowMessage($"当前未找到可用的{slotType}。", "提示");
                return;
            }

            _dialogService.ShowMessage($"已推荐{SelectedReturnTargetLocationOption.DisplayText}", "推荐档口");
        }

        private async Task ShowTargetLocationSnapshotAsync()
        {
            if (!CanShowTargetLocationSnapshot)
            {
                return;
            }

            if (!TryParseCabinetLocation(TargetLocation, out string cabinetName, out CabinetFace face, out string slotCode))
            {
                _dialogService.ShowMessage("当前归档位置无法解析为档口，请重新选择位置后再查看快照。", "提示");
                return;
            }

            var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                .FirstOrDefault(item => item.Type == CabinetType.MagneticDisk && string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                _dialogService.ShowMessage($"未找到柜号 [{cabinetName}] 对应的防磁磁盘柜。", "提示");
                return;
            }

            _dialogService.ShowCabinetOpenDialog(new CabinetOpenRequest
            {
                CabinetId = cabinet.Id,
                CabinetName = cabinet.Name,
                CabinetType = cabinet.Type,
                Face = face,
                LayerCount = cabinet.LayerCount,
                ColumnCount = cabinet.ColumnCount,
                TargetSlotCode = slotCode,
                WidthCm = cabinet.Width,
                HeightCm = cabinet.Height,
                DepthCm = cabinet.Depth
            });
        }

        private string ResolveApplicationTypeByInspectionResult()
        {
            return HardDiskMediaReturnDomainValues.ResolveApplicationTypeByInspectionResult(InspectionResult);
        }

        private string ResolveTargetLocationForSave(string effectiveApplicationType)
        {
            if (effectiveApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            {
                return string.Empty;
            }

            string location = SelectedReturnTargetLocationOption?.Location ?? TargetLocation;
            if (string.Equals(location, HardDiskMediaReturnDomainValues.LossReturnTargetLocationDisplay, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location);
        }

        private static bool TryParseCabinetLocation(string? location, out string cabinetName, out CabinetFace face, out string slotCode)
        {
            cabinetName = string.Empty;
            face = CabinetFace.A;
            slotCode = string.Empty;

            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            var match = Regex.Match(location.Trim(), "^(?<cabinet>.+?)(?<face>[AB])-(?<row>\\d+)-(?<col>\\d+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            cabinetName = match.Groups["cabinet"].Value;
            face = string.Equals(match.Groups["face"].Value, "B", StringComparison.OrdinalIgnoreCase)
                ? CabinetFace.B
                : CabinetFace.A;
            slotCode = $"{match.Groups["row"].Value}-{match.Groups["col"].Value}";
            return !string.IsNullOrWhiteSpace(cabinetName) && !string.IsNullOrWhiteSpace(slotCode);
        }

        private void NotifyEditorStateChanged()
        {
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(EditHeader));
            OnPropertyChanged(nameof(WorkflowHintText));
            OnPropertyChanged(nameof(IsRegistrationEditable));
            OnPropertyChanged(nameof(IsMediumSelectionEnabled));
            OnPropertyChanged(nameof(IsFormatConfirmationEditable));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(HasAbnormalReturnItems));
            OnPropertyChanged(nameof(ShowAbnormalReturnPanel));
            OnPropertyChanged(nameof(ShowNormalReturnReasonField));
            OnPropertyChanged(nameof(HasAbnormalReportUploaded));
            OnPropertyChanged(nameof(AbnormalFlowHint));
            OnPropertyChanged(nameof(CanPrintAbnormalReport));
            OnPropertyChanged(nameof(CanManageAbnormalReportAttachments));
            OnPropertyChanged(nameof(CanDeleteAbnormalReportAttachment));
            OnPropertyChanged(nameof(CanPrintHandoverSheet));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanDeleteAttachment));
            OnPropertyChanged(nameof(RegisterHintText));
            OnPropertyChanged(nameof(PrintHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(ReturnLocationLabel));
            OnPropertyChanged(nameof(ReasonFieldLabel));
            RefreshAbnormalFlowHint();
            CommandManager.InvalidateRequerySuggested();
        }

    }
}
