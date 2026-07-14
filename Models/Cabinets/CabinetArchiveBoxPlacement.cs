namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案盒摆放登记快照。
    /// </summary>
    public sealed class CabinetArchiveBoxPlacement
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 档案盒编号。
        /// </summary>
        public string BoxCode { get; set; } = string.Empty;

        /// <summary>
        /// 档案盒规格。
        /// </summary>
        public string BoxSpecification { get; set; } = string.Empty;

        /// <summary>
        /// 档案柜编号。
        /// </summary>
        public string CabinetName { get; set; } = string.Empty;

        /// <summary>
        /// 面别代码，取值如 A/B。
        /// </summary>
        public string FaceCode { get; set; } = string.Empty;

        /// <summary>
        /// 档口编号，格式如 6-1。
        /// </summary>
        public string SlotCode { get; set; } = string.Empty;

        /// <summary>
        /// 放置方式，取值如 SpineOut/FrontOut。
        /// </summary>
        public string PlacementMode { get; set; } = "SpineOut";

        /// <summary>
        /// 来源类型，取值如 TopoMap/AerialPhoto/OtherMap。
        /// </summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// 来源记录标识。
        /// </summary>
        public string SourceRecordKey { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间。
        /// </summary>
        public string CreatedAt { get; set; } = string.Empty;

        /// <summary>
        /// 更新时间。
        /// </summary>
        public string UpdatedAt { get; set; } = string.Empty;

        /// <summary>
        /// 最后更新人。
        /// </summary>
        public string UpdatedBy { get; set; } = string.Empty;
    }
}
