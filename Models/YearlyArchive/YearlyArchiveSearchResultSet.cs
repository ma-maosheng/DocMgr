using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    [Table("YearlyArchiveSearchResultSets")]
    public sealed class YearlyArchiveSearchResultSet
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ResultSetNo { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string MediaKind { get; set; } = string.Empty;

        public string Status { get; set; } = ArchiveSearchResultSetStatus.Draft;

        public int CreatedByUserId { get; set; }

        public string CreatedByName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string Remarks { get; set; } = string.Empty;

        public string SearchCriteriaJson { get; set; } = string.Empty;

        public List<YearlyArchiveSearchResultSetItem> Items { get; set; } = new();
    }

    [Table("YearlyArchiveSearchResultSetItems")]
    public sealed class YearlyArchiveSearchResultSetItem
    {
        [Key]
        public int Id { get; set; }

        public int ResultSetId { get; set; }

        public int FilingFactId { get; set; }

        public string SelectionScopeKind { get; set; } = ArchiveSearchSelectionScopeKind.WholeMediaItem;

        public int? ContentEntryId { get; set; }

        public string ContentEntryKind { get; set; } = string.Empty;

        public string ContentEntryName { get; set; } = string.Empty;

        public string ContentEntryRelativePath { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public string FormNo { get; set; } = string.Empty;

        public string MaterialName { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string ContainerCode { get; set; } = string.Empty;

        public string StorageLocation { get; set; } = string.Empty;

        public string LifecycleStatus { get; set; } = string.Empty;

        public string BorrowHintLevel { get; set; } = string.Empty;

        public string BorrowHintText { get; set; } = string.Empty;

        /// <summary>筛选池申请份数（模拟介质整子项；默认 1）。</summary>
        public int RequestedCopyCount { get; set; } = 1;

        public DateTime AddedAt { get; set; }

        public YearlyArchiveSearchResultSet ResultSet { get; set; } = null!;
    }
}
