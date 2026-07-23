using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 硬盘台账状态文案归一化：括号半角化 + 历史「出库(销毁)」→「离库(处置)」。
    /// 仅用于硬盘台账/流水，勿用于光盘（光盘仍保留「出库(销毁)」语义）。
    /// </summary>
    public static class HardDiskMediaStatusNormalizer
    {
        public const string LegacyStatusOutDestroyed = "出库(销毁)";

        /// <summary>
        /// 归一化硬盘介质状态；空白输入返回空字符串。
        /// </summary>
        public static string Normalize(string? statusText)
        {
            string normalized = MediumStatusTextNormalizer.Normalize(statusText);
            if (string.Equals(normalized, LegacyStatusOutDestroyed, StringComparison.Ordinal))
            {
                return HardDiskMedium.StatusDisposed;
            }

            return normalized;
        }

        /// <summary>
        /// 归一化硬盘流转类型；历史「出库(销毁)」→「离库(处置)」。
        /// </summary>
        public static string NormalizeTransactionType(string? transactionType)
        {
            string normalized = MediumStatusTextNormalizer.Normalize(transactionType);
            if (string.Equals(normalized, LegacyStatusOutDestroyed, StringComparison.Ordinal))
            {
                return HardDiskMediaTransaction.TypeDisposal;
            }

            return normalized;
        }
    }
}
