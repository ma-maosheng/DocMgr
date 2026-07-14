using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟介质资料子项份数：立档份数取 <see cref="YearlyArchiveFilingFact.ContentCount"/>；
    /// 当前库内份数按「立档 = 库内 + 待还 + 不还 + 灭失」公式实时计算（非登记介质 <see cref="YearlyArchiveRegisterMedia.MediaCount"/>）。
    /// </summary>
    public static class SimulatedInArchiveCopyCountSupport
    {
        public static int ResolveFiledCopyCount(int contentCount) => Math.Max(1, contentCount);

        /// <summary>检索池等场景：仅扣减已办结未归还提档份数。</summary>
        public static int ResolveCurrentInArchiveCopyCount(int filedCopyCount, int withdrawnCopyCount) =>
            ResolveCurrentInArchiveCopyCount(
                filedCopyCount,
                pendingReturnCopyCount: withdrawnCopyCount,
                noReturnCopyCount: 0,
                lostCopyCount: 0);

        /// <summary>
        /// 按份数分解公式计算资料子项当前库内份数。
        /// </summary>
        public static int ResolveCurrentInArchiveCopyCount(
            int filedCopyCount,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount)
        {
            int filed = ResolveFiledCopyCount(filedCopyCount);
            int current = filed
                - Math.Max(0, pendingReturnCopyCount)
                - Math.Max(0, noReturnCopyCount)
                - Math.Max(0, lostCopyCount);
            return Math.Max(0, current);
        }

        public static string FormatDisplay(int filedCopyCount, int withdrawnCopyCount)
        {
            int filed = ResolveFiledCopyCount(filedCopyCount);
            int inArchive = ResolveCurrentInArchiveCopyCount(filed, withdrawnCopyCount);
            return $"{inArchive}/{filed}";
        }
    }
}
