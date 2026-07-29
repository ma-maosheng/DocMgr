using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘离库处置单（主表，一单多盘）。
    /// </summary>
    [Table("HardDiskDisposalRecords")]
    public sealed class HardDiskDisposalRecord
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

        /// <summary>处置单编号。</summary>
        [Required]
        public string DisposalNo { get; set; } = string.Empty;

        /// <summary>工作流状态。</summary>
        public int Status { get; set; } = StatusDraft;

        /// <summary>离库原因汇总（由明细原因去重拼接，供列表/检索；权威值在明细）。</summary>
        public string DisposalReason { get; set; } = string.Empty;

        /// <summary>离库后处置方式汇总（由明细去重拼接，供列表/检索；权威值在明细）。</summary>
        public string DispositionMethod { get; set; } = string.Empty;

        /// <summary>其他原因或处置方式说明。</summary>
        public string OtherRemark { get; set; } = string.Empty;

        /// <summary>申请说明。</summary>
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

        /// <summary>确认可上传签批单的办理人。</summary>
        public string ConfirmedBy { get; set; } = string.Empty;

        public DateTime? ConfirmedTime { get; set; }

        public bool SignedAttachmentUploaded { get; set; }

        public DateTime? SignedAttachmentUploadedTime { get; set; }

        public string SignedAttachmentUploader { get; set; } = string.Empty;

        public bool DiskPhotoUploaded { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string CompletedBy { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        public DateTime? WithdrawnAt { get; set; }

        public string WithdrawReason { get; set; } = string.Empty;

        public int PrintCount { get; set; }

        public DateTime? LastPrintedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<HardDiskDisposalItem> Items { get; set; } = new List<HardDiskDisposalItem>();

        [NotMapped]
        public string StatusDisplay => HardDiskDisposalDomainValues.ToStatusDisplay(Status);

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
