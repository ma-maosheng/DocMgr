namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料流转履历：业务类型与来源标识。
    /// </summary>
    public static class MaterialTransactionDomainValues
    {
        public const string TypeFiling = "Filing";
        public const string TypeRelocation = "Relocation";
        public const string TypeOutbound = "Outbound";
        public const string TypeReturn = "Return";
        public const string TypeInventoryRegister = "InventoryRegister";
        public const string TypeInventoryRegisterLost = "InventoryRegisterLost";
        public const string TypeInventoryRegisterDamage = "InventoryRegisterDamage";
        public const string TypeInventoryRegisterScrap = "InventoryRegisterScrap";
        public const string TypeDisposal = "Disposal";

        public const string SourceFilingFact = "FilingFact";
        public const string SourceRelocationItem = "RelocationItem";
        public const string SourceOutboundSyncEntry = "OutboundSyncEntry";
        public const string SourceReturnItem = "ReturnItem";
        public const string SourceInventoryItem = "InventoryRegisterItem";
        public const string SourceDisposalItem = "DisposalItem";

        public const string ProcessNodeCategoryReservation = "Reservation";
        public const string ProcessNodeCategoryCancelled = "Cancelled";
        public const string ProcessNodeCategoryConfirmed = "Confirmed";

        public static string MapTypeDisplay(string transactionType) => transactionType switch
        {
            TypeFiling => "立档",
            TypeRelocation => "迁档",
            TypeOutbound => "资料出库",
            TypeReturn => "资料归还",
            TypeInventoryRegister => "盘库登记",
            TypeInventoryRegisterLost => "盘库登记(盘失)",
            TypeInventoryRegisterDamage => "盘库登记(损坏)",
            TypeInventoryRegisterScrap => "盘库登记(拟销)",
            TypeDisposal => "资料离库处置",
            _ => transactionType
        };

        public static string MapRelocationModeDisplay(string mode) => mode switch
        {
            ArchiveRelocationMode.PhysicalMove => "物理位置迁移",
            ArchiveRelocationMode.MoveToEmpty => "迁入空盘/空袋",
            ArchiveRelocationMode.MergeToExisting => "并入已有容器",
            ArchiveRelocationMode.BatchPhysicalMove => "档口批量搬迁",
            _ => mode
        };

        public static string MapLifecycleStatusDisplay(string status) => status switch
        {
            FilingFactLifecycleStatus.InArchive => "在库",
            FilingFactLifecycleStatus.Borrowed => "借出中",
            FilingFactLifecycleStatus.Transferred => "已转移",
            FilingFactLifecycleStatus.Destroyed => "已销毁",
            FilingFactLifecycleStatus.Disposed => "已处置",
            _ => string.IsNullOrWhiteSpace(status) ? "—" : status
        };
    }

    /// <summary>
    /// 立档事实流转履历展示行。
    /// </summary>
    public sealed class MaterialTransactionTimelineRow
    {
        public DateTime OperatedAt { get; init; }

        public string OperatedAtDisplay => OperatedAt.ToString("yyyy-MM-dd HH:mm");

        public string TransactionType { get; init; } = string.Empty;

        public string TransactionTypeDisplay => MaterialTransactionDomainValues.MapTypeDisplay(TransactionType);

        public string BusinessNo { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string LocationChangeDisplay { get; init; } = string.Empty;

        public string LifecycleChangeDisplay { get; init; } = string.Empty;

        public string OperatorName { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;
    }

    /// <summary>
    /// 出库申请流程节点展示行（预订/撤销/同步，非实物流转）。
    /// </summary>
    public sealed class MaterialOutboundProcessNodeRow
    {
        public DateTime OperatedAt { get; init; }

        public string OperatedAtDisplay => OperatedAt.ToString("yyyy-MM-dd HH:mm");

        public string OutboundNo { get; init; } = string.Empty;

        public string OutboundStatusDisplay { get; init; } = string.Empty;

        public string NodeCategoryDisplay { get; init; } = string.Empty;

        public string ProcessNodeDisplay { get; init; } = string.Empty;

        public string UsageModeDisplay { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string OperatorName { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public bool IsProcessOnly { get; init; }
    }
}
