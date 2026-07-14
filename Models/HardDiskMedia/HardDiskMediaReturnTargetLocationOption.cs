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
        /// 下拉展示文本。
        /// </summary>
        public string DisplayText => $"{Location}（{ExistingMediumCount}盘）";
    }
}
