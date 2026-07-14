using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料出库物理办结：同步流水、立档事实生命周期（模拟介质份数按资料子项公式校验，不扣减登记介质 MediaCount）。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        private async Task ApplyPhysicalCompletionSyncAsync(YearlyArchiveOutboundRecord record, string operatorName)
        {
            DateTime now = DateTime.Now;
            record.SyncEntries ??= new List<YearlyArchiveOutboundSyncEntry>();
            EnsureSubmitSyncEntriesForCompletion(record, operatorName, now);

            await EnsureSimulatedWithdrawalStockAvailableForCompletionAsync(record);

            var factIds = record.Items.Select(item => item.FilingFactId).Where(id => id > 0).Distinct().ToList();
            var factsById = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(factIds);

            var simulatedWithdrawalFactIds = record.Items
                .Where(ArchiveSimulatedMediaItemStockSupport.IsSimulatedWithdrawalStockItem)
                .Select(item => item.FilingFactId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var copyCountSnapshots = simulatedWithdrawalFactIds.Count == 0
                ? new Dictionary<int, SimulatedFilingFactCopyCountSnapshot>()
                : await _outboundRepository.GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(simulatedWithdrawalFactIds);

            int? excludeRecordId = record.Id > 0 ? record.Id : null;
            var externalReservations = simulatedWithdrawalFactIds.Count == 0
                ? Array.Empty<ActiveWithdrawalReservationSnapshot>()
                : await _outboundRepository.GetActiveWithdrawalReservationsByFilingFactIdsAsync(
                    simulatedWithdrawalFactIds,
                    excludeRecordId);

            var reservedCopyCountByFactId = externalReservations
                .GroupBy(snapshot => snapshot.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(snapshot => snapshot.ReservedCopyCount));

            var lifecycleUpdates = new List<FilingFactLifecycleUpdate>();

            var completedInStockBlankDiskIds = new HashSet<int>();

            foreach (var item in record.Items.Where(i =>
                         i.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal))
            {
                ConfirmWithdrawalSyncEntries(record, item, operatorName, now);
                item.ReservationStatus = ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed;

                if (factsById.TryGetValue(item.FilingFactId, out var fact))
                {
                    lifecycleUpdates.Add(ResolveWithdrawalLifecycleUpdate(
                        record,
                        item,
                        fact,
                        copyCountSnapshots,
                        reservedCopyCountByFactId));

                    if (RequiresFiledOpticalDiscWithdrawalSync(item, fact))
                    {
                        await CompleteFiledOpticalDiscWithdrawalAsync(record, item, fact, operatorName, now);
                    }

                    if (RequiresFiledHardDiskWithdrawalSync(item, fact))
                    {
                        await CompleteFiledHardDiskWithdrawalAsync(record, item, fact, operatorName, now);
                    }
                }

                if (RequiresInStockBlankDiskCompletion(item)
                    && item.RequisitionedMediumId is int withdrawalMediumId
                    && completedInStockBlankDiskIds.Add(withdrawalMediumId))
                {
                    await CompleteInStockBlankDiskOutboundAsync(record, item, operatorName, now, "提档");
                }
            }

            foreach (var item in record.Items.Where(i =>
                         i.UsageMode == ArchiveOutboundDomainValues.UsageModeCopy))
            {
                ConfirmPendingSyncEntry(
                    record,
                    item,
                    ArchiveOutboundDomainValues.SyncEntryKindCopyLedger,
                    operatorName,
                    now);
                item.ReservationStatus = ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed;

                if (factsById.TryGetValue(item.FilingFactId, out _))
                {
                    lifecycleUpdates.Add(new FilingFactLifecycleUpdate(
                        item.FilingFactId,
                        FilingFactLifecycleStatus.InArchive,
                        FilingFactBorrowHintLevel.CopyBorrowed,
                        $"出库单 {record.OutboundNo} 复制借出"));
                }
            }

            foreach (var item in record.Items.Where(i =>
                         i.UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate))
            {
                ConfirmPendingSyncEntry(
                    record,
                    item,
                    ArchiveOutboundDomainValues.SyncEntryKindDuplicateLedger,
                    operatorName,
                    now);
                item.ReservationStatus = ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed;

                if (factsById.TryGetValue(item.FilingFactId, out _))
                {
                    lifecycleUpdates.Add(new FilingFactLifecycleUpdate(
                        item.FilingFactId,
                        FilingFactLifecycleStatus.InArchive,
                        FilingFactBorrowHintLevel.CopyBorrowed,
                        $"出库单 {record.OutboundNo} 拷贝借出"));
                }

                if (RequiresInStockBlankDiskCompletion(item)
                    && item.RequisitionedMediumId is int duplicateMediumId
                    && completedInStockBlankDiskIds.Add(duplicateMediumId))
                {
                    await CompleteInStockBlankDiskOutboundAsync(record, item, operatorName, now, "拷贝");
                }
            }

            await _filingFactRepository.UpdateFilingFactLifecyclesAsync(
                lifecycleUpdates
                    .GroupBy(update => update.FilingFactId)
                    .Select(group => group.Last())
                    .ToList(),
                operatorName);
        }

        private static void EnsureSubmitSyncEntriesForCompletion(
            YearlyArchiveOutboundRecord record,
            string operatorName,
            DateTime now)
        {
            foreach (var item in record.Items)
            {
                string entryKind = item.UsageMode switch
                {
                    ArchiveOutboundDomainValues.UsageModeWithdrawal => ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReservation,
                    ArchiveOutboundDomainValues.UsageModeCopy => ArchiveOutboundDomainValues.SyncEntryKindCopyLedger,
                    ArchiveOutboundDomainValues.UsageModeDuplicate => ArchiveOutboundDomainValues.SyncEntryKindDuplicateLedger,
                    _ => ArchiveOutboundDomainValues.SyncEntryKindCopyLedger
                };

                string expectedPhase = item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                    ? ArchiveOutboundDomainValues.SyncEntryPhaseActive
                    : ArchiveOutboundDomainValues.SyncEntryPhasePending;

                bool hasSubmitEntry = record.SyncEntries.Any(entry =>
                    entry.OutboundItemId == item.Id
                    && entry.EntryKind == entryKind
                    && entry.Phase != ArchiveOutboundDomainValues.SyncEntryPhaseCancelled);

                if (hasSubmitEntry)
                {
                    continue;
                }

                record.SyncEntries.Add(new YearlyArchiveOutboundSyncEntry
                {
                    OutboundRecordId = record.Id,
                    OutboundItemId = item.Id,
                    FilingFactId = item.FilingFactId,
                    EntryKind = entryKind,
                    Phase = expectedPhase,
                    OperatedBy = operatorName,
                    Remark = "办结时补建缺失的提交同步流水",
                    CreatedAt = now
                });
            }
        }

        private static void ConfirmWithdrawalSyncEntries(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item,
            string operatorName,
            DateTime now)
        {
            foreach (var entry in record.SyncEntries.Where(e =>
                         e.OutboundItemId == item.Id
                         && e.EntryKind == ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReservation
                         && e.Phase == ArchiveOutboundDomainValues.SyncEntryPhaseActive))
            {
                entry.Phase = ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed;
                entry.UpdatedAt = now;
                entry.OperatedBy = operatorName;
            }

            bool hasLedger = record.SyncEntries.Any(e =>
                e.OutboundItemId == item.Id
                && e.EntryKind == ArchiveOutboundDomainValues.SyncEntryKindWithdrawalLedger
                && e.Phase == ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed);

            if (!hasLedger)
            {
                record.SyncEntries.Add(new YearlyArchiveOutboundSyncEntry
                {
                    OutboundRecordId = record.Id,
                    OutboundItemId = item.Id,
                    FilingFactId = item.FilingFactId,
                    EntryKind = ArchiveOutboundDomainValues.SyncEntryKindWithdrawalLedger,
                    Phase = ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed,
                    OperatedBy = operatorName,
                    Remark = "资料出库物理办结",
                    CreatedAt = now
                });
            }
        }

        private static void ConfirmPendingSyncEntry(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item,
            string entryKind,
            string operatorName,
            DateTime now)
        {
            foreach (var entry in record.SyncEntries.Where(e =>
                         e.OutboundItemId == item.Id
                         && e.EntryKind == entryKind
                         && e.Phase == ArchiveOutboundDomainValues.SyncEntryPhasePending))
            {
                entry.Phase = ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed;
                entry.UpdatedAt = now;
                entry.OperatedBy = operatorName;
            }
        }

        private static FilingFactLifecycleUpdate ResolveWithdrawalLifecycleUpdate(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item,
            YearlyArchiveFilingFact fact,
            IReadOnlyDictionary<int, SimulatedFilingFactCopyCountSnapshot> copyCountSnapshots,
            IReadOnlyDictionary<int, int> reservedCopyCountByFactId)
        {
            int copyCount = Math.Max(1, item.CopyCount ?? 1);
            bool isSimulatedWithdrawal = ArchiveSimulatedMediaItemStockSupport.IsSimulatedWithdrawalStockItem(item);

            // 仅模拟介质存在部分提档；电子介质恒为整件借出。
            if (isSimulatedWithdrawal)
            {
                var snapshot = copyCountSnapshots.GetValueOrDefault(fact.Id)
                    ?? new SimulatedFilingFactCopyCountSnapshot();
                int currentInArchive = ArchiveSimulatedMediaItemStockSupport.ResolveCurrentInArchiveCopyCount(fact, snapshot);
                int availableCopyCount = ArchiveSimulatedMediaItemStockSupport.ResolveAvailableCopyCount(
                    currentInArchive,
                    reservedCopyCountByFactId.GetValueOrDefault(fact.Id));
                bool fullLineWithdrawal = ArchiveSimulatedLongTermWithdrawalDepletionSupport.WillDepleteAvailableStock(
                    availableCopyCount,
                    copyCount);

                if (!fullLineWithdrawal)
                {
                    return new FilingFactLifecycleUpdate(
                        fact.Id,
                        FilingFactLifecycleStatus.InArchive,
                        FilingFactBorrowHintLevel.PartialAvailable,
                        $"出库单 {record.OutboundNo} 部分提档 {copyCount} 份");
                }
            }

            return new FilingFactLifecycleUpdate(
                fact.Id,
                FilingFactLifecycleStatus.Borrowed,
                FilingFactBorrowHintLevel.OriginalBorrowed,
                $"出库单 {record.OutboundNo} 提档借出");
        }
    }
}
