using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质出库申请预计归还日期辅助逻辑。
    /// </summary>
    public static class HardDiskMediaOutboundReturnSupport
    {
        public const int TemporaryReturnTermMonths = 1;

        public const string NoReturnDateDisplayText = "-";

        /// <summary>
        /// 是否为介质出库申请页可选的出库类型（临时/长期/永久）。
        /// </summary>
        public static bool IsSelectableOutboundApplicationType(string? applicationType) =>
            RequiresExpectedReturnDate(applicationType) || IsNonReturnableOutboundType(applicationType);

        /// <summary>
        /// 是否需填写预计归还日期（仅临时出库）。
        /// </summary>
        public static bool RequiresExpectedReturnDate(string? applicationType) =>
            string.Equals(applicationType, HardDiskMediaApplication.TypeOutboundTemporary, StringComparison.Ordinal);

        /// <summary>
        /// 是否为长期或永久出库（预计归还日期展示为“-”）。
        /// </summary>
        public static bool IsNonReturnableOutboundType(string? applicationType) =>
            string.Equals(applicationType, HardDiskMediaApplication.TypeOutboundLongTerm, StringComparison.Ordinal) ||
            string.Equals(applicationType, HardDiskMediaApplication.TypeOutboundPermanent, StringComparison.Ordinal);

        /// <summary>
        /// 计算临时出库归还期限（申请日起满 1 个月）。
        /// </summary>
        public static DateTime CalculateReturnDeadline(DateTime applyTime)
        {
            DateTime baseDate = applyTime == default ? DateTime.Today : applyTime.Date;
            return baseDate.AddMonths(TemporaryReturnTermMonths);
        }

        /// <summary>
        /// 按申请类型计算默认预计归还日期。
        /// </summary>
        public static DateTime? CalculateDefaultExpectedReturnDate(DateTime applyTime, string? applicationType)
        {
            if (RequiresExpectedReturnDate(applicationType))
            {
                return CalculateReturnDeadline(applyTime);
            }

            return null;
        }

        /// <summary>
        /// 将预计归还日期限制在申请日与归还期限之间。
        /// </summary>
        public static DateTime? ClampExpectedReturnDate(DateTime applyTime, DateTime? expectedReturnDate)
        {
            if (!expectedReturnDate.HasValue)
            {
                return null;
            }

            DateTime baseDate = applyTime == default ? DateTime.Today : applyTime.Date;
            DateTime deadline = CalculateReturnDeadline(applyTime);
            DateTime selected = expectedReturnDate.Value.Date;

            if (selected < baseDate)
            {
                return baseDate;
            }

            if (selected > deadline)
            {
                return deadline;
            }

            return selected;
        }

        /// <summary>
        /// 格式化界面只读展示文本。
        /// </summary>
        public static string FormatExpectedReturnDateDisplay(string? applicationType, DateTime? expectedReturnDate)
        {
            if (IsNonReturnableOutboundType(applicationType))
            {
                return NoReturnDateDisplayText;
            }

            return expectedReturnDate.HasValue
                ? expectedReturnDate.Value.ToString("yyyy-MM-dd")
                : NoReturnDateDisplayText;
        }

        /// <summary>
        /// 格式化打印/导出文本。
        /// </summary>
        public static string FormatExpectedReturnDateText(string? applicationType, DateTime? expectedReturnDate)
        {
            if (IsNonReturnableOutboundType(applicationType))
            {
                return NoReturnDateDisplayText;
            }

            return expectedReturnDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        }

        /// <summary>
        /// 保存前解析预计归还日期：长期/永久出库置空，临时出库校验并归一化。
        /// </summary>
        public static DateTime? ResolveExpectedReturnDateForSave(
            string? applicationType,
            DateTime applyTime,
            DateTime? selectedDate)
        {
            if (IsNonReturnableOutboundType(applicationType))
            {
                return null;
            }

            if (!RequiresExpectedReturnDate(applicationType))
            {
                return selectedDate;
            }

            if (!selectedDate.HasValue)
            {
                return CalculateDefaultExpectedReturnDate(applyTime, applicationType);
            }

            return ClampExpectedReturnDate(applyTime, selectedDate);
        }

        /// <summary>
        /// 校验临时出库预计归还日期是否在申请日与归还期限之间。
        /// </summary>
        public static void ValidateExpectedReturnDate(
            string? applicationType,
            DateTime applyTime,
            DateTime? expectedReturnDate)
        {
            if (!RequiresExpectedReturnDate(applicationType))
            {
                return;
            }

            DateTime baseDate = applyTime == default ? DateTime.Today : applyTime.Date;
            DateTime deadline = CalculateReturnDeadline(applyTime);

            if (!expectedReturnDate.HasValue)
            {
                throw new ArgumentException("临时出库请填写预计归还日期。", nameof(expectedReturnDate));
            }

            DateTime selected = expectedReturnDate.Value.Date;
            if (selected < baseDate)
            {
                throw new ArgumentException("预计归还日期不能早于申请日期。", nameof(expectedReturnDate));
            }

            if (selected > deadline)
            {
                throw new ArgumentException(
                    $"预计归还日期不能晚于归还期限（{deadline:yyyy-MM-dd}）。",
                    nameof(expectedReturnDate));
            }
        }
    }
}
