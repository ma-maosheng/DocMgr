namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘台账当前状态表。
    /// </summary>
    public class HardDiskLedger
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 关联硬盘主表ID。
        /// </summary>
        public int MediumId { get; set; }

        /// <summary>
        /// 硬盘编号。
        /// </summary>
        public string DiskCode { get; set; } = string.Empty;

        /// <summary>
        /// 介质状态。
        /// </summary>
        public string MediaStatus { get; set; } = HardDiskMedium.StatusInStockBlank;

        /// <summary>
        /// 介质属性。
        /// </summary>
        public string MediaNature { get; set; } = HardDiskMedium.NatureBlank;

        /// <summary>
        /// 存放位置。
        /// </summary>
        public string StorageLocation { get; set; } = string.Empty;

        /// <summary>
        /// 持有人/保管单位。
        /// </summary>
        public string HolderOrOrganization { get; set; } = string.Empty;

        /// <summary>
        /// 是否需归还。
        /// </summary>
        public bool NeedReturn { get; set; }

        /// <summary>
        /// 登记人。
        /// </summary>
        public string RegisterPerson { get; set; } = string.Empty;

        /// <summary>
        /// 登记日期。
        /// </summary>
        public DateTime RegisterDate { get; set; }

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 更新时间。
        /// </summary>
        public DateTime UpdatedTime { get; set; }

        /// <summary>
        /// 关联硬盘。
        /// </summary>
        public virtual HardDiskMedium? Medium { get; set; }
    }
}
