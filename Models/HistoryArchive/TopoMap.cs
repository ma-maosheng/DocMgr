namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档纸质地形图实体
    /// </summary>
    public class TopoMap
    {
        public int Id { get; set; }

        /// <summary>
        /// 存档批次键（导入时写入为「地形图」+ Excel 工作表名），用于分表浏览。
        /// </summary>
        public string Category { get; set; } = string.Empty;

        public string Scale { get; set; } = string.Empty; // 比例尺 (必须有值)

        public string BoxNumber { get; set; } = string.Empty;
        public string BoxSpecification { get; set; } = string.Empty;
        public string MapNumber { get; set; } = string.Empty;

        /// <summary>
        /// 按 GB/T 13989 现行地形图编号规则，由比例尺与图上图号换算得到的当前图号。
        /// </summary>
        public string CurrentMapNumber { get; set; } = string.Empty;

        public string MapName { get; set; } = string.Empty;
        public int SheetCount { get; set; }

        public string CreationDate { get; set; } = string.Empty;
        public string SurveyDate { get; set; } = string.Empty;
        public string CoordinateSystem { get; set; } = string.Empty;
        public string ElevationDatum { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;

        public string Registrant { get; set; } = string.Empty;
        public string RegistrationDate { get; set; } = string.Empty;

        // === 之前导致报错的关键字段 ===
        public string Modifier { get; set; } = string.Empty;
        public string ModificationDate { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;

        /// <summary>生命周期：在库 / 离库锁定 / 已离库。</summary>
        public string LifecycleStatus { get; set; } = HistoryArchiveDisposalDomainValues.LifecycleInStock;

        /// <summary>离库办结时写入原盒号。</summary>
        public string LastStorageLocation { get; set; } = string.Empty;
    }
}