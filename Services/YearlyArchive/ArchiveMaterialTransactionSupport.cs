using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 年度资料流转履历：实体映射、展示文案与去重键构造。
    /// </summary>
    internal static class ArchiveMaterialTransactionSupport
    {
        public static string BuildFilingDedupKey(YearlyArchiveFilingFact fact)
            => $"FilingFact:Source:{fact.SourceLinkType}:{fact.SourceLinkId}";

        public static string BuildFilingDedupKeyFromFactId(int filingFactId)
            => $"FilingFact:Id:{filingFactId}";

        public static string BuildRelocationDedupKey(string relocationNo, int filingFactId)
            => $"Relocation:{relocationNo}:Fact:{filingFactId}";

        public static string BuildRelocationItemDedupKey(int relocationItemId)
            => $"RelocationItem:{relocationItemId}";

        public static string BuildOutboundSyncDedupKey(int syncEntryId)
            => $"OutboundSyncEntry:{syncEntryId}";

        public static string BuildReturnItemDedupKey(int returnItemId)
            => $"ReturnItem:{returnItemId}:Completed";

        public static YearlyArchiveMaterialTransaction BuildFilingTransaction(YearlyArchiveFilingFact fact)
        {
            DateTime operatedAt = fact.FiledAt;
            return new YearlyArchiveMaterialTransaction
            {
                FilingFactId = fact.Id,
                TransactionType = MaterialTransactionDomainValues.TypeFiling,
                BusinessNo = fact.FilingFactNo,
                SourceKind = MaterialTransactionDomainValues.SourceFilingFact,
                SourceId = fact.Id,
                DedupKey = fact.Id > 0
                    ? BuildFilingDedupKeyFromFactId(fact.Id)
                    : BuildFilingDedupKey(fact),
                AfterLifecycleStatus = FilingFactLifecycleStatus.InArchive,
                AfterContainerCode = fact.ContainerCode,
                AfterStorageLocation = fact.StorageLocation,
                Summary = $"立档入库 · {fact.ContainerCode} · {fact.StorageLocation}",
                OperatorName = fact.FiledBy.Trim(),
                OperatedAt = operatedAt,
                CreatedAt = DateTime.Now
            };
        }

        public static YearlyArchiveMaterialTransaction BuildRelocationTransaction(
            YearlyArchiveRelocationRecord record,
            YearlyArchiveRelocationItem item)
        {
            string dedupKey = item.Id > 0
                ? BuildRelocationItemDedupKey(item.Id)
                : BuildRelocationDedupKey(record.RelocationNo, item.FilingFactId);

            string modeDisplay = MaterialTransactionDomainValues.MapRelocationModeDisplay(record.RelocationMode);
            bool locationChanged = !string.Equals(
                item.BeforeStorageLocation?.Trim(),
                item.AfterStorageLocation?.Trim(),
                StringComparison.OrdinalIgnoreCase);
            bool containerChanged = !string.Equals(
                item.BeforeContainerCode?.Trim(),
                item.AfterContainerCode?.Trim(),
                StringComparison.OrdinalIgnoreCase);

            string summary = containerChanged
                ? $"迁档（{modeDisplay}）· 容器 {item.BeforeContainerCode} → {item.AfterContainerCode}"
                : locationChanged
                    ? $"迁档（{modeDisplay}）· 位置 {item.BeforeStorageLocation} → {item.AfterStorageLocation}"
                    : $"迁档（{modeDisplay}）";

            return new YearlyArchiveMaterialTransaction
            {
                FilingFactId = item.FilingFactId,
                TransactionType = MaterialTransactionDomainValues.TypeRelocation,
                BusinessNo = record.RelocationNo,
                SourceKind = MaterialTransactionDomainValues.SourceRelocationItem,
                SourceId = item.Id,
                DedupKey = dedupKey,
                BeforeContainerCode = item.BeforeContainerCode ?? string.Empty,
                AfterContainerCode = item.AfterContainerCode ?? string.Empty,
                BeforeStorageLocation = item.BeforeStorageLocation ?? string.Empty,
                AfterStorageLocation = item.AfterStorageLocation ?? string.Empty,
                Summary = summary,
                Remark = record.Remarks?.Trim() ?? string.Empty,
                OperatorName = record.OperatedBy.Trim(),
                OperatedAt = record.OperatedAt,
                CreatedAt = DateTime.Now
            };
        }

        public static IEnumerable<YearlyArchiveMaterialTransaction> BuildOutboundCompletionTransactions(
            YearlyArchiveOutboundRecord record)
        {
            foreach (var entry in record.SyncEntries.Where(ShouldIncludeOutboundSyncEntry))
            {
                var item = record.Items.FirstOrDefault(i => i.Id == entry.OutboundItemId);
                if (item == null)
                {
                    continue;
                }

                yield return BuildOutboundSyncTransaction(record, entry, item);
            }
        }

        public static IEnumerable<YearlyArchiveMaterialTransaction> BuildReturnCompletionTransactions(
            YearlyArchiveReturnRecord returnRecord,
            YearlyArchiveOutboundRecord outboundRecord,
            IReadOnlyDictionary<int, string>? afterLifecycleByFactId = null)
        {
            DateTime operatedAt = returnRecord.CompletedAt ?? returnRecord.UpdatedAt;
            string operatorName = string.IsNullOrWhiteSpace(returnRecord.HandlerName)
                ? returnRecord.RegisteredByName
                : returnRecord.HandlerName;

            foreach (var item in returnRecord.Items)
            {
                int lossCopyCount = ArchiveReturnDomainValues.ResolveLossCopyCount(item);
                string afterLifecycle = afterLifecycleByFactId != null
                    && afterLifecycleByFactId.TryGetValue(item.FilingFactId, out string? resolved)
                    && !string.IsNullOrWhiteSpace(resolved)
                    ? resolved.Trim()
                    : FilingFactLifecycleStatus.InArchive;

                yield return new YearlyArchiveMaterialTransaction
                {
                    FilingFactId = item.FilingFactId,
                    TransactionType = MaterialTransactionDomainValues.TypeReturn,
                    BusinessNo = returnRecord.ReturnNo,
                    SourceKind = MaterialTransactionDomainValues.SourceReturnItem,
                    SourceId = item.Id,
                    DedupKey = item.Id > 0
                        ? BuildReturnItemDedupKey(item.Id)
                        : $"Return:{returnRecord.ReturnNo}:Fact:{item.FilingFactId}",
                    BeforeLifecycleStatus = FilingFactLifecycleStatus.Borrowed,
                    AfterLifecycleStatus = afterLifecycle,
                    Summary = lossCopyCount > 0
                        ? (string.Equals(afterLifecycle, FilingFactLifecycleStatus.Destroyed, StringComparison.Ordinal)
                            ? $"资料归还灭失办结 · 源出库 {returnRecord.SourceOutboundNo}"
                            : $"资料归还入库（含灭失）· 源出库 {returnRecord.SourceOutboundNo}")
                        : $"资料归还入库 · 源出库 {returnRecord.SourceOutboundNo}",
                    Remark = ArchiveReturnDomainValues.BuildReturnCopyCountSummary(item)
                        + (string.IsNullOrWhiteSpace(returnRecord.LossDescription) ? string.Empty : $"；{returnRecord.LossDescription.Trim()}"),
                    OperatorName = operatorName.Trim(),
                    OperatedAt = operatedAt,
                    CreatedAt = DateTime.Now
                };
            }
        }

        public static YearlyArchiveMaterialTransaction? BuildLegacyFilingTransaction(YearlyArchiveFilingFact fact)
        {
            if (fact.FiledAt == default)
            {
                return null;
            }

            return BuildFilingTransaction(fact);
        }

        public static YearlyArchiveMaterialTransaction BuildLegacyOutboundSyncTransaction(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundSyncEntry entry,
            YearlyArchiveOutboundItem item)
            => BuildOutboundSyncTransaction(record, entry, item);

        public static YearlyArchiveMaterialTransaction BuildLegacyReturnTransaction(
            YearlyArchiveReturnItem item,
            YearlyArchiveReturnRecord returnRecord)
        {
            DateTime operatedAt = returnRecord.CompletedAt ?? returnRecord.UpdatedAt;
            string operatorName = string.IsNullOrWhiteSpace(returnRecord.HandlerName)
                ? returnRecord.RegisteredByName
                : returnRecord.HandlerName;
            int lossCopyCount = ArchiveReturnDomainValues.ResolveLossCopyCount(item);
            int intactCopyCount = ArchiveReturnDomainValues.ResolveIntactReturnCopyCount(item);

            return new YearlyArchiveMaterialTransaction
            {
                FilingFactId = item.FilingFactId,
                TransactionType = MaterialTransactionDomainValues.TypeReturn,
                BusinessNo = returnRecord.ReturnNo,
                SourceKind = MaterialTransactionDomainValues.SourceReturnItem,
                SourceId = item.Id,
                DedupKey = item.Id > 0
                    ? BuildReturnItemDedupKey(item.Id)
                    : $"Return:{returnRecord.ReturnNo}:Fact:{item.FilingFactId}",
                BeforeLifecycleStatus = FilingFactLifecycleStatus.Borrowed,
                AfterLifecycleStatus = lossCopyCount > 0 && intactCopyCount <= 0
                    ? FilingFactLifecycleStatus.Destroyed
                    : FilingFactLifecycleStatus.InArchive,
                Summary = lossCopyCount > 0
                    ? $"资料归还入库（含灭失）· 源出库 {returnRecord.SourceOutboundNo}"
                    : $"资料归还入库 · 源出库 {returnRecord.SourceOutboundNo}",
                Remark = ArchiveReturnDomainValues.BuildReturnCopyCountSummary(item),
                OperatorName = operatorName.Trim(),
                OperatedAt = operatedAt,
                CreatedAt = DateTime.Now
            };
        }

        public static MaterialTransactionTimelineRow MapTimelineRow(YearlyArchiveMaterialTransaction transaction)
        {
            return new MaterialTransactionTimelineRow
            {
                OperatedAt = transaction.OperatedAt,
                TransactionType = transaction.TransactionType,
                BusinessNo = transaction.BusinessNo,
                Summary = transaction.Summary,
                LocationChangeDisplay = BuildLocationChangeDisplay(
                    transaction.BeforeContainerCode,
                    transaction.AfterContainerCode,
                    transaction.BeforeStorageLocation,
                    transaction.AfterStorageLocation),
                LifecycleChangeDisplay = BuildLifecycleChangeDisplay(
                    transaction.BeforeLifecycleStatus,
                    transaction.AfterLifecycleStatus),
                OperatorName = transaction.OperatorName,
                Remark = transaction.Remark
            };
        }

        private static YearlyArchiveMaterialTransaction BuildOutboundSyncTransaction(
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundSyncEntry entry,
            YearlyArchiveOutboundItem item)
        {
            ResolveOutboundSyncPresentation(entry, item, out string afterLifecycle, out string summary);

            return new YearlyArchiveMaterialTransaction
            {
                FilingFactId = entry.FilingFactId,
                TransactionType = ResolveOutboundTransactionType(entry),
                BusinessNo = record.OutboundNo,
                SourceKind = MaterialTransactionDomainValues.SourceOutboundSyncEntry,
                SourceId = entry.Id,
                DedupKey = entry.Id > 0
                    ? BuildOutboundSyncDedupKey(entry.Id)
                    : $"OutboundSync:{record.OutboundNo}:Item:{item.Id}:{entry.EntryKind}:{entry.Phase}",
                AfterLifecycleStatus = afterLifecycle,
                Summary = summary,
                Remark = entry.Remark?.Trim() ?? string.Empty,
                OperatorName = entry.OperatedBy.Trim(),
                OperatedAt = entry.UpdatedAt ?? entry.CreatedAt,
                CreatedAt = DateTime.Now
            };
        }

        private static bool ShouldIncludeOutboundSyncEntry(YearlyArchiveOutboundSyncEntry entry)
        {
            if (string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReturned, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(entry.Phase, ArchiveOutboundDomainValues.SyncEntryPhaseCancelled, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(entry.Phase, ArchiveOutboundDomainValues.SyncEntryPhasePending, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReservation, StringComparison.Ordinal)
                && string.Equals(entry.Phase, ArchiveOutboundDomainValues.SyncEntryPhaseActive, StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(entry.Phase, ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed, StringComparison.Ordinal)
                || string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReturned, StringComparison.Ordinal);
        }

        private static string ResolveOutboundTransactionType(YearlyArchiveOutboundSyncEntry entry)
        {
            if (string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReturned, StringComparison.Ordinal))
            {
                return MaterialTransactionDomainValues.TypeReturn;
            }

            return MaterialTransactionDomainValues.TypeOutbound;
        }

        private static void ResolveOutboundSyncPresentation(
            YearlyArchiveOutboundSyncEntry entry,
            YearlyArchiveOutboundItem item,
            out string afterLifecycle,
            out string summary)
        {
            afterLifecycle = string.Empty;
            summary = string.Empty;

            if (string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReturned, StringComparison.Ordinal))
            {
                afterLifecycle = FilingFactLifecycleStatus.InArchive;
                summary = "资料归还入库";
                return;
            }

            if (string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalLedger, StringComparison.Ordinal))
            {
                bool electronicHardDiskDiskReturnOnly =
                    ArchiveOutboundReturnSupport.IsElectronicHardDiskWithdrawalDiskReturnOnly(item);

                afterLifecycle = electronicHardDiskDiskReturnOnly || !item.NeedReturn
                    ? FilingFactLifecycleStatus.Transferred
                    : FilingFactLifecycleStatus.Borrowed;
                summary = item.UsageMode switch
                {
                    ArchiveOutboundDomainValues.UsageModeWithdrawal when electronicHardDiskDiskReturnOnly && item.NeedReturn
                        => "资料出库办结 · 提档（资料不还，载体硬盘待归还）",
                    ArchiveOutboundDomainValues.UsageModeWithdrawal when item.NeedReturn
                        => "资料出库办结 · 提档借出",
                    ArchiveOutboundDomainValues.UsageModeWithdrawal
                        => "资料出库办结 · 提档（不需归还）",
                    _ => "资料出库办结"
                };
                return;
            }

            if (string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindCopyLedger, StringComparison.Ordinal))
            {
                afterLifecycle = FilingFactLifecycleStatus.InArchive;
                summary = "资料出库办结 · 复制借出";
                return;
            }

            if (string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindDuplicateLedger, StringComparison.Ordinal))
            {
                afterLifecycle = FilingFactLifecycleStatus.InArchive;
                summary = "资料出库办结 · 拷贝借出";
                return;
            }

            if (string.Equals(entry.EntryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReservation, StringComparison.Ordinal))
            {
                afterLifecycle = FilingFactLifecycleStatus.Borrowed;
                summary = "资料出库 · 提档预订确认";
            }
        }

        private static string BuildLocationChangeDisplay(
            string beforeContainer,
            string afterContainer,
            string beforeLocation,
            string afterLocation)
        {
            bool containerChanged = !string.IsNullOrWhiteSpace(afterContainer)
                && !string.Equals(beforeContainer?.Trim(), afterContainer.Trim(), StringComparison.OrdinalIgnoreCase);
            bool locationChanged = !string.IsNullOrWhiteSpace(afterLocation)
                && !string.Equals(beforeLocation?.Trim(), afterLocation.Trim(), StringComparison.OrdinalIgnoreCase);

            if (containerChanged && locationChanged)
            {
                return $"{beforeContainer} / {beforeLocation} → {afterContainer} / {afterLocation}";
            }

            if (containerChanged)
            {
                return $"{beforeContainer} → {afterContainer}";
            }

            if (locationChanged)
            {
                return $"{beforeLocation} → {afterLocation}";
            }

            return "—";
        }

        private static string BuildLifecycleChangeDisplay(string beforeStatus, string afterStatus)
        {
            if (string.IsNullOrWhiteSpace(beforeStatus) && string.IsNullOrWhiteSpace(afterStatus))
            {
                return "—";
            }

            if (string.IsNullOrWhiteSpace(beforeStatus))
            {
                return MaterialTransactionDomainValues.MapLifecycleStatusDisplay(afterStatus);
            }

            if (string.IsNullOrWhiteSpace(afterStatus)
                || string.Equals(beforeStatus, afterStatus, StringComparison.Ordinal))
            {
                return MaterialTransactionDomainValues.MapLifecycleStatusDisplay(beforeStatus);
            }

            return $"{MaterialTransactionDomainValues.MapLifecycleStatusDisplay(beforeStatus)} → {MaterialTransactionDomainValues.MapLifecycleStatusDisplay(afterStatus)}";
        }
    }
}
