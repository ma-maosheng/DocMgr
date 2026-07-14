using System.Text;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 将用户通配符输入转换为 SQL LIKE 模式（供 EF.Functions.Like 使用）。
    /// </summary>
    public static class SearchWildcardPatternSupport
    {
        public const char EscapeCharacter = '\\';

        public const string EscapeCharacterString = "\\";

        /// <summary>
        /// 将用户输入转换为 SQL LIKE 模式。
        /// <list type="bullet">
        /// <item><description><c>*</c>：匹配任意长度字符</description></item>
        /// <item><description><c>?</c>：匹配单个字符</description></item>
        /// <item><description>未包含通配符时，按包含匹配处理（等同 <c>*keyword*</c>）</description></item>
        /// <item><description>使用 <c>\</c> 可转义字面量 <c>*</c>、<c>?</c>、<c>%</c>、<c>_</c>、<c>\</c></description></item>
        /// </list>
        /// </summary>
        public static string ToSqlLikePattern(string? userPattern)
        {
            if (string.IsNullOrWhiteSpace(userPattern))
            {
                return "%";
            }

            string pattern = userPattern.Trim();
            bool hasWildcard = ContainsWildcard(pattern);

            var builder = new StringBuilder();
            if (!hasWildcard)
            {
                builder.Append('%');
            }

            for (int index = 0; index < pattern.Length; index++)
            {
                char character = pattern[index];
                if (character == EscapeCharacter && index + 1 < pattern.Length)
                {
                    char next = pattern[index + 1];
                    if (next is '*' or '?' or '%' or '_' or '\\')
                    {
                        builder.Append('\\').Append(next);
                        index++;
                        continue;
                    }
                }

                switch (character)
                {
                    case '*':
                        builder.Append('%');
                        break;
                    case '?':
                        builder.Append('_');
                        break;
                    case '%':
                    case '_':
                    case '\\':
                        builder.Append('\\').Append(character);
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            if (!hasWildcard)
            {
                builder.Append('%');
            }

            return builder.ToString();
        }

        public static bool ContainsWildcard(string? userPattern)
        {
            if (string.IsNullOrEmpty(userPattern))
            {
                return false;
            }

            bool escaped = false;
            foreach (char character in userPattern)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == EscapeCharacter)
                {
                    escaped = true;
                    continue;
                }

                if (character is '*' or '?')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
