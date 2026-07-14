using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
        /// <summary>
        /// 资料子项库存份数：模拟介质取登记介质当前 <see cref="YearlyArchiveRegisterMedia.MediaCount"/>（出库预留），
        /// 电子介质恒为 1。检索列表展示份数请用立档事实 <see cref="YearlyArchiveFilingFact.ContentCount"/>。
        /// </summary>
    public static class ArchiveStockCopyCountSupport
    {
        /// <summary>
        /// 解析资料子项当前可出库库存份数。
        /// </summary>
        public static int ResolveStockCopyCount(string mediaKind, int registerMediaMediaCount)
        {
            if (string.Equals(
                    mediaKind,
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    StringComparison.Ordinal))
            {
                return 1;
            }

            return Math.Max(0, registerMediaMediaCount);
        }

        /// <summary>
        /// 格式化检索结果中的库存份数展示文案。
        /// </summary>
        public static string FormatDisplay(string mediaKind, int stockCopyCount)
        {
            if (string.Equals(
                    mediaKind,
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    StringComparison.Ordinal))
            {
                return "1 份";
            }

            return stockCopyCount > 0 ? $"{stockCopyCount} 份" : "0 份";
        }
    }
}
