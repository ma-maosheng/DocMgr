using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘出库审批窗：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
    /// </summary>
    public partial class HardDiskMediaApprovalEditDialogViewModel
    {
        /// <summary>壳：审批通过命令。</summary>
        public ICommand ApproveCommand => ConfirmCommand;

        /// <summary>壳：审批通过提示。</summary>
        public string ApproveHintText => ConfirmHintText;

        /// <summary>壳：中间解锁步骤（确认实物交接）。</summary>
        public ICommand ConfirmMidStepCommand => ConfirmPhysicalHandoverCommand;

        /// <summary>壳：中间步骤提示。</summary>
        public string ConfirmMidStepHintText => ConfirmPhysicalHandoverHintText;

        /// <summary>壳：签批附件集合。</summary>
        public ObservableCollection<SystemAttachment> SignedAttachments => SignedHandoverAttachments;

        /// <summary>壳：照片附件集合。</summary>
        public ObservableCollection<SystemAttachment> PhotoAttachments => PhysicalPhotoAttachments;

        /// <summary>壳：证明材料集合。</summary>
        public ObservableCollection<SystemAttachment> ProofAttachments => ProofMaterialAttachments;

        /// <summary>壳：照片区标题。</summary>
        public string PhotoZoneTitle => ApprovalWorkflowShellCopySupport.PhysicalPhotoZoneTitle;

        /// <summary>壳：照片区提示。</summary>
        public string PhotoZoneHint => PhysicalPhotoAttachmentHint;

        /// <summary>壳：证明材料区提示。</summary>
        public string ProofZoneHint => ProofMaterialAttachmentHint;

        /// <summary>壳：是否须上传照片。</summary>
        public bool RequiresPhotoAttachment => RequiresPhysicalPhotoAttachment;

        /// <summary>壳：是否须上传证明材料。</summary>
        public bool RequiresProofAttachment => RequiresProofMaterialAttachment;

        /// <summary>壳：可否上传照片。</summary>
        public bool CanUploadPhotoAttachment => CanUploadPhysicalPhotoAttachment;

        /// <summary>壳：可否上传证明材料。</summary>
        public bool CanUploadProofAttachment => CanUploadProofMaterialAttachment;

        /// <summary>壳：可否上传其他附件。</summary>
        public bool CanUploadOtherAttachment => CanUploadSignedAttachment;

        /// <summary>壳：上传签批。</summary>
        public ICommand UploadSignedCommand => UploadSignedAttachmentCommand;

        /// <summary>壳：高影仪签批。</summary>
        public ICommand CaptureSignedCommand => CaptureSignedAttachmentCommand;

        /// <summary>壳：上传照片。</summary>
        public ICommand UploadPhotoCommand => UploadPhysicalPhotoCommand;

        /// <summary>壳：高影仪照片。</summary>
        public ICommand CapturePhotoCommand => CapturePhysicalPhotoCommand;

        /// <summary>壳：上传证明材料。</summary>
        public ICommand UploadProofCommand => UploadProofMaterialCommand;

        /// <summary>壳：高影仪证明材料。</summary>
        public ICommand CaptureProofCommand => CaptureProofMaterialCommand;

        /// <summary>壳：上传其他。</summary>
        public ICommand UploadOtherCommand => UploadOtherAttachmentCommand;

        /// <summary>壳：高影仪其他。</summary>
        public ICommand CaptureOtherCommand => CaptureOtherAttachmentCommand;

        /// <summary>壳：打印。</summary>
        public ICommand PrintCommand => PrintHandoverSheetCommand;

        /// <summary>壳：关闭。</summary>
        public ICommand CloseCommand => CancelCommand;

        /// <summary>申请审批窗不提供草稿/提交/撤回底栏。</summary>
        public bool CanEditHeader => false;

        /// <summary>申请审批窗不提供提交。</summary>
        public bool CanSubmit => false;

        /// <summary>申请审批窗不提供撤回。</summary>
        public bool CanWithdraw => false;

        /// <summary>占位：壳绑定用（本窗不启用）。</summary>
        public ICommand? SaveDraftCommand => null;

        /// <summary>占位：壳绑定用（本窗不启用）。</summary>
        public ICommand? SubmitCommand => null;

        /// <summary>占位：壳绑定用（本窗不启用）。</summary>
        public ICommand? WithdrawCommand => null;

        private void NotifyShellAliasPropertiesChanged()
        {
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(ConfirmMidStepHintText));
            OnPropertyChanged(nameof(PhotoZoneHint));
            OnPropertyChanged(nameof(ProofZoneHint));
            OnPropertyChanged(nameof(RequiresPhotoAttachment));
            OnPropertyChanged(nameof(RequiresProofAttachment));
            OnPropertyChanged(nameof(CanUploadPhotoAttachment));
            OnPropertyChanged(nameof(CanUploadProofAttachment));
            OnPropertyChanged(nameof(CanUploadOtherAttachment));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(PrintHintText));
            OnPropertyChanged(nameof(CanEditSigners));
            OnPropertyChanged(nameof(SignatureDateMin));
        }
    }
}
