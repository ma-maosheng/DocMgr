namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案柜名称规范化工具。
    /// </summary>
    public static class CabinetNameNormalizer
    {
        public static string Normalize(string? cabinetName)
        {
            if (string.IsNullOrWhiteSpace(cabinetName))
            {
                return string.Empty;
            }

            return cabinetName.Trim().ToUpperInvariant();
        }
    }
}
