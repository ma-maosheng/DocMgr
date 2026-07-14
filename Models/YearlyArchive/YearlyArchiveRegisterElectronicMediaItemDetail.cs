using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子登记介质明细扩展信息（与 <see cref="YearlyArchiveRegisterMediaItem"/> 一对一）。
    /// </summary>
    public class YearlyArchiveRegisterElectronicMediaItemDetail
    {
        [Key]
        public int MediaItemId { get; set; }

        /// <summary>
        /// 资料类型：文档类 / 数据类。
        /// </summary>
        public string MaterialCategory { get; set; } = string.Empty;

        /// <summary>
        /// 所属子类，受资料类型 Scope 约束。
        /// </summary>
        public string SubCategory { get; set; } = string.Empty;

        /// <summary>
        /// 数据组织形式：目录型 / 文件型。
        /// </summary>
        public string DataOrganizationForm { get; set; } = string.Empty;

        /// <summary>
        /// 数据量（MB）。
        /// </summary>
        public decimal DataSizeMb { get; set; }

        public virtual YearlyArchiveRegisterMediaItem MediaItem { get; set; } = null!;

        public virtual List<YearlyArchiveRegisterElectronicMediaItemEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// 电子登记介质明细下的目录/文件清单。
    /// </summary>
    public class YearlyArchiveRegisterElectronicMediaItemEntry
    {
        [Key]
        public int Id { get; set; }

        public int ElectronicMediaItemDetailId { get; set; }

        /// <summary>
        /// 目录 / 文件。
        /// </summary>
        public string EntryKind { get; set; } = string.Empty;

        public string EntryName { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;

        public decimal? SizeMb { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public int SortOrder { get; set; }

        public virtual YearlyArchiveRegisterElectronicMediaItemDetail ElectronicDetail { get; set; } = null!;
    }
}
