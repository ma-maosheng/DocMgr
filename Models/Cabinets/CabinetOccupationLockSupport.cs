namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜界面占用/征用标识文案辅助。
    /// </summary>
    public static class CabinetOccupationLockSupport
    {
        /// <summary>占用/征用角标统一符号（WPF 多为单色字形，界面以红色区分）。</summary>
        public const string LockBadgeMark = "🔒";

        /// <summary>
        /// 解析档口卡片角标短文案：有占用时统一显示锁标记。
        /// </summary>
        public static string ResolveBadgeText(CabinetOccupationLockDescriptor descriptor)
        {
            return descriptor.HasLock ? LockBadgeMark : string.Empty;
        }
    }
}
