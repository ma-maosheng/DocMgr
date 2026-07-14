namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案盒规格的统一归一化逻辑。
    /// 将历史录入的「厚/中/薄」等简写映射为标准规格名称，供立档、迁档、占位统计等流程复用，
    /// 避免在各业务服务中平行实现第二套解析规则。
    /// </summary>
    public static class ArchiveBoxSpecificationSupport
    {
        /// <summary>厚盒标准规格名称。</summary>
        public const string ThickStandard = "标准(10cm)";

        /// <summary>中盒标准规格名称，亦作为无法识别时的默认规格。</summary>
        public const string MediumStandard = "标准(5cm)";

        /// <summary>薄盒标准规格名称。</summary>
        public const string ThinStandard = "标准(3cm)";

        /// <summary>
        /// 归一化档案盒规格名称。
        /// 「厚/中/薄」映射为对应标准规格；空白返回默认中盒规格；其余按去除首尾空白后的原值返回。
        /// </summary>
        /// <param name="value">原始规格文本，允许为 null。</param>
        /// <returns>归一化后的规格名称，保证非空。</returns>
        public static string Normalize(string? value)
        {
            return value?.Trim() switch
            {
                "厚" => ThickStandard,
                "中" => MediumStandard,
                "薄" => ThinStandard,
                _ => string.IsNullOrWhiteSpace(value) ? MediumStandard : value.Trim()
            };
        }
    }
}
