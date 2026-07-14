using System;
using System.ComponentModel.DataAnnotations;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质袋与登记介质条目的关联。
    /// </summary>
    public class YearlyElectronicArchiveUnitMediaLink
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 电子介质袋主键。
        /// </summary>
        public int YearlyElectronicArchiveUnitId { get; set; }

        /// <summary>
        /// 登记介质条目主键。
        /// </summary>
        public int YearlyArchiveRegisterMediaId { get; set; }

        /// <summary>
        /// 关联创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 电子介质袋。
        /// </summary>
        public virtual YearlyElectronicArchiveUnit ElectronicArchiveUnit { get; set; } = null!;

        /// <summary>
        /// 登记介质条目。
        /// </summary>
        public virtual YearlyArchiveRegisterMedia MediaEntry { get; set; } = null!;
    }
}
