using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.OpticalDiscMedia;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料电子介质立档单元
    /// </summary>
    [Table("YearlyElectronicArchiveUnits")]
    public class YearlyElectronicArchiveUnit : IArchiveContainer
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 电子立档编号
        /// </summary>
        [Required]
        public string ElectronicArchiveNo { get; set; } = string.Empty;

        /// <summary>
        /// 统一容器编号。
        /// </summary>
        [NotMapped]
        public string ContainerCode => ElectronicArchiveNo;

        /// <summary>
        /// 容器类型。
        /// </summary>
        [NotMapped]
        public ArchiveContainerKind ContainerKind => ArchiveContainerKind.ElectronicBag;

        /// <summary>
        /// 所属项目
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// 所属年度
        /// </summary>
        public string Year { get; set; } = string.Empty;

        /// <summary>
        /// 存储载体类型
        /// </summary>
        public string StorageCarrierType { get; set; } = string.Empty;

        /// <summary>
        /// 存储路径
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>
        /// 存放位置
        /// </summary>
        public string StorageLocation { get; set; } = string.Empty;

        /// <summary>
        /// 关联硬盘编号的反范式快照（逗号拼接），同时兼作"硬盘袋/光盘袋"判别：为空表示光盘袋。
        /// 真相来源为 <see cref="MediumLinks"/>；增删硬盘关联时必须与 <see cref="MediumLinks"/> 成对维护，避免漂移。
        /// </summary>
        public string LinkedMediumCodes { get; set; } = string.Empty;

        /// <summary>
        /// 处置方式
        /// </summary>
        public string Disposition { get; set; } = string.Empty;

        /// <summary>
        /// 介质数量（硬盘袋恒为 1；光盘袋为光盘张数）。非关系计数，维护时须与实际介质数一致。
        /// </summary>
        public int MediaCount { get; set; }

        /// <summary>
        /// 电子介质袋资料摘要（聚合描述，与子项名称不同）。
        /// </summary>
        public string ContentSummary { get; set; } = string.Empty;

        /// <summary>
        /// 归档人
        /// </summary>
        public string ArchivedBy { get; set; } = string.Empty;

        /// <summary>
        /// 归档时间
        /// </summary>
        public DateTime ArchivedDate { get; set; }

        /// <summary>
        /// 来源类型
        /// </summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// 来源记录键
        /// </summary>
        public string SourceRecordKey { get; set; } = string.Empty;

        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks { get; set; } = string.Empty;

        /// <summary>
        /// 电子介质袋生命周期状态（Active / Relocated / Disposed 等）。
        /// </summary>
        public string UnitLifecycleStatus { get; set; } = ArchiveContainerLifecycleStatus.InUse;

        /// <summary>
        /// 关联登记记录
        /// </summary>
        public virtual List<YearlyArchiveRegisterRecord> RegisterRecords { get; set; } = new();

        /// <summary>
        /// 关联硬盘介质集合，是硬盘关联的"真相来源"；<see cref="LinkedMediumCodes"/> 为其反范式快照。
        /// </summary>
        public virtual List<YearlyElectronicArchiveUnitMediumLink> MediumLinks { get; set; } = new();

        /// <summary>
        /// 关联光盘介质集合。
        /// </summary>
        public virtual List<YearlyElectronicArchiveUnitDiscLink> DiscLinks { get; set; } = new();

        /// <summary>
        /// 关联登记介质条目集合。
        /// </summary>
        public virtual List<YearlyElectronicArchiveUnitMediaLink> MediaEntryLinks { get; set; } = new();

        /// <summary>
        /// 关联资料子项（立档明细）。
        /// </summary>
        public virtual List<YearlyElectronicArchiveUnitMediaItemLink> MediaItemLinks { get; set; } = new();
    }
}
