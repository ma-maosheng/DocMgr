using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
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
        private readonly User? _currentUser;

        private string _reviewerName = string.Empty;
        private DateTime _reviewerDate;
        private string _approverName = string.Empty;
        private DateTime _approverDate;
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
            HardDiskMediaApplication application,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(userService);
            ArgumentNullException.ThrowIfNull(hardDiskMediaService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(application);

            _userService = userService;
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _application = application;
            _currentUser = currentUser;

            var now = DateTime.Today;

            // 默认：审核人优先使用申请人所属部门负责人，否则回退到申请人姓名/当前用户
            ReviewerName = ResolveDefaultReviewerName(application, currentUser);
            ReviewerDate = application.ApplyTime == default ? now : application.ApplyTime.Date;

            // 默认：审批人优先使用资料室负责人；若申请单已有审批人则保持原值
            ApproverName = ResolveDefaultApproverName(application, currentUser);
            ApproverDate = (application.ApprovedTime ?? now).Date;

            // 办理交接默认包含两人：申请人 + 资料室资料管理员（如无法查到资料管理员则使用当前用户）
            HandoverApplicant = string.IsNullOrWhiteSpace(application.ApplicantName) ? currentUser?.RealName?.Trim() ?? string.Empty : application.ApplicantName.Trim();
            HandoverAdmin = string.IsNullOrWhiteSpace(application.ExecutedBy) ? currentUser?.RealName?.Trim() ?? string.Empty : application.ExecutedBy.Trim();
            HandoverDate = (application.ExecutedTime ?? now).Date;

            ApprovalOpinion = string.IsNullOrWhiteSpace(application.ApprovalOpinion) ? "同意" : application.ApprovalOpinion.Trim();

            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => CanApprovePass);
            ConfirmPhysicalHandoverCommand = new RelayCommand(async _ => await ConfirmPhysicalHandoverAsync(), _ => CanConfirmPhysicalHandover);
            UploadSignedAttachmentCommand = new RelayCommand(async _ => await UploadSignedAttachmentAsync(), _ => CanUploadSignedAttachment);
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
            "请先查看申请信息与关联介质，再按“审批通过→确认实物交接→上传签批交接单→确认办结→打印交接单”的顺序办理。";

        public string ApplicationNo => _application.ApplicationNo;
        public string ApplicantName => _application.ApplicantName;
        public string ApplicantDept => _application.ApplicantDept;
        public string ApplicationType => _application.ApplicationType;
        public string ApplicationStatus => _application.ApplicationStatus;
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
            ? $"已上传（{_application.SignedAttachmentUploadedTime?.ToString("yyyy-MM-dd HH:mm") ?? "时间未记录"}，上传人：{(_application.SignedAttachmentUploader ?? string.Empty)}）"
            : "未上传";

        public bool IsApprovalEditable => ResolveOutboundButtonState().CanApprovePass;
        public bool IsHandoverEditable => ResolveOutboundButtonState().CanConfirmPhysicalHandover;
        public bool CanApprovePass => ResolveOutboundButtonState().CanApprovePass;
        public bool CanConfirmPhysicalHandover => ResolveOutboundButtonState().CanConfirmPhysicalHandover;
        public bool CanUploadSignedAttachment => ResolveOutboundButtonState().CanUploadSignedAttachment;
        public bool CanComplete => ResolveOutboundButtonState().CanConfirmComplete;
        public bool CanPrintHandoverSheet => ResolveOutboundButtonState().CanPrintHandoverSheet;
        public bool CanDeleteAttachment => !string.Equals(_application.ApplicationStatus, HardDiskMediaApplication.StatusCompleted, StringComparison.Ordinal) &&
                                          !string.Equals(_application.ApplicationStatus, HardDiskMediaApplication.StatusWithdrawn, StringComparison.Ordinal) &&
                                          !string.Equals(_application.ApplicationStatus, HardDiskMediaApplication.StatusForceWithdrawn, StringComparison.Ordinal);

        public string ConfirmHintText => CanApprovePass
            ? "后续：审批通过后，请办理实物交接。"
            : "当前状态不允许执行“审批通过”。";

        public string ConfirmPhysicalHandoverHintText => CanConfirmPhysicalHandover
            ? "后续：确认实物交接后，请上传签批交接单。"
            : "请先执行“审批通过”，再确认实物交接。";

        public string UploadHintText => CanUploadSignedAttachment
            ? "后续：上传签批交接单后，请点击“确认办结”。"
            : "请先确认实物交接，再上传签批交接单。";

        public string CompleteHintText => CanComplete
            ? "确认办结后，可打印交接单备查。"
            : "请先上传签批交接单后再确认办结。";

        public string PrintHintText => CanPrintHandoverSheet
            ? "可打印签批交接单（空白表单供线下签字，或办结后备查）。"
            : "当前状态不允许打印交接单。";

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public SystemAttachment? SelectedAttachment
        {
            get => _selectedAttachment;
            set => SetProperty(ref _selectedAttachment, value);
        }

        public string ReviewerName
        {
            get => _reviewerName;
            set => SetProperty(ref _reviewerName, value);
        }

        public DateTime ReviewerDate
        {
            get => _reviewerDate;
            set => SetProperty(ref _reviewerDate, value);
        }

        public string ApproverName
        {
            get => _approverName;
            set => SetProperty(ref _approverName, value);
        }

        public DateTime ApproverDate
        {
            get => _approverDate;
            set => SetProperty(ref _approverDate, value);
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
        /// 办理交接人：资料室资料管理员
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

        private string ResolveDefaultReviewerName(HardDiskMediaApplication application, User? currentUser)
        {
            string applicantDept = application.ApplicantDept?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(applicantDept))
            {
                string reviewer = _userService
                    .GetAllUsers()
                    .FirstOrDefault(user => string.Equals(user.Department, applicantDept, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(user.RealName)
                        && (user.Role?.Contains("部门负责人", StringComparison.OrdinalIgnoreCase) ?? false))
                    ?.RealName
                    ?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(reviewer))
                {
                    return reviewer;
                }
            }

            if (!string.IsNullOrWhiteSpace(application.ApplicantName))
            {
                return application.ApplicantName.Trim();
            }

            return currentUser?.RealName?.Trim() ?? string.Empty;
        }

        private string ResolveDefaultApproverName(HardDiskMediaApplication application, User? currentUser)
        {
            if (!string.IsNullOrWhiteSpace(application.ApprovedBy))
            {
                return application.ApprovedBy.Trim();
            }

            string approver = _userService
                .GetAllUsers()
                .FirstOrDefault(user => string.Equals(user.Department, "资料室", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(user.RealName)
                    && (user.Role?.Contains("负责人", StringComparison.OrdinalIgnoreCase) ?? false))
                ?.RealName
                ?.Trim() ?? string.Empty;

            return string.IsNullOrWhiteSpace(approver)
                ? currentUser?.RealName?.Trim() ?? string.Empty
                : approver;
        }

        private async Task InitializeAsync()
        {
            await EnsureMediumLoadedAsync();
            await LoadAttachmentsAsync();
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
            var input = BuildApprovalInput();
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
            _dialogService.ShowMessage("实物交接确认成功。下一步：请上传签批交接单。");
        }

        private async Task UploadSignedAttachmentAsync()
        {
            if (!CanUploadSignedAttachment)
            {
                _dialogService.ShowMessage("请先确认实物交接，再上传签批交接单。");
                return;
            }

            var filePath = _dialogService.OpenFileDialog("所有文件|*.*", "选择签批交接单扫描件");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileContent = await File.ReadAllBytesAsync(filePath);
                var uploadResult = await _hardDiskMediaService.UploadSignedAttachmentAsync(_application, _currentUser, fileInfo.Name, fileInfo.Extension, fileInfo.Length, fileContent);
                _dialogService.ShowMessage(uploadResult.Message);
                if (!uploadResult.Success)
                {
                    return;
                }

                HasCommittedChanges = true;
                _application.SignedAttachmentUploaded = true;
                _application.SignedAttachmentUploadedTime = DateTime.Now;
                _application.SignedAttachmentUploader = _currentUser?.RealName?.Trim() ?? string.Empty;
                NotifyWorkflowDisplayChanged();
                await RefreshApplicationStateAsync();
                await LoadAttachmentsAsync();
                _dialogService.ShowMessage("签批交接单上传成功。下一步：请点击“确认办结”。");
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"读取附件失败：{ex.Message}");
            }
        }

        private async Task CompleteAsync()
        {
            if (!CanComplete)
            {
                _dialogService.ShowMessage("请先上传签批交接单后再确认办结。");
                return;
            }

            if (!ValidateHandoverInformationCompleteness(out string validationMessage))
            {
                _dialogService.ShowMessage($"确认办结失败：{validationMessage}", "提示");
                return;
            }

            if (!_application.SignedAttachmentUploaded && Attachments.Count == 0)
            {
                _dialogService.ShowMessage("确认办结失败：请先上传签批交接单。", "提示");
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

            var fullAttachment = result.Attachment;
            if (_dialogService.ShowConfirm("直接打开附件？\n【确定】打开 【取消】另存为", "附件操作"))
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fullAttachment.FileName}");
                await File.WriteAllBytesAsync(tempPath, fullAttachment.FileContent);
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                return;
            }

            var savePath = _dialogService.SaveFileDialog("所有文件|*.*", "另存附件", fullAttachment.FileName);
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

            HasCommittedChanges = true;
            await RefreshApplicationStateAsync();
            await LoadAttachmentsAsync();
            RequestClose?.Invoke(true);
        }

        private HardDiskMediaApprovalInput? BuildApprovalInput()
        {
            if (!CanApprovePass)
            {
                _dialogService.ShowMessage("当前状态仅允许查看，不能提交审批信息。");
                return null;
            }

            if (string.IsNullOrWhiteSpace(ReviewerName))
            {
                _dialogService.ShowMessage("请填写审核人。");
                return null;
            }

            if (ReviewerDate == default)
            {
                _dialogService.ShowMessage("请填写审核日期。");
                return null;
            }

            if (string.IsNullOrWhiteSpace(ApproverName))
            {
                _dialogService.ShowMessage("请填写审批人。");
                return null;
            }

            if (ApproverDate == default)
            {
                _dialogService.ShowMessage("请填写审批日期。");
                return null;
            }

            return new HardDiskMediaApprovalInput
            {
                ReviewerName = ReviewerName.Trim(),
                ReviewerDate = ReviewerDate,
                ApproverName = ApproverName.Trim(),
                ApproverDate = ApproverDate,
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
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async Task LoadAttachmentsAsync()
        {
            int? selectedAttachmentId = SelectedAttachment?.Id;
            Attachments.Clear();
            if (string.IsNullOrWhiteSpace(_application.ApplicationNo))
            {
                SelectedAttachment = null;
                return;
            }

            var attachments = await _hardDiskMediaService.GetApplicationAttachmentsAsync(_application.ApplicationNo);
            foreach (var attachment in attachments)
            {
                Attachments.Add(attachment);
            }

            SelectedAttachment = selectedAttachmentId.HasValue
                ? Attachments.FirstOrDefault(item => item.Id == selectedAttachmentId.Value)
                : Attachments.FirstOrDefault();
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
