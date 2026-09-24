using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 年度资料离库处置窗：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
    /// </summary>
    public partial class ArchiveDisposalEditDialogViewModel
    {
        public string WorkspaceBannerText => BannerText;

        public bool CanApprovePass => CanApprove;

        public ICommand ConfirmMidStepCommand => ConfirmUploadCommand;

        public string ConfirmMidStepHintText => ConfirmUploadHintText;

        public string UploadHintText => UploadAttachmentHintText;

        public ObservableCollection<SystemAttachment> SignedAttachments => SignedFormAttachments;

        public ObservableCollection<SystemAttachment> PhotoAttachments => ScenePhotoAttachments;

        public ObservableCollection<SystemAttachment> ProofAttachments { get; } = new();

        public string PhotoZoneTitle => ApprovalWorkflowShellCopySupport.DisposalMaterialPhotoZoneTitle;

        public string PhotoZoneHint => RequiresScenePhoto
            ? "离库销毁办结前须上传处置资料照片。"
            : "本单无需上传处置资料照片。";

        public string ProofZoneHint => "本业务无需上传证明材料。";

        public bool RequiresPhotoAttachment => RequiresScenePhoto;

        public bool RequiresProofAttachment => false;

        public bool CanUploadSignedAttachment => CanUploadMandatoryAttachment;

        public bool CanUploadPhotoAttachment => CanUploadMandatoryAttachment && RequiresScenePhoto;

        public bool CanUploadProofAttachment => false;

        public ICommand UploadSignedCommand => UploadSignedFormAttachmentCommand;

        public ICommand CaptureSignedCommand => CaptureSignedFormAttachmentCommand;

        public ICommand UploadPhotoCommand => UploadScenePhotoAttachmentCommand;

        public ICommand CapturePhotoCommand => CaptureScenePhotoAttachmentCommand;

        public ICommand? UploadProofCommand => null;

        public ICommand? CaptureProofCommand => null;

        public ICommand UploadOtherCommand => UploadOtherAttachmentCommand;

        public ICommand CaptureOtherCommand => CaptureOtherAttachmentCommand;

        public string PrintHintText => CanPrint
            ? "可打印签批单（空白表单供线下签字，或办结后备查）。"
            : "当前状态不允许打印签批单。";

        private void NotifyShellAliasPropertiesChanged()
        {
            OnPropertyChanged(nameof(WorkspaceBannerText));
            OnPropertyChanged(nameof(CanApprovePass));
            OnPropertyChanged(nameof(ConfirmMidStepHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(PhotoZoneHint));
            OnPropertyChanged(nameof(RequiresPhotoAttachment));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(CanUploadPhotoAttachment));
            OnPropertyChanged(nameof(CanUploadOtherAttachment));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(PrintHintText));
            OnPropertyChanged(nameof(CanEditSigners));
            OnPropertyChanged(nameof(SignatureDateMin));
        }
    }
}
