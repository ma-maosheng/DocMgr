namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档提交请求。
    /// </summary>
    public sealed record ElectronicArchiveSubmissionRequest
    {
        /// <summary>
        /// 当前电子介质袋数据。
        /// </summary>
        public YearlyElectronicArchiveUnit ArchiveUnit { get; init; } = new();

        /// <summary>
        /// 本次拟入袋的资料子项主键集合。
        /// </summary>
        public IReadOnlyList<int> MediaItemIds { get; init; } = [];

        /// <summary>
        /// 立档存储路径（MediaItemId → 路径）；拷贝型立档由第四步编辑。
        /// </summary>
        public IReadOnlyDictionary<int, string> FilingStoragePathByMediaItemId { get; init; }
            = new Dictionary<int, string>();

        /// <summary>
        /// 本次拟入袋的电子介质条目主键集合（由子项推导，兼容旧调用）。
        /// </summary>
        public IReadOnlyList<int> MediaEntryIds { get; init; } = [];

        /// <summary>
        /// 当前电子介质立档提交模式。
        /// </summary>
        public ElectronicArchiveSubmissionMode SubmissionMode { get; init; }

        /// <summary>
        /// 是否为硬盘留存场景。
        /// </summary>
        public bool IsRetainedHardDiskScenario { get; init; }

        /// <summary>
        /// 既有电子介质袋主键；新建时为空。
        /// </summary>
        public int? ExistingElectronicArchiveUnitId { get; init; }

        /// <summary>
        /// 外来硬盘临时登记信息；提交电子立档时再同步入库。
        /// </summary>
        public PendingExternalHardDiskRegistration? PendingExternalHardDisk { get; init; }

        /// <summary>
        /// 留存场景下所选借出硬盘候选信息。
        /// </summary>
        public HardDiskMediaReturnCandidate? BorrowedHardDiskCandidate { get; init; }

        /// <summary>
        /// 是否为光盘直接留袋场景。
        /// </summary>
        public bool IsOpticalDiscArchiveScenario { get; init; }

        /// <summary>
        /// 当前电子介质立档所选处理方式。
        /// </summary>
        public string FilingMode { get; init; } = string.Empty;

        /// <summary>
        /// 若留存硬盘未直接作为立档介质，是否需要对原硬盘执行格式化。
        /// </summary>
        public bool RequiresFormatRetainedHardDisk { get; init; }

        /// <summary>
        /// 并档场景下的目标电子袋存放位置快照（用于执行逻辑报告提示）。
        /// </summary>
        public string AppendTargetStorageLocation { get; init; } = string.Empty;
    }
}
