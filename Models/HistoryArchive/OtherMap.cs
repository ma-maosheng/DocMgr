namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档其他资料实体（HA-OTH）。
    /// </summary>
    public class OtherMap
    {
        public int Id { get; set; }

        /// <summary>
        /// 存档批次键（导入时写入为「其他资料」+ Excel 工作表名），用于分表浏览。
        /// </summary>
        public string Category { get; set; } = string.Empty;

        public string SequenceNumber { get; set; } = string.Empty;

        /// <summary>
        /// 资料分类（模版列「资料分类」）。
        /// </summary>
        public string MaterialCategory { get; set; } = string.Empty;

        /// <summary>
        /// 起始年度；可为空，可与截止年度相同。
        /// </summary>
        public string StartYear { get; set; } = string.Empty;

        /// <summary>
        /// 截止年度；可为空，可与起始年度相同。
        /// </summary>
        public string EndYear { get; set; } = string.Empty;

        /// <summary>
        /// 遗留字段：旧版「比例尺」导入数据；新模版不再使用。
        /// </summary>
        public string Scale { get; set; } = string.Empty;

        public string BoxNumber { get; set; } = string.Empty;
        public string BoxSpecification { get; set; } = string.Empty;

        /// <summary>
        /// 资料内容（模版列「资料内容」）。
        /// </summary>
        public string MapName { get; set; } = string.Empty;

        public string Registrant { get; set; } = string.Empty;
        public string RegistrationDate { get; set; } = string.Empty;
        public string Modifier { get; set; } = string.Empty;
        public string ModificationDate { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;

        /// <summary>生命周期：在库 / 离库锁定 / 已离库。</summary>
        public string LifecycleStatus { get; set; } = HistoryArchiveDisposalDomainValues.LifecycleInStock;

        /// <summary>离库办结时写入原盒号。</summary>
        public string LastStorageLocation { get; set; } = string.Empty;
    }
}
