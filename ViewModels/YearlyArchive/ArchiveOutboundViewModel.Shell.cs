using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.YearlyArchive;

/// <summary>
/// 资料出库审批办理面板：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
/// </summary>
public sealed partial class ArchiveOutboundViewModel
{
    /// <summary>壳：顶部流程横幅。</summary>
    public string WorkspaceBannerText => ApprovalWorkspaceBannerText;

    /// <summary>壳：审批通过命令。</summary>
    public ICommand ApproveCommand => SaveApprovalCommand;

    /// <summary>壳：中间解锁步骤（确认实物交接）。</summary>
    public ICommand ConfirmMidStepCommand => ConfirmPhysicalHandoverCommand;

    /// <summary>壳：中间步骤提示。</summary>
    public string ConfirmMidStepHintText => ConfirmHandoverHintText;

    /// <summary>壳：签批附件集合。</summary>
    public ObservableCollection<SystemAttachment> SignedAttachments => SignedApprovalAttachments;

    /// <summary>壳：照片附件集合。</summary>
    public ObservableCollection<SystemAttachment> PhotoAttachments => MaterialPhotoAttachments;

    /// <summary>壳：证明材料集合。</summary>
    public ObservableCollection<SystemAttachment> ProofAttachments => ProofMaterialAttachments;

    /// <summary>壳：照片区标题。</summary>
    public string PhotoZoneTitle => ApprovalWorkflowShellCopySupport.MaterialPhotoZoneTitle;

    /// <summary>壳：照片区提示。</summary>
    public string PhotoZoneHint => string.Empty;

    /// <summary>壳：证明材料区提示。</summary>
    public string ProofZoneHint => ProofMaterialAttachmentHint;

    /// <summary>壳：是否须上传照片。</summary>
    public bool RequiresPhotoAttachment => true;

    /// <summary>壳：是否须上传证明材料。</summary>
    public bool RequiresProofAttachment => RequiresProofMaterialScanUpload;

    /// <summary>壳：可否上传照片。</summary>
    public bool CanUploadPhotoAttachment => CanUploadSignedAttachment;

    /// <summary>壳：可否上传证明材料。</summary>
    public bool CanUploadProofAttachment => CanUploadProofMaterialAttachment;

    /// <summary>壳：可否上传其他附件。</summary>
    public bool CanUploadOtherAttachment => CanUploadSignedAttachment;

    /// <summary>壳：上传签批。</summary>
    public ICommand UploadSignedCommand => UploadSignedApprovalCommand;

    /// <summary>壳：高影仪签批。</summary>
    public ICommand CaptureSignedCommand => CaptureSignedApprovalCommand;

    /// <summary>壳：上传照片。</summary>
    public ICommand UploadPhotoCommand => UploadMaterialPhotoCommand;

    /// <summary>壳：高影仪照片。</summary>
    public ICommand CapturePhotoCommand => CaptureMaterialPhotoCommand;

    /// <summary>壳：上传证明材料。</summary>
    public ICommand UploadProofCommand => UploadProofMaterialScanCommand;

    /// <summary>壳：高影仪证明材料。</summary>
    public ICommand CaptureProofCommand => CaptureProofMaterialScanCommand;

    /// <summary>壳：上传其他。</summary>
    public ICommand UploadOtherCommand => UploadOtherAttachmentCommand;

    /// <summary>壳：高影仪其他。</summary>
    public ICommand CaptureOtherCommand => CaptureOtherAttachmentCommand;

    /// <summary>壳：打印交接单（覆盖申请侧 PrintApplicationCommand 语义，仅审批面板使用）。</summary>
    public ICommand PrintCommand => PrintHandoverCommand;

    /// <summary>壳：打印提示。</summary>
    public string PrintHintText => PrintHandoverHintText;

    /// <summary>壳：确认办结。</summary>
    public ICommand CompleteCommand => CompleteHandoverCommand;

    /// <summary>壳底栏草稿/提交/撤回：审批面板不启用。</summary>
    public bool CanEditHeader => false;

    /// <summary>壳：提交可用性（审批面板不启用）。</summary>
    public bool CanSubmit => false;

    /// <summary>壳：撤回可用性（审批面板不启用；申请侧撤回走外层）。</summary>
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
    public bool CanEditSigners => CanSaveApproval;

    /// <summary>壳：签字日期下限（不能早于签批单打印日）。</summary>
    public DateTime? SignatureDateMin =>
        ApprovalSignatureDateSupport.ResolveMinDate(Record.FirstPrintedAt, Record.LastPrintedAt, Record.PrintCount);

