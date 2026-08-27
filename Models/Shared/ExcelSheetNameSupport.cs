namespace DocMgr.Models.Shared
{
    /// <summary>
    /// Excel 工作表名称约束：最长 31 个字符，且不得包含 : \ / ? * [ ]。
    /// </summary>
    public static class ExcelSheetNameSupport
    {
        /// <summary>Excel 工作表名最大长度。</summary>
        public const int MaxLength = 31;

        private static readonly char[] InvalidChars = { ':', '\\', '/', '?', '*', '[', ']' };

        /// <summary>
        /// 将任意文本整理为合法的 Excel 工作表名。非法字符替换为下划线。
        /// </summary>
        public static string Sanitize(string? sheetName, string fallback = "Sheet1")
        {
            string name = Clean(sheetName);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = Clean(fallback);
            return string.IsNullOrWhiteSpace(name) ? "Sheet1" : name;
        }

        private static string Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string name = value.Trim();
            foreach (char invalidChar in InvalidChars)
            {
                name = name.Replace(invalidChar, '_');
            }

            if (name.Length > MaxLength)
            {
                name = name[..MaxLength];
            }

            return name.Trim();
        }
    }
}
