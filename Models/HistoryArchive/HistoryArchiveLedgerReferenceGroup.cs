namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史资料迁档引用组：一条历史台账（地形图/航片/其他图件）行及其登记的全部盒号。
    /// 迁移一个盒号时须整体改写该行 BoxNumber 文本。
    /// </summary>
    public sealed class HistoryArchiveLedgerReferenceGroup
    {
        /// <summary>资料类别编码：TopoMap / AerialPhoto / OtherMap。</summary>
        public string MaterialKind { get; init; } = string.Empty;

        /// <summary>台账行主键。</summary>
        public int RecordId { get; init; }

        /// <summary>该行 BoxNumber 拆分后的全部盒号（保持原顺序）。</summary>
        public IReadOnlyList<string> BoxCodes { get; init; } = [];

        /// <summary>该行 BoxNumber 原始文本（迁档改写时以此为基准重建）。</summary>
        public string BoxNumberText { get; init; } = string.Empty;

        /// <summary>台账行实体（TopoMap/AerialPhoto/OtherMap 之一），用于迁档改写。</summary>
        public object LedgerEntity { get; init; } = new();

        public TopoMap? AsTopoMap() => LedgerEntity as TopoMap;

        public AerialPhoto? AsAerialPhoto() => LedgerEntity as AerialPhoto;

        public OtherMap? AsOtherMap() => LedgerEntity as OtherMap;
    }
}
