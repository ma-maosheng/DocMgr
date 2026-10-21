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

        /// <summary>
        /// 两个柜名是否指同一柜。忽略大小写，并将「甲」与「甲柜」视为同一柜。
        /// </summary>
        public static bool AreSame(string? left, string? right)
        {
            string normalizedLeft = Normalize(left);
            string normalizedRight = Normalize(right);
            if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
            {
                return false;
            }

            if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(TrimCabinetSuffix(normalizedLeft), TrimCabinetSuffix(normalizedRight), StringComparison.Ordinal);
        }

        private static string TrimCabinetSuffix(string cabinetName)
        {
            if (cabinetName.EndsWith("柜", StringComparison.Ordinal))
            {
                return cabinetName[..^1];
            }

            return cabinetName;
        }
    }
}
