namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘临时占用锁记录。
    /// </summary>
    public class HardDiskRegisterLock
    {
        public const string BusinessTypeArchiveRegister = "年度资料登记";
        public const string BusinessTypeOutboundApplication = "硬盘借出申请";
        public const string BusinessTypeArchiveOutboundRequisition = "资料出库征用";
        public const string BusinessTypeNetworkOutboundRequisition = "出网征用";

        /// <summary>资料室存档出网预定库内空盘：只占用，不做出库。</summary>
        public const string BusinessTypeNetworkOutboundArchiveHold = "出网存档占盘";
        public const string BusinessTypeDisposal = "硬盘离库处置";
        public const string BusinessTypeInventoryRegister = "硬盘盘库登记";
        public const string BusinessTypeArchiveInventoryRegister = "资料盘库登记";
        public const string BusinessTypeArchiveDisposal = "资料离库处置";

        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 硬盘主表主键。
        /// </summary>
        public int MediumId { get; set; }

        /// <summary>
        /// 占用业务类型。
        /// </summary>
        public string BusinessType { get; set; } = string.Empty;

        /// <summary>
        /// 占用业务记录主键。
        /// </summary>
        public int? BusinessRecordId { get; set; }

        /// <summary>
        /// 占用业务单号。
        /// </summary>
        public string BusinessNo { get; set; } = string.Empty;

        /// <summary>
        /// 进入占用前状态。
        /// </summary>
        public string PreviousStatus { get; set; } = string.Empty;

        /// <summary>
        /// 锁定时间。
        /// </summary>
        public DateTime LockedTime { get; set; }

        /// <summary>
        /// 关联硬盘。
        /// </summary>
        public virtual HardDiskMedium Medium { get; set; } = null!;

        /// <summary>
        /// 是否为年度资料登记占用锁。
        /// </summary>
        public static bool IsArchiveRegister(HardDiskRegisterLock? lockItem)
        {
            return lockItem != null
                && string.Equals(lockItem.BusinessType, BusinessTypeArchiveRegister, StringComparison.Ordinal);
        }

        /// <summary>是否为资料室存档出网对库内空盘的占用锁。</summary>
        public static bool IsNetworkOutboundArchiveHold(HardDiskRegisterLock? lockItem)
        {
            return lockItem != null
                && string.Equals(lockItem.BusinessType, BusinessTypeNetworkOutboundArchiveHold, StringComparison.Ordinal);
        }

        /// <summary>占用锁是否归属指定出网单。</summary>
        public static bool IsOwnedByOutbound(HardDiskRegisterLock? lockItem, string? outboundNo)
        {
            string trimmed = outboundNo?.Trim() ?? string.Empty;
            return lockItem != null
                && !string.IsNullOrWhiteSpace(trimmed)
                && string.Equals(lockItem.BusinessNo, trimmed, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 占用锁是否归属指定年度资料登记申请。
        /// </summary>
        public static bool IsOwnedByArchiveRegisterRecord(HardDiskRegisterLock? lockItem, int recordId, string? formNo)
        {
            if (!IsArchiveRegister(lockItem))
            {
                return false;
            }

            if (recordId > 0 && lockItem!.BusinessRecordId == recordId)
            {
                return true;
            }

            string trimmedFormNo = formNo?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(trimmedFormNo)
                && string.Equals(lockItem!.BusinessNo, trimmedFormNo, StringComparison.OrdinalIgnoreCase);
        }
    }
}
