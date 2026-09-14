using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档航摄影像实体
    /// </summary>
    public class AerialPhoto
    {
        public int Id { get; set; }

        /// <summary>
        /// 存档批次键（导入时写入为「像片」+ Excel 工作表名），用于分表浏览。
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 盒号投影（非映射列）。加载后由 HistoryArchiveBoxProjectionSupport 从
        /// 盒/链接权威源水合；仅导入流程在内存中暂存 Excel 原文，写库不持久化。
        /// </summary>
        [NotMapped]
        public string BoxNumber { get; set; } = string.Empty;

        /// <summary>盒规格投影（非映射列）。权威源为 HistoryArchiveBoxes.BoxSpecification。</summary>
        [NotMapped]
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

        /// <summary>生命周期：在库 / 离库锁定 / 已离库。</summary>
        public string LifecycleStatus { get; set; } = HistoryArchiveDisposalDomainValues.LifecycleInStock;

        /// <summary>离库办结时写入原盒号。</summary>
        public string LastStorageLocation { get; set; } = string.Empty;
    }
}
