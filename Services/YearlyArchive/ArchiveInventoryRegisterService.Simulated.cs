using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.YearlyArchive;

public sealed partial class ArchiveInventoryRegisterService
{
    private async Task<List<YearlyArchiveInventoryRegisterItem>> BuildSimulatedItemsAsync(
        string mediaKind,
        string registerKind,
        IReadOnlyList<ArchiveInventoryRegisterItemDraft> drafts,
        int? excludeRecordId,
        DateTime now)
    {
        if (drafts == null || drafts.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一条模拟资料明细。");
        }

        List<int> factIds = drafts.Select(item => item.FilingFactId).Where(id => id > 0).Distinct().ToList();
        if (factIds.Count == 0 || factIds.Count != drafts.Count)
        {
            throw new InvalidOperationException("所选立档事实无效或存在重复。");
        }

        var facts = await _repository.GetFactsWithDetailsAsync(factIds);
        if (facts.Count != factIds.Count)
        {
            throw new InvalidOperationException("部分所选立档事实不存在。");
        }

        var snapshots = await _outboundRepository.GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(factIds);
        var lostByFactId = drafts.ToDictionary(item => item.FilingFactId, item => Math.Max(0, item.LostCopyCount));

        List<int> projectIds = facts
            .Where(fact => fact.ProjectId.HasValue && fact.ProjectId.Value > 0)
            .Select(fact => fact.ProjectId!.Value)
            .Distinct()
            .ToList();
        Dictionary<int, string> projectYearById = await _repository.GetProjectImplementYearsByIdsAsync(projectIds);

        int sort = 1;
        var items = new List<YearlyArchiveInventoryRegisterItem>();

        foreach (var fact in facts.OrderBy(item => item.ContainerCode, StringComparer.Ordinal).ThenBy(item => item.ItemName, StringComparer.Ordinal))
        {
            if (!string.Equals(fact.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"立档事实【{fact.FilingFactNo}】不是模拟介质。");
            }

            if (!string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"资料【{fact.ItemName}】当前不在库，不可盘库登记。");
            }

            if (await _repository.ExistsActiveArchiveInventoryForFilingFactAsync(fact.Id, excludeRecordId))
            {
                throw new InvalidOperationException($"资料【{fact.ItemName}】已存在未办结的盘库登记明细。");
            }

            var snapshot = snapshots.GetValueOrDefault(fact.Id) ?? new SimulatedFilingFactCopyCountSnapshot();
            int currentInArchive = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(fact.ContentCount, snapshot);
            // 盘失可登记量不含已拟销；拟销须等于当前库内（拟销不扣减库内）。
            int available = SimulatedInArchiveCopyCountSupport.ResolveAvailableCopyCount(
                currentInArchive,
                snapshot.InventoryScrapCopyCount);
            int lostCopyCount = lostByFactId[fact.Id];
            bool isScrap = ArchiveInventoryRegisterDomainValues.IsScrapRegisterKind(registerKind);

            if (isScrap && snapshot.PendingReturnCopyCount > 0)
            {
                throw new InvalidOperationException(
                    $"资料【{fact.ItemName}】存在出库待还份数 {snapshot.PendingReturnCopyCount}，不可拟销登记。");
            }

            if (lostCopyCount <= 0)
            {
                throw new InvalidOperationException($"资料【{fact.ItemName}】登记份数须大于 0。");
            }

            if (isScrap)
            {
                if (currentInArchive <= 0)
                {
                    throw new InvalidOperationException($"资料【{fact.ItemName}】当前库内份数为 0，不可拟销登记。");
                }

                if (lostCopyCount != currentInArchive)
                {
                    throw new InvalidOperationException(
                        $"资料【{fact.ItemName}】拟销份数须等于当前库内份数 {currentInArchive}（不允许部分拟销）。");
                }
            }
            else if (lostCopyCount > available)
            {
                throw new InvalidOperationException(
                    $"资料【{fact.ItemName}】当前可盘库登记份数为 {available}（库内 {currentInArchive}，已拟销 {snapshot.InventoryScrapCopyCount}），登记份数不可超过可登记份数。");
            }

            items.Add(new YearlyArchiveInventoryRegisterItem
            {
                SortOrder = sort++,
                FilingFactId = fact.Id,
                MediaItemId = fact.MediaItemId,
                ContainerId = fact.ContainerId,
                ContainerCode = !string.IsNullOrWhiteSpace(fact.CurrentContainerCode)
                    ? fact.CurrentContainerCode.Trim()
                    : fact.ContainerCode?.Trim() ?? string.Empty,
                BeforeStorageLocation = !string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? fact.CurrentStorageLocation.Trim()
                    : fact.StorageLocation?.Trim() ?? string.Empty,
                LostCopyCount = lostCopyCount,
                BeforeAvailableCopyCount = isScrap ? currentInArchive : available,
                ProjectName = fact.ProjectName?.Trim() ?? string.Empty,
                Year = fact.ProjectId.HasValue
                    ? projectYearById.GetValueOrDefault(fact.ProjectId.Value, string.Empty).Trim()
                    : string.Empty,
                MaterialName = fact.MaterialName?.Trim() ?? string.Empty,
                ItemName = fact.ItemName?.Trim() ?? string.Empty,
                CreatedAt = now,
            });
        }

