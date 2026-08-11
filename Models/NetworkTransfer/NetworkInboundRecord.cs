using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 年度资料入网申请单。
    /// </summary>
    [Table("NetworkInboundRecords")]
    public sealed class NetworkInboundRecord
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
        public string InboundNo { get; set; } = string.Empty;

        public int Status { get; set; } = StatusDraft;

        /// <summary>数据来源：档外资料 / 立档资料。</summary>
        public string SourceKind { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string Year { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;

        /// <summary>证明材料备注；「无」表示未附，有材料时填写名称。</summary>
        public string ProofMaterialNote { get; set; } = string.Empty;

        /// <summary>已立档入网时挂接的电子检索结果集 Id（唯一明细来源）。</summary>
        public int? SourceResultSetId { get; set; }

        public string SourceResultSetNo { get; set; } = string.Empty;

        public int ApplicantUserId { get; set; }

        public string ApplicantName { get; set; } = string.Empty;

        public string ApplicantDept { get; set; } = string.Empty;

        public DateTime ApplyTime { get; set; }

        public string ProdLeader { get; set; } = string.Empty;

        public DateTime? ProdDate { get; set; }

        public string RndLeader { get; set; } = string.Empty;

        public DateTime? RndDate { get; set; }

        public string DeputyLeader { get; set; } = string.Empty;

        public DateTime? DeputyDate { get; set; }

        public string Deliverer { get; set; } = string.Empty;

        public DateTime? DeliverDate { get; set; }

        public string Administrator { get; set; } = string.Empty;

        public DateTime? AdminDate { get; set; }

        public string DeptLeader { get; set; } = string.Empty;

        public DateTime? DeptDate { get; set; }

        public bool SignedAttachmentUploaded { get; set; }

        public DateTime? SignedAttachmentUploadedTime { get; set; }

        public string SignedAttachmentUploader { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime? HandoverConfirmedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string CompletedBy { get; set; } = string.Empty;

        public DateTime? WithdrawnAt { get; set; }

        public string WithdrawReason { get; set; } = string.Empty;

        public int PrintCount { get; set; }

        public DateTime? LastPrintedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<NetworkInboundItem> Items { get; set; } = new List<NetworkInboundItem>();

        [NotMapped]
        public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(Status);

        [NotMapped]
        public int ItemCount => Items?.Count ?? 0;

        [NotMapped]
        public bool IsDraft => Status == StatusDraft;

        [NotMapped]
        public bool IsCompleted => Status == StatusCompleted;
    }
}
