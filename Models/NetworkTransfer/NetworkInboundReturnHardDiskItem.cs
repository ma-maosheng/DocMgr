using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.NetworkTransfer
{
    /// <summary>
    /// 档外资料入网时，申请人拟随入网资料归还的借出硬盘明细。
    /// </summary>
    [Table("NetworkInboundReturnHardDiskItems")]
    public sealed class NetworkInboundReturnHardDiskItem
    {
        [Key]
        public int Id { get; set; }

        public int InboundRecordId { get; set; }

        public int SortOrder { get; set; }

        public int MediumId { get; set; }

        public string DiskCode { get; set; } = string.Empty;

        /// <summary>来源硬盘借出申请单 Id（介质管理出库）。</summary>
        public int? SourceApplicationId { get; set; }

        /// <summary>来源资料出库单 Id（库内空盘征用等）。</summary>
        public int? SourceOutboundRecordId { get; set; }

        /// <summary>资料入网后空白硬盘归位档口（审批环节指定）。</summary>
        public string TargetBlankSlotLocation { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public NetworkInboundRecord? InboundRecord { get; set; }
    }
}
