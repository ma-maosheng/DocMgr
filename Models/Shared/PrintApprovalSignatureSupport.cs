namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 打印表单审核/审批签字栏统一格式：仅「签字」与「日期」，签名即代表同意，不输出意见正文。
    /// </summary>
    public static class PrintApprovalSignatureSupport
    {
        /// <summary>签字位留白。</summary>
        public const string BlankSignerSlot = "________________";

        /// <summary>日期位留白。</summary>
        public const string BlankDateText = "______年___月___日";

        /// <summary>
        /// 单行签字栏：<c>签字：{姓名或留白}    日期：{日期或留白}</c>。
        /// </summary>
        public static string FormatInline(string? signer, string? dateText)
        {
            string slot = string.IsNullOrWhiteSpace(signer) ? BlankSignerSlot : signer.Trim();
            string date = string.IsNullOrWhiteSpace(dateText) ? BlankDateText : dateText.Trim();
            return $"签字：{slot}    日期：{date}";
        }

        /// <summary>空白签字栏（线下手签）。</summary>
        public static string FormatBlankInline() => FormatInline(null, null);

        /// <summary>
        /// 带角色前缀的单行签字：<c>{角色}：{姓名或留白}    日期：{日期或留白}</c>。
        /// 用于处置签批等多角色同栏场景。
        /// </summary>
        public static string FormatLabeledInline(string roleLabel, string? signer, string? dateText)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleLabel);

            string slot = string.IsNullOrWhiteSpace(signer) ? BlankSignerSlot : signer.Trim();
            string date = string.IsNullOrWhiteSpace(dateText) ? BlankDateText : dateText.Trim();
            string label = roleLabel.Trim();
            if (!label.EndsWith('：') && !label.EndsWith(':'))
            {
                label += "：";
            }

            return $"{label}{slot}    日期：{date}";
        }

        /// <summary>带角色前缀的空白签字行。</summary>
        public static string FormatBlankLabeledInline(string roleLabel) =>
            FormatLabeledInline(roleLabel, null, null);
    }
}
