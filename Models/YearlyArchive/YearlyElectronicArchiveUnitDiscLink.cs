using DocMgr.Models.OpticalDiscMedia;
using System.ComponentModel.DataAnnotations;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料电子立档单元与光盘介质关联。
    /// </summary>
    public class YearlyElectronicArchiveUnitDiscLink
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 电子立档单元主键。
        /// </summary>
        public int YearlyElectronicArchiveUnitId { get; set; }

        /// <summary>
        /// 光盘介质主键。
        /// </summary>
        public int OpticalDiscMediumId { get; set; }

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 电子立档单元。
        /// </summary>
        public virtual YearlyElectronicArchiveUnit ElectronicArchiveUnit { get; set; } = null!;

        /// <summary>
        /// 光盘介质。
        /// </summary>
        public virtual OpticalDiscMedium OpticalDiscMedium { get; set; } = null!;
    }
}
