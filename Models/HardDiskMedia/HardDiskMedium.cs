using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质主表，记录介质当前状态。
    /// </summary>
    public class HardDiskMedium
    {
        public const string NatureBlank = "空白介质";
        public const string NatureDataCarrier = "资料载体";

        public const string StatusInStockBlank = "在库(空盘)";
        public const string StatusInStockData = "在库(资料)";
        public const string StatusInStockDamaged = "在库(损坏)";
        public const string StatusInStockLost = "在库(盘失)";
        public const string StatusInStockScrap = "在库(拟销)";
        public const string StatusOutTemporary = "出库(临时)";
        public const string StatusOutLongTerm = "出库(长期)";
        public const string StatusOutPermanent = "出库(永久)";
        public const string StatusOutLost = "出库(挂失)";
        public const string StatusDisposed = "离库(处置)";

        public const string StatusBlankInStock = StatusInStockBlank;
        public const string StatusBorrowed = StatusOutTemporary;
        public const string StatusCarrierInStock = StatusInStockData;
        public const string StatusTransferred = StatusOutPermanent;

        /// <summary>
        /// 判断硬盘是否处于在库可迁档状态（含资料盘与损坏盘；不含盘失终态）。
        /// </summary>
        public static bool IsInStockRelocatableStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            string normalized = status.Trim();
            return string.Equals(normalized, StatusInStockData, StringComparison.Ordinal)
                || string.Equals(normalized, StatusInStockDamaged, StringComparison.Ordinal);
        }

        /// <summary>
        /// 是否为不可再业务占用的终态（离库处置 / 出库挂失 / 在库盘失）。
        /// </summary>
        public static bool IsTerminalUnavailableStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            string normalized = status.Trim();
            return string.Equals(normalized, StatusDisposed, StringComparison.Ordinal)
                || string.Equals(normalized, StatusOutLost, StringComparison.Ordinal)
                || string.Equals(normalized, StatusInStockLost, StringComparison.Ordinal)
                || string.Equals(normalized, StatusInStockScrap, StringComparison.Ordinal);
        }

        public const string RegistrationMethodImported = "文件导入登记";
        public const string RegistrationMethodManual = "手工录入登记";
        public const string RegistrationMethodArchive = "资料存档登记";

        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 硬盘编号。
        /// </summary>
        public string DiskCode { get; set; } = string.Empty;

        /// <summary>
        /// 序列号。
        /// </summary>
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>
        /// 硬盘类型。
        /// </summary>
        public string DiskType { get; set; } = string.Empty;

        /// <summary>
        /// 品牌。
        /// </summary>
        public string Brand { get; set; } = string.Empty;

        /// <summary>
        /// 容量。
        /// </summary>
        public string Capacity { get; set; } = string.Empty;

        /// <summary>
        /// 接口类型。
        /// </summary>
        public string InterfaceType { get; set; } = string.Empty;

        /// <summary>
        /// 登记人。
        /// </summary>
        public string RegisterPerson { get; set; } = string.Empty;

        /// <summary>
        /// 登记日期。
        /// </summary>
        public DateTime RegisterDate { get; set; }

        /// <summary>
        /// 出厂日期。
        /// </summary>
        public DateTime? FactoryDate { get; set; }

        /// <summary>
        /// 登记方式。
        /// </summary>
        public string RegistrationMethod { get; set; } = string.Empty;

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remark { get; set; } = string.Empty;

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
        /// 关联申请单集合。
        /// </summary>
        public virtual List<HardDiskMediaApplication> Applications { get; set; } = new();

        /// <summary>
        /// 关联流转记录集合。
        /// </summary>
        public virtual List<HardDiskMediaTransaction> Transactions { get; set; } = new();

        /// <summary>
        /// 关联电子立档单元集合。
        /// </summary>
        public virtual List<YearlyElectronicArchiveUnitMediumLink> ElectronicArchiveLinks { get; set; } = new();

        /// <summary>
        /// 关联硬盘台账（当前状态）。
        /// </summary>
        public virtual HardDiskLedger? Ledger { get; set; }

        /// <summary>
        /// 当前临时占用锁。
        /// </summary>
        public virtual HardDiskRegisterLock? RegisterLock { get; set; }

        /// <summary>
        /// 台账列表征用标识列标记（有征用锁时显示锁图标；WPF 多为单色，界面以红色显示）。
        /// </summary>
        [NotMapped]
        public string RegisterLockMarkText => RegisterLock != null
            ? CabinetOccupationLockSupport.LockBadgeMark
            : string.Empty;

        /// <summary>
        /// 台账列表征用详情列显示文本。
        /// </summary>
        [NotMapped]
        public string RegisterLockDisplayText => HardDiskRegisterLockFilterSupport.GetGridDisplayText(RegisterLock);
    }
}
