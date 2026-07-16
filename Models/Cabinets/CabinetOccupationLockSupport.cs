using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜界面占用/征用标识文案辅助。
    /// </summary>
    public static class CabinetOccupationLockSupport
    {
        private const string WithdrawalLockKindText = "出库预订";

        /// <summary>
        /// 解析档口卡片角标短文案。
        /// </summary>
        public static string ResolveBadgeText(CabinetOccupationLockDescriptor descriptor)
        {
            if (!descriptor.HasLock)
            {
                return string.Empty;
            }

            return ResolveBadgeText(descriptor.LockKindText, descriptor.BusinessTypeText);
        }

        /// <summary>
        /// 按占用类别与业务类型解析角标短文案。
        /// </summary>
        public static string ResolveBadgeText(string? lockKindText, string? businessTypeText)
        {
            string businessType = businessTypeText?.Trim() ?? string.Empty;
            if (string.Equals(businessType, HardDiskRegisterLock.BusinessTypeArchiveOutboundRequisition, StringComparison.Ordinal))
            {
                return "征用";
            }

            if (string.Equals(businessType, HardDiskRegisterLock.BusinessTypeArchiveRegister, StringComparison.Ordinal))
            {
                return "登记";
            }

            if (string.Equals(businessType, HardDiskRegisterLock.BusinessTypeOutboundApplication, StringComparison.Ordinal))
            {
                return "借出";
            }

            if (string.Equals(lockKindText?.Trim(), WithdrawalLockKindText, StringComparison.Ordinal))
            {
                return "预订";
            }

            return "占用";
        }
    }
}
