using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 档案盒内资料子项份数分解：立档份数 = 库内 + 待还 + 不还 + 灭失。
    /// 模拟介质当前库内份数按公式实时计算，不直接读取登记介质 <see cref="YearlyArchiveRegisterMedia.MediaCount"/>。
    /// </summary>
    public static class ArchiveBoxMediaItemCopyCountSupport
    {
        public static MediaItemCopyCountBreakdown Resolve(
            YearlyArchiveFilingFact fact,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount)
        {
            ArgumentNullException.ThrowIfNull(fact);

            bool isElectronic = string.Equals(
                fact.MediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal);

            if (isElectronic)
            {
                return ResolveElectronic(fact, pendingReturnCopyCount, noReturnCopyCount, lostCopyCount);
            }

            int filedCopyCount = SimulatedInArchiveCopyCountSupport.ResolveFiledCopyCount(fact.ContentCount);
            int pending = Math.Max(0, pendingReturnCopyCount);
            int noReturn = Math.Max(0, noReturnCopyCount);
            int lost = Math.Max(0, lostCopyCount);
            int currentInArchive = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                filedCopyCount,
                pending,
                noReturn,
                lost);

            return new MediaItemCopyCountBreakdown
            {
                FiledCopyCount = filedCopyCount,
                CurrentInArchiveCopyCount = currentInArchive,
                PendingReturnCopyCount = pending,
                NoReturnCopyCount = noReturn,
                LostCopyCount = lost,
            };
        }

        private static MediaItemCopyCountBreakdown ResolveElectronic(
            YearlyArchiveFilingFact fact,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount)
        {
            // 电子介质不存在部分提档，待还按整件（0 或 1）表达。
            int pending = Math.Max(0, pendingReturnCopyCount) > 0 ? 1 : 0;
            int noReturn = Math.Max(0, noReturnCopyCount);
            int lost = Math.Max(0, lostCopyCount);
            int current = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                filedCopyCount: 1,
                pendingReturnCopyCount: pending,
                noReturnCopyCount: noReturn,
                lostCopyCount: lost);

            return new MediaItemCopyCountBreakdown
            {
                FiledCopyCount = 1,
                CurrentInArchiveCopyCount = current,
                PendingReturnCopyCount = pending > 0 ? 1 : 0,
                NoReturnCopyCount = noReturn > 0 ? 1 : 0,
                LostCopyCount = lost > 0 ? 1 : 0,
                ElectronicStockStatusText = BuildElectronicStockStatusText(current, pending, noReturn, lost, fact.LifecycleStatus),
            };
        }

        private static string BuildElectronicStockStatusText(
            int currentInArchive,
            int pendingReturn,
            int noReturn,
            int lost,
            string lifecycleStatus)
        {
            if (lost > 0)
            {
                return "灭失";
            }

            if (noReturn > 0)
            {
                return "出库不还";
            }

            if (pendingReturn > 0)
            {
                return "出库待还";
            }

            if (currentInArchive > 0)
            {
                return "在库";
            }

            if (string.Equals(lifecycleStatus, FilingFactLifecycleStatus.Borrowed, StringComparison.Ordinal))
            {
                return "借出中";
            }

            if (string.Equals(lifecycleStatus, FilingFactLifecycleStatus.Disposed, StringComparison.Ordinal))
            {
                return "已处置";
            }

            return "不在库";
        }
    }
}
