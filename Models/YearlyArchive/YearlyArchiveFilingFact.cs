using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.ArchiveContainers;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 立档事实台账：立档成功后永久保留，供全生命周期检索。
    /// </summary>
    [Table("YearlyArchiveFilingFacts")]
    public sealed class YearlyArchiveFilingFact
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FilingFactNo { get; set; } = string.Empty;

        [Required]
        public string MediaKind { get; set; } = string.Empty;

        public int RegisterRecordId { get; set; }

        public int RegisterMediaId { get; set; }

        public int MediaItemId { get; set; }

        public string FormNo { get; set; } = string.Empty;

        public string MaterialName { get; set; } = string.Empty;

        public int? ProjectId { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public string ProvideUnit { get; set; } = string.Empty;

        public string ApplicantName { get; set; } = string.Empty;

        public string ItemType { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string ConfidentialLevel { get; set; } = ArchiveRegisterDomainValues.ConfidentialLevelNone;

        public int ContentCount { get; set; }

        public ArchiveContainerKind ContainerKind { get; set; }

        public int ContainerId { get; set; }

        public string ContainerCode { get; set; } = string.Empty;

        public string StorageLocation { get; set; } = string.Empty;

        public string CabinetName { get; set; } = string.Empty;

        public string BoxLocationCode { get; set; } = string.Empty;

        public string BoxSpecs { get; set; } = string.Empty;

        public string StorageCarrierType { get; set; } = string.Empty;

        public string Disposition { get; set; } = string.Empty;

        public string MediumCode { get; set; } = string.Empty;

        public string FilingStoragePath { get; set; } = string.Empty;

        public decimal DataSizeMb { get; set; }

        public DateTime FiledAt { get; set; }

        public string FiledBy { get; set; } = string.Empty;

        [Required]
        public string SourceLinkType { get; set; } = string.Empty;

        public int SourceLinkId { get; set; }

        public string LifecycleStatus { get; set; } = FilingFactLifecycleStatus.InArchive;

        public string CurrentContainerCode { get; set; } = string.Empty;

        public string CurrentStorageLocation { get; set; } = string.Empty;

        public DateTime? LifecycleUpdatedAt { get; set; }

        public string LifecycleRemark { get; set; } = string.Empty;

        public string BorrowHintLevel { get; set; } = FilingFactBorrowHintLevel.None;

        public string BorrowHintText { get; set; } = string.Empty;

        public DateTime? BorrowHintUpdatedAt { get; set; }

        /// <summary>
        /// 备份副本指向的原件立档事实 Id；原件为 null。
        /// </summary>
        public int? PrimaryFilingFactId { get; set; }

        /// <summary>
        /// 原件 / 备份角色，见 <see cref="FilingFactArchiveCopyRole"/>。
        /// </summary>
        public string ArchiveCopyRole { get; set; } = FilingFactArchiveCopyRole.Original;

        public DateTime CreatedAt { get; set; }
    }
}
