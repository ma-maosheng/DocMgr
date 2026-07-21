namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 多节点审核/审批意见一致性：同一表单内「同意」要么都有，要么都没有，避免部分「同意」、部分空白/「无」。
    /// </summary>
    public static class ApprovalOpinionUniformitySupport
    {
        /// <summary>
        /// 将一组意见规范为一致结果：全部为空则全空；若非空值唯一（如均为「同意」），则空槽一并填入该值。
        /// </summary>
        public static string[] NormalizeUniform(params string?[] opinions)
        {
            ArgumentNullException.ThrowIfNull(opinions);

            var trimmed = new string[opinions.Length];
            for (int i = 0; i < opinions.Length; i++)
            {
                trimmed[i] = opinions[i]?.Trim() ?? string.Empty;
            }

            var distinctFilled = trimmed
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (distinctFilled.Length == 0)
            {
                return trimmed;
            }

            if (distinctFilled.Length == 1)
            {
                string uniform = distinctFilled[0];
                for (int i = 0; i < trimmed.Length; i++)
                {
                    trimmed[i] = uniform;
                }

                return trimmed;
            }

            // 存在互不相同的非空意见时保持原样（仅做 Trim），由业务侧另行处理。
            return trimmed;
        }

        /// <summary>
        /// 展示用：空意见返回空白，禁止用「(无)」占位，以免与「同意」混排。
        /// </summary>
        public static string FormatForDisplay(string? opinion)
        {
            string trimmed = opinion?.Trim() ?? string.Empty;
            return trimmed;
        }
    }
}
