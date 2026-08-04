namespace DocMgr.Models.Cabinets
{
    public sealed class CabinetArchiveBoxContentDescriptor
    {
        public string BoxCode { get; init; } = string.Empty;

        public string SourceType { get; init; } = string.Empty;

        public string CategoryText { get; init; } = string.Empty;

        public string IdentifierText { get; init; } = string.Empty;

        public string TitleText { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ProjectYear { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string ProvideUnit { get; init; } = string.Empty;

        public string ItemType { get; init; } = string.Empty;

        public string ConfidentialLevel { get; init; } = string.Empty;

        /// <summary>审批确定的资料子项份数。</summary>
        public int ApprovedCopyCount { get; init; }

        public string Note { get; init; } = string.Empty;

        /// <summary>更具体的载体类型（介质类型与载体类别合并）。</summary>
        public string CarrierTypeText { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        /// <summary>登记申请上的归档目的。</summary>
        public string ArchivePurpose { get; init; } = string.Empty;

        public string StoragePath { get; init; } = string.Empty;

        public string FilingStoragePath { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = string.Empty;

        public string SubCategory { get; init; } = string.Empty;

        public string DataOrganizationForm { get; init; } = string.Empty;

        public string DataSizeText { get; init; } = string.Empty;

        public string ContentEntryBreakdownText { get; init; } = string.Empty;

        public string ContainerCode { get; init; } = string.Empty;

        public string BoxSpecs { get; init; } = string.Empty;

        public string MediumCode { get; init; } = string.Empty;

        public string FiledBy { get; init; } = string.Empty;

        public string ArchiveCopyRoleDisplay { get; init; } = string.Empty;

        public string QuantityText { get; init; } = string.Empty;

        public string DetailText { get; init; } = string.Empty;

        public string DateText { get; init; } = string.Empty;

        public bool IsMixedPlacement { get; init; }

        public string OriginalBoxNumberText { get; init; } = string.Empty;

        public string RelatedBoxCodesText { get; init; } = string.Empty;

        public int RelatedBoxCount { get; init; }

        public string PlacementNote { get; init; } = string.Empty;

        /// <summary>是否为年度资料子项行（含份数分解）。</summary>
        public bool IsYearlyArchiveMediaItem { get; init; }

        /// <summary>是否为电子介质子项（展示规则简化）。</summary>
        public bool IsElectronicMedia { get; init; }

        public int FiledCopyCount { get; init; }

        public int CurrentInArchiveCopyCount { get; init; }

        public int PendingReturnCopyCount { get; init; }

        public int NoReturnCopyCount { get; init; }

        public int LostCopyCount { get; init; }

        /// <summary>盘库登记丢失份数。</summary>
        public int InventoryLostCopyCount { get; init; }

        /// <summary>盘库登记拟销份数。</summary>
        public int InventoryScrapCopyCount { get; init; }

        /// <summary>介质盘库状态（电子：-/盘损/盘失/盘销；模拟：-）。</summary>
        public string ElectronicStockStatusText { get; init; } = string.Empty;

        public bool HasOccupationLock { get; init; }

        /// <summary>资料子项级占用状态（出库预订等）。</summary>
        public string OccupationLockDisplayText { get; init; } = string.Empty;

        public string CopyCountDisplayText =>
            IsElectronicMedia
                ? ElectronicStockStatusText
                : $"{FiledCopyCount}/{CurrentInArchiveCopyCount}/{PendingReturnCopyCount}/{NoReturnCopyCount}/{LostCopyCount}/{InventoryLostCopyCount}/{InventoryScrapCopyCount}";

        public CabinetArchiveContainerViewMode ViewMode { get; init; } = CabinetArchiveContainerViewMode.HistoryArchiveBox;
    }
}
