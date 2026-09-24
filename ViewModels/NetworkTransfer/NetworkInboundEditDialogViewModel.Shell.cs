using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 入网编辑弹窗：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
/// </summary>
public sealed partial class NetworkInboundEditDialogViewModel
{
    /// <summary>壳：审批通过命令。</summary>
    public ICommand ApproveCommand => SaveApprovalCommand;

    /// <summary>壳：中间解锁步骤（确认实物交接）。</summary>
    public ICommand ConfirmMidStepCommand => ConfirmPhysicalHandoverCommand;

    /// <summary>壳：中间步骤提示。</summary>
    public string ConfirmMidStepHintText => ConfirmHandoverHintText;

    /// <summary>壳：签批附件集合。</summary>
    public ObservableCollection<SystemAttachment> SignedAttachments => SignedHandoverAttachments;

    /// <summary>壳：照片附件集合。</summary>
    public ObservableCollection<SystemAttachment> PhotoAttachments => MaterialPhotoAttachments;

    /// <summary>壳：证明材料集合。</summary>
    public ObservableCollection<SystemAttachment> ProofAttachments => ProofMaterialAttachments;

    /// <summary>壳：照片区标题。</summary>
    public string PhotoZoneTitle => ApprovalWorkflowShellCopySupport.MaterialPhotoZoneTitle;

    /// <summary>壳：照片区提示。</summary>
    public string PhotoZoneHint => "入网业务无需上传资料照片。";

    /// <summary>壳：证明材料区提示。</summary>
    public string ProofZoneHint => ProofMaterialAttachmentHint;

    /// <summary>壳：是否须上传照片。</summary>
    public bool RequiresPhotoAttachment => false;

    /// <summary>壳：是否须上传证明材料。</summary>
    public bool RequiresProofAttachment => RequiresProofMaterialScanUpload;

    /// <summary>壳：可否上传照片。</summary>
    public bool CanUploadPhotoAttachment => false;

    /// <summary>壳：可否上传证明材料。</summary>
    public bool CanUploadProofAttachment => CanUploadProofMaterialAttachment;

    /// <summary>壳：上传签批。</summary>
    public ICommand UploadSignedCommand => UploadSignedHandoverAttachmentCommand;

    /// <summary>壳：高影仪签批。</summary>
    public ICommand CaptureSignedCommand => CaptureSignedHandoverAttachmentCommand;

    /// <summary>壳：上传照片（入网不启用）。</summary>
    public ICommand? UploadPhotoCommand => null;

    /// <summary>壳：高影仪照片（入网不启用）。</summary>
    public ICommand? CapturePhotoCommand => null;

    /// <summary>壳：上传证明材料。</summary>
    public ICommand UploadProofCommand => UploadProofMaterialAttachmentCommand;

    /// <summary>壳：高影仪证明材料。</summary>
    public ICommand CaptureProofCommand => CaptureProofMaterialAttachmentCommand;

    /// <summary>壳：上传其他。</summary>
    public ICommand UploadOtherCommand => UploadOtherAttachmentCommand;

    /// <summary>壳：高影仪其他。</summary>
    public ICommand CaptureOtherCommand => CaptureOtherAttachmentCommand;

    /// <summary>壳：打印。</summary>
    public ICommand PrintCommand => PrintApprovalCommand;

    /// <summary>壳：提交占位（草稿提交仍走外层底栏）。</summary>
    public ICommand? SubmitCommand => null;

    /// <summary>壳：撤回占位（本窗不启用）。</summary>
    public ICommand? WithdrawCommand => null;

    /// <summary>壳：撤回可用性（本窗不启用）。</summary>
    public bool CanWithdraw => false;

    /// <summary>壳：部门审核签字卡。</summary>
    public bool ShowDeptHead => EnableDeptHead;

    /// <summary>壳：资料室签字卡。</summary>
    public bool ShowArchiveRoomHead => EnableArchiveRoomHead;

    /// <summary>壳：生产科签字卡。</summary>
    public bool ShowProductionHead => EnableProductionHead;

    /// <summary>壳：分管资料院长签字卡。</summary>
    public bool ShowArchiveDeputyPresident => EnableArchiveDeputyPresident;

    /// <summary>壳：分管生产院长签字卡。</summary>
    public bool ShowProductionVicePresident => EnableProductionVicePresident;

    /// <summary>壳：审核区分组标题。</summary>
    public bool ShowReviewSignerSection =>
        EnableDeptHead || EnableArchiveRoomHead || EnableProductionHead;

    /// <summary>壳：审批区分组标题。</summary>
    public bool ShowApproveSignerSection =>
        EnableArchiveDeputyPresident || EnableProductionVicePresident;

    /// <summary>壳：可否编辑签字。</summary>
    public bool CanEditSigners => CanApprovePass;

    /// <summary>壳：签字日期下限。</summary>
    public DateTime? SignatureDateMin =>
        ApprovalSignatureDateSupport.ResolveMinDate(_record.FirstPrintedAt, _record.LastPrintedAt, _record.PrintCount);

    private void NotifyShellAliasPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConfirmMidStepHintText));
        OnPropertyChanged(nameof(PhotoZoneHint));
        OnPropertyChanged(nameof(ProofZoneHint));
        OnPropertyChanged(nameof(RequiresPhotoAttachment));
        OnPropertyChanged(nameof(RequiresProofAttachment));
        OnPropertyChanged(nameof(CanUploadPhotoAttachment));
        OnPropertyChanged(nameof(CanUploadProofAttachment));
        OnPropertyChanged(nameof(CanUploadSignedAttachment));
        OnPropertyChanged(nameof(CanUploadOtherAttachment));
        OnPropertyChanged(nameof(ApproveHintText));
        OnPropertyChanged(nameof(UploadHintText));
        OnPropertyChanged(nameof(CompleteHintText));
        OnPropertyChanged(nameof(PrintHintText));
        OnPropertyChanged(nameof(CanEditSigners));
        OnPropertyChanged(nameof(SignatureDateMin));
        OnPropertyChanged(nameof(ShowDeptHead));
        OnPropertyChanged(nameof(ShowArchiveRoomHead));
        OnPropertyChanged(nameof(ShowProductionHead));
        OnPropertyChanged(nameof(ShowArchiveDeputyPresident));
        OnPropertyChanged(nameof(ShowProductionVicePresident));
        OnPropertyChanged(nameof(ShowReviewSignerSection));
        OnPropertyChanged(nameof(ShowApproveSignerSection));
    }
}
