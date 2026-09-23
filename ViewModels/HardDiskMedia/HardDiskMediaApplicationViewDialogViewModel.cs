using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.Shared;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;
using Microsoft.Win32;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质申请单只读查看弹窗 ViewModel。
    /// 资料管理员可在办结后增补「其他附件」（仅新增、不可删除）。
    /// </summary>
    public sealed class HardDiskMediaApplicationViewDialogViewModel : ViewModelBase
    {
        private readonly HardDiskMediaApplication _application;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

        public HardDiskMediaApplicationViewDialogViewModel(
            IHardDiskMediaService hardDiskMediaService,
            IDialogService dialogService,
            IUserContextService userContextService,
            HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(hardDiskMediaService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(userContextService);
            ArgumentNullException.ThrowIfNull(application);

            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _application = application;

            ViewAttachmentCommand = new RelayCommand(
                async attachment => await ViewAttachmentAsync(attachment as SystemAttachment),
                attachment => attachment is SystemAttachment);
            SupplementOtherAttachmentCommand = new RelayCommand(
                async _ => await SupplementOtherAttachmentAsync(),
                _ => CanSupplementOtherAttachments);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public string WindowTitle => $"查看申请 · {_application.ApplicationNo} · {_application.StatusStr}";

        public string WorkspaceBannerText => CanSupplementOtherAttachments
            ? "本窗口以查看为主；办结后资料管理员可增补「其他附件」，不可删除已有附件。"
            : "本窗口仅用于查看申请单信息，不允许编辑。";

        public string ApplicationNo => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ApplicationNo);
        public string ApplicationType => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ApplicationType);
        public string ApplicationStatus => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.StatusStr);
        public string DiskCode => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.Medium?.DiskCode);
        public string ApplicantName => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ApplicantName);
        public string ApplicantDept => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ApplicantDept);
        public string ApplyDateDisplay => _application.ApplyTime == default
            ? "(无)"
            : _application.ApplyTime.ToString("yyyy-MM-dd");
        public string ExpectedReturnDateDisplay =>
            HardDiskMediaApplicationViewModelHelper.FormatExpectedReturnDateDisplay(
                _application.ApplicationType,
                _application.ExpectedReturnDate);
        public string TargetPersonOrUnit => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.TargetPersonOrUnit);
        public string CurrentLocation => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.CurrentLocation);
        public string TargetLocation => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.TargetLocation);
        public string Reason => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.Reason);
        public string ProofMaterialDisplay =>
            HardDiskOutboundDomainValues.HasProofMaterial(_application.ProofMaterialNote)
                ? HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ProofMaterialNote)
                : HardDiskOutboundDomainValues.ProofMaterialNoneText;
        public string RelatedBatch => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.RelatedBatch);
        public string RelatedArchiveTitle => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.RelatedArchiveTitle);
        public string Remark => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.Remark);

        public string SignedAttachmentStatusText => _application.SignedAttachmentUploaded
            ? $"已上传（{_application.SignedAttachmentUploadedTime?.ToString("yyyy-MM-dd HH:mm") ?? "时间未记录"}，上传人：{HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.SignedAttachmentUploader)}）"
            : "未上传";

        public string DeptHead => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.DeptHead);
        public string DeptHeadDateDisplay => HardDiskMediaApplicationViewModelHelper.FormatDate(_application.DeptHeadDate);
        public string ArchiveRoomHead => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ArchiveRoomHead);
        public string ApprovedTimeDisplay => HardDiskMediaApplicationViewModelHelper.FormatDate(_application.ArchiveRoomHeadDate);
        public string ArchiveDeputyPresident =>
            HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ArchiveDeputyPresident);
        public string ArchiveDeputyPresidentDateDisplay =>
            HardDiskMediaApplicationViewModelHelper.FormatDate(_application.ArchiveDeputyPresidentDate);
        public bool HasArchiveDeputyPresident =>
            !string.IsNullOrWhiteSpace(_application.ArchiveDeputyPresident)
            || _application.ArchiveDeputyPresidentDate.HasValue;
        public string ApprovalOpinion =>
            ApprovalOpinionUniformitySupport.FormatForDisplay(_application.ApprovalOpinion);
        public string ExecutedBy => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ExecutedBy);
        public string ExecutedTimeDisplay => HardDiskMediaApplicationViewModelHelper.FormatDateTime(_application.ExecutedTime);
        public string PrintInfoDisplay => _application.PrintCount > 0
            ? $"已打印 {_application.PrintCount} 次（最近：{HardDiskMediaApplicationViewModelHelper.FormatDateTime(_application.PrintedTime)}）"
            : "未打印";

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public bool CanPrint => _application.Id > 0;

        /// <summary>办结后，资料管理员可增补其他附件。</summary>
        public bool CanSupplementOtherAttachments =>
            _application.Id > 0
            && _application.ApplicationStatus == HardDiskMediaApplication.StatusCompleted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public string SupplementAttachmentHint =>
            "办结后仅可增补「其他附件」；请确保文件命名清晰准确。本页不可删除已有附件。";

        public ICommand ViewAttachmentCommand { get; }
        public ICommand SupplementOtherAttachmentCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand CloseCommand { get; }

        public event Action<bool?>? RequestClose;

        private async Task InitializeAsync()
        {
            await EnsureMediumLoadedAsync();
            await LoadAttachmentsAsync();
            OnPropertyChanged(nameof(CanSupplementOtherAttachments));
            OnPropertyChanged(nameof(WorkspaceBannerText));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async Task EnsureMediumLoadedAsync()
        {
            if (!string.IsNullOrWhiteSpace(_application.Medium?.DiskCode))
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

            if (refreshed == null)
            {
                return;
            }

            _application.ApplicationStatus = refreshed.ApplicationStatus;
            _application.ProofMaterialNote = refreshed.ProofMaterialNote;
            _application.SignedAttachmentUploaded = refreshed.SignedAttachmentUploaded;
            _application.SignedAttachmentUploadedTime = refreshed.SignedAttachmentUploadedTime;
            _application.SignedAttachmentUploader = refreshed.SignedAttachmentUploader;
            _application.PrintCount = refreshed.PrintCount;
            _application.PrintedTime = refreshed.PrintedTime;
            if (refreshed.Medium != null)
            {
                _application.Medium = refreshed.Medium;
                OnPropertyChanged(nameof(DiskCode));
            }

            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ApplicationStatus));
            OnPropertyChanged(nameof(SignedAttachmentStatusText));
            OnPropertyChanged(nameof(ProofMaterialDisplay));
            OnPropertyChanged(nameof(PrintInfoDisplay));
        }

        private async Task LoadAttachmentsAsync()
        {
            Attachments.Clear();
            if (string.IsNullOrWhiteSpace(_application.ApplicationNo))
            {
                return;
            }

            var attachments = await _hardDiskMediaService.GetApplicationAttachmentsAsync(_application.ApplicationNo);
            foreach (var attachment in attachments)
            {
                Attachments.Add(attachment);
            }
        }

        private async Task SupplementOtherAttachmentAsync()
        {
            if (!CanSupplementOtherAttachments || _userContextService.CurrentUser == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = $"请选择其他附件（{SystemAttachmentUploadSupport.AllowedFormatsDescription}）；请确保文件命名清晰准确",
                Filter = SystemAttachmentUploadSupport.OpenFileDialogFilter
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            bool anySuccess = false;
            foreach (string path in dialog.FileNames)
            {
                try
                {
                    var fileInfo = new FileInfo(path);
                    var fileContent = await File.ReadAllBytesAsync(path);
                    var uploadResult = await _hardDiskMediaService.UploadApplicationAttachmentAsync(
                        _application,
                        _userContextService.CurrentUser,
                        HardDiskOutboundDomainValues.AttachmentCategoryOther,
                        fileInfo.Name,
                        fileInfo.Extension,
                        fileInfo.Length,
                        fileContent);
                    if (uploadResult.Success)
                    {
                        anySuccess = true;
                    }
                    else
                    {
                        _dialogService.ShowMessage(uploadResult.Message);
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError($"增补附件失败：{ex.Message}");
                }
            }

            if (anySuccess)
            {
                await LoadAttachmentsAsync();
                _dialogService.ShowMessage("其他附件已增补。", "增补附件");
            }
        }

        private async Task ViewAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            try
            {
                var result = await _hardDiskMediaService.PrepareApplicationAttachmentViewAsync(attachment);
                if (!result.Success || result.Attachment?.FileContent == null)
                {
                    _dialogService.ShowMessage(result.Message);
                    return;
                }

                _dialogService.ShowSystemAttachmentView(result.Attachment);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查看附件失败：{ex.Message}");
            }
        }

        private async Task PrintAsync()
        {
            if (!CanPrint)
            {
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
                await RefreshPrintInfoAsync();
                previewWindow.ShowDialog();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"打印失败：{ex.Message}");
            }
        }

        private async Task RefreshPrintInfoAsync()
        {
            var applications = await _hardDiskMediaService.SearchApplicationsAsync(_application.ApplicationNo, null, null);
            var refreshed = applications.FirstOrDefault(item => item.Id == _application.Id)
                ?? applications.FirstOrDefault(item =>
                    string.Equals(item.ApplicationNo, _application.ApplicationNo, StringComparison.OrdinalIgnoreCase));
            if (refreshed == null)
            {
                return;
            }

            _application.PrintCount = refreshed.PrintCount;
            _application.PrintedTime = refreshed.PrintedTime;
            OnPropertyChanged(nameof(PrintInfoDisplay));
        }
    }
}
