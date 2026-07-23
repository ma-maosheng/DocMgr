using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘盘库登记单（主表，一单多盘；轻量草稿/办结/作废）。
    /// </summary>
    [Table("HardDiskInventoryRegisterRecords")]
    public sealed class HardDiskInventoryRegisterRecord
    {
        public const int StatusDraft = ApplicationWorkflowStatus.Draft;
        public const int StatusCompleted = ApplicationWorkflowStatus.Completed;
        public const int StatusWithdrawn = ApplicationWorkflowStatus.Withdrawn;

        [Key]
        public int Id { get; set; }

        /// <summary>登记单编号。</summary>
        [Required]
        public string RegisterNo { get; set; } = string.Empty;

        /// <summary>工作流状态（草稿/已办结/已撤回作废）。</summary>
        public int Status { get; set; } = StatusDraft;

        /// <summary>登记类型（整单唯一）：损坏登记/盘失登记/损坏档口调整。</summary>
        public string RegisterKind { get; set; } = string.Empty;

        /// <summary>登记说明。</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>备注。</summary>
        public string Remark { get; set; } = string.Empty;

        public int ApplicantUserId { get; set; }

        public string ApplicantName { get; set; } = string.Empty;

        public string ApplicantDept { get; set; } = string.Empty;

        public DateTime ApplyTime { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string CompletedBy { get; set; } = string.Empty;

        public DateTime? WithdrawnAt { get; set; }

        public string WithdrawReason { get; set; } = string.Empty;

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
