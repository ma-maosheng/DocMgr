using System.Text.RegularExpressions;

namespace DocMgr.Models.ArchiveContainers
{
    /// <summary>
    /// 立档容器编号在档口卡片等紧凑场景下的简写展示。
    /// </summary>
    public static class ArchiveContainerCodeDisplaySupport
    {
        private static readonly Regex PrefixedContainerCodeRegex = new(
            "^年度(?:电子|模拟)-(?<short>\\d{4}-\\d{3})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// 将完整容器编号转为简写（如「年度电子-2026-001」→「2026-001」）。
        /// </summary>
        public static string ToShortDisplayCode(string? containerCode)
        {
            if (string.IsNullOrWhiteSpace(containerCode))
            {
                return string.Empty;
            }

            string normalized = containerCode.Trim();
            Match match = PrefixedContainerCodeRegex.Match(normalized);
            return match.Success ? match.Groups["short"].Value : normalized;
        }
    }
}
