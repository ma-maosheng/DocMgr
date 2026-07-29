using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.YearlyArchive;

public sealed partial class ArchiveInventoryRegisterService
{
    private async Task<List<YearlyArchiveInventoryRegisterItem>> BuildSimulatedItemsAsync(
        string mediaKind,
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
            int available = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(fact.ContentCount, snapshot);
            int lostCopyCount = lostByFactId[fact.Id];

            if (lostCopyCount <= 0)
            {
                throw new InvalidOperationException($"资料【{fact.ItemName}】丢失份数须大于 0。");
            }

            if (lostCopyCount > available)
            {
                throw new InvalidOperationException(
                    $"资料【{fact.ItemName}】当前库内份数为 {available}，丢失份数不可超过库内份数。");
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
                BeforeAvailableCopyCount = available,
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

        foreach (var item in record.Items.OrderBy(detail => detail.SortOrder))
        {
            var fact = facts.FirstOrDefault(f => f.Id == item.FilingFactId)
                ?? throw new InvalidOperationException($"立档事实【{item.FilingFactId}】不存在。");

            var snapshot = snapshots.GetValueOrDefault(fact.Id) ?? new SimulatedFilingFactCopyCountSnapshot();
            string beforeLifecycle = fact.LifecycleStatus?.Trim() ?? string.Empty;

            fact.InventoryLostCopyCount = Math.Max(0, fact.InventoryLostCopyCount) + Math.Max(0, item.LostCopyCount);

            int currentAfter = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                fact.ContentCount,
                snapshot.PendingReturnCopyCount,
                snapshot.NoReturnCopyCount,
                snapshot.LostCopyCount,
                fact.InventoryLostCopyCount);

            bool isScrap = string.Equals(
                record.RegisterKind?.Trim(),
                ArchiveInventoryRegisterDomainValues.KindScrap,
                StringComparison.Ordinal);

            string afterLifecycle = beforeLifecycle;
            if (currentAfter <= 0 && snapshot.PendingReturnCopyCount <= 0)
            {
                fact.LifecycleStatus = FilingFactLifecycleStatus.Destroyed;
                afterLifecycle = FilingFactLifecycleStatus.Destroyed;
                fact.BorrowHintLevel = FilingFactBorrowHintLevel.None;
                fact.BorrowHintText = string.Empty;
                fact.BorrowHintUpdatedAt = now;
                fact.LifecycleUpdatedAt = now;
                fact.LifecycleRemark = isScrap
                    ? $"盘库拟销登记 {record.RegisterNo}：库内份数已耗尽（无存档价值）"
                    : $"盘库登记 {record.RegisterNo}：库内份数已耗尽";
            }

            string summaryDetail = isScrap
                ? $"拟销 {item.LostCopyCount} 份"
                : $"盘库丢失 {item.LostCopyCount} 份";

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
        string dedupKey = item.Id > 0
            ? $"InventoryRegisterItem:{item.Id}:Completed"
            : $"InventoryRegisterItem:Draft:{record.Id}:Fact:{fact.Id}";

        return new YearlyArchiveMaterialTransaction
        {
            FilingFactId = fact.Id,
            TransactionType = MaterialTransactionDomainValues.TypeInventoryRegister,
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
