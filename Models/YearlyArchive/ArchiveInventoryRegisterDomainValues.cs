using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Shared;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料盘库登记业务域值与规则。
    /// </summary>
    public static class ArchiveInventoryRegisterDomainValues
    {
        public const string MediaKindSimulated = ArchiveRegisterDomainValues.MediaKindSimulated;
        public const string MediaKindElectronic = ArchiveRegisterDomainValues.MediaKindElectronic;

        public const string KindLost = "盘失登记";
        public const string KindDamage = "损坏登记";
        /// <summary>拟销登记：登记无存档价值的资料（仅模拟轨）。</summary>
        public const string KindScrap = "拟销登记";

        public const string MediumKindHardDisk = "硬盘";
        public const string MediumKindOpticalDisc = "光盘";

        public const string TransactionTypeInventoryRegister = MaterialTransactionDomainValues.TypeInventoryRegister;

        /// <summary>电子轨登记类型选项（盘失/损坏）。</summary>
        public static IReadOnlyList<string> RegisterKindOptions { get; } =
        [
            KindLost,
            KindDamage
        ];

        /// <summary>模拟轨登记类型选项（盘失/拟销）。</summary>
        public static IReadOnlyList<string> SimulatedRegisterKindOptions { get; } =
        [
            KindLost,
            KindScrap
        ];

        /// <summary>整单介质轨选项。</summary>
        public static IReadOnlyList<string> MediaKindOptions { get; } =
        [
            MediaKindSimulated,
            MediaKindElectronic
        ];

        public static bool IsValidMediaKind(string? mediaKind)
        {
            string normalized = mediaKind?.Trim() ?? string.Empty;
            return MediaKindOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal));
        }

        public static bool IsValidRegisterKind(string? kind)
        {
            string normalized = kind?.Trim() ?? string.Empty;
            return RegisterKindOptions.Any(item => string.Equals(item, normalized, StringComparison.Ordinal))
                || string.Equals(normalized, KindScrap, StringComparison.Ordinal);
        }

        public static bool IsValidMediumKind(string? mediumKind)
        {
            string normalized = mediumKind?.Trim() ?? string.Empty;
            return string.Equals(normalized, MediumKindHardDisk, StringComparison.Ordinal)
                || string.Equals(normalized, MediumKindOpticalDisc, StringComparison.Ordinal);
        }

        /// <summary>模拟轨支持盘失/拟销；电子轨支持盘失/损坏。</summary>
        public static bool IsRegisterKindAllowedForMediaKind(string? mediaKind, string? registerKind)
        {
            string normalizedMediaKind = mediaKind?.Trim() ?? string.Empty;
            string normalizedRegisterKind = registerKind?.Trim() ?? string.Empty;

            if (string.Equals(normalizedMediaKind, MediaKindSimulated, StringComparison.Ordinal))
            {
                return SimulatedRegisterKindOptions.Any(item =>
                    string.Equals(item, normalizedRegisterKind, StringComparison.Ordinal));
            }

            return RegisterKindOptions.Any(item =>
                string.Equals(item, normalizedRegisterKind, StringComparison.Ordinal));
        }

        public static string ResolveMediumCodeDisplay(string? mediumKind, string? mediumCode)
        {
            if (string.Equals(mediumKind?.Trim(), MediumKindOpticalDisc, StringComparison.Ordinal))
            {
                return "-";
            }

            return mediumCode?.Trim() ?? string.Empty;
        }

        public static string ResolveHardDiskTransactionType(string? registerKind)
        {
            string normalized = registerKind?.Trim() ?? string.Empty;
            return string.Equals(normalized, KindLost, StringComparison.Ordinal)
                ? HardDiskMediaTransaction.TypeInventoryRegisterLost
                : HardDiskMediaTransaction.TypeInventoryRegisterDamage;
        }

        public static string ResolveOpticalDiscTransactionType(string? registerKind)
        {
            string normalized = registerKind?.Trim() ?? string.Empty;
            return string.Equals(normalized, KindLost, StringComparison.Ordinal)
                ? OpticalDiscMediaTransaction.TypeInventoryRegisterLost
                : OpticalDiscMediaTransaction.TypeInventoryRegisterDamage;
        }

        public static string ResolveHardDiskAfterMediaStatus(string? registerKind, string? beforeStatus)
        {
            string normalizedKind = registerKind?.Trim() ?? string.Empty;
            string normalizedBefore = beforeStatus?.Trim() ?? string.Empty;

            return normalizedKind switch
            {
                KindDamage => HardDiskMedium.StatusInStockDamaged,
                KindLost => HardDiskMedium.StatusInStockLost,
                _ => normalizedBefore
            };
        }

        public static string ResolveOpticalDiscAfterMediaStatus(string? registerKind, string? beforeStatus)
        {
            string normalizedKind = registerKind?.Trim() ?? string.Empty;
            string normalizedBefore = beforeStatus?.Trim() ?? string.Empty;

            return normalizedKind switch
            {
                KindDamage => OpticalDiscMedium.StatusDamaged,
                KindLost => OpticalDiscMedium.StatusLost,
                _ => normalizedBefore
            };
        }

        public static string ToStatusDisplay(int status) => status switch
        {
            YearlyArchiveInventoryRegisterRecord.StatusDraft => ApplicationWorkflowStatus.TextDraft,
            YearlyArchiveInventoryRegisterRecord.StatusCompleted => ApplicationWorkflowStatus.TextCompleted,
            YearlyArchiveInventoryRegisterRecord.StatusWithdrawn => ApplicationWorkflowStatus.TextWithdrawn,
            _ => ApplicationWorkflowStatus.ToDisplay(status)
        };
    }
}
