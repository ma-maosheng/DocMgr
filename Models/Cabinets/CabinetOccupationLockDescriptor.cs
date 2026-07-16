namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜界面容器/介质占用标识（硬盘占用锁、出库提档预订等）。
    /// </summary>
    public sealed class CabinetOccupationLockDescriptor
    {
        public static CabinetOccupationLockDescriptor Empty { get; } = new();

        public bool HasLock { get; init; }

        /// <summary>占用锁 / 出库预订 等摘要类别。</summary>
        public string LockKindText { get; init; } = string.Empty;

        public string BusinessTypeText { get; init; } = string.Empty;

        public string BusinessNoText { get; init; } = string.Empty;

        public int ReservedCopyCount { get; init; }

        /// <summary>追加到 ToolTip 的占用说明。</summary>
        public string ToolTipSupplement { get; init; } = string.Empty;

        /// <summary>档口卡片角标短文案（如「征用」「预订」）。</summary>
        public string BadgeText => CabinetOccupationLockSupport.ResolveBadgeText(this);
    }
}
