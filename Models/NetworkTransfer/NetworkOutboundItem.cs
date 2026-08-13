using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 年度资料出网申请明细（可手工录入；可选关联在网台账；不含介质字段）。
    /// </summary>
    [Table("NetworkOutboundItems")]
    public sealed class NetworkOutboundItem
    {
        [Key]
        public int Id { get; set; }

        public int OutboundRecordId { get; set; }

        public int SortOrder { get; set; }

        /// <summary>关联在网台账 Id；手工录入明细为 null。</summary>
        public int? OnNetAssetId { get; set; }

        public string AssetNo { get; set; } = string.Empty;

        public string AssetKind { get; set; } = string.Empty;

        public string AssetName { get; set; } = string.Empty;

        /// <summary>资料明细名称。</summary>
        public string ItemName { get; set; } = string.Empty;

        public string ServerPath { get; set; } = string.Empty;

        public string ConfidentialLevel { get; set; } = string.Empty;

        public string DataSizeText { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string Year { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public NetworkOutboundRecord? OutboundRecord { get; set; }

        public NetworkOnNetAsset? OnNetAsset { get; set; }
    }
}
