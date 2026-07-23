using DocMgr.Models.Shared;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘盘库登记业务域值与规则。
    /// </summary>
    public static class HardDiskInventoryRegisterDomainValues
    {
        public const string KindDamage = "损坏登记";
        public const string KindLost = "盘失登记";
        public const string KindRelocateDamaged = "损坏档口调整";

        /// <summary>登记类型选项（整单唯一）。</summary>
        public static IReadOnlyList<string> RegisterKindOptions { get; } =
        [
            KindDamage,
            KindLost,
            KindRelocateDamaged
        ];

        /// <summary>可纳入盘库登记的介质状态。</summary>
        public static IReadOnlyList<string> SelectableMediaStatusOptions { get; } =
        [
            HardDiskMedium.StatusInStockBlank,
            HardDiskMedium.StatusInStockDamaged
        ];

        public static bool IsValidRegisterKind(string? kind)
        {
            string normalized = kind?.Trim() ?? string.Empty;
            return RegisterKindOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal));
        }

        public static string ResolveTransactionType(string? kind)
        {
            string normalized = kind?.Trim() ?? string.Empty;
            return normalized switch
            {
                KindDamage => HardDiskMediaTransaction.TypeInventoryRegisterDamage,
                KindLost => HardDiskMediaTransaction.TypeInventoryRegisterLost,
                KindRelocateDamaged => HardDiskMediaTransaction.TypeInventoryRegisterRelocate,
                _ => HardDiskMediaTransaction.TypeInventoryRegisterDamage
            };
        }

        public static string ResolveAfterMediaStatus(string? kind, string? beforeStatus)
        {
            string normalizedKind = kind?.Trim() ?? string.Empty;
            string normalizedBefore = beforeStatus?.Trim() ?? string.Empty;

            return normalizedKind switch
            {
                KindDamage => HardDiskMedium.StatusInStockDamaged,
                KindLost => HardDiskMedium.StatusInStockLost,
                KindRelocateDamaged => HardDiskMedium.StatusInStockDamaged,
                _ => normalizedBefore
            };
        }

        public static bool RequiresDamagedTargetLocation(string? kind)
        {
            string normalized = kind?.Trim() ?? string.Empty;
            return string.Equals(normalized, KindDamage, StringComparison.Ordinal)
                || string.Equals(normalized, KindRelocateDamaged, StringComparison.Ordinal);
        }

        public static bool ClearsStorageLocation(string? kind) =>
            string.Equals(kind?.Trim(), KindLost, StringComparison.Ordinal);

        public static string ToStatusDisplay(int status) => status switch
        {
            HardDiskInventoryRegisterRecord.StatusDraft => ApplicationWorkflowStatus.TextDraft,
            HardDiskInventoryRegisterRecord.StatusCompleted => ApplicationWorkflowStatus.TextCompleted,
            HardDiskInventoryRegisterRecord.StatusWithdrawn => ApplicationWorkflowStatus.TextWithdrawn,
            _ => ApplicationWorkflowStatus.ToDisplay(status)
        };
    }
}
