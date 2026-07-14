namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档纸质地形图实体
    /// </summary>
    public class TopoMap
    {
        public int Id { get; set; }
        public string Scale { get; set; } = string.Empty; // 比例尺 (必须有值)

        public string BoxNumber { get; set; } = string.Empty;
        public string BoxSpecification { get; set; } = string.Empty;
        public string MapNumber { get; set; } = string.Empty;
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
    }
}