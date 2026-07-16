namespace DocMgr.Models.Cabinets
{
    public sealed class CabinetArchiveBoxDescriptor
    {
        public string BoxCode { get; init; } = string.Empty;

        public string BoxLabel { get; init; } = string.Empty;

        public string CategoryText { get; init; } = string.Empty;

        public string ArchiveTypeText { get; init; } = string.Empty;

        public string ArchiveIdentifierText { get; init; } = string.Empty;

        public bool IsYearlyArchiveDisplay { get; init; }

        public string ArchiveSequenceNoShortText { get; init; } = string.Empty;

        public string YearText { get; init; } = string.Empty;

        public string ProjectText { get; init; } = string.Empty;

        public string CountText { get; init; } = string.Empty;

        public int SequenceIndex { get; init; }

        public int ItemCount { get; init; }

        public string SlotCode { get; init; } = string.Empty;

        public bool IsMixedPlacement { get; init; }

        public string OriginalBoxNumberText { get; init; } = string.Empty;

        public string RelatedBoxCodesText { get; init; } = string.Empty;

        public int RelatedBoxCount { get; init; }

        public string MixedPlacementHint { get; init; } = string.Empty;

        public string SourceSummaryText { get; init; } = string.Empty;

        public int PendingSortingRecordCount { get; init; }

        public string BoxSpecification { get; init; } = string.Empty;

        public string PlacementMode { get; init; } = string.Empty;

        public double LayoutX { get; init; }

        public double LayoutY { get; init; }

        public double LayoutWidth { get; init; }

        public double LayoutHeight { get; init; }

        public int YearlyArchiveBoxId { get; init; }

        /// <summary>模拟介质档案盒内出库待还份数合计（年度盒专用）。</summary>
        public int PendingReturnCopyCount { get; init; }

        public bool HasOccupationLock { get; init; }

        public string OccupationLockToolTipText { get; init; } = string.Empty;

        public string OccupationLockBadgeText { get; init; } = string.Empty;
    }
}
