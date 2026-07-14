using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料（立档事实）流转履历：立档、迁档、出库、归还等业务留痕。
    /// </summary>
    [Table("YearlyArchiveMaterialTransactions")]
    public sealed class YearlyArchiveMaterialTransaction
    {
        [Key]
        public int Id { get; set; }

        public int FilingFactId { get; set; }

        [Required]
        public string TransactionType { get; set; } = string.Empty;

        [Required]
        public string BusinessNo { get; set; } = string.Empty;

        [Required]
        public string SourceKind { get; set; } = string.Empty;

        public int SourceId { get; set; }

        /// <summary>
        /// 全局去重键，避免与历史聚合数据重复写入。
        /// </summary>
        [Required]
        public string DedupKey { get; set; } = string.Empty;

        public string BeforeLifecycleStatus { get; set; } = string.Empty;

        public string AfterLifecycleStatus { get; set; } = string.Empty;

        public string BeforeContainerCode { get; set; } = string.Empty;

        public string AfterContainerCode { get; set; } = string.Empty;

        public string BeforeStorageLocation { get; set; } = string.Empty;

        public string AfterStorageLocation { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;

        public string OperatorName { get; set; } = string.Empty;

        public DateTime OperatedAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
