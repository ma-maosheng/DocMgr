using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 档案盒内资料子项份数分解：立档份数 = 库内 + 待还 + 不还 + 出库灭失 + 盘库丢失（拟销不进恒等式，仍展示）。
    /// 电子「介质盘库状态」由台账映射，不使用本类文案。
    /// </summary>
    public static class ArchiveBoxMediaItemCopyCountSupport
    {
        public static MediaItemCopyCountBreakdown Resolve(
            YearlyArchiveFilingFact fact,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount,
            int inventoryLostCopyCount = 0,
            int inventoryScrapCopyCount = 0)
        {
            ArgumentNullException.ThrowIfNull(fact);

            bool isElectronic = string.Equals(
                fact.MediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal);

            if (isElectronic)
            {
                return ResolveElectronic(
                    pendingReturnCopyCount,
                    noReturnCopyCount,
                    lostCopyCount,
                    inventoryLostCopyCount,
                    inventoryScrapCopyCount);
            }

            int filedCopyCount = SimulatedInArchiveCopyCountSupport.ResolveFiledCopyCount(fact.ContentCount);
            int pending = Math.Max(0, pendingReturnCopyCount);
            int noReturn = Math.Max(0, noReturnCopyCount);
            int lost = Math.Max(0, lostCopyCount);
            int inventoryLost = Math.Max(0, inventoryLostCopyCount);
            int inventoryScrap = Math.Max(0, inventoryScrapCopyCount);
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
                InventoryScrapCopyCount = inventoryScrap,
            };
        }

        private static MediaItemCopyCountBreakdown ResolveElectronic(
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount,
            int inventoryLostCopyCount,
            int inventoryScrapCopyCount)
        {
            // 电子介质拷贝型出库、无需归还：份数列在 UI 显示为 —；此处仍按整件快照计算内部值。
            int pending = Math.Max(0, pendingReturnCopyCount) > 0 ? 1 : 0;
            int noReturn = Math.Max(0, noReturnCopyCount);
            int lost = Math.Max(0, lostCopyCount);
            int inventoryLost = Math.Max(0, inventoryLostCopyCount);
            int inventoryScrap = Math.Max(0, inventoryScrapCopyCount);
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
                InventoryScrapCopyCount = inventoryScrap > 0 ? 1 : 0,
                ElectronicStockStatusText = ArchiveMediumInventoryStatusSupport.DisplayNormal,
            };
        }
    }
}
