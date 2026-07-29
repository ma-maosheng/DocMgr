using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 档案盒内资料子项份数分解：立档份数 = 库内 + 待还 + 不还 + 归还灭失 + 盘库丢失。
    /// </summary>
    public static class ArchiveBoxMediaItemCopyCountSupport
    {
        public static MediaItemCopyCountBreakdown Resolve(
            YearlyArchiveFilingFact fact,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount,
            int inventoryLostCopyCount = 0)
        {
            ArgumentNullException.ThrowIfNull(fact);

            bool isElectronic = string.Equals(
                fact.MediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal);

            if (isElectronic)
            {
                return ResolveElectronic(
                    fact,
                    pendingReturnCopyCount,
                    noReturnCopyCount,
                    lostCopyCount,
                    inventoryLostCopyCount);
            }

            int filedCopyCount = SimulatedInArchiveCopyCountSupport.ResolveFiledCopyCount(fact.ContentCount);
            int pending = Math.Max(0, pendingReturnCopyCount);
            int noReturn = Math.Max(0, noReturnCopyCount);
            int lost = Math.Max(0, lostCopyCount);
            int inventoryLost = Math.Max(0, inventoryLostCopyCount);
            int currentInArchive = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                filedCopyCount,
                pending,
                noReturn,
                lost,
                inventoryLost);

            return new MediaItemCopyCountBreakdown
            {
                FiledCopyCount = filedCopyCount,
                CurrentInArchiveCopyCount = currentInArchive,
                PendingReturnCopyCount = pending,
                NoReturnCopyCount = noReturn,
                LostCopyCount = lost,
                InventoryLostCopyCount = inventoryLost,
            };
        }

        private static MediaItemCopyCountBreakdown ResolveElectronic(
            YearlyArchiveFilingFact fact,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount,
            int inventoryLostCopyCount)
        {
            // 电子介质不存在部分提档，待还按整件（0 或 1）表达。
            int pending = Math.Max(0, pendingReturnCopyCount) > 0 ? 1 : 0;
            int noReturn = Math.Max(0, noReturnCopyCount);
            int lost = Math.Max(0, lostCopyCount);
            int inventoryLost = Math.Max(0, inventoryLostCopyCount);
            int current = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                filedCopyCount: 1,
                pendingReturnCopyCount: pending,
                noReturnCopyCount: noReturn,
                lostCopyCount: lost,
                inventoryLostCopyCount: inventoryLost);

            return new MediaItemCopyCountBreakdown
            {
                FiledCopyCount = 1,
                CurrentInArchiveCopyCount = current,
                PendingReturnCopyCount = pending > 0 ? 1 : 0,
                NoReturnCopyCount = noReturn > 0 ? 1 : 0,
                LostCopyCount = lost > 0 ? 1 : 0,
                InventoryLostCopyCount = inventoryLost > 0 ? 1 : 0,
                ElectronicStockStatusText = BuildElectronicStockStatusText(current, pending, noReturn, lost, inventoryLost, fact.LifecycleStatus),
            };
        }

        private static string BuildElectronicStockStatusText(
            int currentInArchive,
            int pendingReturn,
            int noReturn,
            int lost,
            int inventoryLost,
            string lifecycleStatus)
        {
            if (lost > 0 || inventoryLost > 0)
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
