using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还单主表：对已办结借出单中提档(借出原件)项的收回入库。
    /// 状态与硬盘/资料申请单统一为 7 态工作流。
    /// </summary>
    [Table("YearlyArchiveReturnRecords")]
    public sealed class YearlyArchiveReturnRecord
    {
        public const int Unsubmitted = ApplicationWorkflowStatus.Draft;
        public const int Submitted = ApplicationWorkflowStatus.Submitted;
        public const int Approved = ApplicationWorkflowStatus.Approved;
        public const int SignedUploaded = ApplicationWorkflowStatus.SignedUploaded;
        public const int Completed = ApplicationWorkflowStatus.Completed;
        public const int WithdrawnVoid = ApplicationWorkflowStatus.Withdrawn;
        public const int ForceVoided = ApplicationWorkflowStatus.ForceWithdrawn;

        /// <summary>兼容旧名：草稿。</summary>
        public const int Draft = Unsubmitted;

        /// <summary>兼容旧名：已登记（现对应已提交-待审批）。</summary>
        public const int Registered = Submitted;

        /// <summary>兼容旧名：已作废（现对应撤回作废）。</summary>
        public const int Voided = WithdrawnVoid;

        [Key]
        public int Id { get; set; }

        [Required]
        public string ReturnNo { get; set; } = string.Empty;

        public int Status { get; set; } = Unsubmitted;

        /// <summary>源出库单 Id。</summary>
        public int SourceOutboundRecordId { get; set; }

        public string SourceOutboundNo { get; set; } = string.Empty;

        public int? ArchiveYear { get; set; }

        public int? ProjectId { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        /// <summary>原借出人（取自出库申请人，快照）。</summary>
        public string BorrowerName { get; set; } = string.Empty;

        public string BorrowerDept { get; set; } = string.Empty;

        /// <summary>归还申请人用户 Id。</summary>
        public int RegisteredByUserId { get; set; }

        public string RegisteredByName { get; set; } = string.Empty;

        public string RegisteredByDept { get; set; } = string.Empty;

        public DateTime ReturnDate { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;

        /// <summary>资料灭失具体情况说明（存在灭失份数时填写）。</summary>
        public string LossDescription { get; set; } = string.Empty;

        /// <summary>办结（核对入库）管理员。</summary>
        public string HandlerName { get; set; } = string.Empty;

        public DateTime? RegisteredAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime? SignedUploadedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? WithdrawnAt { get; set; }

        public DateTime? VoidedAt { get; set; }

        public string VoidReason { get; set; } = string.Empty;

        public string ForceVoidReason { get; set; } = string.Empty;

        public DateTime? ForceVoidedAt { get; set; }

        public int PrintCount { get; set; }

        public DateTime? LastPrintedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public List<YearlyArchiveReturnItem> Items { get; set; } = new();

        [NotMapped]
        public bool IsDraft => Status == Unsubmitted;

        [NotMapped]
        public bool IsSubmitted => Status == Submitted;

        /// <summary>兼容旧名。</summary>
        [NotMapped]
        public bool IsRegistered => IsSubmitted;

        [NotMapped]
        public bool IsApproved => Status == Approved;

        [NotMapped]
        public bool IsSignedUploaded => Status == SignedUploaded;

        [NotMapped]
        public bool IsCompleted => Status == Completed;

        [NotMapped]
        public bool IsWithdrawnVoid => Status == WithdrawnVoid;

        [NotMapped]
        public bool IsForceVoided => Status == ForceVoided;

        /// <summary>兼容旧名：任一作废态。</summary>
        [NotMapped]
        public bool IsVoided => IsWithdrawnVoid || IsForceVoided;

        [NotMapped]
        public string StatusStr => ApplicationWorkflowStatus.ToDisplay(Status);

        public void MarkAsDraft()
        {
            Status = Unsubmitted;
        }

        public void MarkAsSubmitted()
        {
            Status = Submitted;
            DateTime now = DateTime.Now;
            SubmittedAt = now;
            RegisteredAt = now;
        }

        /// <summary>兼容旧名：登记完成 = 提交待审批。</summary>
        public void MarkAsRegistered() => MarkAsSubmitted();

        public void MarkAsApproved()
        {
            Status = Approved;
            ApprovedAt = DateTime.Now;
        }

        public void MarkAsSignedUploaded()
        {
            Status = SignedUploaded;
            SignedUploadedAt = DateTime.Now;
        }

        public void MarkAsCompleted(string? handlerName = null)
        {
            Status = Completed;
            CompletedAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(handlerName))
            {
                HandlerName = handlerName.Trim();
            }
        }

        public void MarkAsWithdrawnVoid(string? reason = null)
        {
            Status = WithdrawnVoid;
            DateTime now = DateTime.Now;
            WithdrawnAt = now;
            VoidedAt = now;
            VoidReason = reason?.Trim() ?? string.Empty;
        }

        public void MarkAsForceVoided(string? reason = null)
        {
            Status = ForceVoided;
            DateTime now = DateTime.Now;
            ForceVoidedAt = now;
            VoidedAt = now;
            ForceVoidReason = reason?.Trim() ?? string.Empty;
            VoidReason = ForceVoidReason;
        }

        /// <summary>兼容旧名：作废默认按撤回处理。</summary>
        public void MarkAsVoided(string? reason = null) => MarkAsWithdrawnVoid(reason);
    }
}
