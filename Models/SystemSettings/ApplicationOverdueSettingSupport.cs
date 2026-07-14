using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.SystemSettings
{
    /// <summary>
    /// 申请单逾期强制作废时限判定支持。
    /// </summary>
    public static class ApplicationOverdueSettingSupport
    {
        /// <summary>
        /// 规范化逾期设置编码。
        /// </summary>
        public static string Normalize(string? settingCode)
        {
            string code = settingCode?.Trim() ?? string.Empty;
            if (string.Equals(code, ApplicationOverdueDomainValues.Days7, StringComparison.Ordinal))
            {
                return ApplicationOverdueDomainValues.Days7;
            }

            if (string.Equals(code, ApplicationOverdueDomainValues.Days30, StringComparison.Ordinal))
            {
                return ApplicationOverdueDomainValues.Days30;
            }

            return ApplicationOverdueDomainValues.SameDay;
        }

        /// <summary>
        /// 获取展示名称。
        /// </summary>
        public static string GetDisplayLabel(string? settingCode)
        {
            string normalized = Normalize(settingCode);
            return ApplicationOverdueDomainValues.AllOptions
                .First(option => string.Equals(option.Code, normalized, StringComparison.Ordinal))
                .Label;
        }

        /// <summary>
        /// 获取资料室管理员可强制作废前需等待的自然日天数。
        /// 当天=0，7天=6，30天=29。
        /// </summary>
        public static int GetAdminForceVoidWaitDays(string? settingCode)
        {
            return Normalize(settingCode) switch
            {
                ApplicationOverdueDomainValues.Days7 => 6,
                ApplicationOverdueDomainValues.Days30 => 29,
                _ => 0
            };
        }

        /// <summary>
        /// 判断申请单是否已达到资料室管理员强制作废时限。
        /// </summary>
        public static bool IsEligibleForAdminForceVoid(DateTime applyDate, string? settingCode, DateTime? asOf = null)
        {
            int waitDays = GetAdminForceVoidWaitDays(settingCode);
            DateTime referenceDate = (asOf ?? DateTime.Now).Date;
            return referenceDate >= applyDate.Date.AddDays(waitDays);
        }

        /// <summary>
        /// 构建未达时限时的提示文案。
        /// </summary>
        public static string BuildNotEligibleMessage(string? settingCode)
        {
            string label = GetDisplayLabel(settingCode);
            return $"当前申请单尚未达到「{label}」逾期强制作废时限，暂不允许强制作废。";
        }

        /// <summary>
        /// 解析资料登记申请单的申请日期。
        /// </summary>
        public static DateTime ResolveRegisterApplyDate(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (record.ApplicantDate != DateTime.MinValue)
            {
                return record.ApplicantDate;
            }

            return record.CreatedDate;
        }

        /// <summary>
        /// 解析资料借出申请单的申请日期。
        /// </summary>
        public static DateTime ResolveOutboundApplyDate(YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (record.SubmittedAt.HasValue)
            {
                return record.SubmittedAt.Value;
            }

            if (record.ApplyDate != DateTime.MinValue)
            {
                return record.ApplyDate;
            }

            return DateTime.Now;
        }
    }
}
