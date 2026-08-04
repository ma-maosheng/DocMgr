namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜界面占用/征用标识文案辅助。
    /// </summary>
    public static class CabinetOccupationLockSupport
    {
        /// <summary>占用/征用数据层符号；开柜界面左下角统一显示「预订」（见 <see cref="CabinetOpenStatusBadgeSupport"/>）。</summary>
        public const string LockBadgeMark = "🔒";

        /// <summary>
        /// 解析档口卡片占用数据标记（锁符号）；展示文案由 <see cref="CabinetOpenStatusBadgeSupport.NormalizeReservationDisplayText"/> 统一为「预订」。
        /// </summary>
        public static string ResolveBadgeText(CabinetOccupationLockDescriptor descriptor)
        {
            return descriptor.HasLock ? LockBadgeMark : string.Empty;
        }
    }
}
