using DocMgr.Models.HardDiskMedia;
using System.ComponentModel.DataAnnotations;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料电子立档单元与硬盘介质关联。
    /// </summary>
    public class YearlyElectronicArchiveUnitMediumLink
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
        /// 硬盘介质主键。
        /// </summary>
        public int HardDiskMediumId { get; set; }

        /// <summary>
        /// 电子立档单元。
        /// </summary>
        public virtual YearlyElectronicArchiveUnit ElectronicArchiveUnit { get; set; } = null!;

        /// <summary>
        /// 硬盘介质。
        /// </summary>
        public virtual HardDiskMedium HardDiskMedium { get; set; } = null!;
    }
}
