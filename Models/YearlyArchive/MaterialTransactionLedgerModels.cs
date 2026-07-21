using DocMgr.Models.ArchiveContainers;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 迁档台账查询条件。
    /// </summary>
    public sealed class RelocationLedgerSearchCriteria
    {
        public DateTime? OperatedFrom { get; set; }

        public DateTime? OperatedTo { get; set; }

        public string RelocationMode { get; set; } = string.Empty;

        public string MediaKind { get; set; } = string.Empty;

        public string BusinessNo { get; set; } = string.Empty;

        public string OperatorName { get; set; } = string.Empty;

        public string Keyword { get; set; } = string.Empty;
    }

    /// <summary>
    /// 流转台账（实物流转）查询条件；不含迁档、不含立档业务。
    /// </summary>
    public sealed class CirculationLedgerSearchCriteria
    {
        public DateTime? OperatedFrom { get; set; }

        public DateTime? OperatedTo { get; set; }

        /// <summary>空表示全部；资料出库/资料归还。</summary>
        public string TransactionType { get; set; } = string.Empty;

        public string MediaKind { get; set; } = string.Empty;

        public string BusinessNo { get; set; } = string.Empty;

        public string OperatorName { get; set; } = string.Empty;

        public string Keyword { get; set; } = string.Empty;

        /// <summary>
        /// 容器列表范围，见 <see cref="CirculationLedgerListingMode"/>。
        /// </summary>
        public string ListingMode { get; set; } = CirculationLedgerListingMode.CirculationOnly;
    }

    /// <summary>
    /// 实物流转台账：一级容器列表范围。
    /// </summary>
    public static class CirculationLedgerListingMode
    {
        /// <summary>仅展示有出库/归还流水的容器。</summary>
        public const string CirculationOnly = "CirculationOnly";

        /// <summary>同时展示已立档入库、从未出库/归还的在库容器。</summary>
        public const string IncludeNeverCirculated = "IncludeNeverCirculated";

        public static string MapDisplay(string mode) => mode switch
        {
            IncludeNeverCirculated => "含未流转在库容器",
            _ => "仅有流转记录"
        };
    }

    /// <summary>
    /// 出库流程节点横向查询条件。
    /// </summary>
    public sealed class OutboundProcessNodeLedgerSearchCriteria
    {
        public DateTime? OperatedFrom { get; set; }

        public DateTime? OperatedTo { get; set; }

        public string OutboundNo { get; set; } = string.Empty;

        /// <summary>流程预订 / 流程撤销 / 办结同步；空表示全部。</summary>
        public string NodeCategory { get; set; } = string.Empty;

        public string OperatorName { get; set; } = string.Empty;

        public string ApplicantName { get; set; } = string.Empty;

        public string Keyword { get; set; } = string.Empty;
    }

    /// <summary>
    /// 迁档/流转台账共用流水行。
    /// </summary>
    public sealed class MaterialTransactionLedgerRow
    {
        public int TransactionId { get; init; }

        public int FilingFactId { get; init; }

        public DateTime OperatedAt { get; init; }

        public string OperatedAtDisplay => OperatedAt.ToString("yyyy-MM-dd HH:mm");

        public string TransactionType { get; init; } = string.Empty;

        public string TransactionTypeDisplay =>
            MaterialTransactionDomainValues.MapTypeDisplay(TransactionType);

        public string BusinessNo { get; init; } = string.Empty;

        public string RelocationMode { get; init; } = string.Empty;

        public string RelocationModeDisplay =>
            string.IsNullOrWhiteSpace(RelocationMode)
                ? string.Empty
                : MaterialTransactionDomainValues.MapRelocationModeDisplay(RelocationMode);

        public string FilingFactNo { get; init; } = string.Empty;

        public string FormNo { get; init; } = string.Empty;

        public string MediaKind { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string LocationChangeDisplay { get; init; } = string.Empty;

        public string LifecycleChangeDisplay { get; init; } = string.Empty;

        public string OperatorName { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public string ContainerCode { get; init; } = string.Empty;

        public ArchiveContainerKind ContainerKind { get; init; }

        public string BeforeContainerCode { get; init; } = string.Empty;

        public string AfterContainerCode { get; init; } = string.Empty;

        public string ContainerYear { get; init; } = string.Empty;

        public string ContainerProjectName { get; init; } = string.Empty;

        public string ContainerLocationDisplay { get; init; } = string.Empty;

        public string ContainerStatusDisplay { get; init; } = string.Empty;
    }

    /// <summary>
    /// 流转台账一级：按容器（档案盒 / 电子介质袋）汇总。
    /// </summary>
    public sealed class CirculationContainerMasterRow
    {
        public string ContainerCode { get; init; } = string.Empty;

        public ArchiveContainerKind ContainerKind { get; init; }

        public string ContainerKindDisplay =>
            CirculationLedgerDisplayValues.MapContainerKindDisplay(ContainerKind);

        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string LocationDisplay { get; init; } = string.Empty;

        public string ContainerStatusDisplay { get; init; } = string.Empty;

        public int MaterialCount { get; init; }

        public int TransactionCount { get; init; }

        public int ProcessNodeCount { get; init; }

        public bool HasCirculationActivity => TransactionCount > 0 || ProcessNodeCount > 0;

        public string ActivitySummary =>
            HasCirculationActivity
                ? $"出库/归还 {TransactionCount} · 申请节点 {ProcessNodeCount}"
                : CirculationLedgerDisplayValues.NeverCirculatedDisplay;

        public DateTime LatestOperatedAt { get; init; }

        public string LatestOperatedAtDisplay => LatestOperatedAt.ToString("yyyy-MM-dd HH:mm");

        public string LatestTransactionTypeDisplay { get; init; } = string.Empty;

        public int RepresentativeFilingFactId { get; init; }

        public bool HasCirculationTransactions => HasCirculationActivity;
    }

    /// <summary>
    /// 流转台账二级：业务单（出库单 / 归还单）。
    /// </summary>
    public sealed class CirculationLedgerBusinessRow
    {
        public string BusinessKind { get; init; } = string.Empty;

        public string BusinessKindDisplay =>
            CirculationLedgerBusinessKind.MapDisplay(BusinessKind);

        public string BusinessNo { get; init; } = string.Empty;

        public string DisplayTitle => $"{BusinessKindDisplay} · {BusinessNo}";

        public DateTime LatestOperatedAt { get; init; }

        public string LatestOperatedAtDisplay => LatestOperatedAt.ToString("yyyy-MM-dd HH:mm");

        public string LatestSummary { get; init; } = string.Empty;

        public int SubItemCount { get; init; }

        public string OutboundStatusDisplay { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public int RepresentativeFilingFactId { get; init; }
    }

    /// <summary>
    /// 流转台账三级：业务单下的明细时间线行（出库/归还落账与申请节点合并）。
    /// </summary>
    public sealed class CirculationLedgerSubItemRow
    {
        public int ItemId { get; init; }

        public CirculationLedgerSubItemKind Kind { get; init; }

        public DateTime OperatedAt { get; init; }

        public string OperatedAtDisplay => OperatedAt.ToString("yyyy-MM-dd HH:mm");

        public string CategoryDisplay { get; init; } = string.Empty;

        public string DetailDisplay { get; init; } = string.Empty;

        public string FilingFactNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string LocationChangeDisplay { get; init; } = string.Empty;

        public string LifecycleChangeDisplay { get; init; } = string.Empty;

        public string OutboundStatusDisplay { get; init; } = string.Empty;

        public string UsageModeDisplay { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string OperatorName { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public int FilingFactId { get; init; }
    }

    public enum CirculationLedgerSubItemKind
    {
        ProcessNode = 0,
        PhysicalTransaction = 1
    }

    public static class CirculationLedgerBusinessKind
    {
        public const string Outbound = "Outbound";
        public const string Return = "Return";

        public static string MapDisplay(string kind) => kind switch
        {
            Outbound => "资料出库",
            Return => "资料归还",
            _ => kind
        };
    }

    /// <summary>
    /// 出库流程节点台账一级：按容器汇总。
    /// </summary>
    public sealed class OutboundProcessNodeContainerMasterRow
    {
        public string ContainerCode { get; init; } = string.Empty;

        public ArchiveContainerKind ContainerKind { get; init; }

        public string ContainerKindDisplay =>
            CirculationLedgerDisplayValues.MapContainerKindDisplay(ContainerKind);

        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string LocationDisplay { get; init; } = string.Empty;

        public string ContainerStatusDisplay { get; init; } = string.Empty;

        public int MaterialCount { get; init; }

        public int NodeCount { get; init; }

        public string RelatedOutboundSummary { get; init; } = string.Empty;

        public DateTime LatestOperatedAt { get; init; }

        public string LatestOperatedAtDisplay => LatestOperatedAt.ToString("yyyy-MM-dd HH:mm");

        public string LatestNodeSummary { get; init; } = string.Empty;

        public int RepresentativeFilingFactId { get; init; }
    }

    /// <summary>
    /// 流转台账容器聚合键。
    /// </summary>
    public readonly record struct CirculationContainerKey(string ContainerCode, ArchiveContainerKind ContainerKind);

    /// <summary>
    /// 流转台账容器展示文案。
    /// </summary>
    public static class CirculationLedgerDisplayValues
    {
        public static string MapContainerKindDisplay(ArchiveContainerKind kind) => kind switch
        {
            ArchiveContainerKind.ArchiveBox => "档案盒",
            ArchiveContainerKind.ElectronicBag => "电子介质袋",
            _ => kind.ToString()
        };

        public static string MapContainerStatusDisplay(string? status) => status switch
        {
            ArchiveContainerLifecycleStatus.InUse => "在用",
            ArchiveContainerLifecycleStatus.Emptied => "已清空",
            ArchiveContainerLifecycleStatus.Retired => "已销号",
            ArchiveContainerLifecycleStatus.Relocated => "已迁出",
            ArchiveContainerLifecycleStatus.Disposed => "已处置",
            _ => string.IsNullOrWhiteSpace(status) ? "—" : status
        };

        public const string NeverCirculatedDisplay = "未流转";
    }

    /// <summary>
    /// 出库流程节点横向台账行。
    /// </summary>
    public sealed class MaterialOutboundProcessNodeSearchRow
    {
        public int SyncEntryId { get; init; }

        public int FilingFactId { get; init; }

        public DateTime OperatedAt { get; init; }

        public string OperatedAtDisplay => OperatedAt.ToString("yyyy-MM-dd HH:mm");

        public string OutboundNo { get; init; } = string.Empty;

        public string OutboundStatusDisplay { get; init; } = string.Empty;

        public string NodeCategoryDisplay { get; init; } = string.Empty;

        public string ProcessNodeDisplay { get; init; } = string.Empty;

        public string UsageModeDisplay { get; init; } = string.Empty;

        public string FilingFactNo { get; init; } = string.Empty;

        public string FormNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string OperatorName { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public string ContainerCode { get; init; } = string.Empty;

        public ArchiveContainerKind ContainerKind { get; init; }

        public string ContainerYear { get; init; } = string.Empty;

        public string ContainerProjectName { get; init; } = string.Empty;

        public string ContainerLocationDisplay { get; init; } = string.Empty;

        public string ContainerStatusDisplay { get; init; } = string.Empty;
    }
}
