namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 档案盒与资料子项的关联关系。
    /// </summary>
    public sealed class YearlyArchiveBoxMediaItemLink
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 档案盒主键。
        /// </summary>
        public int YearlyArchiveBoxId { get; set; }

        /// <summary>
        /// 资料子项主键。
        /// </summary>
        public int YearlyArchiveRegisterMediaItemId { get; set; }

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 档案盒。
        /// </summary>
        public YearlyArchiveBox ArchiveBox { get; set; } = null!;

        /// <summary>
        /// 资料子项。
        /// </summary>
        public YearlyArchiveRegisterMediaItem MediaItem { get; set; } = null!;
    }
}
