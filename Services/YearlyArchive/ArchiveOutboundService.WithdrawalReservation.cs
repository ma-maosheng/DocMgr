using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料出库提交前校验：模拟介质「提档」份数与资料子项库内可用份数（「复制」不校验、不扣库存）。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        /// <summary>
        /// 办结出库前复用提交校验，确保库内份数规则一致。
        /// </summary>
        private async Task EnsureSimulatedWithdrawalStockAvailableForCompletionAsync(YearlyArchiveOutboundRecord record)
        {
            var errors = await CollectSimulatedOutboundStockErrorsAsync(record);
            if (errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "办结资料出库时库内份数校验未通过：\n\n" + string.Join(Environment.NewLine, errors));
        }

        /// <summary>
        /// 提交申请前校验模拟介质提档拟领用份数是否超过资料子项当前库内可用份数。
        /// </summary>
        private async Task<IReadOnlyList<string>> CollectSimulatedOutboundStockErrorsAsync(
            YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var withdrawalItems = record.Items
                .Where(ArchiveSimulatedMediaItemStockSupport.IsSimulatedWithdrawalStockItem)
                .Where(item => item.FilingFactId > 0)
                .ToList();

            if (withdrawalItems.Count == 0)
            {
                return Array.Empty<string>();
            }

            var factIds = withdrawalItems.Select(item => item.FilingFactId).Distinct().ToList();
            var factsById = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(factIds);
            var copyCountSnapshots = await _outboundRepository
                .GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(factIds);

            int? excludeRecordId = record.Id > 0 ? record.Id : null;
            var externalReservations = await _outboundRepository
                .GetActiveWithdrawalReservationsByFilingFactIdsAsync(factIds, excludeRecordId);

            var reservedCopyCountByFactId = externalReservations
                .GroupBy(snapshot => snapshot.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(snapshot => snapshot.ReservedCopyCount));

            var requestedCopyCountByFactId = withdrawalItems
                .GroupBy(item => item.FilingFactId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => ResolveOutboundCopyCount(item)));

            var errors = new List<string>();

            foreach (var (filingFactId, requestedCopyCount) in requestedCopyCountByFactId)
            {
                if (!factsById.TryGetValue(filingFactId, out var fact))
                {
                    errors.Add($"• 未找到立档事实（Id={filingFactId}），无法提交出库申请。");
                    continue;
                }

                string itemLabel = ResolveSimulatedItemLabel(withdrawalItems, fact);
                var factItems = withdrawalItems.Where(item => item.FilingFactId == filingFactId).ToList();

                var snapshot = copyCountSnapshots.GetValueOrDefault(filingFactId)
                    ?? new SimulatedFilingFactCopyCountSnapshot();
                int currentInArchive = ArchiveSimulatedMediaItemStockSupport.ResolveCurrentInArchiveCopyCount(fact, snapshot);
                int reservedCopyCount = reservedCopyCountByFactId.GetValueOrDefault(filingFactId);
                int availableCopyCount = ArchiveSimulatedMediaItemStockSupport.ResolveAvailableCopyCount(
                    currentInArchive,
                    reservedCopyCount);

                RefreshSimulatedStockCopyCountSnapshot(factItems, currentInArchive);

                if (requestedCopyCount <= availableCopyCount)
                {
                    continue;
                }

                string inTransitReservationDetail = ArchiveSimulatedMediaItemStockSupport
                    .FormatInTransitReservationDetail(externalReservations, filingFactId);

                errors.Add(ArchiveSimulatedMediaItemStockSupport.FormatInsufficientWithdrawalStockReason(
                    itemLabel,
                    availableCopyCount,
                    requestedCopyCount,
                    inTransitReservationDetail));
            }

            return errors;
        }

        /// <summary>
        /// 收集长期存档模拟介质提档后库内份数归零的提交提醒项。
        /// </summary>
        private async Task<IReadOnlyList<SimulatedLongTermStockDepletionWarning>> CollectSimulatedLongTermStockDepletionWarningsAsync(
            YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var withdrawalItems = record.Items
                .Where(ArchiveSimulatedLongTermWithdrawalDepletionSupport.IsTargetItem)
                .Where(item => item.FilingFactId > 0)
                .ToList();

            if (withdrawalItems.Count == 0)
            {
                return Array.Empty<SimulatedLongTermStockDepletionWarning>();
            }

            var factIds = withdrawalItems.Select(item => item.FilingFactId).Distinct().ToList();
            var factsById = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(factIds);
            var copyCountSnapshots = await _outboundRepository
                .GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(factIds);

            int? excludeRecordId = record.Id > 0 ? record.Id : null;
            var externalReservations = await _outboundRepository
                .GetActiveWithdrawalReservationsByFilingFactIdsAsync(factIds, excludeRecordId);

            var reservedCopyCountByFactId = externalReservations
                .GroupBy(snapshot => snapshot.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(snapshot => snapshot.ReservedCopyCount));

            var requestedCopyCountByFactId = withdrawalItems
                .GroupBy(item => item.FilingFactId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(ArchiveSimulatedLongTermWithdrawalDepletionSupport.ResolveOutboundCopyCount));

            var warnings = new List<SimulatedLongTermStockDepletionWarning>();

            foreach (var (filingFactId, requestedCopyCount) in requestedCopyCountByFactId)
            {
                if (!factsById.TryGetValue(filingFactId, out var fact))
                {
                    continue;
                }

                var snapshot = copyCountSnapshots.GetValueOrDefault(filingFactId)
                    ?? new SimulatedFilingFactCopyCountSnapshot();
                int currentInArchive = ArchiveSimulatedMediaItemStockSupport.ResolveCurrentInArchiveCopyCount(fact, snapshot);
                int reservedCopyCount = reservedCopyCountByFactId.GetValueOrDefault(filingFactId);
                int availableCopyCount = ArchiveSimulatedMediaItemStockSupport.ResolveAvailableCopyCount(
                    currentInArchive,
                    reservedCopyCount);

                if (!ArchiveSimulatedLongTermWithdrawalDepletionSupport.WillDepleteAvailableStock(
                        availableCopyCount,
                        requestedCopyCount))
                {
                    continue;
                }

                string itemLabel = ResolveSimulatedItemLabel(withdrawalItems, fact);
                warnings.Add(new SimulatedLongTermStockDepletionWarning(
                    filingFactId,
                    itemLabel,
                    availableCopyCount,
                    requestedCopyCount));
            }

            return warnings
                .OrderBy(warning => warning.ItemLabel, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 解析打印申请单中应标注「库内归零」的立档事实 Id（与提交提醒使用同一套份数公式）。
        /// </summary>
        private async Task<HashSet<int>> ResolveDepletedFilingFactIdsForPrintAsync(
            YearlyArchiveOutboundRecord record)
        {
            var warnings = await CollectSimulatedLongTermStockDepletionWarningsAsync(record);
            return warnings.Select(warning => warning.FilingFactId).ToHashSet();
        }

        private static int ResolveOutboundCopyCount(YearlyArchiveOutboundItem item) =>
            Math.Max(1, item.CopyCount ?? 1);

        private static string ResolveSimulatedItemLabel(
            IReadOnlyList<YearlyArchiveOutboundItem> items,
            YearlyArchiveFilingFact fact)
        {
            var item = items.FirstOrDefault(candidate => candidate.FilingFactId == fact.Id);
            if (item != null && !string.IsNullOrWhiteSpace(item.ItemName))
            {
                return item.ItemName.Trim();
            }

            return string.IsNullOrWhiteSpace(fact.ItemName) ? fact.MaterialName.Trim() : fact.ItemName.Trim();
        }

        private static void RefreshSimulatedStockCopyCountSnapshot(
            IReadOnlyList<YearlyArchiveOutboundItem> items,
            int currentInArchiveCopyCount)
        {
            foreach (var item in items)
            {
                item.StockCopyCount = currentInArchiveCopyCount;
            }
        }

        private static string FormatReservationOwnerNos(
            IReadOnlyList<ActiveWithdrawalReservationSnapshot> externalReservations,
            int filingFactId)
        {
            return string.Join(
                "、",
                externalReservations
                    .Where(snapshot => snapshot.FilingFactId == filingFactId)
                    .Select(snapshot => snapshot.OutboundNo.Trim())
                    .Where(outboundNo => outboundNo.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private async Task<IReadOnlyList<string>> CollectElectronicWithdrawalReservationErrorsAsync(
            YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var withdrawalItems = record.Items
                .Where(item => string.Equals(
                    item.UsageMode,
                    ArchiveOutboundDomainValues.UsageModeWithdrawal,
                    StringComparison.Ordinal)
                    && string.Equals(
                        item.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindElectronic,
                        StringComparison.Ordinal)
                    && item.FilingFactId > 0)
                .ToList();

            if (withdrawalItems.Count == 0)
            {
                return Array.Empty<string>();
            }

            var factIds = withdrawalItems.Select(item => item.FilingFactId).Distinct().ToList();
            int? excludeRecordId = record.Id > 0 ? record.Id : null;
            var externalReservations = await _outboundRepository
                .GetActiveWithdrawalReservationsByFilingFactIdsAsync(factIds, excludeRecordId);

            var reservedCopyCountByFactId = externalReservations
                .GroupBy(snapshot => snapshot.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(snapshot => snapshot.ReservedCopyCount));

            var errors = new List<string>();

            foreach (var group in withdrawalItems.GroupBy(item => item.FilingFactId))
            {
                int filingFactId = group.Key;
                var sample = group.First();
                string itemLabel = string.IsNullOrWhiteSpace(sample.ItemName)
                    ? sample.MaterialName
                    : sample.ItemName;

                int reservedCopyCount = reservedCopyCountByFactId.GetValueOrDefault(filingFactId);
                if (reservedCopyCount <= 0)
                {
                    continue;
                }

                string ownerNos = FormatReservationOwnerNos(externalReservations, filingFactId);
                errors.Add($"• [{itemLabel}] 已被出库申请【{ownerNos}】提档预订占用，不可重复申请。");
            }

            return errors;
        }
    }
}
