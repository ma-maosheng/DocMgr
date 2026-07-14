using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 电子介质立档：载体类型与防磁磁盘柜档口专用类别对齐规则。
    /// </summary>
    internal static class ArchiveElectronicStorageSlotCategorySupport
    {
        /// <summary>
        /// 按立档载体类型及关联硬盘状态，解析应使用的专用档口类别。
        /// </summary>
        internal static string ResolveExpectedDedicatedSlotCategory(
            string? storageCarrierType,
            string? linkedMediumMediaStatus)
        {
            if (ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(storageCarrierType))
            {
                return CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc;
            }

            if (ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(storageCarrierType))
            {
                if (string.Equals(
                        linkedMediumMediaStatus?.Trim(),
                        HardDiskMedium.StatusInStockDamaged,
                        StringComparison.Ordinal))
                {
                    return CabinetHardDiskSlotCategoryAssignment.CategoryDamaged;
                }

                return CabinetHardDiskSlotCategoryAssignment.CategoryData;
            }

            throw new InvalidOperationException(
                $"无法识别的电子介质载体类型 [{storageCarrierType?.Trim() ?? string.Empty}]，不能校验物理存放位置。");
        }

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
