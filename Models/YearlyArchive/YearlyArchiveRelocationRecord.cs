using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    [Table("YearlyArchiveRelocationRecords")]
    public class YearlyArchiveRelocationRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RelocationNo { get; set; } = string.Empty;

        [Required]
        public string MediaKind { get; set; } = string.Empty;

        [Required]
        public string RelocationMode { get; set; } = string.Empty;

        public int SourceContainerId { get; set; }

        public string SourceContainerCode { get; set; } = string.Empty;

        public string SourceStorageLocation { get; set; } = string.Empty;

        public int? TargetContainerId { get; set; }

        public string TargetContainerCode { get; set; } = string.Empty;

        public string TargetStorageLocation { get; set; } = string.Empty;

        public string SourceMediumDisposition { get; set; } = ArchiveRelocationSourceDisposition.None;

        public string OperatedBy { get; set; } = string.Empty;

        public DateTime OperatedAt { get; set; }

        public string Remarks { get; set; } = string.Empty;

        public string PreviewReport { get; set; } = string.Empty;

        public virtual List<YearlyArchiveRelocationItem> Items { get; set; } = new();
    }
}
