namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 迁档明细中以实体（档案盒/介质袋/硬盘/光盘）为单位的迁档前后物理位置编号对应关系。
    /// </summary>
    public sealed class CabinetOpenRelocationSessionItemRoute
    {
        public const string ArchiveBoxLabel = "档案盒";
        public const string ElectronicBagLabel = "介质袋";
        public const string HardDiskLabel = "硬盘";
        public const string OpticalDiscLabel = "光盘";

        /// <summary>实体类别（档案盒/介质袋/硬盘/光盘）；空表示仅档口级路线。</summary>
        public string EntityKindLabel { get; init; } = string.Empty;

        /// <summary>实体编号（盒号/袋号/盘号；袋可为复合文本如「袋号（硬盘 盘号）」）。</summary>
        public string EntityCode { get; init; } = string.Empty;

        /// <summary>迁移前完整物理位置编码（含档内序号，如 辛甲-1-1-01）。</summary>
        public string SourceLocation { get; init; } = string.Empty;

        /// <summary>迁移后完整物理位置编码（含档内序号）。</summary>
        public string TargetLocation { get; init; } = string.Empty;
    }

    /// <summary>
    /// 开柜会话内一次成功迁档的摘要（用于关窗核对打印）。
    /// </summary>
    public sealed class CabinetOpenRelocationSessionEntry
    {
        public int Sequence { get; init; }

        public DateTime OperatedAt { get; init; }

        public string MediaKind { get; init; } = string.Empty;

        public string ModeLabel { get; init; } = string.Empty;

        public string RelocationNo { get; init; } = string.Empty;

        /// <summary>原档口（柜体+面别+档口，便于阅读）。</summary>
        public string SourceSlotText { get; init; } = string.Empty;

        /// <summary>目标档口（柜体+面别+档口，便于阅读）。</summary>
        public string TargetSlotText { get; init; } = string.Empty;

        /// <summary>
        /// 以实体（档案盒/介质袋/硬盘/光盘）为单位的前后物理位置对应关系；
        /// 仅档口级路线（如空白盘整档口批量）时含一条 EntityKindLabel 为空的记录。
        /// </summary>
        public IReadOnlyList<CabinetOpenRelocationSessionItemRoute> ItemRoutes { get; init; } = [];

        public string SummaryText { get; init; } = string.Empty;
    }

    /// <summary>
    /// 开柜本次迁档汇总打印数据。
    /// </summary>
    public sealed class CabinetOpenRelocationSessionPrintData
    {
        public string CabinetName { get; init; } = string.Empty;

        public string FaceDisplayName { get; init; } = string.Empty;

        public string CabinetTypeText { get; init; } = string.Empty;

        public string OperatorName { get; init; } = string.Empty;

        public DateTime PrintedAt { get; init; }

        public IReadOnlyList<CabinetOpenRelocationSessionEntry> Entries { get; init; } = [];
    }
}
