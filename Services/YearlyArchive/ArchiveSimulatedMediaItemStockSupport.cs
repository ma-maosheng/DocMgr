using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟介质资料子项可出库份数：仅「提档」领用方式扣减/校验库内份数；「复制」不影响库存。
    /// </summary>
    public static class ArchiveSimulatedMediaItemStockSupport
    {
        /// <summary>
        /// 是否模拟介质提档明细（须校验库内份数、办结时扣减登记介质库存）。
        /// </summary>
        public static bool IsSimulatedWithdrawalStockItem(YearlyArchiveOutboundItem item) =>
            string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal)
            && string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal);

        public static int ResolveCurrentInArchiveCopyCount(
            YearlyArchiveFilingFact fact,
            SimulatedFilingFactCopyCountSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(fact);
            ArgumentNullException.ThrowIfNull(snapshot);

            return SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                fact.ContentCount,
                snapshot.PendingReturnCopyCount,
                snapshot.NoReturnCopyCount,
                snapshot.LostCopyCount,
                snapshot.InventoryLostCopyCount);
        }

        public static int ResolveAvailableCopyCount(
            int currentInArchiveCopyCount,
            int reservedCopyCount) =>
            Math.Max(0, currentInArchiveCopyCount - Math.Max(0, reservedCopyCount));

        /// <summary>
        /// 格式化在途提档预订明细，如「2份（CK202501-001）、1份（CK202501-002）」。
        /// </summary>
        public static string FormatInTransitReservationDetail(
            IEnumerable<ActiveWithdrawalReservationSnapshot> reservations,
            int filingFactId)
        {
            ArgumentNullException.ThrowIfNull(reservations);

            var parts = reservations
                .Where(snapshot => snapshot.FilingFactId == filingFactId)
                .GroupBy(
                    snapshot => snapshot.OutboundNo.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Key.Length > 0)
                .Select(group => $"{group.Sum(snapshot => snapshot.ReservedCopyCount)}份（{group.Key}）")
                .ToList();

            return parts.Count == 0 ? string.Empty : string.Join("、", parts);
        }

        /// <summary>
        /// 格式化模拟介质提档份数不足时的提交校验提示。
        /// </summary>
        public static string FormatInsufficientWithdrawalStockReason(
            string itemLabel,
            int availableCopyCount,
            int requestedCopyCount,
            string? inTransitReservationDetail = null)
        {
            string message =
                $"• [{itemLabel}] 资料库内份数为 {availableCopyCount}，拟提份数为 {requestedCopyCount}";

            if (!string.IsNullOrWhiteSpace(inTransitReservationDetail))
            {
                message += $"，在途预订 {inTransitReservationDetail.Trim()}";
            }

            return message + "，库内份数不足，不可提档。";
        }
    }
}
