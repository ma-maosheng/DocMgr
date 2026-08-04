namespace DocMgr.Models.Cabinets
{
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
        /// 物理位置迁移路线（完整位置编码，顿号分隔），如「辛甲-1-1-01->辛甲-1-2-01」。
        /// </summary>
        public string LocationRoutesText { get; init; } = string.Empty;

        /// <summary>盒编号或电子介质袋编号（顿号分隔）。</summary>
        public string ContainerCodesText { get; init; } = string.Empty;

        /// <summary>硬盘编号（顿号分隔）。</summary>
        public string HardDiskCodesText { get; init; } = string.Empty;

        /// <summary>光盘编号（顿号分隔；年度数据光盘或损坏光盘）。</summary>
        public string OpticalDiscCodesText { get; init; } = string.Empty;

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
