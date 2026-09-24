using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子资料盘库登记窗：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
    /// </summary>
    public partial class ElectronicArchiveInventoryRegisterEditDialogViewModel
    {
        public string WorkspaceBannerText => BannerText;

        public bool CanApprovePass => CanApprove;

        public ICommand ConfirmMidStepCommand => ConfirmUploadCommand;

        public string ConfirmMidStepHintText => ConfirmUploadHintText;

        public string UploadHintText => UploadAttachmentHintText;

        public ObservableCollection<SystemAttachment> SignedAttachments => SignedFormAttachments;

        public ObservableCollection<SystemAttachment> PhotoAttachments { get; } = new();

        public ObservableCollection<SystemAttachment> ProofAttachments { get; } = new();

        public string PhotoZoneTitle => ApprovalWorkflowShellCopySupport.MaterialPhotoZoneTitle;

        public string PhotoZoneHint => "盘库登记无需上传资料照片。";

        public string ProofZoneHint => "本业务无需上传证明材料。";

        public bool RequiresPhotoAttachment => false;

        public bool RequiresProofAttachment => false;

        public bool CanUploadSignedAttachment => CanUploadMandatoryAttachment;

        public bool CanUploadPhotoAttachment => false;

        public bool CanUploadProofAttachment => false;

        public ICommand UploadSignedCommand => UploadSignedFormAttachmentCommand;

        public ICommand CaptureSignedCommand => CaptureSignedFormAttachmentCommand;

        public ICommand? UploadPhotoCommand => null;

        public ICommand? CapturePhotoCommand => null;

        public ICommand? UploadProofCommand => null;

        public ICommand? CaptureProofCommand => null;

        public ICommand UploadOtherCommand => UploadOtherAttachmentCommand;

        public ICommand CaptureOtherCommand => CaptureOtherAttachmentCommand;

        /// <summary>撤回在列表页办理；编辑窗不提供撤回按钮。</summary>
        public bool CanWithdraw => false;

        public ICommand? WithdrawCommand => null;

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
            OnPropertyChanged(nameof(CanUploadOtherAttachment));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(PrintHintText));
            OnPropertyChanged(nameof(CanEditSigners));
            OnPropertyChanged(nameof(SignatureDateMin));
        }
    }
}
