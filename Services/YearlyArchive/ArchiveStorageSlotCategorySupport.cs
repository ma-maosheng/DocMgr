using DocMgr.Models.Cabinets;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 标准滑道式档案柜：模拟介质（档案盒）落位与档口用途对齐规则。
    /// </summary>
    internal static class ArchiveStorageSlotCategorySupport
    {
        /// <summary>
        /// 年度模拟立档 / 归还目标盒应使用的档口用途。
        /// </summary>
        internal static string ExpectedYearlyMaterialsCategory =>
            CabinetArchiveSlotCategoryAssignment.CategoryYearlyMaterials;

        /// <summary>
        /// 历史存档资料应使用的档口用途。
        /// </summary>
        internal static string ExpectedHistoricalMaterialsCategory =>
            CabinetArchiveSlotCategoryAssignment.CategoryHistoricalMaterials;

        /// <summary>
        /// 将档口用途转为界面提示用短名称。
        /// </summary>
        internal static string ResolveCategoryDisplayName(string? categoryName)
        {
            string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(categoryName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return CabinetArchiveSlotCategoryAssignment.CategoryUnset;
            }

            const string suffix = "专用档口";
            if (normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                return normalized[..^suffix.Length];
            }

            return normalized;
        }

        /// <summary>
        /// 判断档口已配置用途是否满足期望（仅比较规范化后的用途名）。
        /// </summary>
        internal static bool MatchesExpectedCategory(string? storedCategoryName, string expectedCategory)
        {
            return CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(storedCategoryName),
                expectedCategory);
        }

        /// <summary>
        /// 年度或历史资料落位：匹配期望专用用途，或已是「混用档口」。
        /// </summary>
        internal static bool MatchesCompatibleLandingCategory(string? storedCategoryName, string expectedCategory)
        {
            string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(storedCategoryName);
            return MatchesExpectedCategory(normalized, expectedCategory)
                || CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryMixed);
        }

        /// <summary>
        /// 校验标准滑道式档案柜档口用途；非 Standard 柜型不做用途限制（返回 null 表示通过）。
        /// </summary>
        /// <returns>错误信息；通过时为 null。</returns>
        internal static string? TryValidateStandardSlotCategory(
            Cabinet? cabinet,
            string? faceCode,
            string? slotCode,
            string? storedCategoryName,
            string expectedCategory,
            string locationDisplay)
        {
            if (cabinet == null)
            {
                return "未找到目标档案柜，请重新选择存放位置。";
            }

            if (cabinet.Type != CabinetType.Standard)
            {
                return null;
            }

            string expectedDisplay = ResolveCategoryDisplayName(expectedCategory);
            string face = faceCode?.Trim() ?? string.Empty;
            string slot = slotCode?.Trim() ?? string.Empty;
            string slotLabel = string.IsNullOrWhiteSpace(face) && string.IsNullOrWhiteSpace(slot)
                ? locationDisplay.Trim()
                : $"{cabinet.Name}{face}-{slot}";

            if (string.IsNullOrWhiteSpace(storedCategoryName))
            {
                return $"档口 [{slotLabel}] 尚未设置用途。当前资料应放入「{expectedDisplay}」档口，请重新选择位置或在开柜界面完成设置。";
            }

            string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(storedCategoryName);
            if (MatchesCompatibleLandingCategory(normalized, expectedCategory))
            {
                return null;
            }

            string actualDisplay = ResolveCategoryDisplayName(normalized);
            string locationText = string.IsNullOrWhiteSpace(locationDisplay) ? slotLabel : locationDisplay.Trim();
            return $"存放位置 [{locationText}] 的档口用途为「{actualDisplay}」，与当前资料要求的「{expectedDisplay}」不一致，请重新选择位置。";
        }

        /// <summary>
        /// 由柜面与行列组装档口编号（如 3-2）。
        /// </summary>
        internal static string BuildSlotCode(int row, int column) => $"{row}-{column}";

        /// <summary>
        /// 组装档口用途查找键：柜体Id:面:档口编号。
        /// </summary>
        internal static string BuildCategoryLookupKey(int cabinetId, string faceCode, string slotCode)
            => $"{cabinetId}:{faceCode.Trim()}:{slotCode.Trim()}";
    }
}
