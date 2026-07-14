using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 出库提交/办结同步流水（提档预订、复制/拷贝 Pending、正式落账等）。
    /// </summary>
    [Table("YearlyArchiveOutboundSyncEntries")]
    public sealed class YearlyArchiveOutboundSyncEntry
    {
        [Key]
        public int Id { get; set; }

        public int OutboundRecordId { get; set; }

        public int OutboundItemId { get; set; }

        public int FilingFactId { get; set; }

        public string EntryKind { get; set; } = string.Empty;

        public string Phase { get; set; } = string.Empty;

        public string OperatedBy { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public YearlyArchiveOutboundRecord OutboundRecord { get; set; } = null!;
    }
}
