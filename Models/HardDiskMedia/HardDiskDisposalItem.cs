using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘离库处置明细（一单多盘）。
    /// </summary>
    [Table("HardDiskDisposalItems")]
    public sealed class HardDiskDisposalItem
    {
        [Key]
        public int Id { get; set; }

        public int DisposalRecordId { get; set; }

        public int SortOrder { get; set; }

        public int MediumId { get; set; }

        /// <summary>硬盘编号快照。</summary>
        public string DiskCode { get; set; } = string.Empty;

        /// <summary>序列号快照。</summary>
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>处置前介质状态快照。</summary>
        public string BeforeMediaStatus { get; set; } = string.Empty;

        /// <summary>处置前存放位置快照。</summary>
        public string BeforeStorageLocation { get; set; } = string.Empty;

        /// <summary>处置前介质属性快照。</summary>
        public string BeforeMediaNature { get; set; } = string.Empty;

        /// <summary>离库原因（按盘）：淘汰/损坏/盘失（由处置前介质状态自动赋值）。</summary>
        public string DisposalReason { get; set; } = string.Empty;

        /// <summary>离库后处置方式（按盘）：直接销毁/退还办公室/库内注销/其他。</summary>
        public string DispositionMethod { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public HardDiskDisposalRecord? DisposalRecord { get; set; }

        public HardDiskMedium? Medium { get; set; }
    }
}
