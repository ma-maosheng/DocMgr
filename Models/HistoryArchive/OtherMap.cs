namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档其他图件实体
    /// </summary>
    public class OtherMap
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string SequenceNumber { get; set; } = string.Empty;
        public string Scale { get; set; } = string.Empty;
        public string BoxNumber { get; set; } = string.Empty;
        public string BoxSpecification { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public int SheetCount { get; set; }
        public string Registrant { get; set; } = string.Empty;
        public string RegistrationDate { get; set; } = string.Empty;
        public string Modifier { get; set; } = string.Empty;
        public string ModificationDate { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
    }
}
