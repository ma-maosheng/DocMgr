using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质出库申请预计归还日期与目标去向辅助逻辑。
    /// </summary>
    public static class HardDiskMediaOutboundReturnSupport
    {
        public const int TemporaryReturnTermMonths = 1;

        public const string NoReturnDateDisplayText = "-";

        /// <summary>永久出库目标去向：本部门。</summary>
        public const string DestinationKindInternal = HardDiskMediaApplication.DestinationKindInternal;

        /// <summary>永久出库目标去向：外单位。</summary>
        public const string DestinationKindExternal = HardDiskMediaApplication.DestinationKindExternal;

        /// <summary>永久出库目标去向可选值。</summary>
        public static IReadOnlyList<string> PermanentDestinationKindOptions { get; } =
        [
            DestinationKindInternal,
            DestinationKindExternal
        ];

        /// <summary>
        /// 是否为介质出库申请页可选的出库类型（临时/长期/永久）。
        /// </summary>
        public static bool IsSelectableOutboundApplicationType(string? applicationType) =>
            RequiresExpectedReturnDate(applicationType) || IsNonReturnableOutboundType(applicationType);

        /// <summary>
        /// 是否为永久出库申请（需选择目标去向）。
        /// </summary>
        public static bool RequiresDestinationKind(string? applicationType) =>
            string.Equals(applicationType, HardDiskMediaApplication.TypeOutboundPermanent, StringComparison.Ordinal);

        /// <summary>
        /// 目标去向是否为外单位。
        /// </summary>
        public static bool IsExternalDestination(string? destinationKind) =>
            string.Equals(destinationKind?.Trim(), DestinationKindExternal, StringComparison.Ordinal);

        /// <summary>
        /// 目标去向是否为本部门。
        /// </summary>
        public static bool IsInternalDestination(string? destinationKind) =>
            string.Equals(destinationKind?.Trim(), DestinationKindInternal, StringComparison.Ordinal);

        /// <summary>
        /// 解析保存用的目标去向明细：本部门取申请部门，外单位取「目标位置/去向」输入。
        /// </summary>
        public static string ResolveTargetPersonOrUnitForSave(
            string? applicationType,
            string? destinationKind,
            string? applicantDept,
            string? targetLocation)
        {
            if (!RequiresDestinationKind(applicationType))
            {
                return applicantDept?.Trim() ?? string.Empty;
            }

            if (IsInternalDestination(destinationKind))
            {
                return applicantDept?.Trim() ?? string.Empty;
            }

            if (IsExternalDestination(destinationKind))
            {
                return targetLocation?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// 校验永久出库目标去向：本部门须为申请人所属部门；外单位须填写且不能是本院部门。
        /// </summary>
        public static void ValidatePermanentDestination(
            string? applicationType,
            string? destinationKind,
            string? applicantDept,
            string? targetPersonOrUnit,
            IReadOnlyCollection<string> instituteDepartmentNames)
        {
            if (!RequiresDestinationKind(applicationType))
            {
                return;
            }

            string kind = destinationKind?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(kind)
                || !PermanentDestinationKindOptions.Contains(kind, StringComparer.Ordinal))
            {
                throw new ArgumentException("永久出库请选择目标去向（本部门或外单位）。", nameof(destinationKind));
            }

            string applicantDeptText = applicantDept?.Trim() ?? string.Empty;
            string destinationText = targetPersonOrUnit?.Trim() ?? string.Empty;

            if (IsInternalDestination(kind))
            {
                if (string.IsNullOrWhiteSpace(applicantDeptText))
                {
                    throw new ArgumentException("申请人所属部门为空，无法将目标去向设为本部门。", nameof(applicantDept));
                }

                if (!string.Equals(destinationText, applicantDeptText, StringComparison.Ordinal))
                {
                    throw new ArgumentException("目标去向为本部门时，须与申请人所属部门一致。", nameof(targetPersonOrUnit));
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(destinationText))
            {
                throw new ArgumentException("目标去向为外单位时，请填写具体单位名称。", nameof(targetPersonOrUnit));
            }

            if (IsInstituteDepartmentName(destinationText, instituteDepartmentNames))
            {
                throw new ArgumentException("目标去向为外单位时，单位名称不能是本院部门。", nameof(targetPersonOrUnit));
            }
        }

        /// <summary>
        /// 判断名称是否与本院任一部门名称相同（忽略首尾空白）。
        /// </summary>
        public static bool IsInstituteDepartmentName(
            string? name,
            IReadOnlyCollection<string> instituteDepartmentNames)
        {
            string trimmed = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed) || instituteDepartmentNames.Count == 0)
            {
                return false;
            }

            return instituteDepartmentNames.Any(item =>
                string.Equals(item?.Trim(), trimmed, StringComparison.Ordinal));
        }

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
