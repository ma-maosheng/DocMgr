namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 介质状态文本归一化：去除首尾空白并将全角括号（）统一为半角 ()，
    /// 保证如「在库(空盘)」与「在库（空盘）」等值在比较与存储时一致。
    /// 供数据写入（AppDbContext）与开柜布局等流程复用，避免平行实现。
    /// </summary>
    public static class MediumStatusTextNormalizer
    {
        /// <summary>
        /// 归一化介质状态文本；空白输入返回空字符串。
        /// </summary>
        public static string Normalize(string? statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return string.Empty;
            }

            return statusText.Trim()
                .Replace('（', '(')
                .Replace('）', ')');
        }
    }
}
