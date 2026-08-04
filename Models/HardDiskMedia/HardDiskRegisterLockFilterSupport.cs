namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘台账征用锁筛选辅助。
    /// </summary>
    public static class HardDiskRegisterLockFilterSupport
    {
        public const string All = "全部";
        public const string None = "无征用锁";
        public const string Any = "有征用锁";

        private static readonly (string BusinessType, string DisplayLabel)[] BusinessTypeOptions =
        [
            (HardDiskRegisterLock.BusinessTypeArchiveRegister, "年度资料登记（已借出硬盘）"),
            (HardDiskRegisterLock.BusinessTypeOutboundApplication, "硬盘借出申请（库内空盘）"),
            (HardDiskRegisterLock.BusinessTypeArchiveOutboundRequisition, "资料出库征用（库内空盘）"),
            (HardDiskRegisterLock.BusinessTypeInventoryRegister, "硬盘盘库登记"),
            (HardDiskRegisterLock.BusinessTypeDisposal, "硬盘离库处置"),
            (HardDiskRegisterLock.BusinessTypeArchiveInventoryRegister, "资料盘库登记"),
            (HardDiskRegisterLock.BusinessTypeArchiveDisposal, "资料离库处置"),
        ];

        private static readonly Dictionary<string, string> DisplayToBusinessType =
            BusinessTypeOptions.ToDictionary(item => item.DisplayLabel, item => item.BusinessType, StringComparer.Ordinal);

        private static readonly Dictionary<string, string> BusinessTypeToDisplay =
            BusinessTypeOptions.ToDictionary(item => item.BusinessType, item => item.DisplayLabel, StringComparer.Ordinal);

        /// <summary>
        /// 征用锁筛选下拉选项（含全部、无征用、有征用及各类业务征用）。
        /// </summary>
        public static IReadOnlyList<string> FilterOptions { get; } =
        [
            All,
            None,
            Any,
            ..BusinessTypeOptions.Select(item => item.DisplayLabel),
        ];

        /// <summary>
        /// 台账列表征用详情列显示文本。
        /// </summary>
        public static string GetGridDisplayText(HardDiskRegisterLock? registerLock)
        {
            if (registerLock == null)
            {
                return string.Empty;
            }

            return GetDisplayLabel(registerLock.BusinessType);
        }

        /// <summary>
        /// 将占用业务类型转为带盘态说明的显示文本。
        /// </summary>
        public static string GetDisplayLabel(string? businessType)
        {
            if (string.IsNullOrWhiteSpace(businessType))
            {
                return string.Empty;
            }

            string trimmed = businessType.Trim();
            return BusinessTypeToDisplay.TryGetValue(trimmed, out string? displayLabel)
                ? displayLabel
                : trimmed;
        }

        /// <summary>
        /// 按征用锁筛选条件过滤硬盘列表。
        /// </summary>
        public static IEnumerable<HardDiskMedium> ApplyFilter(IEnumerable<HardDiskMedium> items, string? selectedFilter)
        {
            ArgumentNullException.ThrowIfNull(items);

            string normalized = selectedFilter?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized) || string.Equals(normalized, All, StringComparison.Ordinal))
            {
                return items;
            }

            if (string.Equals(normalized, None, StringComparison.Ordinal))
            {
                return items.Where(item => item.RegisterLock == null);
            }

            if (string.Equals(normalized, Any, StringComparison.Ordinal))
            {
                return items.Where(item => item.RegisterLock != null);
            }

            if (DisplayToBusinessType.TryGetValue(normalized, out string? businessType))
            {
                return items.Where(item =>
                    item.RegisterLock != null
                    && string.Equals(item.RegisterLock.BusinessType, businessType, StringComparison.Ordinal));
            }

            return items.Where(item =>
                item.RegisterLock != null
                && string.Equals(item.RegisterLock.BusinessType, normalized, StringComparison.Ordinal));
        }
    }
}