        return items;
    }

    private async Task CompleteSimulatedAsync(
        YearlyArchiveInventoryRegisterRecord record,
        string operatorName,
        DateTime now)
    {
        var factIds = record.Items.Select(item => item.FilingFactId).Where(id => id > 0).Distinct().ToList();
        var facts = await _repository.GetFactsWithDetailsAsync(factIds);
        var snapshots = await _outboundRepository.GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(factIds);
        bool isScrap = ArchiveInventoryRegisterDomainValues.IsScrapRegisterKind(record.RegisterKind);

        foreach (var item in record.Items.OrderBy(detail => detail.SortOrder))
        {
            var fact = facts.FirstOrDefault(f => f.Id == item.FilingFactId)
                ?? throw new InvalidOperationException($"立档事实【{item.FilingFactId}】不存在。");

            var snapshot = snapshots.GetValueOrDefault(fact.Id) ?? new SimulatedFilingFactCopyCountSnapshot();
            string beforeLifecycle = fact.LifecycleStatus?.Trim() ?? string.Empty;
            int registerCount = Math.Max(0, item.LostCopyCount);

            if (isScrap)
            {
                if (snapshot.PendingReturnCopyCount > 0)
                {
                    throw new InvalidOperationException(
                        $"资料【{fact.ItemName}】存在出库待还份数 {snapshot.PendingReturnCopyCount}，不可拟销登记。");
                }

                int currentBefore = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                    fact.ContentCount,
                    snapshot);
                if (currentBefore <= 0 || registerCount != currentBefore)
                {
                    throw new InvalidOperationException(
                        $"资料【{fact.ItemName}】拟销份数须等于当前库内份数 {currentBefore}（不允许部分拟销）。");
                }
            }

            // 盘失与拟销写入独立累计字段，避免语义与份数互相覆盖。
            if (isScrap)
            {
                fact.InventoryScrapCopyCount = Math.Max(0, fact.InventoryScrapCopyCount) + registerCount;
            }
            else
            {
                fact.InventoryLostCopyCount = Math.Max(0, fact.InventoryLostCopyCount) + registerCount;
            }

            int currentAfter = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                fact.ContentCount,
                snapshot.PendingReturnCopyCount,
                snapshot.NoReturnCopyCount,
                snapshot.LostCopyCount,
                fact.InventoryLostCopyCount,
                fact.InventoryScrapCopyCount);
            int availableAfter = SimulatedInArchiveCopyCountSupport.ResolveAvailableCopyCount(
                currentAfter,
                fact.InventoryScrapCopyCount);

            // 办结后再次核验：盘失+拟销合计不可超过「立档−待还−不还−出库灭失」。
            int occupiedWithoutInventory = Math.Max(0, snapshot.PendingReturnCopyCount)
                + Math.Max(0, snapshot.NoReturnCopyCount)
                + Math.Max(0, snapshot.LostCopyCount);
            int filed = SimulatedInArchiveCopyCountSupport.ResolveFiledCopyCount(fact.ContentCount);
            int maxInventoryTotal = Math.Max(0, filed - occupiedWithoutInventory);
            int inventoryTotal = Math.Max(0, fact.InventoryLostCopyCount) + Math.Max(0, fact.InventoryScrapCopyCount);
            if (inventoryTotal > maxInventoryTotal)
            {
                throw new InvalidOperationException(
                    $"资料【{fact.ItemName}】盘失份数({fact.InventoryLostCopyCount})与拟销份数({fact.InventoryScrapCopyCount})合计已超过可登记上限 {maxInventoryTotal}。");
            }

            string afterLifecycle = beforeLifecycle;
            if (availableAfter <= 0 && snapshot.PendingReturnCopyCount <= 0)
            {
                fact.LifecycleStatus = FilingFactLifecycleStatus.Destroyed;
                afterLifecycle = FilingFactLifecycleStatus.Destroyed;
                fact.BorrowHintLevel = FilingFactBorrowHintLevel.None;
                fact.BorrowHintText = string.Empty;
                fact.BorrowHintUpdatedAt = now;
                fact.LifecycleUpdatedAt = now;
                fact.LifecycleRemark = isScrap
                    ? $"盘库拟销登记 {record.RegisterNo}：可借库内已耗尽（无存档价值）"
                    : $"盘库盘失登记 {record.RegisterNo}：库内份数已耗尽";
            }

            string summaryDetail = isScrap
                ? $"拟销 {registerCount} 份"
                : $"盘库丢失 {registerCount} 份";

            _repository.AddMaterialTransaction(BuildInventoryMaterialTransaction(
                record,
                item,
                fact,
                beforeLifecycle,
                afterLifecycle,
                operatorName,
                now,
                summaryDetail));
        }
    }

    private static YearlyArchiveMaterialTransaction BuildInventoryMaterialTransaction(
        YearlyArchiveInventoryRegisterRecord record,
        YearlyArchiveInventoryRegisterItem item,
        YearlyArchiveFilingFact fact,
        string beforeLifecycle,
        string afterLifecycle,
        string operatorName,
        DateTime operatedAt,
        string summaryDetail)
    {
        // 电子轨一块介质可关联多条立档事实，DedupKey 必须含 FactId，否则唯一索引冲突。
        string dedupKey = item.Id > 0
            ? $"InventoryRegisterItem:{item.Id}:Fact:{fact.Id}:Completed"
            : $"InventoryRegisterItem:Draft:{record.Id}:Fact:{fact.Id}";

        return new YearlyArchiveMaterialTransaction
        {
            FilingFactId = fact.Id,
            TransactionType = ArchiveInventoryRegisterDomainValues.ResolveMaterialTransactionType(record.RegisterKind),
            BusinessNo = record.RegisterNo,
            SourceKind = MaterialTransactionDomainValues.SourceInventoryItem,
            SourceId = item.Id,
            DedupKey = dedupKey,
            BeforeLifecycleStatus = beforeLifecycle,
            AfterLifecycleStatus = afterLifecycle,
            BeforeContainerCode = fact.CurrentContainerCode?.Trim() ?? fact.ContainerCode?.Trim() ?? string.Empty,
            AfterContainerCode = fact.CurrentContainerCode?.Trim() ?? fact.ContainerCode?.Trim() ?? string.Empty,
            BeforeStorageLocation = fact.CurrentStorageLocation?.Trim() ?? fact.StorageLocation?.Trim() ?? string.Empty,
            AfterStorageLocation = fact.CurrentStorageLocation?.Trim() ?? fact.StorageLocation?.Trim() ?? string.Empty,
            Summary = $"盘库登记 · {summaryDetail} · {fact.ContainerCode}",
            Remark = record.Remark?.Trim() ?? string.Empty,
            OperatorName = operatorName,
            OperatedAt = operatedAt,
            CreatedAt = DateTime.Now,
        };
    }
}
