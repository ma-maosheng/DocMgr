using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘离库处置窗：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
    /// </summary>
    public partial class HardDiskDisposalEditDialogViewModel
    {
        /// <summary>壳：顶部流程说明（与 BannerText 同文）。</summary>
        public string WorkspaceBannerText => BannerText;

        /// <summary>壳：审批通过可用性（与 CanApprove 同义）。</summary>
        public bool CanApprovePass => CanApprove;

        /// <summary>壳：中间解锁步骤（确认可上传）。</summary>
        public ICommand ConfirmMidStepCommand => ConfirmUploadCommand;

        /// <summary>壳：中间步骤提示。</summary>
        public string ConfirmMidStepHintText => ConfirmUploadHintText;

        /// <summary>壳：附件上传提示。</summary>
        public string UploadHintText => UploadAttachmentHintText;

        /// <summary>壳：签批附件集合。</summary>
        public ObservableCollection<SystemAttachment> SignedAttachments => SignedFormAttachments;

        /// <summary>壳：照片附件集合。</summary>
        public ObservableCollection<SystemAttachment> PhotoAttachments => DiskPhotoAttachments;

        /// <summary>壳：证明材料集合（本业务为空，区显示占位）。</summary>
        public ObservableCollection<SystemAttachment> ProofAttachments { get; } = new();

        /// <summary>壳：照片区标题。</summary>
        public string PhotoZoneTitle => ApprovalWorkflowShellCopySupport.DiskPhotoZoneTitle;

        /// <summary>壳：照片区提示。</summary>
        public string PhotoZoneHint => string.Empty;

        /// <summary>壳：证明材料区提示。</summary>
        public string ProofZoneHint => "本业务无需上传证明材料。";

        /// <summary>壳：是否须上传照片。</summary>
        public bool RequiresPhotoAttachment => RequiresDiskPhotoAttachment;

        /// <summary>壳：是否须上传证明材料。</summary>
        public bool RequiresProofAttachment => false;

        /// <summary>壳：可否上传签批（必备类）。</summary>
        public bool CanUploadSignedAttachment => CanUploadMandatoryAttachment;

        /// <summary>壳：可否上传照片。</summary>
        public bool CanUploadPhotoAttachment => CanUploadMandatoryAttachment;

        /// <summary>壳：可否上传证明材料。</summary>
        public bool CanUploadProofAttachment => false;

        /// <summary>壳：上传签批。</summary>
        public ICommand UploadSignedCommand => UploadSignedFormAttachmentCommand;

        /// <summary>壳：高影仪签批。</summary>
        public ICommand CaptureSignedCommand => CaptureSignedFormAttachmentCommand;

        /// <summary>壳：上传照片。</summary>
        public ICommand UploadPhotoCommand => UploadDiskPhotoAttachmentCommand;

        /// <summary>壳：高影仪照片。</summary>
        public ICommand CapturePhotoCommand => CaptureDiskPhotoAttachmentCommand;

        /// <summary>壳：证明材料上传（本业务不启用）。</summary>
        public ICommand? UploadProofCommand => null;

        /// <summary>壳：证明材料高影仪（本业务不启用）。</summary>
        public ICommand? CaptureProofCommand => null;

        /// <summary>壳：上传其他。</summary>
        public ICommand UploadOtherCommand => UploadOtherAttachmentCommand;

        /// <summary>壳：高影仪其他。</summary>
        public ICommand CaptureOtherCommand => CaptureOtherAttachmentCommand;

        /// <summary>壳：打印提示。</summary>
        public string PrintHintText => CanPrint
            ? "可打印签批单（空白表单供线下签字，或办结后备查）。"
            : "当前状态不允许打印签批单。";

        private void NotifyShellAliasPropertiesChanged()
        {
            OnPropertyChanged(nameof(WorkspaceBannerText));
            OnPropertyChanged(nameof(CanApprovePass));
            OnPropertyChanged(nameof(ConfirmMidStepHintText));
            OnPropertyChanged(nameof(UploadHintText));
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
