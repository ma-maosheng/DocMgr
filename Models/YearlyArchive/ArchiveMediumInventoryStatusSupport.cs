using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质盘库状态展示：CB-BOX-CNT「介质盘库状态」列取值（-/盘损/盘失/盘销）。
    /// </summary>
    public static class ArchiveMediumInventoryStatusSupport
    {
        public const string DisplayNormal = "-";
        public const string DisplayDamaged = "盘损";
        public const string DisplayLost = "盘失";
        public const string DisplayScrap = "盘销";

        /// <summary>将硬盘/光盘台账状态映射为四态展示文案。</summary>
        public static string ToDisplay(string? mediaStatus)
        {
            string normalized = mediaStatus?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return DisplayNormal;
            }

            if (string.Equals(normalized, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal)
                || string.Equals(normalized, OpticalDiscMedium.StatusDamaged, StringComparison.Ordinal))
            {
                return DisplayDamaged;
            }

            if (string.Equals(normalized, HardDiskMedium.StatusInStockLost, StringComparison.Ordinal)
                || string.Equals(normalized, OpticalDiscMedium.StatusLost, StringComparison.Ordinal))
            {
                return DisplayLost;
            }

            if (string.Equals(normalized, HardDiskMedium.StatusInStockScrap, StringComparison.Ordinal)
                || string.Equals(normalized, OpticalDiscMedium.StatusScrap, StringComparison.Ordinal))
            {
                return DisplayScrap;
            }

            return DisplayNormal;
        }

        /// <summary>
        /// 按介质编号命中台账；未命中时取袋内介质最严重盘库态（盘损 &gt; 盘失 &gt; 盘销 &gt; -）。
        /// </summary>
        public static string ResolveDisplay(
            string? mediumCode,
            IReadOnlyDictionary<string, string> mediaStatusByMediumCode)
        {
            ArgumentNullException.ThrowIfNull(mediaStatusByMediumCode);

            string code = mediumCode?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(code)
                && mediaStatusByMediumCode.TryGetValue(code, out string? status))
            {
                return ToDisplay(status);
            }

            return AggregateDisplay(mediaStatusByMediumCode.Values);
        }

        /// <summary>袋内多介质时取最严重盘库态。</summary>
        public static string AggregateDisplay(IEnumerable<string?> mediaStatuses)
        {
            bool hasDamaged = false;
            bool hasLost = false;
            bool hasScrap = false;

            foreach (string? status in mediaStatuses)
            {
                string display = ToDisplay(status);
                if (string.Equals(display, DisplayDamaged, StringComparison.Ordinal))
                {
                    hasDamaged = true;
                }
                else if (string.Equals(display, DisplayLost, StringComparison.Ordinal))
                {
                    hasLost = true;
                }
                else if (string.Equals(display, DisplayScrap, StringComparison.Ordinal))
                {
                    hasScrap = true;
                }
            }

            if (hasDamaged)
            {
                return DisplayDamaged;
            }

            if (hasLost)
            {
                return DisplayLost;
            }

            if (hasScrap)
            {
                return DisplayScrap;
            }

            return DisplayNormal;
        }
    }
}
