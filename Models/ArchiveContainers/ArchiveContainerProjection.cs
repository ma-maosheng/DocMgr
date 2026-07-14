namespace DocMgr.Models.ArchiveContainers
{
    /// <summary>
    /// 立档容器统一摘要视图 <c>vw_ArchiveContainerSummaries</c> 的只读投影。
    /// </summary>
    public sealed class ArchiveContainerProjection
    {
        /// <summary>
        /// 容器类型。
        /// </summary>
        public ArchiveContainerKind Kind { get; set; }

        /// <summary>
        /// 容器编号（档案盒号或电子介质袋号）。
        /// </summary>
        public string ContainerCode { get; set; } = string.Empty;

        /// <summary>
        /// 所属项目。
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// 所属年度。
        /// </summary>
        public string Year { get; set; } = string.Empty;

        /// <summary>
        /// 立档人。
        /// </summary>
        public string ArchivedBy { get; set; } = string.Empty;

        /// <summary>
        /// 立档日期。
        /// </summary>
        public DateTime ArchivedDate { get; set; }

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remarks { get; set; } = string.Empty;
    }
}
