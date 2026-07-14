namespace DocMgr.Models.OpticalDiscMedia
{
    /// <summary>
    /// 数据光盘台账当前状态表（与 <see cref="OpticalDiscMedium"/> 一一对应，承载动态状态/位置/持有人）。
    /// </summary>
    public class OpticalDiscLedger
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 关联光盘主表 ID。
        /// </summary>
        public int MediumId { get; set; }

        /// <summary>
        /// 光盘编号（冗余便于检索）。
        /// </summary>
        public string DiscCode { get; set; } = string.Empty;

        /// <summary>
        /// 介质状态。
        /// </summary>
        public string MediaStatus { get; set; } = OpticalDiscMedium.StatusInStock;

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
        /// 关联光盘。
        /// </summary>
        public virtual OpticalDiscMedium? Medium { get; set; }
    }
}
