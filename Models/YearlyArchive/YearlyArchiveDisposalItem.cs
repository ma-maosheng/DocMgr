using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料离库处置明细（模拟=立档事实；电子=袋内介质）。
    /// </summary>
    [Table("YearlyArchiveDisposalItems")]
    public sealed class YearlyArchiveDisposalItem
    {
        [Key]
        public int Id { get; set; }

        public int DisposalRecordId { get; set; }

        public int SortOrder { get; set; }

        /// <summary>立档事实 ID（模拟必填；电子可为 0）。</summary>
        public int FilingFactId { get; set; }

        /// <summary>容器 ID（模拟=档案盒；电子=介质袋）。</summary>
        public int ContainerId { get; set; }

        /// <summary>容器编号快照。</summary>
        public string ContainerCode { get; set; } = string.Empty;

        /// <summary>处置前存储位置快照。</summary>
        public string BeforeStorageLocation { get; set; } = string.Empty;

        /// <summary>来源盘库登记类型（盘失登记/损坏登记/拟销登记）。</summary>
        public string SourceRegisterKind { get; set; } = string.Empty;

        /// <summary>离库原因。</summary>
        public string DisposalReason { get; set; } = string.Empty;

        /// <summary>处置方式。</summary>
        public string DispositionMethod { get; set; } = string.Empty;

        /// <summary>资料名称快照（模拟）。</summary>
        public string MaterialName { get; set; } = string.Empty;

        /// <summary>明细名称快照（模拟）。</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>表单编号快照。</summary>
        public string FormNo { get; set; } = string.Empty;

        /// <summary>盘库丢失份数快照（模拟）。</summary>
        public int InventoryLostCopyCount { get; set; }

        /// <summary>盘库拟销份数快照（模拟）。</summary>
        public int InventoryScrapCopyCount { get; set; }

        /// <summary>处置前生命周期状态快照。</summary>
        public string BeforeLifecycleStatus { get; set; } = string.Empty;

        /// <summary>电子介质类别：硬盘 / 光盘。</summary>
        public string MediumKind { get; set; } = string.Empty;

        /// <summary>电子介质 ID。</summary>
        public int MediumId { get; set; }

        /// <summary>电子介质编号快照。</summary>
        public string MediumCode { get; set; } = string.Empty;

        /// <summary>电子立档单元 ID。</summary>
        public int ElectronicArchiveUnitId { get; set; }

        /// <summary>电子立档编号快照。</summary>
        public string ElectronicArchiveNo { get; set; } = string.Empty;

        /// <summary>处置前介质台账状态快照（电子）。</summary>
        public string BeforeMediaStatus { get; set; } = string.Empty;

        /// <summary>硬盘低格留存目标空白档口。</summary>
        public string TargetBlankSlotLocation { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public YearlyArchiveDisposalRecord? DisposalRecord { get; set; }
    }
}
