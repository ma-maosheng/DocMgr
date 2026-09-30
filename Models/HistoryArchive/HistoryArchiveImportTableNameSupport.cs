namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档 Excel 导入的逻辑表名：前缀（含全角冒号）+ 后缀（默认 Excel 工作表名）。
    /// </summary>
    public static class HistoryArchiveImportTableNameSupport
    {
        /// <summary>地形图逻辑表名前缀。</summary>
        public const string TopoMapPrefix = "地形图：";

        /// <summary>航摄影像逻辑表名前缀。</summary>
        public const string AerialPhotoPrefix = "航片：";

        /// <summary>其他资料逻辑表名前缀。</summary>
        public const string OtherMapPrefix = "其他：";

        /// <summary>旧版地形图逻辑表名前缀，仅用于存量数据识别。</summary>
        public const string LegacyTopoMapPrefix = "历史存档纸质地形图";

        /// <summary>无冒号版地形图前缀，仅用于存量数据识别。</summary>
        public const string LegacyTopoMapImportPrefix = "地形图";

        /// <summary>旧版航摄影像逻辑表名前缀，仅用于存量数据识别。</summary>
        public const string LegacyAerialPhotoPrefix = "历史存档航摄影像";

        /// <summary>无冒号版航片前缀，仅用于存量数据识别。</summary>
        public const string LegacyAerialPhotoBarePrefix = "航片";

        /// <summary>迁移前航摄影像前缀「像片」，仅用于存量数据识别。</summary>
        public const string LegacyAerialPhotoImportPrefix = "像片";

        /// <summary>无冒号版其他前缀，仅用于存量数据识别。</summary>
        public const string LegacyOtherMapBarePrefix = "其他";

        /// <summary>迁移前其他资料前缀「其他资料」，仅用于存量数据识别。</summary>
        public const string LegacyOtherMapPrefix = "其他资料";

        /// <summary>
        /// 生成地形图默认分类：地形图： + 后缀。
        /// </summary>
        public static string BuildTopoMapTableName(string? suffix)
            => BuildDefaultCategoryName(TopoMapPrefix, suffix);

        /// <summary>
        /// 生成航片默认分类：航片： + 后缀。
        /// </summary>
        public static string BuildAerialPhotoTableName(string? suffix)
            => BuildDefaultCategoryName(AerialPhotoPrefix, suffix);

        /// <summary>
        /// 生成其他默认分类：其他： + 后缀。
        /// </summary>
        public static string BuildOtherMapTableName(string? suffix)
            => BuildDefaultCategoryName(OtherMapPrefix, suffix);

        /// <summary>
        /// 按前缀与后缀生成默认分类名。
        /// </summary>
        public static string BuildDefaultCategoryName(string prefix, string? suffix)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
            string trimmedSuffix = suffix?.Trim() ?? string.Empty;
            ArgumentException.ThrowIfNullOrWhiteSpace(trimmedSuffix);
            return prefix.Trim() + trimmedSuffix;
        }

        /// <summary>
        /// 校验分类是否符合「前缀****」形式（须以 requiredPrefix 开头且冒号后有非空内容）。
        /// </summary>
        public static bool TryValidateCategoryName(
            string? categoryName,
            string requiredPrefix,
            out string? errorMessage)
        {
            string prefix = requiredPrefix?.Trim() ?? string.Empty;
            string name = categoryName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(prefix))
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errorMessage = "请填写分类！";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errorMessage = $"请填写分类，格式须为「{prefix}****」。";
                return false;
            }

            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                errorMessage = $"分类须以「{prefix}」开头，格式如「{prefix}****」。";
                return false;
            }

            string rest = name[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(rest))
            {
                errorMessage = $"「{prefix}」后须填写具体名称，格式如「{prefix}****」。";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 规范化完整分类名；空白则抛。
        /// </summary>
        public static string NormalizeCategoryName(string? categoryName)
        {
            string name = categoryName?.Trim() ?? string.Empty;
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return name;
        }
    }
}
