using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还单主表：对"已办结出库"中提档(借出原件)项的收回入库。轻量两步流程：登记 → 办结。
    /// </summary>
    [Table("YearlyArchiveReturnRecords")]
    public sealed class YearlyArchiveReturnRecord
    {
        public const int Draft = 0;
        public const int Registered = 1;
        public const int Completed = 2;
        public const int Voided = 3;

        [Key]
        public int Id { get; set; }

        [Required]
        public string ReturnNo { get; set; } = string.Empty;

        public int Status { get; set; } = Draft;

        /// <summary>源出库单 Id。</summary>
        public int SourceOutboundRecordId { get; set; }

        public string SourceOutboundNo { get; set; } = string.Empty;

        public int? ArchiveYear { get; set; }

        public int? ProjectId { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        /// <summary>原借出人（取自出库申请人，快照）。</summary>
        public string BorrowerName { get; set; } = string.Empty;

        public string BorrowerDept { get; set; } = string.Empty;

        /// <summary>归还登记人用户 Id。</summary>
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

        public DateTime? CompletedAt { get; set; }

        public DateTime? VoidedAt { get; set; }

        public string VoidReason { get; set; } = string.Empty;

        public int PrintCount { get; set; }

        public DateTime? LastPrintedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public List<YearlyArchiveReturnItem> Items { get; set; } = new();

        [NotMapped]
        public bool IsDraft => Status == Draft;

        [NotMapped]
        public bool IsRegistered => Status == Registered;

        [NotMapped]
        public bool IsCompleted => Status == Completed;

        [NotMapped]
        public bool IsVoided => Status == Voided;

        [NotMapped]
        public string StatusStr => Status switch
        {
            Draft => "草稿",
            Registered => "已登记",
            Completed => "已办结",
            Voided => "已作废",
            _ => "未知"
        };

        public void MarkAsRegistered()
        {
            Status = Registered;
            RegisteredAt = DateTime.Now;
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

        public void MarkAsVoided(string? reason = null)
        {
            Status = Voided;
            VoidedAt = DateTime.Now;
            VoidReason = reason?.Trim() ?? string.Empty;
        }
    }
}
