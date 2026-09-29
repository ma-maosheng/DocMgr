using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 电子介质立档/迁档：防磁磁盘柜档口专用类别对齐规则。
    /// 档口由「资料类型（年度/历史/损坏）× 介质类型（硬盘/光盘）」共同决定。
    /// </summary>
    internal static class ArchiveElectronicStorageSlotCategorySupport
    {
        /// <summary>资料类型维度：年度数据 / 历史数据 / 损坏介质。</summary>
        internal enum SlotMaterialKind
        {
            YearlyData,
            HistoricalData,
            Damaged
        }

        /// <summary>介质类型维度：硬盘 / 光盘。</summary>
        internal enum SlotMediaKind
        {
            HardDisk,
            OpticalDisc
        }

        /// <summary>
        /// 按立档载体类型及关联硬盘状态，解析应使用的专用档口类别（立档默认按年度资料）。
        /// </summary>
        internal static string ResolveExpectedDedicatedSlotCategory(
            string? storageCarrierType,
            string? linkedMediumMediaStatus)
        {
            SlotMediaKind mediaKind = ResolveMediaKindFromCarrierType(storageCarrierType);
            SlotMaterialKind materialKind = ResolveDamagedMaterialKindOrDefault(
                linkedMediumMediaStatus,
                SlotMaterialKind.YearlyData);
            return ResolveDedicatedSlotCategory(materialKind, mediaKind);
        }

        /// <summary>
        /// 按资料类型 × 介质类型解析专用档口类别。
        /// </summary>
        internal static string ResolveDedicatedSlotCategory(SlotMaterialKind materialKind, SlotMediaKind mediaKind)
        {
            return (materialKind, mediaKind) switch
            {
                (SlotMaterialKind.YearlyData, SlotMediaKind.HardDisk)
                    => CabinetHardDiskSlotCategoryAssignment.CategoryData,
                (SlotMaterialKind.YearlyData, SlotMediaKind.OpticalDisc)
                    => CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc,
                (SlotMaterialKind.HistoricalData, SlotMediaKind.HardDisk)
                    => CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataHardDisk,
                (SlotMaterialKind.HistoricalData, SlotMediaKind.OpticalDisc)
                    => CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataOpticalDisc,
                (SlotMaterialKind.Damaged, SlotMediaKind.HardDisk)
                    => CabinetHardDiskSlotCategoryAssignment.CategoryDamaged,
                (SlotMaterialKind.Damaged, SlotMediaKind.OpticalDisc)
                    => CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc,
                _ => CabinetHardDiskSlotCategoryAssignment.CategoryData
            };
        }

        /// <summary>
        /// 从已配置的专用档口类别反解资料类型与介质类型。
        /// </summary>
        internal static bool TryResolveMaterialAndMediaKind(
            string? categoryName,
            out SlotMaterialKind materialKind,
            out SlotMediaKind mediaKind)
        {
            materialKind = SlotMaterialKind.YearlyData;
            mediaKind = SlotMediaKind.HardDisk;

            string normalized = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(categoryName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetHardDiskSlotCategoryAssignment.CategoryData))
            {
                materialKind = SlotMaterialKind.YearlyData;
                mediaKind = SlotMediaKind.HardDisk;
                return true;
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc))
            {
                materialKind = SlotMaterialKind.YearlyData;
                mediaKind = SlotMediaKind.OpticalDisc;
                return true;
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataHardDisk))
            {
                materialKind = SlotMaterialKind.HistoricalData;
                mediaKind = SlotMediaKind.HardDisk;
                return true;
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataOpticalDisc))
            {
                materialKind = SlotMaterialKind.HistoricalData;
                mediaKind = SlotMediaKind.OpticalDisc;
                return true;
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDamaged))
            {
                materialKind = SlotMaterialKind.Damaged;
                mediaKind = SlotMediaKind.HardDisk;
                return true;
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc))
            {
                materialKind = SlotMaterialKind.Damaged;
                mediaKind = SlotMediaKind.OpticalDisc;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 由电子介质袋载体类型解析介质类型维度。
        /// </summary>
        internal static SlotMediaKind ResolveMediaKindFromCarrierType(string? storageCarrierType)
        {
            if (ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(storageCarrierType))
            {
                return SlotMediaKind.OpticalDisc;
            }

            if (ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(storageCarrierType))
            {
                return SlotMediaKind.HardDisk;
            }

            throw new InvalidOperationException(
                $"无法识别的电子介质载体类型 [{storageCarrierType?.Trim() ?? string.Empty}]，不能校验物理存放位置。");
        }

        /// <summary>
        /// 关联硬盘为损坏在库时视为损坏资料类型，否则返回默认资料类型。
        /// </summary>
        internal static SlotMaterialKind ResolveDamagedMaterialKindOrDefault(
            string? linkedMediumMediaStatus,
            SlotMaterialKind defaultMaterialKind)
        {
            if (string.Equals(
                    linkedMediumMediaStatus?.Trim(),
                    HardDiskMedium.StatusInStockDamaged,
                    StringComparison.Ordinal))
            {
                return SlotMaterialKind.Damaged;
            }

            return defaultMaterialKind;
        }

        /// <summary>
        /// 资料类型展示名。
        /// </summary>
        internal static string ResolveMaterialKindDisplayName(SlotMaterialKind materialKind)
            => materialKind switch
            {
                SlotMaterialKind.HistoricalData => "历史数据",
                SlotMaterialKind.Damaged => "损坏介质",
                _ => "年度数据"
            };

        /// <summary>
        /// 介质类型展示名。
        /// </summary>
        internal static string ResolveMediaKindDisplayName(SlotMediaKind mediaKind)
            => mediaKind == SlotMediaKind.OpticalDisc ? "光盘" : "硬盘";

        /// <summary>
        /// 从已关联硬盘中取首块硬盘台账状态（用于损坏盘专用档口判定）。
        /// </summary>
        internal static string ResolveLinkedMediumMediaStatus(IReadOnlyList<HardDiskMedium> linkedMedia)
        {
            if (linkedMedia.Count == 0)
            {
                return string.Empty;
            }

            return linkedMedia[0].Ledger?.MediaStatus?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 将专用档口类别转为界面提示用短名称。
        /// </summary>
        internal static string ResolveCategoryDisplayName(string categoryName)
        {
            string normalized = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(categoryName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return categoryName.Trim();
            }

            const string suffix = "专用档口";
            if (normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                return normalized[..^suffix.Length];
            }

            return normalized;
        }
    }
}
