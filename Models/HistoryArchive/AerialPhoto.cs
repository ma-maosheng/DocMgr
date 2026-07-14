namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档航摄影像实体
    /// </summary>
    public class AerialPhoto
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty; // 关键分类字段

        public string BoxNumber { get; set; } = string.Empty;
        public string BoxSpecification { get; set; } = string.Empty;
        public string SurveyArea { get; set; } = string.Empty;
        public string Scale { get; set; } = string.Empty;
        public string PhotographyDate { get; set; } = string.Empty;
        public string BoxContents { get; set; } = string.Empty;
        public int PhotoCount { get; set; }

        public string Registrant { get; set; } = string.Empty;
        public string RegistrationDate { get; set; } = string.Empty;

        // === 预防报错 ===
        public string Modifier { get; set; } = string.Empty;
        public string ModificationDate { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
    }
}
