namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档 Excel 导入的逻辑表名：前缀 + Excel 工作表名。
    /// </summary>
    public static class HistoryArchiveImportTableNameSupport
    {
        /// <summary>地形图逻辑表名前缀。</summary>
        public const string TopoMapPrefix = "地形图";

        /// <summary>航摄影像逻辑表名前缀。</summary>
        public const string AerialPhotoPrefix = "像片";

        /// <summary>其他资料逻辑表名前缀。</summary>
        public const string OtherMapPrefix = "其他资料";

        /// <summary>旧版地形图逻辑表名前缀，仅用于存量数据识别。</summary>
        public const string LegacyTopoMapPrefix = "历史存档纸质地形图";

        /// <summary>旧版航摄影像逻辑表名前缀，仅用于存量数据识别。</summary>
        public const string LegacyAerialPhotoPrefix = "历史存档航摄影像";

        /// <summary>
        /// 生成地形图逻辑表名：地形图 + Excel 工作表名。
        /// </summary>
        public static string BuildTopoMapTableName(string? excelSheetName)
        {
            return Build(TopoMapPrefix, excelSheetName);
        }

        /// <summary>
        /// 生成像片逻辑表名：像片 + Excel 工作表名。
        /// </summary>
        public static string BuildAerialPhotoTableName(string? excelSheetName)
        {
            return Build(AerialPhotoPrefix, excelSheetName);
        }

        /// <summary>
        /// 生成其他资料逻辑表名：其他资料 + Excel 工作表名。
        /// </summary>
        public static string BuildOtherMapTableName(string? excelSheetName)
        {
            return Build(OtherMapPrefix, excelSheetName);
        }

        private static string Build(string prefix, string? excelSheetName)
        {
            string sheetName = excelSheetName?.Trim() ?? string.Empty;
            ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
            return prefix + sheetName;
        }
    }
}
