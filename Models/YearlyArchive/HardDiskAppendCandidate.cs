namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 硬盘上已立档资料所属的项目与实施年度。
    /// </summary>
    public sealed class HardDiskAppendProjectRef
    {
        public int? ProjectId { get; init; }

        public string ProjectName { get; init; } = string.Empty;

        public string ImplementYear { get; init; } = string.Empty;
    }

    /// <summary>
    /// 可并入的在用数据硬盘。本项目已有的排在其他非空硬盘之前。
    /// </summary>
    public sealed class HardDiskAppendCandidate
    {
        public required YearlyElectronicArchiveUnit Unit { get; init; }

        /// <summary>
        /// 该盘的立档事实里已经有本次项目。
        /// </summary>
        public bool BelongsToCurrentProject { get; init; }

        /// <summary>
        /// 该盘已立档资料涉及的项目与实施年度。
        /// </summary>
        public IReadOnlyList<HardDiskAppendProjectRef> Projects { get; init; } = Array.Empty<HardDiskAppendProjectRef>();
    }
}
