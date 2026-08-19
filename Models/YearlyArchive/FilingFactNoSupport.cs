namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 立档事实编号解析：格式为 <c>立档-{介质}-{年度}-{序号}</c>。
    /// </summary>
    public static class FilingFactNoSupport
    {
        private const string Prefix = "立档-";

        /// <summary>
        /// 从立档编号解析档案归属年度；无法解析时返回 <see langword="null"/>。
        /// </summary>
        public static int? TryParseArchiveYear(string? filingFactNo)
        {
            if (string.IsNullOrWhiteSpace(filingFactNo))
            {
                return null;
            }

            string trimmed = filingFactNo.Trim();
            if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return null;
            }

            string[] parts = trimmed.Split('-');
            if (parts.Length < 4)
            {
                return null;
            }

            return int.TryParse(parts[^2], out int year) && year is >= 1900 and <= 2100
                ? year
                : null;
        }

        /// <summary>
        /// 构造按立档编号年度段匹配的 SQL LIKE 模式（<c>立档-%-{year}-%</c>）。
        /// </summary>
        public static string BuildArchiveYearLikePattern(int archiveYear)
            => $"立档-%-{archiveYear}-%";
    }
}
