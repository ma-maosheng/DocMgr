namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史资料迁档引用组：一条历史台账（地形图/航片/其他图件）行及其登记的全部盒号。
    /// 盒号为投影属性：迁档只改盒实体，台账行零触碰；本组仅用于定位受影响台账与预览。
    /// </summary>
    public sealed class HistoryArchiveLedgerReferenceGroup
    {
        /// <summary>资料类别编码：TopoMap / AerialPhoto / OtherMap。</summary>
        public string MaterialKind { get; init; } = string.Empty;

        /// <summary>台账行主键。</summary>
        public int RecordId { get; init; }

        /// <summary>该行关联的全部盒号（按链接顺序）。</summary>
        public IReadOnlyList<string> BoxCodes { get; init; } = [];

        /// <summary>该行盒号投影文本（水合值，供预览展示）。</summary>
        public string BoxNumberText { get; init; } = string.Empty;
    }
}
