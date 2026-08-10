using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 在网数据处置明细。
    /// </summary>
    [Table("NetworkOnNetDisposalItems")]
    public sealed class NetworkOnNetDisposalItem
    {
        [Key]
        public int Id { get; set; }

        public int DisposalRecordId { get; set; }

        public int SortOrder { get; set; }

        public int OnNetAssetId { get; set; }

        public string AssetNo { get; set; } = string.Empty;

        public string AssetKind { get; set; } = string.Empty;

        public string AssetName { get; set; } = string.Empty;

        public string ServerPath { get; set; } = string.Empty;

        public string BeforeLifecycleStatus { get; set; } = string.Empty;

        public string DisposalReason { get; set; } = string.Empty;

        public string DispositionMethod { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public NetworkOnNetDisposalRecord? DisposalRecord { get; set; }

        public NetworkOnNetAsset? OnNetAsset { get; set; }
    }
}
