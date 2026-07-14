using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料出库（借出）申请单主表。状态枚举与登记申请一致。
    /// </summary>
    [Table("YearlyArchiveOutboundRecords")]
    public sealed class YearlyArchiveOutboundRecord
    {
        public const int Unsubmitted = 0;
        public const int Submitted = 1;
        public const int Approved = 2;
        public const int SignedUploaded = 3;
        public const int Completed = 4;
        public const int WithdrawnVoid = 5;
        public const int ForceVoided = 6;

        [Key]
        public int Id { get; set; }

        [Required]
        public string OutboundNo { get; set; } = string.Empty;

        public int Status { get; set; } = Unsubmitted;

        public int? ArchiveYear { get; set; }

        public int? ProjectId { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public int ApplicantUserId { get; set; }

        public string ApplicantName { get; set; } = string.Empty;

        public string ApplicantDept { get; set; } = string.Empty;

        public DateTime ApplyDate { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string DestinationKind { get; set; } = ArchiveOutboundDomainValues.DestinationInternal;

        public string ExternalUnit { get; set; } = string.Empty;

        public string SelfRetainDisposition { get; set; } = string.Empty;

        public string ProofMaterialNote { get; set; } = string.Empty;

        public string MaterialSummary { get; set; } = string.Empty;

        public DateTime? ExpectedReturnDate { get; set; }

        public int? SourceResultSetId { get; set; }

        public string SourceResultSetNo { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime? SignedUploadedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? WithdrawnAt { get; set; }

        public string WithdrawReason { get; set; } = string.Empty;

        public DateTime? ForceVoidedAt { get; set; }

        public string ForceVoidReason { get; set; } = string.Empty;

        public string ForceVoidKind { get; set; } = string.Empty;

        public DateTime? ApprovalDeadline { get; set; }

        public DateTime? OverdueRemindedAt { get; set; }

        public int PrintCount { get; set; }

        public DateTime? LastPrintedAt { get; set; }

        public string HandoverRemark { get; set; } = string.Empty;

        public string PhysicallyCompletedBy { get; set; } = string.Empty;

        public string DeptAuditOpinion { get; set; } = string.Empty;

        public string DeptAuditor { get; set; } = string.Empty;

        public DateTime? DeptAuditDate { get; set; }

        public string ArchiveRoomHeadOpinion { get; set; } = string.Empty;

        public string ArchiveRoomHead { get; set; } = string.Empty;

        public DateTime? ArchiveRoomHeadDate { get; set; }

        public string ProductionHeadOpinion { get; set; } = string.Empty;

        public string ProductionHead { get; set; } = string.Empty;

        public DateTime? ProductionHeadDate { get; set; }

        public string VicePresidentOpinion { get; set; } = string.Empty;

        public string VicePresident { get; set; } = string.Empty;

        public DateTime? VicePresidentDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public List<YearlyArchiveOutboundItem> Items { get; set; } = new();

        public List<YearlyArchiveOutboundSyncEntry> SyncEntries { get; set; } = new();

        [NotMapped]
        public bool IsDraft => Status == Unsubmitted;

        [NotMapped]
        public bool IsSubmitted => Status == Submitted;

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

        /// <summary>待归还明细上的盒位提示汇总（盒位已变/盒已失效）。</summary>
        [NotMapped]
        public string ContainerStatusHintSummary
        {
            get
            {
                bool hasInvalid = Items.Any(item =>
                    string.Equals(
                        item.ContainerStatusHint,
                        ArchiveOutboundDomainValues.ContainerStatusHintBoxInvalid,
                        StringComparison.Ordinal));
                if (hasInvalid)
                {
                    return "盒已失效";
                }

                bool hasChanged = Items.Any(item =>
                    string.Equals(
                        item.ContainerStatusHint,
                        ArchiveOutboundDomainValues.ContainerStatusHintLocationChanged,
                        StringComparison.Ordinal));
                return hasChanged ? "盒位已变" : string.Empty;
            }
        }

        [NotMapped]
        public bool HasApprovalInput =>
            !string.IsNullOrWhiteSpace(DeptAuditor)
            || DeptAuditDate.HasValue
            || !string.IsNullOrWhiteSpace(ArchiveRoomHead)
            || ArchiveRoomHeadDate.HasValue
            || !string.IsNullOrWhiteSpace(ProductionHead)
            || ProductionHeadDate.HasValue
            || !string.IsNullOrWhiteSpace(VicePresident)
            || VicePresidentDate.HasValue;

        [NotMapped]
        public bool CanApplicantWithdraw => Id > 0 && (IsDraft || IsSubmitted) && !HasApprovalInput;

        [NotMapped]
        public bool CanForceVoid => IsSubmitted && !HasApprovalInput;

        [NotMapped]
        public string StatusStr => Status switch
        {
            Unsubmitted => "未提交",
            Submitted => "已提交",
            Approved => "已审批",
            SignedUploaded => "已办结审批",
            Completed => "已办结出库",
            WithdrawnVoid => "已撤回作废",
            ForceVoided => "已强制作废",
            _ => "未知"
        };

        /// <summary>
        /// 明细涉及的介质类别摘要（模拟/电子），用于列表展示。
        /// </summary>
        [NotMapped]
        public string MediaKindSummary
        {
            get
            {
                var kinds = Items
                    .Select(item => item.MediaKind?.Trim() ?? string.Empty)
                    .Where(kind => !string.IsNullOrWhiteSpace(kind))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(kind => kind, StringComparer.Ordinal)
                    .ToList();

                return kinds.Count == 0 ? string.Empty : string.Join("、", kinds);
            }
        }

        public void MarkAsSubmitted()
        {
            Status = Submitted;
            SubmittedAt = DateTime.Now;
        }

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

        public void MarkAsCompleted()
        {
            Status = Completed;
            CompletedAt = DateTime.Now;
        }

        public void MarkAsWithdrawnVoid(string? reason = null)
        {
            Status = WithdrawnVoid;
            WithdrawnAt = DateTime.Now;
            WithdrawReason = reason?.Trim() ?? string.Empty;
        }

        public void MarkAsForceVoided(string kind, string? reason = null)
        {
            Status = ForceVoided;
            ForceVoidedAt = DateTime.Now;
            ForceVoidKind = kind?.Trim() ?? string.Empty;
            ForceVoidReason = reason?.Trim() ?? string.Empty;
        }
    }
}
