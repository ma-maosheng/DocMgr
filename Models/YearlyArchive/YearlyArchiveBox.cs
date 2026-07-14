using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.ArchiveContainers;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料档案盒（立档实体）
    /// </summary>
    [Table("YearlyArchiveBoxes")]
    public class YearlyArchiveBox : IArchiveContainer
    {
        [Key]
        public int Id { get; set; }

        // 年度档案序号 (例如: 2026-001) - 身份ID
        [Required]
        public string ArchiveSequenceNo { get; set; } = string.Empty;

        /// <summary>
        /// 统一容器编号。
        /// </summary>
        [NotMapped]
        public string ContainerCode => ArchiveSequenceNo;

        /// <summary>
        /// 容器类型。
        /// </summary>
        [NotMapped]
        public ArchiveContainerKind ContainerKind => ArchiveContainerKind.ArchiveBox;

        // 物理位置编号 (例如: 甲A-1-1-01) - 居住地址
        public string BoxLocationCode { get; set; } = string.Empty;

        // 结构化位置信息
        public string CabinetName { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public int Row { get; set; }
        public int Column { get; set; }
        public int BoxIndex { get; set; }

        // 业务属性
        public string ProjectName { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;

        // 档案盒规格
        public string Specs { get; set; } = "中";

        // 档案盒放置方式（默认：盒脊向外）
        public string PlacementMode { get; set; } = "SpineOut";

        // 入档信息
        public string ArchivedBy { get; set; } = string.Empty;
        public DateTime ArchivedDate { get; set; }
        public string Remarks { get; set; } = string.Empty;

        /// <summary>
        /// 容器生命周期状态（InUse / Retired 等）。
        /// </summary>
        public string ContainerLifecycleStatus { get; set; } = ArchiveContainerLifecycleStatus.InUse;

        /// <summary>
        /// 销号前最后占用的物理位置编号。
        /// </summary>
        public string LastStorageLocation { get; set; } = string.Empty;

        public DateTime? RetiredAt { get; set; }

        public string RetiredBy { get; set; } = string.Empty;

        // [重要修改] 这里必须去掉 [NotMapped]，才能在数据库生成多对多关联表
        public virtual List<YearlyArchiveRegisterRecord> RegisterRecords { get; set; } = new List<YearlyArchiveRegisterRecord>();

        /// <summary>
        /// 归入当前档案盒的资料子项关联。
        /// </summary>
        public virtual List<YearlyArchiveBoxMediaItemLink> MediaItemLinks { get; set; } = new List<YearlyArchiveBoxMediaItemLink>();
    }
}