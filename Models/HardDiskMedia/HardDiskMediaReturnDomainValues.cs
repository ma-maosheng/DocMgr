namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘归还登记领域常量与判定。
    /// </summary>
    public static class HardDiskMediaReturnDomainValues
    {
        /// <summary>登记类型（页面展示）：正常归还。</summary>
        public const string RegistrationKindNormalReturn = "正常归还";

        /// <summary>登记类型（页面展示）：损坏归还。</summary>
        public const string RegistrationKindDamagedReturn = "损坏归还";

        /// <summary>登记类型（页面展示）：挂失登记。</summary>
        public const string RegistrationKindLossRegistration = "挂失登记";

        /// <summary>挂失登记时归还位置展示值（表示无归位档口）。</summary>
        public const string LossReturnTargetLocationDisplay = "-";

        /// <summary>历史完好性取值：损坏登记（兼容旧数据）。</summary>
        public const string LegacyInspectionDamagedRegistration = "损坏登记";

        /// <summary>非正常归还情况表扫描件附件类别。</summary>
        public const string AttachmentKindSignedAbnormalReturnReport = "SignedAbnormalReturnReport";

        /// <summary>页面登记类型筛选项（不含「全部」）。</summary>
        public static IReadOnlyList<string> RegistrationKindFilterOptions { get; } =
        [
            RegistrationKindNormalReturn,
            RegistrationKindDamagedReturn,
            RegistrationKindLossRegistration
        ];

        /// <summary>根据申请类型与完好性确认解析页面登记类型展示名。</summary>
        public static string ResolveRegistrationKindDisplay(string? applicationType, string? inspectionResult)
        {
            if (applicationType == HardDiskMediaApplication.TypeLossRegistration ||
                IsLossRegistrationInspection(inspectionResult))
            {
                return RegistrationKindLossRegistration;
            }

            if (applicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                IsDamagedReturnInspection(inspectionResult))
            {
                return RegistrationKindDamagedReturn;
            }

            return RegistrationKindNormalReturn;
        }

        /// <summary>登记类型筛选是否匹配。</summary>
        public static bool MatchesRegistrationKindFilter(
            string? filterKind,
            string? applicationType,
            string? inspectionResult)
        {
            if (string.IsNullOrWhiteSpace(filterKind) || string.Equals(filterKind, "全部", StringComparison.Ordinal))
            {
                return true;
            }

            string displayKind = ResolveRegistrationKindDisplay(applicationType, inspectionResult);
            return string.Equals(displayKind, filterKind.Trim(), StringComparison.Ordinal);
        }

        /// <summary>完好性是否为正常归还。</summary>
        public static bool IsNormalReturnInspection(string? inspectionResult)
        {
            string normalized = inspectionResult?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalized) ||
                   string.Equals(normalized, RegistrationKindNormalReturn, StringComparison.Ordinal);
        }

        /// <summary>完好性是否为损坏归还。</summary>
        public static bool IsDamagedReturnInspection(string? inspectionResult)
        {
            string normalized = inspectionResult?.Trim() ?? string.Empty;
            return string.Equals(normalized, RegistrationKindDamagedReturn, StringComparison.Ordinal) ||
                   string.Equals(normalized, LegacyInspectionDamagedRegistration, StringComparison.Ordinal);
        }

        /// <summary>完好性是否为挂失登记。</summary>
        public static bool IsLossRegistrationInspection(string? inspectionResult)
        {
            return string.Equals(inspectionResult?.Trim(), RegistrationKindLossRegistration, StringComparison.Ordinal);
        }

        /// <summary>根据完好性确认解析持久化申请类型。</summary>
        public static string ResolveApplicationTypeByInspectionResult(string? inspectionResult)
        {
            if (IsLossRegistrationInspection(inspectionResult))
            {
                return HardDiskMediaApplication.TypeLossRegistration;
            }

            if (IsDamagedReturnInspection(inspectionResult))
            {
                return HardDiskMediaApplication.TypeReturnDamagedRegistration;
            }

            return HardDiskMediaApplication.TypeReturnBlankRegistration;
        }

        /// <summary>根据申请类型与完好性确认解析默认完好性展示值。</summary>
        public static string ResolveInspectionResultDisplay(string? applicationType, string? inspectionResult)
        {
            if (!string.IsNullOrWhiteSpace(inspectionResult))
            {
                string normalized = inspectionResult.Trim();
                if (IsDamagedReturnInspection(normalized))
                {
                    return RegistrationKindDamagedReturn;
                }

                if (IsLossRegistrationInspection(normalized))
                {
                    return RegistrationKindLossRegistration;
                }

                if (IsNormalReturnInspection(normalized))
                {
                    return RegistrationKindNormalReturn;
                }
            }

            return applicationType switch
            {
                HardDiskMediaApplication.TypeReturnDamagedRegistration => RegistrationKindDamagedReturn,
                HardDiskMediaApplication.TypeLossRegistration => RegistrationKindLossRegistration,
                _ => RegistrationKindNormalReturn
            };
        }

        /// <summary>判定是否为非正常归还（损坏归还 / 挂失登记）。</summary>
        public static bool IsAbnormalReturn(HardDiskMediaApplication? application)
        {
            if (application == null)
            {
                return false;
            }

            return IsAbnormalReturn(application.ApplicationType, application.InspectionResult);
        }

        /// <summary>判定是否为非正常归还（损坏归还 / 挂失登记）。</summary>
        public static bool IsAbnormalReturn(string? applicationType, string? inspectionResult)
        {
            if (applicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                applicationType == HardDiskMediaApplication.TypeLossRegistration)
            {
                return true;
            }

            return IsDamagedReturnInspection(inspectionResult) ||
                   IsLossRegistrationInspection(inspectionResult);
        }
    }
}
