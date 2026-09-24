using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.YearlyArchive;

/// <summary>
/// 资料登记编辑窗：对接 <see cref="Views.Shared.ApprovalHandoverWorkspaceShell"/> 的契约别名。
/// </summary>
public partial class ArchiveRegisterViewModel
{
    /// <summary>壳：顶部流程横幅。</summary>
    public string WorkspaceBannerText => RegisterWorkspaceBannerText;

    /// <summary>壳：审批通过命令。</summary>
    public ICommand ApproveCommand => SaveApprovalCommand;

    /// <summary>壳：中间解锁步骤（确认实物交接）。</summary>
    public ICommand ConfirmMidStepCommand => ConfirmPhysicalHandoverCommand;

    /// <summary>壳底栏草稿编辑：审批分区嵌入时不启用（外层底栏负责）。</summary>
    public bool CanEditHeader => false;

    /// <summary>壳底栏提交：审批分区嵌入时不启用。</summary>
    public bool CanSubmit => false;

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
    public ICommand UploadSignedCommand => UploadSignedHandoverAttachmentCommand;

    /// <summary>壳：高影仪签批。</summary>
    public ICommand CaptureSignedCommand => CaptureSignedHandoverAttachmentCommand;

    /// <summary>壳：上传照片。</summary>
    public ICommand UploadPhotoCommand => UploadMaterialPhotoAttachmentCommand;

    /// <summary>壳：高影仪照片。</summary>
    public ICommand CapturePhotoCommand => CaptureMaterialPhotoAttachmentCommand;

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

    /// <summary>壳：签字日期下限（交接单首次/最近打印日）。</summary>
    public DateTime? SignatureDateMin =>
        CurrentRecord == null
            ? null
            : ApprovalSignatureDateSupport.ResolveMinDate(
                CurrentRecord.FirstPrintedAt,
                CurrentRecord.LastPrintedAt,
                CurrentRecord.PrintCount);

    /// <summary>壳：部门审核签字（代理 CurrentRecord）。</summary>
    public string DeptHead
    {
        get => CurrentRecord?.DeptHead ?? string.Empty;
        set
        {
            if (CurrentRecord == null) return;
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(CurrentRecord.DeptHead, normalized, StringComparison.Ordinal)) return;
            CurrentRecord.DeptHead = normalized;
            OnPropertyChanged(nameof(DeptHead));
        }
    }

    /// <summary>壳：部门审核日期。</summary>
    public DateTime? DeptHeadDate
    {
        get => CurrentRecord?.DeptHeadDate;
        set
        {
            if (CurrentRecord == null) return;
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (CurrentRecord.DeptHeadDate == clamped) return;
            CurrentRecord.DeptHeadDate = clamped;
            OnPropertyChanged(nameof(DeptHeadDate));
        }
    }

    /// <summary>壳：资料室签字。</summary>
    public string ArchiveRoomHead
    {
        get => CurrentRecord?.ArchiveRoomHead ?? string.Empty;
        set
        {
            if (CurrentRecord == null) return;
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(CurrentRecord.ArchiveRoomHead, normalized, StringComparison.Ordinal)) return;
            CurrentRecord.ArchiveRoomHead = normalized;
            OnPropertyChanged(nameof(ArchiveRoomHead));
        }
    }

    /// <summary>壳：资料室签字日期。</summary>
    public DateTime? ArchiveRoomHeadDate
    {
        get => CurrentRecord?.ArchiveRoomHeadDate;
        set
        {
            if (CurrentRecord == null) return;
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (CurrentRecord.ArchiveRoomHeadDate == clamped) return;
            CurrentRecord.ArchiveRoomHeadDate = clamped;
            OnPropertyChanged(nameof(ArchiveRoomHeadDate));
        }
    }

    /// <summary>壳：生产科签字。</summary>
    public string ProductionHead
    {
        get => CurrentRecord?.ProductionHead ?? string.Empty;
        set
        {
            if (CurrentRecord == null) return;
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(CurrentRecord.ProductionHead, normalized, StringComparison.Ordinal)) return;
            CurrentRecord.ProductionHead = normalized;
            OnPropertyChanged(nameof(ProductionHead));
        }
    }

    /// <summary>壳：生产科签字日期。</summary>
    public DateTime? ProductionHeadDate
    {
        get => CurrentRecord?.ProductionHeadDate;
        set
        {
            if (CurrentRecord == null) return;
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (CurrentRecord.ProductionHeadDate == clamped) return;
            CurrentRecord.ProductionHeadDate = clamped;
            OnPropertyChanged(nameof(ProductionHeadDate));
        }
    }

    /// <summary>壳：分管资料院长签字。</summary>
    public string ArchiveDeputyPresident
    {
        get => CurrentRecord?.ArchiveDeputyPresident ?? string.Empty;
        set
        {
            if (CurrentRecord == null) return;
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(CurrentRecord.ArchiveDeputyPresident, normalized, StringComparison.Ordinal)) return;
            CurrentRecord.ArchiveDeputyPresident = normalized;
            OnPropertyChanged(nameof(ArchiveDeputyPresident));
        }
    }

    /// <summary>壳：分管资料院长签字日期。</summary>
    public DateTime? ArchiveDeputyPresidentDate
    {
        get => CurrentRecord?.ArchiveDeputyPresidentDate;
        set
        {
            if (CurrentRecord == null) return;
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (CurrentRecord.ArchiveDeputyPresidentDate == clamped) return;
            CurrentRecord.ArchiveDeputyPresidentDate = clamped;
            OnPropertyChanged(nameof(ArchiveDeputyPresidentDate));
        }
    }

    /// <summary>壳：分管生产院长签字。</summary>
    public string ProductionVicePresident
    {
        get => CurrentRecord?.ProductionVicePresident ?? string.Empty;
        set
        {
            if (CurrentRecord == null) return;
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(CurrentRecord.ProductionVicePresident, normalized, StringComparison.Ordinal)) return;
            CurrentRecord.ProductionVicePresident = normalized;
            OnPropertyChanged(nameof(ProductionVicePresident));
        }
    }

    /// <summary>壳：分管生产院长签字日期。</summary>
    public DateTime? ProductionVicePresidentDate
    {
        get => CurrentRecord?.ProductionVicePresidentDate;
        set
        {
            if (CurrentRecord == null) return;
            var clamped = ApprovalSignatureDateSupport.Clamp(value, SignatureDateMin);
            if (CurrentRecord.ProductionVicePresidentDate == clamped) return;
            CurrentRecord.ProductionVicePresidentDate = clamped;
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
    }
}
