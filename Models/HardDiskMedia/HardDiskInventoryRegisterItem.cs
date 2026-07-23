using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘盘库登记明细（一单多盘）。
    /// </summary>
    [Table("HardDiskInventoryRegisterItems")]
    public sealed class HardDiskInventoryRegisterItem
    {
        [Key]
        public int Id { get; set; }

        public int RegisterRecordId { get; set; }

        public int SortOrder { get; set; }

        public int MediumId { get; set; }

        /// <summary>硬盘编号快照。</summary>
        public string DiskCode { get; set; } = string.Empty;

        /// <summary>序列号快照。</summary>
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>登记前介质状态快照。</summary>
        public string BeforeMediaStatus { get; set; } = string.Empty;

        /// <summary>登记前存放位置快照。</summary>
        public string BeforeStorageLocation { get; set; } = string.Empty;

        /// <summary>登记前介质属性快照。</summary>
        public string BeforeMediaNature { get; set; } = string.Empty;

        /// <summary>目标存放位置（损坏登记/档口调整必填；盘失为空）。</summary>
        public string TargetStorageLocation { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public HardDiskInventoryRegisterRecord? RegisterRecord { get; set; }

        public HardDiskMedium? Medium { get; set; }
    }
}
