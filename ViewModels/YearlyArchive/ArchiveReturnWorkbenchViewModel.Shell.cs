using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive;

/// <summary>
/// 资料归还工作台：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
/// </summary>
public sealed partial class ArchiveReturnWorkbenchViewModel
{
    private readonly ObservableCollection<SystemAttachment> _shellPhotoAttachments = new();
    private readonly ObservableCollection<SystemAttachment> _shellProofAttachments = new();
    private readonly ObservableCollection<SystemAttachment> _shellOtherAttachments = new();
    private RelayCommand? _shellViewAttachmentCommand;
    private RelayCommand? _shellDeleteAttachmentCommand;

    /// <summary>壳：窗口/分区标题（嵌入态标题栏隐藏，仍供壳绑定）。</summary>
    public string WindowTitle => PageTitle;

    /// <summary>壳：顶部流程横幅。</summary>
    public string WorkspaceBannerText =>
        ApprovalWorkflowShellCopySupport.GetWorkspaceBannerText(ApprovalWorkflowShellKind.ApplicationHandover);

    /// <summary>壳：中间解锁步骤（确认实物交接）。</summary>
    public ICommand ConfirmMidStepCommand => ConfirmHandoverCommand;

    /// <summary>壳：中间步骤提示。</summary>
    public string ConfirmMidStepHintText => ConfirmHandoverHintText;

    /// <summary>壳：照片附件（归还不启用）。</summary>
    public ObservableCollection<SystemAttachment> PhotoAttachments => _shellPhotoAttachments;

    /// <summary>壳：证明材料（归还不启用）。</summary>
    public ObservableCollection<SystemAttachment> ProofAttachments => _shellProofAttachments;

    /// <summary>壳：其他附件（办结后增补；由 LoadAttachmentsAsync 按分类装入）。</summary>
    public ObservableCollection<SystemAttachment> OtherAttachments => _shellOtherAttachments;

    /// <summary>壳：部门审核卡（完好归还或灭失均显示）。</summary>
    public bool ShowDeptHead => ShowIntactApprovalSigner || ShowLossApprovalSigners;

    /// <summary>壳：资料室签字卡（仅灭失）。</summary>
    public bool ShowArchiveRoomHead => ShowLossApprovalSigners;

    /// <summary>壳：生产科签字卡（仅灭失）。</summary>
    public bool ShowProductionHead => ShowLossApprovalSigners;

    /// <summary>壳：分管资料院长签字卡（仅灭失）。</summary>
    public bool ShowArchiveDeputyPresident => ShowLossApprovalSigners;

    /// <summary>壳：分管生产院长签字卡（仅灭失）。</summary>
    public bool ShowProductionVicePresident => ShowLossApprovalSigners;

    /// <summary>壳：审核区分组标题。</summary>
    public bool ShowReviewSignerSection => ShowDeptHead || ShowArchiveRoomHead || ShowProductionHead;

    /// <summary>壳：审批区分组标题。</summary>
    public bool ShowApproveSignerSection => ShowArchiveDeputyPresident || ShowProductionVicePresident;

    /// <summary>壳：生产科签字（对标 ProductionHeadName）。</summary>
    public string ProductionHead
    {
        get => ProductionHeadName;
        set => ProductionHeadName = value;
    }

    /// <summary>壳：分管资料院长签字（对标 ArchiveDeputyPresidentName）。</summary>
    public string ArchiveDeputyPresident
    {
        get => ArchiveDeputyPresidentName;
        set => ArchiveDeputyPresidentName = value;
    }

    /// <summary>壳：分管生产院长签字（对标 ProductionVicePresidentName）。</summary>
    public string ProductionVicePresident
    {
        get => ProductionVicePresidentName;
        set => ProductionVicePresidentName = value;
    }

    /// <summary>壳：照片区标题。</summary>
    public string PhotoZoneTitle => ApprovalWorkflowShellCopySupport.PhysicalPhotoZoneTitle;

    /// <summary>壳：照片区提示。</summary>
    public string PhotoZoneHint => "资料归还无需上传资料照片。";

    /// <summary>壳：证明材料区提示。</summary>
    public string ProofZoneHint => "资料归还无需上传证明材料。";

    /// <summary>壳：是否须上传照片。</summary>
    public bool RequiresPhotoAttachment => false;

    /// <summary>壳：是否须上传证明材料。</summary>
    public bool RequiresProofAttachment => false;

    /// <summary>壳：可否上传照片。</summary>
    public bool CanUploadPhotoAttachment => false;

    /// <summary>壳：可否上传证明材料。</summary>
    public bool CanUploadProofAttachment => false;

    /// <summary>壳：可否上传其他附件（办结后增补）。</summary>
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

    /// <summary>壳：查看附件（带参数）。</summary>
    public ICommand ViewAttachmentCommand =>
        _shellViewAttachmentCommand ??= new RelayCommand(
            async param =>
            {
                if (param is not SystemAttachment attachment)
                {
                    return;
                }

                SelectedSignedAttachment = attachment;
                await ViewSignedAttachmentAsync();
            });

    /// <summary>壳：删除附件（带参数）。</summary>
    public ICommand DeleteAttachmentCommand =>
        _shellDeleteAttachmentCommand ??= new RelayCommand(
            async param =>
            {
                if (param is not SystemAttachment attachment)
                {
                    return;
                }

                SelectedSignedAttachment = attachment;
                await DeleteSignedAttachmentAsync();
            },
            param => param is SystemAttachment && CanDeleteSignedAttachment);

    /// <summary>壳：打印交接单。</summary>
    public ICommand PrintCommand => PrintHandoverSheetCommand;

    /// <summary>壳：打印提示。</summary>
    public string PrintHintText => CanPrintHandoverSheet
        ? "可打印资料归还交接单。"
        : "当前状态不允许打印交接单。";

    /// <summary>壳：关闭。</summary>
    public ICommand CloseCommand => CancelEditCommand;

    /// <summary>壳底栏草稿/提交/撤回：审批嵌入区不启用。</summary>
    public bool CanEditHeader => false;

    /// <summary>壳：撤回可用性（强制作废仍走外层按钮）。</summary>
    public bool CanWithdraw => false;

    /// <summary>壳：撤回占位。</summary>
    public ICommand? WithdrawCommand => null;

    /// <summary>壳：提交占位（申请提交仍走外层）。</summary>
    public ICommand? SubmitCommand => null;

    /// <summary>壳：可否编辑签字。</summary>
    public bool CanEditSigners => CanApprove;

    /// <summary>壳：签字日期下限。</summary>
    public DateTime? SignatureDateMin =>
        ApprovalSignatureDateSupport.ResolveMinDate(
            EditingRecord?.FirstPrintedAt,
            EditingRecord?.LastPrintedAt,
            EditingRecord?.PrintCount ?? 0);

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
        OnPropertyChanged(nameof(ProductionHead));
        OnPropertyChanged(nameof(ArchiveDeputyPresident));
        OnPropertyChanged(nameof(ProductionVicePresident));
    }
}
