using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 生产网在网台账（入网办结或加工产出登记写入）。
    /// </summary>
    [Table("NetworkOnNetAssets")]
    public sealed class NetworkOnNetAsset
    {
        [Key]
        public int Id { get; set; }

        /// <summary>在网资产编号。</summary>
        [Required]
        public string AssetNo { get; set; } = string.Empty;

        public string AssetKind { get; set; } = string.Empty;

        public string AssetName { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string Year { get; set; } = string.Empty;

        public string ServerPath { get; set; } = string.Empty;

        public string ConfidentialLevel { get; set; } = string.Empty;

        public string DataSizeText { get; set; } = string.Empty;

        public string VersionText { get; set; } = string.Empty;

        /// <summary>入网产生 / 加工产出。</summary>
        public string OriginKind { get; set; } = string.Empty;

        public int? OriginInboundItemId { get; set; }

        /// <summary>来源出网明细 Id（出网办结写入台账）。</summary>
        public int? OriginOutboundItemId { get; set; }

        public int? ParentAssetId { get; set; }

        public int? SourceFilingFactId { get; set; }

        public string LifecycleStatus { get; set; } = NetworkTransferDomainValues.LifecycleOnNet;

        public string Remark { get; set; } = string.Empty;

        public string RegisteredBy { get; set; } = string.Empty;

        public DateTime RegisteredAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        [NotMapped]
        public bool CanOutbound =>
            NetworkTransferDomainValues.CanOutbound(OriginKind, LifecycleStatus);

        [NotMapped]
        public bool CanDispose =>
            NetworkTransferDomainValues.CanDispose(LifecycleStatus);
    }
}
