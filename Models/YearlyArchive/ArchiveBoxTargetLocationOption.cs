namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 模拟介质立档可选物理档口。
    /// </summary>
    public sealed record ArchiveBoxTargetLocationOption
    {
        /// <summary>
        /// 档口键（柜面-层-列，不含盒内序号）。
        /// </summary>
        public string Location { get; init; } = string.Empty;

        /// <summary>
        /// 柜体名称。
        /// </summary>
        public string CabinetName { get; init; } = string.Empty;

        /// <summary>
        /// 面别。
        /// </summary>
        public string Side { get; init; } = string.Empty;

        /// <summary>
        /// 层号。
        /// </summary>
        public int Row { get; init; }

        /// <summary>
        /// 列号。
        /// </summary>
        public int Column { get; init; }

        /// <summary>
        /// 档口当前已有档案盒数量。
        /// </summary>
        public int ExistingBoxCount { get; init; }

        /// <summary>
        /// 推荐优先级（越小越优先）。
        /// </summary>
        public int Priority { get; init; }

        /// <summary>
        /// 当前规格下是否满足容量规则。
        /// </summary>
        public bool FitsCapacity { get; init; }

        /// <summary>
        /// 下拉展示文本。
        /// </summary>
        public string DisplayText => $"{Location}（{ExistingBoxCount}盒）";
    }
}
