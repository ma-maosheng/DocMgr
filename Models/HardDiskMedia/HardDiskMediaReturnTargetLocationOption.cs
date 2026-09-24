using DocMgr.Models.Cabinets;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 归还登记可选归位位置。
    /// </summary>
    public sealed record HardDiskMediaReturnTargetLocationOption
    {
        /// <summary>
        /// 实际归位位置。
        /// </summary>
        public string Location { get; init; } = string.Empty;

        /// <summary>
        /// 档口当前在位硬盘数量。
        /// </summary>
        public int ExistingMediumCount { get; init; }

        /// <summary>
        /// 该档口所属柜体配置的最大容量；0 表示未知（按默认硬盘容量处理）。
        /// </summary>
        public int SlotCapacity { get; init; }

        /// <summary>
        /// 用于展示与空余计算的有效容量（容量未配置时回退默认值）。
        /// </summary>
        public int ResolvedSlotCapacity => SlotCapacity > 0
            ? SlotCapacity
            : CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;

        /// <summary>
        /// 空余盘位数（不低于 0）。
        /// </summary>
        public int RemainingCapacity => Math.Max(0, ResolvedSlotCapacity - ExistingMediumCount);

        /// <summary>
        /// 下拉展示文本：优先标出空余盘位，便于推荐与挑选。
        /// </summary>
        public string DisplayText =>
            $"{Location}（空{RemainingCapacity}｜{ExistingMediumCount}/{ResolvedSlotCapacity}）";
    }
}
