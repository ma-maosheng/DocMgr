namespace DocMgr.Models.ArchiveContainers
{
    /// <summary>
    /// 立档容器显示扩展。
    /// </summary>
    public static class ArchiveContainerDisplayExtensions
    {
        /// <summary>
        /// 获取容器编号文本，缺失时返回空字符串。
        /// </summary>
        public static string GetContainerCodeText(this IArchiveContainer? container)
        {
            return container?.ContainerCode?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 转换为统一容器摘要。
        /// </summary>
        public static ArchiveContainerSummary ToSummary(this IArchiveContainer container)
        {
            ArgumentNullException.ThrowIfNull(container);

            return new ArchiveContainerSummary
            {
                Kind = container.ContainerKind,
                ContainerCode = container.GetContainerCodeText(),
                ProjectName = container.ProjectName?.Trim() ?? string.Empty,
                Year = container.Year?.Trim() ?? string.Empty
            };
        }

        /// <summary>
        /// 将容器集合转换为统一容器摘要集合。
        /// </summary>
        public static List<ArchiveContainerSummary> ToSummaries(this IEnumerable<IArchiveContainer> containers)
        {
            ArgumentNullException.ThrowIfNull(containers);

            return containers
                .Select(container => container.ToSummary())
                .ToList();
        }

        /// <summary>
        /// 构建并入模式文案。
        /// </summary>
        public static string ToAppendModeText(this ArchiveContainerSummary? summary)
        {
            string containerCode = summary?.ContainerCode?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(containerCode)
                ? "并入既有立档容器"
                : $"并入既有立档容器（{containerCode}）";
        }

        /// <summary>
        /// 将数据库投影转换为统一容器摘要。
        /// </summary>
        public static ArchiveContainerSummary ToSummary(this ArchiveContainerProjection projection)
        {
            ArgumentNullException.ThrowIfNull(projection);

            return new ArchiveContainerSummary
            {
                Kind = projection.Kind,
                ContainerCode = projection.ContainerCode?.Trim() ?? string.Empty,
                ProjectName = projection.ProjectName?.Trim() ?? string.Empty,
                Year = projection.Year?.Trim() ?? string.Empty
            };
        }
    }
}
