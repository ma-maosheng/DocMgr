using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 介质台账状态文案 → 展示色（HD-TXN 状态列、CB-OPEN 状态描述等共用）。
    /// </summary>
    public static class MediumStatusDisplayColorSupport
    {
        public const string DefaultForeground = "#64748B";

        /// <summary>
        /// 按归一化后的介质状态返回前景色十六进制；空白或未知状态返回默认灰。
        /// </summary>
        public static string ResolveForeground(string? statusText)
        {
            string normalized = MediumStatusTextNormalizer.Normalize(statusText);
            if (string.IsNullOrEmpty(normalized))
            {
                return DefaultForeground;
            }

            // 硬盘历史「出库(销毁)」与现行「离库(处置)」同色。
            string hardDiskNormalized = HardDiskMediaStatusNormalizer.Normalize(normalized);

            return hardDiskNormalized switch
            {
                HardDiskMedium.StatusInStockBlank => "#64748B", // 在库(空盘)
                HardDiskMedium.StatusInStockData => "#1D4ED8", // 在库(资料)
                HardDiskMedium.StatusInStockDamaged => "#D97706", // 在库(损坏)
                HardDiskMedium.StatusInStockLost => "#DC2626", // 在库(盘失)
                HardDiskMedium.StatusInStockScrap => "#7C3AED", // 在库(拟销)
                HardDiskMedium.StatusOutTemporary => "#0D9488", // 出库(临时)
                HardDiskMedium.StatusOutLongTerm => "#4F46E5", // 出库(长期)
                HardDiskMedium.StatusOutPermanent => "#0F766E", // 出库(永久)
                HardDiskMedium.StatusOutLost => "#B91C1C", // 出库(挂失)
                HardDiskMedium.StatusDisposed => "#57534E", // 离库(处置)
                _ => ResolveOpticalOrUnknown(normalized)
            };
        }

        private static string ResolveOpticalOrUnknown(string normalized)
        {
            // 光盘与硬盘共用文案时已在上方命中；此处仅补光盘独有「出库(销毁)」。
            if (string.Equals(normalized, OpticalDiscMedium.StatusDestroyed, StringComparison.Ordinal))
            {
                return "#57534E";
            }

            return DefaultForeground;
        }
    }
}
