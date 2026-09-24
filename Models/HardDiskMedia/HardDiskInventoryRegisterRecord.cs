using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘盘库登记单（主表，一单多盘；B 流线下签批：确认可上传）。
    /// </summary>
    [Table("HardDiskInventoryRegisterRecords")]
    public sealed class HardDiskInventoryRegisterRecord
    {
        public const int StatusDraft = ApplicationWorkflowStatus.Draft;
        public const int StatusSubmitted = ApplicationWorkflowStatus.Submitted;
        public const int StatusApproved = ApplicationWorkflowStatus.Approved;
        public const int StatusSignedUploaded = ApplicationWorkflowStatus.SignedUploaded;
        public const int StatusCompleted = ApplicationWorkflowStatus.Completed;
        public const int StatusWithdrawn = ApplicationWorkflowStatus.Withdrawn;
        public const int StatusForceWithdrawn = ApplicationWorkflowStatus.ForceWithdrawn;

        [Key]
        public int Id { get; set; }

        /// <summary>登记单编号。</summary>
        [Required]
        public string RegisterNo { get; set; } = string.Empty;

        /// <summary>工作流状态。</summary>
        public int Status { get; set; } = StatusDraft;

        /// <summary>登记类型（整单唯一）：损坏登记/盘失登记（历史单可能含损坏档口调整）。</summary>
        public string RegisterKind { get; set; } = string.Empty;

        /// <summary>登记说明。</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>备注。</summary>
        public string Remark { get; set; } = string.Empty;

        public int ApplicantUserId { get; set; }

        public string ApplicantName { get; set; } = string.Empty;

        public string ApplicantDept { get; set; } = string.Empty;

        public DateTime ApplyTime { get; set; }

        public string ApprovedBy { get; set; } = string.Empty;

        public DateTime? ApprovedTime { get; set; }

        public string ApprovalOpinion { get; set; } = string.Empty;

        public string DeptHead { get; set; } = string.Empty;

        public DateTime? DeptHeadDate { get; set; }

        public string ArchiveRoomHead { get; set; } = string.Empty;

        public DateTime? ArchiveRoomHeadDate { get; set; }

        public string ProductionHead { get; set; } = string.Empty;

        public DateTime? ProductionHeadDate { get; set; }

        public string ArchiveDeputyPresident { get; set; } = string.Empty;

        public DateTime? ArchiveDeputyPresidentDate { get; set; }

        public string ProductionVicePresident { get; set; } = string.Empty;

        public DateTime? ProductionVicePresidentDate { get; set; }

        /// <summary>确认可上传签批单的办理人。</summary>
        public string ConfirmedBy { get; set; } = string.Empty;

        public DateTime? ConfirmedTime { get; set; }

        public bool SignedAttachmentUploaded { get; set; }

        public DateTime? SignedAttachmentUploadedTime { get; set; }

        public string SignedAttachmentUploader { get; set; } = string.Empty;

        /// <summary>损坏登记：已确认完成损坏硬盘迁档（确认可上传附件信息时必填并锁定）。</summary>
        public bool DamagedDiskRelocationConfirmed { get; set; }

        public DateTime? DamagedDiskRelocationConfirmedAt { get; set; }

        public string DamagedDiskRelocationConfirmedBy { get; set; } = string.Empty;

        public DateTime? CompletedAt { get; set; }

        public string CompletedBy { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        public DateTime? WithdrawnAt { get; set; }

        public string WithdrawReason { get; set; } = string.Empty;

        public int PrintCount { get; set; }

        public DateTime? LastPrintedAt { get; set; }

        public DateTime? FirstPrintedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<HardDiskInventoryRegisterItem> Items { get; set; } = new List<HardDiskInventoryRegisterItem>();

        [NotMapped]
        public string StatusDisplay => HardDiskInventoryRegisterDomainValues.ToStatusDisplay(Status);

        [NotMapped]
        public string DiskCodesSummary =>
            Items == null || Items.Count == 0
                ? string.Empty
                : string.Join("、", Items.OrderBy(item => item.SortOrder).Select(item => item.DiskCode).Where(code => !string.IsNullOrWhiteSpace(code)));

        [NotMapped]
        public int ItemCount => Items?.Count ?? 0;

        [NotMapped]
        public bool IsCompleted => Status == StatusCompleted;
    }
}
