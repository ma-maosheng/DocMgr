using System;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 防磁磁盘柜档口专用类别配置。
    /// </summary>
    public class CabinetHardDiskSlotCategoryAssignment
    {
        /// <summary>
        /// 损坏硬盘专用档口。
        /// </summary>
        public const string CategoryDamaged = "损坏硬盘专用档口";

        /// <summary>
        /// 损坏光盘专用档口。
        /// </summary>
        public const string CategoryDamagedOpticalDisc = "损坏光盘专用档口";

        /// <summary>
        /// 年度数据硬盘专用档口（存放年度立档电子介质硬盘袋）。
        /// </summary>
        public const string CategoryData = "年度数据硬盘专用档口";

        /// <summary>
        /// 年度数据光盘专用档口（存放年度立档电子介质光盘袋）。
        /// </summary>
        public const string CategoryDataOpticalDisc = "年度数据光盘专用档口";

        /// <summary>
        /// 历史数据硬盘专用档口。
        /// </summary>
        public const string CategoryHistoricalDataHardDisk = "历史数据硬盘专用档口";

        /// <summary>
        /// 历史数据光盘专用档口。
        /// </summary>
        public const string CategoryHistoricalDataOpticalDisc = "历史数据光盘专用档口";

        /// <summary>
        /// 空白硬盘专用档口。
        /// </summary>
        public const string CategoryBlank = "空白硬盘专用档口";

        /// <summary>
        /// 防磁磁盘柜档口可存放的年度数据硬盘数量（5×2 矩阵）。
        /// </summary>
        public const int DedicatedHardDiskSlotCapacity = 10;

        /// <summary>
        /// 防磁磁盘柜档口可存放的年度数据光盘数量（5×4 矩阵）。
        /// </summary>
        public const int DedicatedOpticalDiscSlotCapacity = 20;

        private const string LegacyCategoryData = "数据硬盘专用档口";

        private const string LegacyCategoryDataOpticalDisc = "数据光盘专用档口";

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
        /// 专用类别。
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
        /// 判断档口已存储的类别名称是否与目标类别一致（兼容旧名称）。
        /// </summary>
        public static bool MatchesCategory(string? storedCategoryName, string expectedCategory)
        {
            if (string.IsNullOrWhiteSpace(storedCategoryName) || string.IsNullOrWhiteSpace(expectedCategory))
            {
                return false;
            }

            if (string.Equals(storedCategoryName.Trim(), expectedCategory.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return expectedCategory switch
            {
                CategoryData => string.Equals(storedCategoryName, LegacyCategoryData, StringComparison.OrdinalIgnoreCase),
                CategoryDataOpticalDisc => string.Equals(storedCategoryName, LegacyCategoryDataOpticalDisc, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        /// <summary>
        /// 将旧档口类别名称规范化为当前名称。
        /// </summary>
        public static string NormalizeCategoryName(string? categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return string.Empty;
            }

            string trimmed = categoryName.Trim();
            if (string.Equals(trimmed, LegacyCategoryData, StringComparison.OrdinalIgnoreCase))
            {
                return CategoryData;
            }

            if (string.Equals(trimmed, LegacyCategoryDataOpticalDisc, StringComparison.OrdinalIgnoreCase))
            {
                return CategoryDataOpticalDisc;
            }

            return trimmed;
        }

        /// <summary>
        /// 按专用档口类别返回物理可存放数量；未知类别默认按硬盘档口容量处理。
        /// </summary>
        public static int ResolveDedicatedSlotCapacity(string? categoryName)
        {
            if (MatchesCategory(categoryName, CategoryDataOpticalDisc)
                || MatchesCategory(categoryName, CategoryHistoricalDataOpticalDisc)
                || MatchesCategory(categoryName, CategoryDamagedOpticalDisc))
            {
                return DedicatedOpticalDiscSlotCapacity;
            }

            return DedicatedHardDiskSlotCapacity;
        }
    }
}
