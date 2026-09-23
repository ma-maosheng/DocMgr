using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.Services.Shared;
using DocMgr.Services.SystemSettings;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质审批信息录入弹窗 ViewModel。
    /// </summary>
    public class HardDiskMediaApprovalEditDialogViewModel : ViewModelBase
    {
        private readonly HardDiskMediaApplication _application;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private readonly IUserService _userService;
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private readonly User? _currentUser;

        private string _reviewerName = string.Empty;
        private DateTime _reviewerDate;
        private string _archiveRoomHead = string.Empty;
        private DateTime _archiveRoomHeadDate;
        private string _archiveDeputyPresident = string.Empty;
        private DateTime _archiveDeputyPresidentDate;
        private bool _enableDeptHead = true;
        private bool _enableArchiveRoomHead = true;
        private bool _enableArchiveDeputyPresident;
        private string _handoverApplicant = string.Empty;
        private string _handoverAdmin = string.Empty;
        private DateTime _handoverDate;
        private string _approvalOpinion = "同意";
        private bool _hasCommittedChanges;
        private SystemAttachment? _selectedAttachment;

        public HardDiskMediaApprovalEditDialogViewModel(
            IUserService userService,
            IHardDiskMediaService hardDiskMediaService,
            IDialogService dialogService,
            IApprovalWorkflowService approvalWorkflowService,
            HardDiskMediaApplication application,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(userService);
            ArgumentNullException.ThrowIfNull(hardDiskMediaService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(approvalWorkflowService);
            ArgumentNullException.ThrowIfNull(application);

            _userService = userService;
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _approvalWorkflowService = approvalWorkflowService;
            _application = application;
            _currentUser = currentUser;

            var now = DateTime.Today;

            DeptHead = application.DeptHead?.Trim() ?? string.Empty;
            DeptHeadDate = application.ApplyTime == default ? now : application.ApplyTime.Date;
            ArchiveRoomHead = application.ArchiveRoomHead?.Trim() ?? string.Empty;
            ArchiveRoomHeadDate = (application.ArchiveRoomHeadDate ?? now).Date;
            ArchiveDeputyPresident = application.ArchiveDeputyPresident?.Trim() ?? string.Empty;
            ArchiveDeputyPresidentDate = (application.ArchiveDeputyPresidentDate ?? now).Date;

            // 办理交接默认包含两人：申请人 + 资料管理员（如无法查到资料管理员则使用当前用户）
            HandoverApplicant = string.IsNullOrWhiteSpace(application.ApplicantName) ? currentUser?.RealName?.Trim() ?? string.Empty : application.ApplicantName.Trim();
            HandoverAdmin = string.IsNullOrWhiteSpace(application.ExecutedBy) ? currentUser?.RealName?.Trim() ?? string.Empty : application.ExecutedBy.Trim();
            HandoverDate = (application.ExecutedTime ?? now).Date;

            ApprovalOpinion = string.IsNullOrWhiteSpace(application.ApprovalOpinion) ? "同意" : application.ApprovalOpinion.Trim();

            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => CanApprovePass);
            ConfirmPhysicalHandoverCommand = new RelayCommand(async _ => await ConfirmPhysicalHandoverAsync(), _ => CanConfirmPhysicalHandover);
            UploadSignedAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskOutboundDomainValues.AttachmentCategorySignedHandover),
                _ => CanUploadSignedAttachment);
            CaptureSignedAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskOutboundDomainValues.AttachmentCategorySignedHandover),
                _ => CanUploadSignedAttachment);
            UploadPhysicalPhotoCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskOutboundDomainValues.AttachmentCategoryPhysicalPhoto),
                _ => CanUploadPhysicalPhotoAttachment);
            CapturePhysicalPhotoCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskOutboundDomainValues.AttachmentCategoryPhysicalPhoto),
                _ => CanUploadPhysicalPhotoAttachment);
            UploadProofMaterialCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskOutboundDomainValues.AttachmentCategoryProofMaterial),
                _ => CanUploadProofMaterialAttachment);
            CaptureProofMaterialCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskOutboundDomainValues.AttachmentCategoryProofMaterial),
                _ => CanUploadProofMaterialAttachment);
            UploadOtherAttachmentCommand = new RelayCommand(
                async _ => await UploadAttachmentByCategoryAsync(HardDiskOutboundDomainValues.AttachmentCategoryOther),
                _ => CanUploadSignedAttachment);
            CaptureOtherAttachmentCommand = new RelayCommand(
                async _ => await CaptureAttachmentByCategoryAsync(HardDiskOutboundDomainValues.AttachmentCategoryOther),
                _ => CanUploadSignedAttachment);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            PrintHandoverSheetCommand = new RelayCommand(async _ => await PrintHandoverSheetAsync(), _ => CanPrintHandoverSheet);
            ViewAttachmentCommand = new RelayCommand(async attachment => await ViewAttachmentAsync(attachment as SystemAttachment), attachment => attachment is SystemAttachment);
            DeleteAttachmentCommand = new RelayCommand(async attachment => await DeleteAttachmentAsync(attachment as SystemAttachment), attachment => attachment is SystemAttachment && CanDeleteAttachment);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public string WindowTitle => $"硬盘借出 · {ApplicationNo} · {ApplicationStatusDisplay}";

        /// <summary>界面展示用流程状态（上传签批交接单后会与库内状态区分）。</summary>
        public string ApplicationStatusDisplay =>
            HardDiskMediaApplication.ResolveOutboundWorkflowStatusDisplay(
                _application.ApplicationStatus,
                _application.SignedAttachmentUploaded);

        /// <summary>顶部流程说明文案。</summary>
        public string WorkspaceBannerText =>
            "请先查看申请信息与关联介质，再按“审批通过→确认实物交接→分区上传附件→确认办结→打印交接单”的顺序办理。";

        public string ApplicationNo => _application.ApplicationNo;
        public string ApplicantName => _application.ApplicantName;
        public string ApplicantDept => _application.ApplicantDept;
        public string ApplicationType => _application.ApplicationType;
        public string ApplicationStatus => _application.StatusStr;
        public string DiskCode =>
            _application.Medium?.DiskCode?.Trim() ?? string.Empty;

        public string Reason => _application.Reason;
        public string CurrentLocation => _application.CurrentLocation;
        public string TargetLocation => _application.TargetLocation;

        public string ApplyDateDisplay =>
            _application.ApplyTime == default ? string.Empty : _application.ApplyTime.ToString("yyyy-MM-dd");

        public string ExpectedReturnDateDisplay =>
            HardDiskMediaApplicationViewModelHelper.FormatExpectedReturnDateDisplay(
                _application.ApplicationType,
                _application.ExpectedReturnDate);
        public string SignedAttachmentStatusText => _application.SignedAttachmentUploaded
            ? $"签批交接单已上传（{_application.SignedAttachmentUploadedTime?.ToString("yyyy-MM-dd HH:mm") ?? "时间未记录"}，上传人：{(_application.SignedAttachmentUploader ?? string.Empty)}）"
            : "签批交接单未上传";

        public bool IsApprovalEditable => ResolveOutboundButtonState().CanApprovePass;
        public bool IsHandoverEditable => ResolveOutboundButtonState().CanConfirmPhysicalHandover;
        public bool CanApprovePass => ResolveOutboundButtonState().CanApprovePass;
        public bool CanConfirmPhysicalHandover => ResolveOutboundButtonState().CanConfirmPhysicalHandover;
        public bool CanUploadSignedAttachment => ResolveOutboundButtonState().CanUploadSignedAttachment;
        public bool CanUploadPhysicalPhotoAttachment =>
            CanUploadSignedAttachment && RequiresPhysicalPhotoAttachment;
        public bool CanUploadProofMaterialAttachment =>
            CanUploadSignedAttachment && RequiresProofMaterialAttachment;
        public bool CanComplete => ResolveOutboundButtonState().CanConfirmComplete;
        public bool CanPrintHandoverSheet => ResolveOutboundButtonState().CanPrintHandoverSheet;
        public bool CanDeleteAttachment => _application.ApplicationStatus != HardDiskMediaApplication.StatusCompleted &&
                                          _application.ApplicationStatus != HardDiskMediaApplication.StatusWithdrawn &&
                                          _application.ApplicationStatus != HardDiskMediaApplication.StatusForceWithdrawn;

        /// <summary>本单是否存在实物流转（须上传实物照片）。</summary>
        public bool RequiresPhysicalPhotoAttachment =>
            HardDiskOutboundDomainValues.RequiresPhysicalPhotoAttachment(_application.ApplicationType);

        /// <summary>申请是否声明附有证明材料（须上传证明材料）。</summary>
        public bool RequiresProofMaterialAttachment =>
            HardDiskOutboundDomainValues.RequiresProofMaterialAttachment(_application.ProofMaterialNote);

        public string ProofMaterialDisplay => RequiresProofMaterialAttachment
            ? (_application.ProofMaterialNote?.Trim() ?? string.Empty)
            : HardDiskOutboundDomainValues.ProofMaterialNoneText;

        public string PhysicalPhotoAttachmentHint => RequiresPhysicalPhotoAttachment
            ? "本单存在实物流转，须上传实物照片后方可办结。"
            : "本单无实物流转，无需上传实物照片。";

        public string ProofMaterialAttachmentHint => RequiresProofMaterialAttachment
            ? $"申请时已声明证明材料「{ProofMaterialDisplay}」，须上传扫描件后方可办结。"
            : "申请时未声明证明材料，本区无需上传。";

        public string ConfirmHintText => CanApprovePass
            ? "后续：审批通过后，请办理实物交接。"
            : "当前状态不允许执行“审批通过”。";

        public string ConfirmPhysicalHandoverHintText => CanConfirmPhysicalHandover
            ? "后续：确认实物交接后，请按附件区提示分区上传材料。"
            : "请先执行“审批通过”，再确认实物交接。";

        public string UploadHintText
        {
            get
            {
                if (!CanUploadSignedAttachment)
                {
                    return "请先确认实物交接，再上传附件。";
                }

                var required = new List<string> { "签批交接单" };
                if (RequiresPhysicalPhotoAttachment)
                {
                    required.Add("实物照片");
                }

                if (RequiresProofMaterialAttachment)
                {
                    required.Add("证明材料");
                }

                return $"格式限 PDF / 常见图像；{string.Join("、", required)}必传，其他附件可选。";
            }
        }

        public string CompleteHintText
        {
            get
            {
                if (CanComplete)
                {
                    return "确认办结后，可打印交接单备查。";
                }

                if (!CanUploadSignedAttachment)
                {
                    return "请先确认实物交接并上传必传附件后再确认办结。";
                }

                var missing = new List<string>();
                if (!_application.SignedAttachmentUploaded && SignedHandoverAttachments.Count == 0)
                {
                    missing.Add("签批交接单");
                }

                if (RequiresPhysicalPhotoAttachment && PhysicalPhotoAttachments.Count == 0)
                {
                    missing.Add("实物照片");
                }

                if (RequiresProofMaterialAttachment && ProofMaterialAttachments.Count == 0)
                {
                    missing.Add("证明材料");
                }

                return missing.Count > 0
                    ? $"请先在附件区上传{string.Join("、", missing)}后再确认办结。"
                    : "请先上传必传附件后再确认办结。";
            }
        }

        public string PrintHintText => CanPrintHandoverSheet
            ? "可打印签批交接单（空白表单供线下签字，或办结后备查）。"
            : "当前状态不允许打印交接单。";

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();
        public ObservableCollection<SystemAttachment> SignedHandoverAttachments { get; } = new();
        public ObservableCollection<SystemAttachment> PhysicalPhotoAttachments { get; } = new();
        public ObservableCollection<SystemAttachment> ProofMaterialAttachments { get; } = new();
        public ObservableCollection<SystemAttachment> OtherAttachments { get; } = new();

        public SystemAttachment? SelectedAttachment
        {
            get => _selectedAttachment;
            set => SetProperty(ref _selectedAttachment, value);
        }

        public string DeptHead
        {
            get => _reviewerName;
            set => SetProperty(ref _reviewerName, value);
        }

        public DateTime DeptHeadDate
        {
            get => _reviewerDate;
            set => SetProperty(ref _reviewerDate, value);
        }

        public string ArchiveRoomHead
        {
            get => _archiveRoomHead;
            set => SetProperty(ref _archiveRoomHead, value);
        }

        public DateTime ArchiveRoomHeadDate
        {
            get => _archiveRoomHeadDate;
            set => SetProperty(ref _archiveRoomHeadDate, value);
        }

        public string ArchiveDeputyPresident
        {
            get => _archiveDeputyPresident;
            set => SetProperty(ref _archiveDeputyPresident, value);
        }

        public DateTime ArchiveDeputyPresidentDate
        {
            get => _archiveDeputyPresidentDate;
            set => SetProperty(ref _archiveDeputyPresidentDate, value);
        }

        public bool EnableDeptHead
        {
            get => _enableDeptHead;
            private set => SetProperty(ref _enableDeptHead, value);
        }

        public bool EnableArchiveRoomHead
        {
            get => _enableArchiveRoomHead;
            private set => SetProperty(ref _enableArchiveRoomHead, value);
        }

        public bool EnableArchiveDeputyPresident
        {
            get => _enableArchiveDeputyPresident;
            private set => SetProperty(ref _enableArchiveDeputyPresident, value);
        }

        /// <summary>
        /// 办理交接人：申请人
        /// </summary>
        public string HandoverApplicant
        {
            get => _handoverApplicant;
            set => SetProperty(ref _handoverApplicant, value);
        }

        /// <summary>
        /// 办理交接人：资料管理员
        /// </summary>
        public string HandoverAdmin
        {
            get => _handoverAdmin;
            set => SetProperty(ref _handoverAdmin, value);
        }

        public DateTime HandoverDate
        {
            get => _handoverDate;
            set => SetProperty(ref _handoverDate, value);
        }

        public string ApprovalOpinion
        {
            get => _approvalOpinion;
            set => SetProperty(ref _approvalOpinion, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand ConfirmPhysicalHandoverCommand { get; }
        public ICommand UploadSignedAttachmentCommand { get; }
        public ICommand CaptureSignedAttachmentCommand { get; }
        public ICommand UploadPhysicalPhotoCommand { get; }
        public ICommand CapturePhysicalPhotoCommand { get; }
        public ICommand UploadProofMaterialCommand { get; }
        public ICommand CaptureProofMaterialCommand { get; }
        public ICommand UploadOtherAttachmentCommand { get; }
        public ICommand CaptureOtherAttachmentCommand { get; }
        public ICommand CompleteCommand { get; }
        public ICommand PrintHandoverSheetCommand { get; }
        public ICommand ViewAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }
        public ICommand CancelCommand { get; }

        public bool HasCommittedChanges
        {
            get => _hasCommittedChanges;
            private set => SetProperty(ref _hasCommittedChanges, value);
        }

        public HardDiskMediaApprovalInput? Result { get; private set; }

        public event Action<bool?>? RequestClose;

        private async Task InitializeAsync()
        {
            await EnsureMediumLoadedAsync();
            await LoadAttachmentsAsync();

            var users = _userService.GetAllUsers();
            var chain = await _approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalChainApplySupport.ResolveHardDiskApplicationBusinessType(_application),
                    ApplicantDept = _application.ApplicantDept,
                    FieldValues = ApprovalChainApplySupport.BuildHardDiskApplicationFieldValues(_application)
                },
                users);

            EnableDeptHead = chain.DeptHead.IsEnabled;
            EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled;
            EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled;

            await HardDiskMediaApplicationViewModelHelper.ApplyDefaultApprovalFromWorkflowAsync(
                _application,
                _approvalWorkflowService,
                users,
                _currentUser);

            if (EnableDeptHead && string.IsNullOrWhiteSpace(DeptHead))
            {
                DeptHead = _application.DeptHead;
            }

            if (EnableArchiveRoomHead && string.IsNullOrWhiteSpace(ArchiveRoomHead))
            {
                ArchiveRoomHead = _application.ArchiveRoomHead;
            }

            if (EnableArchiveDeputyPresident && string.IsNullOrWhiteSpace(ArchiveDeputyPresident))
            {
                ArchiveDeputyPresident = _application.ArchiveDeputyPresident;
            }
        }

        /// <summary>
        /// 审批弹窗展示介质编号；克隆申请单时可能未带上导航属性，此处补全。
        /// </summary>
        private async Task EnsureMediumLoadedAsync()
        {
            if (!string.IsNullOrWhiteSpace(DiskCode))
            {
                return;
            }

            if (_application.Id <= 0 && string.IsNullOrWhiteSpace(_application.ApplicationNo))
            {
                return;
            }

            var applications = await _hardDiskMediaService.SearchApplicationsAsync(_application.ApplicationNo, null, null);
            var refreshed = applications.FirstOrDefault(item => item.Id == _application.Id)
                ?? applications.FirstOrDefault(item =>
                    string.Equals(item.ApplicationNo, _application.ApplicationNo, StringComparison.OrdinalIgnoreCase));

            if (refreshed?.Medium == null)
            {
                return;
            }

            _application.Medium = refreshed.Medium;
            OnPropertyChanged(nameof(DiskCode));
        }

        private async Task ConfirmAsync()
        {
            var input = await BuildApprovalInputAsync();
            if (input == null)
            {
                return;
            }

            var result = await _hardDiskMediaService.ApproveApplicationAsync(_application, _currentUser, input);
            _dialogService.ShowMessage(result.Message);
            if (!result.Success)
            {
                return;
            }

            HasCommittedChanges = true;
            Result = input;
            await RefreshApplicationStateAsync();
            await LoadAttachmentsAsync();
            _dialogService.ShowMessage("审批通过成功。下一步：请确认实物交接。");
        }

        private async Task ConfirmPhysicalHandoverAsync()
        {
            if (!CanConfirmPhysicalHandover)
            {
                _dialogService.ShowMessage("请先执行“审批通过”，再确认实物交接。");
                return;
            }

            var input = BuildHandoverInput();
            if (input == null)
            {
                return;
            }

            var result = await _hardDiskMediaService.ConfirmPhysicalHandoverAsync(_application, _currentUser, input);
            _dialogService.ShowMessage(result.Message);
            if (!result.Success)
            {
                return;
            }

            HasCommittedChanges = true;
            await RefreshApplicationStateAsync();
            await LoadAttachmentsAsync();
            _dialogService.ShowMessage("实物交接确认成功。下一步：请分区上传签批交接单与实物照片。");
        }

        private async Task UploadAttachmentByCategoryAsync(string fileCategory)
        {
            string category = fileCategory?.Trim() ?? string.Empty;
            if (!CanUploadCategory(category, out string blockedMessage))
            {
                _dialogService.ShowMessage(blockedMessage);
                return;
            }

            var filePath = _dialogService.OpenFileDialog(
                SystemAttachmentUploadSupport.OpenFileDialogFilter,
                $"选择{category}");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileContent = await File.ReadAllBytesAsync(filePath);
                var uploadResult = await _hardDiskMediaService.UploadApplicationAttachmentAsync(
                    _application,
                    _currentUser,
                    category,
                    fileInfo.Name,
                    fileInfo.Extension,
                    fileInfo.Length,
                    fileContent);
                _dialogService.ShowMessage(uploadResult.Message);
                if (!uploadResult.Success)
                {
                    return;
                }

                HasCommittedChanges = true;
                if (HardDiskOutboundDomainValues.IsSignedHandoverCategory(category))
                {
                    _application.SignedAttachmentUploaded = true;
                    _application.SignedAttachmentUploadedTime = DateTime.Now;
                    _application.SignedAttachmentUploader = _currentUser?.RealName?.Trim() ?? string.Empty;
                    NotifyWorkflowDisplayChanged();
                }

                await RefreshApplicationStateAsync();
                await LoadAttachmentsAsync();
                _dialogService.ShowMessage(BuildUploadSuccessFollowUpMessage(category));
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"读取附件失败：{ex.Message}");
            }
        }

        private async Task CaptureAttachmentByCategoryAsync(string fileCategory)
        {
            string category = fileCategory?.Trim() ?? string.Empty;
            if (!CanUploadCategory(category, out string blockedMessage))
            {
                _dialogService.ShowMessage(blockedMessage);
                return;
            }

            DocumentCameraCaptureResult? captured = DocumentCameraAttachmentCaptureSupport.Capture(_dialogService);
            if (captured == null)
            {
                return;
            }

            try
            {
                string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(ApplicationNo, category, "盘审批");
                var uploadResult = await _hardDiskMediaService.UploadApplicationAttachmentAsync(
                    _application,
                    _currentUser,
                    category,
                    fileName,
                    ".jpg",
                    captured.JpegContent.LongLength,
                    captured.JpegContent);
                _dialogService.ShowMessage(uploadResult.Message);
                if (!uploadResult.Success)
                {
                    return;
                }

                HasCommittedChanges = true;
                if (HardDiskOutboundDomainValues.IsSignedHandoverCategory(category))
                {
                    _application.SignedAttachmentUploaded = true;
                    _application.SignedAttachmentUploadedTime = DateTime.Now;
                    _application.SignedAttachmentUploader = _currentUser?.RealName?.Trim() ?? string.Empty;
                    NotifyWorkflowDisplayChanged();
                }

                await RefreshApplicationStateAsync();
                await LoadAttachmentsAsync();
                _dialogService.ShowMessage(BuildUploadSuccessFollowUpMessage(category));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"上传失败：{ex.Message}");
            }
        }

        private bool CanUploadCategory(string fileCategory, out string message)
        {
            if (!CanUploadSignedAttachment)
            {
                message = "请先确认实物交接，再上传附件。";
                return false;
            }

            if (string.Equals(fileCategory, HardDiskOutboundDomainValues.AttachmentCategoryPhysicalPhoto, StringComparison.Ordinal)
                && !RequiresPhysicalPhotoAttachment)
            {
                message = "本单无实物流转，无需上传实物照片。";
                return false;
            }

            if (string.Equals(fileCategory, HardDiskOutboundDomainValues.AttachmentCategoryProofMaterial, StringComparison.Ordinal)
                && !RequiresProofMaterialAttachment)
            {
                message = "申请时未声明附有证明材料，无需上传证明材料扫描件。";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private string BuildUploadSuccessFollowUpMessage(string fileCategory)
        {
            var missing = CollectMissingRequiredAttachmentLabels();
            if (missing.Count == 0)
            {
                return $"{fileCategory}上传成功。下一步：请点击“确认办结”。";
            }

            return $"{fileCategory}上传成功。下一步：请上传{string.Join("、", missing)}。";
        }

        private List<string> CollectMissingRequiredAttachmentLabels()
        {
            var missing = new List<string>();
            if (!_application.SignedAttachmentUploaded && SignedHandoverAttachments.Count == 0)
            {
                missing.Add("签批交接单");
            }

            if (RequiresPhysicalPhotoAttachment && PhysicalPhotoAttachments.Count == 0)
            {
                missing.Add("实物照片");
            }

            if (RequiresProofMaterialAttachment && ProofMaterialAttachments.Count == 0)
            {
                missing.Add("证明材料");
            }

            return missing;
        }

        private async Task CompleteAsync()
        {
            if (!CanComplete)
            {
                _dialogService.ShowMessage(CompleteHintText);
                return;
            }

            if (!ValidateHandoverInformationCompleteness(out string validationMessage))
            {
                _dialogService.ShowMessage($"确认办结失败：{validationMessage}", "提示");
                return;
            }

            var missing = CollectMissingRequiredAttachmentLabels();
            if (missing.Count > 0)
            {
                _dialogService.ShowMessage($"确认办结失败：请先上传{string.Join("、", missing)}。", "提示");
                return;
            }

            var completeResult = await _hardDiskMediaService.CompleteApplicationAsync(_application, _currentUser);
            _dialogService.ShowMessage(completeResult.Message);
            if (!completeResult.Success)
            {
                return;
            }

            HasCommittedChanges = true;
            await RefreshApplicationStateAsync();
            await LoadAttachmentsAsync();
            _dialogService.ShowMessage("办结确认成功。下一步：请打印交接单。");
        }

        private HardDiskOutboundApprovalButtonSupport.ButtonState ResolveOutboundButtonState()
        {
            if (_application.Id <= 0)
            {
                return new HardDiskOutboundApprovalButtonSupport.ButtonState(false, false, false, false, false);
            }

            return HardDiskOutboundApprovalButtonSupport.Resolve(
                HardDiskOutboundApprovalButtonSupport.ResolvePhase(_application),
                _currentUser != null);
        }

        private async Task PrintHandoverSheetAsync()
        {
            if (!CanPrintHandoverSheet)
            {
                _dialogService.ShowMessage("当前状态不允许打印交接单。");
                return;
            }

            try
            {
                var data = await _hardDiskMediaService.BuildPrintDataAsync(_application);
                var document = HardDiskMediaPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };

                await _hardDiskMediaService.MarkApplicationPrintedAsync(_application);
                previewWindow.ShowDialog();
                HasCommittedChanges = true;
                await RefreshApplicationStateAsync();
                _dialogService.ShowMessage("交接单打印完成。");
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
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

            _dialogService.ShowSystemAttachmentView(result.Attachment);
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

            HasCommittedChanges = true;
            await RefreshApplicationStateAsync();
            await LoadAttachmentsAsync();
        }

        private async Task<HardDiskMediaApprovalInput?> BuildApprovalInputAsync()
        {
            if (!CanApprovePass)
            {
                _dialogService.ShowMessage("当前状态仅允许查看，不能提交审批信息。");
                return null;
            }

            _application.DeptHead = DeptHead?.Trim() ?? string.Empty;
            _application.ArchiveRoomHead = ArchiveRoomHead?.Trim() ?? string.Empty;
            _application.ArchiveDeputyPresident = ArchiveDeputyPresident?.Trim() ?? string.Empty;
            var missing = await HardDiskMediaApplicationViewModelHelper.CollectMissingApprovalErrorsAsync(
                _application,
                _approvalWorkflowService,
                _userService.GetAllUsers());
            if (missing.Count > 0)
            {
                _dialogService.ShowMessage(string.Join(Environment.NewLine, missing));
                return null;
            }

            if (EnableDeptHead && DeptHeadDate == default)
            {
                _dialogService.ShowMessage("请填写审核日期。");
                return null;
            }

            if (EnableArchiveRoomHead && ArchiveRoomHeadDate == default)
            {
                _dialogService.ShowMessage("请填写审批日期。");
                return null;
            }

            if (EnableArchiveDeputyPresident && ArchiveDeputyPresidentDate == default)
            {
                _dialogService.ShowMessage($"请填写{ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident}日期。");
                return null;
            }

            return new HardDiskMediaApprovalInput
            {
                DeptHead = DeptHead.Trim(),
                DeptHeadDate = DeptHeadDate,
                ArchiveRoomHead = ArchiveRoomHead.Trim(),
                ArchiveRoomHeadDate = ArchiveRoomHeadDate,
                ArchiveDeputyPresident = ArchiveDeputyPresident.Trim(),
                ArchiveDeputyPresidentDate = ArchiveDeputyPresidentDate,
                ApprovalOpinion = string.IsNullOrWhiteSpace(ApprovalOpinion) ? "同意" : ApprovalOpinion.Trim()
            };
        }

        private HardDiskMediaApprovalInput? BuildHandoverInput()
        {
            if (!CanConfirmPhysicalHandover)
            {
                _dialogService.ShowMessage("当前状态不允许确认实物交接。");
                return null;
            }

            if (string.IsNullOrWhiteSpace(HandoverApplicant))
            {
                _dialogService.ShowMessage("请填写办理交接人（申请人）。");
                return null;
            }

            if (string.IsNullOrWhiteSpace(HandoverAdmin))
            {
                _dialogService.ShowMessage("请填写办理交接人（资料管理员）。");
                return null;
            }

            if (HandoverDate == default)
            {
                _dialogService.ShowMessage("请填写办理交接日期。");
                return null;
            }

            return new HardDiskMediaApprovalInput
            {
                HandoverApplicant = HandoverApplicant.Trim(),
                HandoverAdmin = HandoverAdmin.Trim(),
                HandoverName = HandoverAdmin.Trim(),
                HandoverDate = HandoverDate
            };
        }

        private async Task RefreshApplicationStateAsync()
        {
            var applications = await _hardDiskMediaService.SearchApplicationsAsync(_application.ApplicationNo, null, null);
            var refreshed = applications.FirstOrDefault(item => item.Id == _application.Id)
                ?? applications.FirstOrDefault(item => string.Equals(item.ApplicationNo, _application.ApplicationNo, StringComparison.OrdinalIgnoreCase));

            if (refreshed == null)
            {
                return;
            }

            _application.ApplicationStatus = refreshed.ApplicationStatus;
            _application.PrintCount = refreshed.PrintCount;
            _application.SignedAttachmentUploaded = refreshed.SignedAttachmentUploaded;
            _application.SignedAttachmentUploadedTime = refreshed.SignedAttachmentUploadedTime;
            _application.SignedAttachmentUploader = refreshed.SignedAttachmentUploader;
            _application.ProofMaterialNote = refreshed.ProofMaterialNote;
            if (refreshed.Medium != null)
            {
                _application.Medium = refreshed.Medium;
            }

            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ApplicationStatus));
            OnPropertyChanged(nameof(ApplicationStatusDisplay));
            OnPropertyChanged(nameof(DiskCode));
            OnPropertyChanged(nameof(SignedAttachmentStatusText));
            OnPropertyChanged(nameof(IsApprovalEditable));
            OnPropertyChanged(nameof(IsHandoverEditable));
            OnPropertyChanged(nameof(CanApprovePass));
            OnPropertyChanged(nameof(CanConfirmPhysicalHandover));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(CanUploadPhysicalPhotoAttachment));
            OnPropertyChanged(nameof(CanUploadProofMaterialAttachment));
            OnPropertyChanged(nameof(RequiresPhysicalPhotoAttachment));
            OnPropertyChanged(nameof(RequiresProofMaterialAttachment));
            OnPropertyChanged(nameof(ProofMaterialDisplay));
            OnPropertyChanged(nameof(PhysicalPhotoAttachmentHint));
            OnPropertyChanged(nameof(ProofMaterialAttachmentHint));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrintHandoverSheet));
            OnPropertyChanged(nameof(CanDeleteAttachment));
            OnPropertyChanged(nameof(ConfirmHintText));
            OnPropertyChanged(nameof(ConfirmPhysicalHandoverHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(PrintHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void NotifyWorkflowDisplayChanged()
        {
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ApplicationStatusDisplay));
            OnPropertyChanged(nameof(SignedAttachmentStatusText));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(CanUploadPhysicalPhotoAttachment));
            OnPropertyChanged(nameof(CanUploadProofMaterialAttachment));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async Task LoadAttachmentsAsync()
        {
            int? selectedAttachmentId = SelectedAttachment?.Id;
            Attachments.Clear();
            SignedHandoverAttachments.Clear();
            PhysicalPhotoAttachments.Clear();
            ProofMaterialAttachments.Clear();
            OtherAttachments.Clear();
            if (string.IsNullOrWhiteSpace(_application.ApplicationNo))
            {
                SelectedAttachment = null;
                return;
            }

            var attachments = await _hardDiskMediaService.GetApplicationAttachmentsAsync(_application.ApplicationNo);
            foreach (var attachment in attachments)
            {
                Attachments.Add(attachment);
                string category = attachment.FileCategory?.Trim() ?? string.Empty;
                if (HardDiskOutboundDomainValues.IsSignedHandoverCategory(category))
                {
                    SignedHandoverAttachments.Add(attachment);
                }
                else if (string.Equals(category, HardDiskOutboundDomainValues.AttachmentCategoryPhysicalPhoto, StringComparison.Ordinal))
                {
                    PhysicalPhotoAttachments.Add(attachment);
                }
                else if (string.Equals(category, HardDiskOutboundDomainValues.AttachmentCategoryProofMaterial, StringComparison.Ordinal))
                {
                    ProofMaterialAttachments.Add(attachment);
                }
                else if (string.Equals(category, HardDiskOutboundDomainValues.AttachmentCategoryOther, StringComparison.Ordinal))
                {
                    OtherAttachments.Add(attachment);
                }
                else
                {
                    // 历史未分类附件归入其他附件区，便于查看删除
                    OtherAttachments.Add(attachment);
                }
            }

            SelectedAttachment = selectedAttachmentId.HasValue
                ? Attachments.FirstOrDefault(item => item.Id == selectedAttachmentId.Value)
                : Attachments.FirstOrDefault();
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private bool ValidateHandoverInformationCompleteness(out string message)
        {
            if (string.IsNullOrWhiteSpace(HandoverApplicant))
            {
                message = "办理交接人（申请人）不能为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(HandoverAdmin))
            {
                message = "办理交接人（资料管理员）不能为空。";
                return false;
            }

            if (HandoverDate == default)
            {
                message = "办理交接日期不能为空。";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
