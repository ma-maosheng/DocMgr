namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 审批签字日期下限与校验：不能早于签批单首次打印日期。
    /// </summary>
    public static class ApprovalSignatureDateSupport
    {
        /// <summary>签字日期下限：取首次打印日（优先 FirstPrintedAt，否则 LastPrintedAt）。</summary>
        public static DateTime? ResolveMinDate(DateTime? firstPrintedAt, DateTime? lastPrintedAt, int printCount)
        {
            if (firstPrintedAt.HasValue) return firstPrintedAt.Value.Date;
            if (printCount > 0 && lastPrintedAt.HasValue) return lastPrintedAt.Value.Date;
            return null;
        }

        public static DateTime? Clamp(DateTime? value, DateTime? minDate)
        {
            if (!value.HasValue) return value;
            if (minDate.HasValue && value.Value.Date < minDate.Value.Date) return minDate.Value.Date;
            return value.Value.Date;
        }

        public static string? ValidateNotBeforePrint(DateTime? value, DateTime? minDate, string fieldLabel)
        {
            if (!value.HasValue || !minDate.HasValue) return null;
            if (value.Value.Date < minDate.Value.Date)
                return $"{fieldLabel}不能早于签批单首次打印日期（{minDate.Value:yyyy-MM-dd}）。";
            return null;
        }

        /// <summary>记录一次打印：首次打印写入 firstPrintedAt，并更新 lastPrintedAt 与 printCount。</summary>
        public static void RecordPrint(ref int printCount, ref DateTime? firstPrintedAt, ref DateTime? lastPrintedAt, DateTime? printedAt = null)
        {
            var now = printedAt ?? DateTime.Now;
            if (!firstPrintedAt.HasValue)
                firstPrintedAt = now;
            lastPrintedAt = now;
            printCount++;
        }
    }
}
