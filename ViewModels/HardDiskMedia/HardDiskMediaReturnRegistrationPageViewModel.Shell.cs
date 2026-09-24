using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.HardDiskMedia;

/// <summary>
/// 硬盘归还登记页：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
/// </summary>
public partial class HardDiskMediaReturnRegistrationPageViewModel
{
    private readonly ObservableCollection<SystemAttachment> _shellPhotoAttachments = new();
    private readonly ObservableCollection<SystemAttachment> _shellProofAttachments = new();
    private readonly ObservableCollection<SystemAttachment> _shellOtherAttachments = new();

    /// <summary>壳：窗口/分区标题（嵌入态标题栏隐藏，仍供壳绑定）。</summary>
    public string WindowTitle => PageTitle;

    /// <summary>壳：顶部流程横幅。</summary>
    public string WorkspaceBannerText =>
        ApprovalWorkflowShellCopySupport.GetWorkspaceBannerText(ApprovalWorkflowShellKind.ApplicationHandover);

    /// <summary>壳：中间解锁步骤（确认实物交接）。</summary>
    public ICommand ConfirmMidStepCommand => ConfirmHandoverCommand;

    /// <summary>壳：中间步骤提示。</summary>
    public string ConfirmMidStepHintText => ConfirmHandoverHintText;

    /// <summary>壳底栏提交：审批嵌入区不启用（申请提交仍走外层 SubmitCommand）。</summary>
    public bool CanSubmit => false;

    /// <summary>壳：签批附件集合。</summary>
    public ObservableCollection<SystemAttachment> SignedAttachments => Attachments;

    /// <summary>壳：照片附件（归还不启用）。</summary>
    public ObservableCollection<SystemAttachment> PhotoAttachments => _shellPhotoAttachments;

    /// <summary>壳：证明材料（归还不启用）。</summary>
    public ObservableCollection<SystemAttachment> ProofAttachments => _shellProofAttachments;

    /// <summary>壳：其他附件（办结后增补；由 LoadEditorAttachmentsAsync 按分类装入）。</summary>
    public ObservableCollection<SystemAttachment> OtherAttachments => _shellOtherAttachments;

    /// <summary>壳：照片区标题。</summary>
    public string PhotoZoneTitle => ApprovalWorkflowShellCopySupport.PhysicalPhotoZoneTitle;

    /// <summary>壳：照片区提示。</summary>
    public string PhotoZoneHint => "硬盘归还无需上传实物照片。";

    /// <summary>壳：证明材料区提示。</summary>
    public string ProofZoneHint => "硬盘归还不按证明材料分区上传。";

    /// <summary>壳：是否须上传照片。</summary>
    public bool RequiresPhotoAttachment => false;

    /// <summary>壳：是否须上传证明材料。</summary>
    public bool RequiresProofAttachment => false;

    /// <summary>壳：可否上传照片。</summary>
    public bool CanUploadPhotoAttachment => false;

    /// <summary>壳：可否上传证明材料。</summary>
    public bool CanUploadProofAttachment => false;

    /// <summary>壳：可否上传其他附件。</summary>
    public bool CanUploadOtherAttachment => CanSupplementOtherAttachments;

    /// <summary>壳：上传签批。</summary>
    public ICommand UploadSignedCommand => UploadSignedAttachmentCommand;

    /// <summary>壳：高影仪签批。</summary>
    public ICommand CaptureSignedCommand => CaptureSignedAttachmentCommand;

    /// <summary>壳：上传照片（不启用）。</summary>
    public ICommand? UploadPhotoCommand => null;

    /// <summary>壳：高影仪照片（不启用）。</summary>
    public ICommand? CapturePhotoCommand => null;

    /// <summary>壳：上传证明材料（不启用）。</summary>
    public ICommand? UploadProofCommand => null;

    /// <summary>壳：高影仪证明材料（不启用）。</summary>
    public ICommand? CaptureProofCommand => null;

    /// <summary>壳：上传其他。</summary>
    public ICommand UploadOtherCommand => SupplementOtherAttachmentCommand;

    /// <summary>壳：高影仪其他。</summary>
    public ICommand CaptureOtherCommand => CaptureOtherAttachmentCommand;

    /// <summary>壳：打印交接单。</summary>
    public ICommand PrintCommand => PrintHandoverSheetCommand;

    /// <summary>壳：关闭。</summary>
    public ICommand CloseCommand => CancelEditCommand;

    /// <summary>壳底栏草稿/提交/撤回：审批嵌入区不启用（申请侧按钮仍走外层）。</summary>
    public bool CanEditHeader => false;

    /// <summary>壳：撤回可用性（强制作废仍走外层按钮）。</summary>
    public bool CanWithdraw => false;

    /// <summary>壳：部门审核卡。</summary>
    public bool ShowDeptHead => EnableDeptHead;

    /// <summary>壳：资料室签字卡。</summary>
    public bool ShowArchiveRoomHead => EnableArchiveRoomHead;

    /// <summary>壳：生产科签字卡（硬盘归还不启用）。</summary>
    public bool ShowProductionHead => false;

    /// <summary>壳：分管资料院长签字卡。</summary>
    public bool ShowArchiveDeputyPresident => EnableArchiveDeputyPresident;

    /// <summary>壳：分管生产院长签字卡（硬盘归还不启用）。</summary>
    public bool ShowProductionVicePresident => false;

    /// <summary>壳：审核区分组标题。</summary>
    public bool ShowReviewSignerSection => EnableDeptHead || EnableArchiveRoomHead;

    /// <summary>壳：审批区分组标题。</summary>
    public bool ShowApproveSignerSection => EnableArchiveDeputyPresident;

    /// <summary>壳：可否编辑签字。</summary>
    public bool CanEditSigners => CanApprove;

    /// <summary>壳：签字日期下限。</summary>
    public DateTime? SignatureDateMin =>
        ApprovalSignatureDateSupport.ResolveMinDate(
            _editingApplication?.FirstPrintedAt,
            _editingApplication?.PrintedTime,
            _editingApplication?.PrintCount ?? 0);

    /// <summary>壳：生产科签字占位。</summary>
    public string ProductionHead
    {
        get => string.Empty;
        set { }
    }

    /// <summary>壳：生产科签字日期占位。</summary>
    public DateTime? ProductionHeadDate
    {
        get => null;
        set { }
    }

    /// <summary>壳：分管生产院长签字占位。</summary>
    public string ProductionVicePresident
    {
        get => string.Empty;
        set { }
    }

    /// <summary>壳：分管生产院长签字日期占位。</summary>
    public DateTime? ProductionVicePresidentDate
    {
        get => null;
        set { }
    }

    private void NotifyShellAliasPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConfirmMidStepHintText));
        OnPropertyChanged(nameof(PhotoZoneHint));
        OnPropertyChanged(nameof(ProofZoneHint));
        OnPropertyChanged(nameof(RequiresPhotoAttachment));
        OnPropertyChanged(nameof(RequiresProofAttachment));
        OnPropertyChanged(nameof(CanUploadPhotoAttachment));
        OnPropertyChanged(nameof(CanUploadProofAttachment));
        OnPropertyChanged(nameof(CanUploadOtherAttachment));
        OnPropertyChanged(nameof(CanUploadSignedAttachment));
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
