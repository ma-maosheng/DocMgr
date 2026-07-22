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

        public DateTime CreatedAt { get; set; }

        public HardDiskDisposalRecord? DisposalRecord { get; set; }

        public HardDiskMedium? Medium { get; set; }
    }
}
