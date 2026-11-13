namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 可并入非空硬盘选择弹窗中的一行。筛选标记在打开弹窗时算好。
    /// </summary>
    public sealed class AppendHardDiskPickerRow
    {
        public int UnitId { get; init; }

        public string GroupLabel { get; init; } = string.Empty;

        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string DiskCode { get; init; } = string.Empty;

        /// <summary>实施年度与项目名称，形如 2026-基础测绘。多项以顿号连接。</summary>
        public string YearProjectText { get; init; } = "—";

        public string AvailableCapacityText { get; init; } = "—";

        public decimal AvailableCapacityMb { get; init; }

        public string StorageLocation { get; init; } = string.Empty;

        public string Remarks { get; init; } = string.Empty;

        public bool CanSelect { get; init; }

        public bool CanHoldPendingBatch { get; init; }

        public bool BelongsToCurrentProject { get; init; }

        public bool BelongsToCurrentYear { get; init; }

        /// <summary>勾选同一项目名称时是否保留。只比较项目名称。</summary>
        public bool MatchesProjectFilter { get; init; }

        /// <summary>勾选同一实施年度时是否保留。只比较实施年度。</summary>
        public bool MatchesYearFilter { get; init; }

        /// <summary>两个条件都勾选时是否保留。</summary>
        public bool MatchesProjectYear { get; init; }

        /// <summary>出网申请已预定的并入目标，筛选时始终保留。</summary>
        public bool IsPredetermined { get; init; }
    }

    /// <summary>
    /// 可并入非空硬盘选择结果。
    /// </summary>
    public sealed class AppendHardDiskSelectionResult
    {
        public int UnitId { get; init; }

        public bool SameProjectOnly { get; init; }

        public bool SameYearOnly { get; init; }
    }
}
