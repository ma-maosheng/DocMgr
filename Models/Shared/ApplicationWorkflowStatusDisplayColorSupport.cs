namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 申请单业务状态文案 → 列表展示色（方案 A：待办琥珀 / 办结翠绿 / 作废石棕）。
    /// </summary>
    public static class ApplicationWorkflowStatusDisplayColorSupport
    {
        public const string DefaultForeground = "#64748B";

        /// <summary>草稿～待上传等进行中 / 待办。</summary>
        public const string PendingForeground = "#D97706";

        /// <summary>已办结（业务已闭环）。</summary>
        public const string CompletedForeground = "#059669";

        /// <summary>撤回 / 强制作废。</summary>
        public const string VoidedForeground = "#78716C";

        /// <summary>按状态码返回前景色十六进制。</summary>
        public static string ResolveForeground(int status) => status switch
        {
            ApplicationWorkflowStatus.Draft
                or ApplicationWorkflowStatus.Submitted
                or ApplicationWorkflowStatus.Approved
                or ApplicationWorkflowStatus.SignedUploaded => PendingForeground,
            ApplicationWorkflowStatus.Completed => CompletedForeground,
            ApplicationWorkflowStatus.Withdrawn
                or ApplicationWorkflowStatus.ForceWithdrawn => VoidedForeground,
            _ => DefaultForeground
        };

        /// <summary>
        /// 按展示文案返回前景色；优先走统一状态解析，再按关键词兜底（含「待办结」等变体文案）。
        /// </summary>
        public static string ResolveForeground(string? statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return DefaultForeground;
            }

            string trimmed = statusText.Trim();
            int? code = ApplicationWorkflowStatus.TryParseStoredText(trimmed);
            if (code.HasValue)
            {
                return ResolveForeground(code.Value);
            }

            // 归还/出库等：「已上传签批交接单-待办结」
            if (trimmed.Contains("待办结", StringComparison.Ordinal)
                || trimmed.Contains("待提交", StringComparison.Ordinal)
                || trimmed.Contains("待审批", StringComparison.Ordinal)
                || trimmed.Contains("待实物", StringComparison.Ordinal)
                || trimmed.Contains("待上传", StringComparison.Ordinal)
                || trimmed.Contains("进行中", StringComparison.Ordinal))
            {
                return PendingForeground;
            }

            if (trimmed.Contains("作废", StringComparison.Ordinal)
                || trimmed.Contains("撤回", StringComparison.Ordinal))
            {
                return VoidedForeground;
            }

            if (trimmed.Contains("办结", StringComparison.Ordinal))
            {
                return CompletedForeground;
            }

            if (trimmed.Contains("草稿", StringComparison.Ordinal)
                || trimmed.Contains("已提交", StringComparison.Ordinal)
                || trimmed.Contains("已审批", StringComparison.Ordinal)
                || trimmed.Contains("已实物", StringComparison.Ordinal)
                || trimmed.Contains("已上传", StringComparison.Ordinal)
                || trimmed.Contains("已登记", StringComparison.Ordinal))
            {
                return PendingForeground;
            }

            return DefaultForeground;
        }
    }
}
