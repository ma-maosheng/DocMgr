using System.ComponentModel.DataAnnotations;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 模拟登记介质明细扩展信息（与 <see cref="YearlyArchiveRegisterMediaItem"/> 一对一）。
    /// </summary>
    public class YearlyArchiveRegisterSimulatedMediaItemDetail
    {
        [Key]
        public int MediaItemId { get; set; }

        /// <summary>
        /// 资料类型：文本 / 图件。
        /// </summary>
        public string MaterialCategory { get; set; } = string.Empty;

        /// <summary>
        /// 所属子类，受资料类型 Scope 约束。
        /// </summary>
        public string SubCategory { get; set; } = string.Empty;

        /// <summary>
        /// 组织形式：散页 / 装订。
        /// </summary>
        public string OrganizationForm { get; set; } = string.Empty;

        public virtual YearlyArchiveRegisterMediaItem MediaItem { get; set; } = null!;
    }
}
