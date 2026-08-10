using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 年度资料入网申请明细。已立档源时由电子检索结果集条目生成，不跟踪中间过程介质。
    /// </summary>
    [Table("NetworkInboundItems")]
    public sealed class NetworkInboundItem
    {
        [Key]
        public int Id { get; set; }

        public int InboundRecordId { get; set; }

        public int SortOrder { get; set; }

        public string AssetKind { get; set; } = string.Empty;

        public string AssetName { get; set; } = string.Empty;

        public string ConfidentialLevel { get; set; } = string.Empty;

        public string DataSizeText { get; set; } = string.Empty;

        public string TargetServerPath { get; set; } = string.Empty;

        public string SourceKind { get; set; } = string.Empty;

        public int? SourceResultSetItemId { get; set; }

        public int? SourceFilingFactId { get; set; }

        public string FormNo { get; set; } = string.Empty;

        public string MaterialName { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string ContainerCode { get; set; } = string.Empty;

        public string StorageLocation { get; set; } = string.Empty;

        /// <summary>办结后回写的在网台账 Id。</summary>
        public int? OnNetAssetId { get; set; }

        public DateTime CreatedAt { get; set; }

        public NetworkInboundRecord? InboundRecord { get; set; }
    }
}
