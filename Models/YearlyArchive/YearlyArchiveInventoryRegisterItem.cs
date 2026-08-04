using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料盘库登记明细。
    /// </summary>
    [Table("YearlyArchiveInventoryRegisterItems")]
    public sealed class YearlyArchiveInventoryRegisterItem
    {
        [Key]
        public int Id { get; set; }

        public int RegisterRecordId { get; set; }

        public int SortOrder { get; set; }

        /// <summary>立档事实 ID（模拟轨必填；电子轨为 0，不建外键）。</summary>
        public int FilingFactId { get; set; }

        /// <summary>登记介质明细 ID（模拟轨）。</summary>
        public int MediaItemId { get; set; }

        /// <summary>容器 ID（模拟档案盒 / 电子袋）。</summary>
        public int ContainerId { get; set; }

        /// <summary>容器编号快照。</summary>
        public string ContainerCode { get; set; } = string.Empty;

        /// <summary>盘库丢失份数（模拟轨）。</summary>
        public int LostCopyCount { get; set; }

        /// <summary>登记前可用库内份数快照。</summary>
        public int BeforeAvailableCopyCount { get; set; }

        /// <summary>介质类别：硬盘 / 光盘（电子轨）。</summary>
        public string MediumKind { get; set; } = string.Empty;

        /// <summary>介质 ID（电子轨）。</summary>
        public int MediumId { get; set; }

        /// <summary>介质编号快照（电子轨）。</summary>
        public string MediumCode { get; set; } = string.Empty;

        /// <summary>电子立档单元 ID（电子轨）。</summary>
        public int ElectronicArchiveUnitId { get; set; }

        /// <summary>电子立档编号快照（电子轨）。</summary>
        public string ElectronicArchiveNo { get; set; } = string.Empty;

        /// <summary>登记前介质状态快照（电子轨）。</summary>
        public string BeforeMediaStatus { get; set; } = string.Empty;

        /// <summary>登记前存放位置快照（模拟档口 / 电子介质档口）。</summary>
        public string BeforeStorageLocation { get; set; } = string.Empty;

        /// <summary>所属项目快照。</summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>项目年度快照。</summary>
        public string Year { get; set; } = string.Empty;

        /// <summary>资料名称快照。</summary>
        public string MaterialName { get; set; } = string.Empty;

        /// <summary>明细名称快照。</summary>
        public string ItemName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public YearlyArchiveInventoryRegisterRecord? RegisterRecord { get; set; }

        /// <summary>立档事实导航（仅模拟轨有值；电子轨不映射外键，FilingFactId 可为 0）。</summary>
        [NotMapped]
        public YearlyArchiveFilingFact? FilingFact { get; set; }
    }
}