    /// <summary>壳：部门审核签字。</summary>
    public string DeptHead
    {
        get => Record.DeptHead ?? string.Empty;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Record.DeptHead, normalized, StringComparison.Ordinal)) return;
            Record.DeptHead = normalized;
            OnPropertyChanged(nameof(DeptHead));
        }
    }

    /// <summary>壳：部门审核日期。</summary>
    public DateTime? DeptHeadDate
    {
        get => Record.DeptHeadDate;
        set
        {
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (Record.DeptHeadDate == clamped) return;
            Record.DeptHeadDate = clamped;
            OnPropertyChanged(nameof(DeptHeadDate));
        }
    }

    /// <summary>壳：资料室签字。</summary>
    public string ArchiveRoomHead
    {
        get => Record.ArchiveRoomHead ?? string.Empty;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Record.ArchiveRoomHead, normalized, StringComparison.Ordinal)) return;
            Record.ArchiveRoomHead = normalized;
            OnPropertyChanged(nameof(ArchiveRoomHead));
        }
    }

    /// <summary>壳：资料室签字日期。</summary>
    public DateTime? ArchiveRoomHeadDate
    {
        get => Record.ArchiveRoomHeadDate;
        set
        {
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (Record.ArchiveRoomHeadDate == clamped) return;
            Record.ArchiveRoomHeadDate = clamped;
            OnPropertyChanged(nameof(ArchiveRoomHeadDate));
        }
    }

    /// <summary>壳：生产科签字。</summary>
    public string ProductionHead
    {
        get => Record.ProductionHead ?? string.Empty;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Record.ProductionHead, normalized, StringComparison.Ordinal)) return;
            Record.ProductionHead = normalized;
            OnPropertyChanged(nameof(ProductionHead));
        }
    }

    /// <summary>壳：生产科签字日期。</summary>
    public DateTime? ProductionHeadDate
    {
        get => Record.ProductionHeadDate;
        set
        {
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (Record.ProductionHeadDate == clamped) return;
            Record.ProductionHeadDate = clamped;
            OnPropertyChanged(nameof(ProductionHeadDate));
        }
    }

    /// <summary>壳：分管资料院长签字。</summary>
    public string ArchiveDeputyPresident
    {
        get => Record.ArchiveDeputyPresident ?? string.Empty;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Record.ArchiveDeputyPresident, normalized, StringComparison.Ordinal)) return;
            Record.ArchiveDeputyPresident = normalized;
            OnPropertyChanged(nameof(ArchiveDeputyPresident));
        }
    }

    /// <summary>壳：分管资料院长签字日期。</summary>
    public DateTime? ArchiveDeputyPresidentDate
    {
        get => Record.ArchiveDeputyPresidentDate;
        set
        {
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (Record.ArchiveDeputyPresidentDate == clamped) return;
            Record.ArchiveDeputyPresidentDate = clamped;
            OnPropertyChanged(nameof(ArchiveDeputyPresidentDate));
        }
    }

    /// <summary>壳：分管生产院长签字。</summary>
    public string ProductionVicePresident
    {
        get => Record.ProductionVicePresident ?? string.Empty;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Record.ProductionVicePresident, normalized, StringComparison.Ordinal)) return;
            Record.ProductionVicePresident = normalized;
            OnPropertyChanged(nameof(ProductionVicePresident));
        }
    }

    /// <summary>壳：分管生产院长签字日期。</summary>
    public DateTime? ProductionVicePresidentDate
    {
        get => Record.ProductionVicePresidentDate;
        set
        {
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (Record.ProductionVicePresidentDate == clamped) return;
            Record.ProductionVicePresidentDate = clamped;
            OnPropertyChanged(nameof(ProductionVicePresidentDate));
        }
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
        OnPropertyChanged(nameof(DeptHead));
        OnPropertyChanged(nameof(DeptHeadDate));
        OnPropertyChanged(nameof(ArchiveRoomHead));
        OnPropertyChanged(nameof(ArchiveRoomHeadDate));
        OnPropertyChanged(nameof(ProductionHead));
        OnPropertyChanged(nameof(ProductionHeadDate));
        OnPropertyChanged(nameof(ArchiveDeputyPresident));
        OnPropertyChanged(nameof(ArchiveDeputyPresidentDate));
        OnPropertyChanged(nameof(ProductionVicePresident));
        OnPropertyChanged(nameof(ProductionVicePresidentDate));
        OnPropertyChanged(nameof(CanEditHeader));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CanWithdraw));
    }
}
