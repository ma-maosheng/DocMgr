using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质申请单只读查看弹窗 ViewModel。
    /// </summary>
    public sealed class HardDiskMediaApplicationViewDialogViewModel : ViewModelBase
    {
        private readonly HardDiskMediaApplication _application;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;

        public HardDiskMediaApplicationViewDialogViewModel(
            IHardDiskMediaService hardDiskMediaService,
            IDialogService dialogService,
            HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(hardDiskMediaService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(application);

            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _application = application;

            ViewAttachmentCommand = new RelayCommand(async attachment => await ViewAttachmentAsync(attachment as SystemAttachment), attachment => attachment is SystemAttachment);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public string WindowTitle => $"查看申请 · {_application.ApplicationNo} · {_application.StatusStr}";

        public string WorkspaceBannerText => "本窗口仅用于查看申请单信息，不允许编辑。";

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
        public string RelatedBatch => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.RelatedBatch);
        public string RelatedArchiveTitle => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.RelatedArchiveTitle);
        public string Remark => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.Remark);

        public string SignedAttachmentStatusText => _application.SignedAttachmentUploaded
            ? $"已上传（{_application.SignedAttachmentUploadedTime?.ToString("yyyy-MM-dd HH:mm") ?? "时间未记录"}，上传人：{HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.SignedAttachmentUploader)}）"
            : "未上传";

        public string ReviewerName => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ReviewerName);
        public string ReviewerDateDisplay => HardDiskMediaApplicationViewModelHelper.FormatDate(_application.ReviewerDate);
        public string ApprovedBy => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ApprovedBy);
        public string ApprovedTimeDisplay => HardDiskMediaApplicationViewModelHelper.FormatDate(_application.ApprovedTime);
        public string ApprovalOpinion =>
            ApprovalOpinionUniformitySupport.FormatForDisplay(_application.ApprovalOpinion);
        public string ExecutedBy => HardDiskMediaApplicationViewModelHelper.EmptyAsPlaceholder(_application.ExecutedBy);
        public string ExecutedTimeDisplay => HardDiskMediaApplicationViewModelHelper.FormatDateTime(_application.ExecutedTime);
        public string PrintInfoDisplay => _application.PrintCount > 0
            ? $"已打印 {_application.PrintCount} 次（最近：{HardDiskMediaApplicationViewModelHelper.FormatDateTime(_application.PrintedTime)}）"
            : "未打印";

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public bool CanPrint => _application.Id > 0;

        public ICommand ViewAttachmentCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand CloseCommand { get; }

        public event Action<bool?>? RequestClose;

        private async Task InitializeAsync()
        {
            await EnsureMediumLoadedAsync();
            await LoadAttachmentsAsync();
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

            if (refreshed?.Medium == null)
            {
                return;
            }

            _application.Medium = refreshed.Medium;
            OnPropertyChanged(nameof(DiskCode));
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
        }

        private async Task RefreshPrintInfoAsync()
        {
            if (_application.Id <= 0)
            {
                return;
            }

            var applications = await _hardDiskMediaService.SearchApplicationsAsync(_application.ApplicationNo, null, null);
            var refreshed = applications.FirstOrDefault(item => item.Id == _application.Id);
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
