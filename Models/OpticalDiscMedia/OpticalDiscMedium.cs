using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.OpticalDiscMedia
{
    /// <summary>
    /// 数据光盘介质主表（静态信息）。动态状态/位置/持有人见 <see cref="OpticalDiscLedger"/>（台账分离）。
    /// </summary>
    public class OpticalDiscMedium
    {
        /// <summary>
        /// 在库状态。
        /// </summary>
        public const string StatusInStock = "在库(资料)";

        /// <summary>
        /// 出库状态。
        /// </summary>
        public const string StatusOut = "出库(临时)";

        /// <summary>
        /// 损坏状态。
        /// </summary>
        public const string StatusDamaged = "在库(损坏)";

        /// <summary>
        /// 销毁状态。
        /// </summary>
        public const string StatusDestroyed = "出库(销毁)";

        public const string RegistrationMethodManual = "手工录入登记";
        public const string RegistrationMethodArchive = "资料存档登记";

        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 光盘编号。
        /// </summary>
        public string DiscCode { get; set; } = string.Empty;

        /// <summary>
        /// 光盘类型。
        /// </summary>
        public string DiscType { get; set; } = "数据光盘";

        /// <summary>
        /// 容量。
        /// </summary>
        public string Capacity { get; set; } = string.Empty;

        /// <summary>
        /// 登记人。
        /// </summary>
        public string RegisterPerson { get; set; } = string.Empty;

        /// <summary>
        /// 登记日期。
        /// </summary>
        public DateTime RegisterDate { get; set; }

        /// <summary>
        /// 登记方式。
        /// </summary>
        public string RegistrationMethod { get; set; } = string.Empty;

        /// <summary>
        /// 来源类型。
        /// </summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// 来源记录键。
        /// </summary>
        public string SourceRecordKey { get; set; } = string.Empty;

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remarks { get; set; } = string.Empty;

        /// <summary>
        /// 是否逻辑删除。
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 更新时间。
        /// </summary>
        public DateTime UpdatedTime { get; set; }

        /// <summary>
        /// 关联电子立档单元集合。
        /// </summary>
        public virtual List<YearlyElectronicArchiveUnitDiscLink> ElectronicArchiveLinks { get; set; } = new();

        /// <summary>
        /// 关联光盘台账（当前状态）。
        /// </summary>
        public virtual OpticalDiscLedger? Ledger { get; set; }

        /// <summary>
        /// 关联流转记录集合。
        /// </summary>
        public virtual List<OpticalDiscMediaTransaction> Transactions { get; set; } = new();
    }
}
