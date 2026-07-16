namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质业务编号解析辅助。
    /// </summary>
    public static class HardDiskMediaApplicationNoSupport
    {
        /// <summary>
        /// 从业务编号（如「盘-出-申-2026-0001」「资-出-申-2026-0001」）解析年度。
        /// </summary>
        public static bool TryParseBusinessNoYear(string? businessNo, out int year)
        {
            year = 0;
            if (string.IsNullOrWhiteSpace(businessNo))
            {
                return false;
            }

            foreach (string part in businessNo.Trim().Split('-'))
            {
                if (part.Length == 4
                    && int.TryParse(part, out int parsedYear)
                    && parsedYear is > 2000 and < 2100)
                {
                    year = parsedYear;
                    return true;
                }
            }

            return false;
        }
    }
}
