using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料离库处置单（主表；按 MediaKind 区分模拟/电子）。
    /// </summary>
    [Table("YearlyArchiveDisposalRecords")]
    public sealed class YearlyArchiveDisposalRecord
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

        /// <summary>介质轨：模拟 / 电子。</summary>
        public string MediaKind { get; set; } = string.Empty;

        /// <summary>工作流状态。</summary>
        public int Status { get; set; } = StatusDraft;

        /// <summary>离库原因汇总（明细去重拼接；权威值在明细）。</summary>
        public string DisposalReason { get; set; } = string.Empty;

        /// <summary>处置方式汇总（明细去重拼接；权威值在明细）。</summary>
        public string DispositionMethod { get; set; } = string.Empty;

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

        /// <summary>是否已上传处置现场照片（销毁类必填）。</summary>
        public bool ScenePhotoUploaded { get; set; }

        /// <summary>办结前确认：已完成空档案盒/介质袋物理移除。</summary>
        public bool PhysicalRemovalConfirmed { get; set; }

        public DateTime? PhysicalRemovalConfirmedAt { get; set; }

        public string PhysicalRemovalConfirmedBy { get; set; } = string.Empty;

        /// <summary>办结前确认：拟销硬盘已完成低级格式化（含低格留存时必填）。</summary>
        public bool FormatRetainedConfirmed { get; set; }

        public DateTime? FormatRetainedConfirmedAt { get; set; }

        public string FormatRetainedConfirmedBy { get; set; } = string.Empty;

        public DateTime? CompletedAt { get; set; }

        public string CompletedBy { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        public DateTime? WithdrawnAt { get; set; }

        public string WithdrawReason { get; set; } = string.Empty;

        public int PrintCount { get; set; }

        public DateTime? LastPrintedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<YearlyArchiveDisposalItem> Items { get; set; } = new List<YearlyArchiveDisposalItem>();

        [NotMapped]
        public string StatusDisplay => ArchiveDisposalDomainValues.ToStatusDisplay(Status);

        [NotMapped]
        public string ItemsSummary
        {
            get
            {
                if (Items == null || Items.Count == 0)
                {
                    return string.Empty;
                }

                if (string.Equals(MediaKind?.Trim(), ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
                {
                    return string.Join(
                        "、",
                        Items.OrderBy(item => item.SortOrder)
                            .Select(item => string.IsNullOrWhiteSpace(item.MediumCode) ? item.ContainerCode : item.MediumCode)
                            .Where(code => !string.IsNullOrWhiteSpace(code)));
                }

                return string.Join(
                    "、",
                    Items.OrderBy(item => item.SortOrder)
                        .Select(item => string.IsNullOrWhiteSpace(item.ItemName) ? item.MaterialName : item.ItemName)
                        .Where(name => !string.IsNullOrWhiteSpace(name)));
            }
        }

        [NotMapped]
        public int ItemCount => Items?.Count ?? 0;

        [NotMapped]
        public bool IsCompleted => Status == StatusCompleted;

        [NotMapped]
        public bool IsSimulated =>
            string.Equals(MediaKind?.Trim(), ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);
    }
}
