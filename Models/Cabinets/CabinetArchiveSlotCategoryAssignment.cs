using System;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 标准滑道式档案柜档口用途配置（模拟介质档案盒存放档口）。
    /// </summary>
    public class CabinetArchiveSlotCategoryAssignment
    {
        /// <summary>
        /// 未设置（默认用途；兼容历史写法「未定义」）。
        /// </summary>
        public const string CategoryUnset = "未设置";

        /// <summary>
        /// 「未定义」别名，与 <see cref="CategoryUnset"/> 等价。
        /// </summary>
        public const string CategoryUndefinedAlias = "未定义";

        /// <summary>
        /// 年度资料专用档口。
        /// </summary>
        public const string CategoryYearlyMaterials = "年度资料专用档口";

        /// <summary>
        /// 历史资料专用档口。
        /// </summary>
        public const string CategoryHistoricalMaterials = "历史资料专用档口";

        /// <summary>
        /// 混用档口（年度资料与历史资料均可存放）。
        /// </summary>
        public const string CategoryMixed = "混用档口";

        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 柜体主键。
        /// </summary>
        public int CabinetId { get; set; }

        /// <summary>
        /// 门别编码。
        /// </summary>
        public string FaceCode { get; set; } = string.Empty;

        /// <summary>
        /// 档口编号。
        /// </summary>
        public string SlotCode { get; set; } = string.Empty;

        /// <summary>
        /// 档口用途。
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 更新时间。
        /// </summary>
        public DateTime UpdatedTime { get; set; }

        /// <summary>
        /// 关联柜体。
        /// </summary>
        public Cabinet? Cabinet { get; set; }

        /// <summary>
        /// 判断已存储用途是否与目标用途一致（「未定义」视为「未设置」）。
        /// </summary>
        public static bool MatchesCategory(string? storedCategoryName, string expectedCategory)
        {
            if (string.IsNullOrWhiteSpace(storedCategoryName) || string.IsNullOrWhiteSpace(expectedCategory))
            {
                return false;
            }

            string normalizedStored = NormalizeCategoryName(storedCategoryName);
            string normalizedExpected = NormalizeCategoryName(expectedCategory);
            return string.Equals(normalizedStored, normalizedExpected, StringComparison.Ordinal);
        }

        /// <summary>
        /// 将用途名称规范化为当前常量（空值与「未定义」归一为「未设置」）。
        /// </summary>
        public static string NormalizeCategoryName(string? categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return CategoryUnset;
            }

            string trimmed = categoryName.Trim();
            if (string.Equals(trimmed, CategoryUndefinedAlias, StringComparison.Ordinal)
                || string.Equals(trimmed, CategoryUnset, StringComparison.Ordinal))
            {
                return CategoryUnset;
            }

            return trimmed;
        }

        /// <summary>
        /// 是否为已明确指定的专用用途（非未设置）。
        /// </summary>
        public static bool IsDedicatedCategory(string? categoryName)
        {
            string normalized = NormalizeCategoryName(categoryName);
            return MatchesCategory(normalized, CategoryYearlyMaterials)
                || MatchesCategory(normalized, CategoryHistoricalMaterials)
                || MatchesCategory(normalized, CategoryMixed);
        }

        /// <summary>
        /// 是否为允许的档口用途取值。
        /// </summary>
        public static bool IsKnownCategory(string? categoryName)
        {
            string normalized = NormalizeCategoryName(categoryName);
            return MatchesCategory(normalized, CategoryUnset)
                || MatchesCategory(normalized, CategoryYearlyMaterials)
                || MatchesCategory(normalized, CategoryHistoricalMaterials)
                || MatchesCategory(normalized, CategoryMixed);
        }
    }
}
