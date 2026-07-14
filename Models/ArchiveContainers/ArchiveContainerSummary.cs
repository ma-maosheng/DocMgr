namespace DocMgr.Models.ArchiveContainers
{
    /// <summary>
    /// 统一立档容器摘要。
    /// </summary>
    public sealed class ArchiveContainerSummary
    {
        /// <summary>
        /// 容器类型。
        /// </summary>
        public ArchiveContainerKind Kind { get; init; }

        /// <summary>
        /// 容器编号。
        /// </summary>
        public string ContainerCode { get; init; } = string.Empty;

        /// <summary>
        /// 所属项目。
        /// </summary>
        public string ProjectName { get; init; } = string.Empty;

        /// <summary>
        /// 所属年度。
        /// </summary>
        public string Year { get; init; } = string.Empty;

        /// <summary>
        /// 容器显示类型名称。
        /// </summary>
        public string KindDisplayName => Kind == ArchiveContainerKind.ArchiveBox ? "档案盒" : "电子介质袋";

        /// <summary>
        /// 容器显示文本。
        /// </summary>
        public string DisplayText => string.IsNullOrWhiteSpace(ContainerCode)
            ? KindDisplayName
            : $"{KindDisplayName} {ContainerCode}";
    }
}
