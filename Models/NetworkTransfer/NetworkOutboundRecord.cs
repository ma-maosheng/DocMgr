using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 年度资料出网申请单。明细可手工录入，不强制关联在网台账；不跟踪中间过程介质。
    /// </summary>
    [Table("NetworkOutboundRecords")]
    public sealed class NetworkOutboundRecord
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
        public string OutboundNo { get; set; } = string.Empty;

        public int Status { get; set; } = StatusDraft;

        public string DestinationKind { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string Year { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;

        /// <summary>证明材料备注；「无」表示未附，有材料时填写名称。</summary>
        public string ProofMaterialNote { get; set; } = string.Empty;

        /// <summary>目的地=资料室立档时，办结后写入的建档草稿 Id。</summary>
        public int? TargetRegisterRecordId { get; set; }

        public string TargetRegisterFormNo { get; set; } = string.Empty;

        /// <summary>跨域业务链 Id；用于关联出网、在网台账和后续建档申请。</summary>
        public int? BusinessChainId { get; set; }

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

        public ICollection<NetworkOutboundItem> Items { get; set; } = new List<NetworkOutboundItem>();

        public NetworkArchiveBusinessChain? BusinessChain { get; set; }

        [NotMapped]
        public string StatusDisplay => NetworkTransferDomainValues.ToStatusDisplay(Status);

        [NotMapped]
        public int ItemCount => Items?.Count ?? 0;

        [NotMapped]
        public bool IsDraft => Status == StatusDraft;

        [NotMapped]
        public bool IsCompleted => Status == StatusCompleted;

        [NotMapped]
        public string BusinessChainProgressDisplay =>
            BusinessChain == null
                ? "关联业务链：待建立"
                : $"关联业务链 {BusinessChain.ChainNo} · {BusinessChain.StatusSummary}";
    }
}
