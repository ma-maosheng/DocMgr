using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档离库处置明细（一盒一行）。
    /// </summary>
    [Table("HistoryArchiveDisposalItems")]
    public sealed class HistoryArchiveDisposalItem
    {
        [Key]
        public int Id { get; set; }

        public int DisposalRecordId { get; set; }

        public int SortOrder { get; set; }

        /// <summary>档案盒编号（四段盒号）。</summary>
        public string BoxCode { get; set; } = string.Empty;

        public string BoxSpecification { get; set; } = string.Empty;

        public string CabinetName { get; set; } = string.Empty;

        public string FaceCode { get; set; } = string.Empty;

        public string SlotCode { get; set; } = string.Empty;

        /// <summary>原完整存放位置（通常等于盒号）。</summary>
        public string BeforeStorageLocation { get; set; } = string.Empty;

        /// <summary>盒内资料简要描述（提交时固化）。</summary>
        public string ContentSummary { get; set; } = string.Empty;

        public int LedgerRecordCount { get; set; }

        /// <summary>关联台账键，如 TopoMap:12|TopoMap:15。</summary>
        public string SourceRecordKeys { get; set; } = string.Empty;

        /// <summary>是否混放待梳理盒。</summary>
        public bool IsMixedPlacement { get; set; }

        /// <summary>关联混放盒号（分号连接）。</summary>
        public string RelatedBoxCodes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public HistoryArchiveDisposalRecord? DisposalRecord { get; set; }
    }
}
