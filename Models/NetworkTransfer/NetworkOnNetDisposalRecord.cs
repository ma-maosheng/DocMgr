using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 在网数据处置单。
    /// </summary>
    [Table("NetworkOnNetDisposalRecords")]
    public sealed class NetworkOnNetDisposalRecord
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

        [Required]
        public string DisposalNo { get; set; } = string.Empty;

        public int Status { get; set; } = StatusDraft;

        public string DisposalReason { get; set; } = string.Empty;

        public string DispositionMethod { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;

        public int ApplicantUserId { get; set; }

        public string ApplicantName { get; set; } = string.Empty;

        public string ApplicantDept { get; set; } = string.Empty;

        public DateTime ApplyTime { get; set; }

        public string ApprovedBy { get; set; } = string.Empty;

        public DateTime? ApprovedTime { get; set; }

        public string ApprovalOpinion { get; set; } = string.Empty;

        /// <summary>资料室负责人（审核签字）。</summary>
        public string ArchiveRoomHead { get; set; } = string.Empty;

        /// <summary>资料室负责人审核日期。</summary>
        public DateTime? ArchiveRoomHeadDate { get; set; }

        /// <summary>分管资料副院长（审批签字）。</summary>
        public string ArchiveDeputyPresident { get; set; } = string.Empty;

        /// <summary>分管资料副院长审批日期。</summary>
        public DateTime? ArchiveDeputyPresidentDate { get; set; }

        public string ConfirmedBy { get; set; } = string.Empty;

        public DateTime? ConfirmedTime { get; set; }

        public bool SignedAttachmentUploaded { get; set; }

        public DateTime? SignedAttachmentUploadedTime { get; set; }

        public string SignedAttachmentUploader { get; set; } = string.Empty;

        public DateTime? CompletedAt { get; set; }

        public string CompletedBy { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        public DateTime? WithdrawnAt { get; set; }

        public string WithdrawReason { get; set; } = string.Empty;

        public int PrintCount { get; set; }

        public DateTime? LastPrintedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<NetworkOnNetDisposalItem> Items { get; set; } = new List<NetworkOnNetDisposalItem>();

        [NotMapped]
        public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(Status);

        [NotMapped]
        public int ItemCount => Items?.Count ?? 0;
    }
}
