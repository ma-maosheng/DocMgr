using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还办结后，模拟介质档案盒占格同步。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        private async Task<IReadOnlyList<EmptiedArchiveBoxHint>> SyncSimulatedArchiveBoxSlotsAfterReturnAsync(
            YearlyArchiveReturnRecord record,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
            DateTime operatedAt)
        {
            var boxIds = record.Items
                .Where(item => string.Equals(
                    item.MediaKind,
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal))
                .Select(item => item.FilingFactId)
                .Where(id => id > 0)
                .Distinct()
                .Select(id => factsById.TryGetValue(id, out var fact) ? fact : null)
                .Where(fact => fact != null
                    && fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                    && fact.ContainerId > 0)
                .Select(fact => fact!.ContainerId)
                .Distinct()
                .ToList();

            if (boxIds.Count == 0)
            {
                return [];
            }

            return await _simulatedBoxSlotSyncService.SyncBoxesByIdsAsync(boxIds, operatedAt);
        }

        private async Task SyncElectronicArchiveBagSlotsAfterReturnAsync(
            YearlyArchiveReturnRecord record,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
            DateTime operatedAt)
        {
            var unitIds = record.Items
                .Where(item => string.Equals(
                    item.MediaKind,
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    StringComparison.Ordinal))
                .Select(item => item.FilingFactId)
                .Where(id => id > 0)
                .Distinct()
                .Select(id => factsById.TryGetValue(id, out var fact) ? fact : null)
                .Where(fact => fact != null
                    && fact.ContainerKind == ArchiveContainerKind.ElectronicBag
                    && fact.ContainerId > 0)
                .Select(fact => fact!.ContainerId)
                .Distinct()
                .ToList();

            if (unitIds.Count == 0)
            {
                return;
            }

            _ = await _electronicBagSlotSyncService.SyncUnitsByIdsAsync(unitIds, operatedAt);
        }

        /// <summary>
        /// 本单灭失明细涉及的模拟档案盒 Id（用于筛选“因灭失变空盒”的提示）。
        /// </summary>
        private static HashSet<int> ResolveLossRelatedSimulatedBoxIds(
            YearlyArchiveReturnRecord record,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById)
        {
            var boxIds = new HashSet<int>();
            foreach (var item in record.Items)
            {
                if (ArchiveReturnDomainValues.ResolveLossCopyCount(item) <= 0)
                {
                    continue;
                }

                if (!string.Equals(
                        item.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindSimulated,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!factsById.TryGetValue(item.FilingFactId, out var fact)
                    || fact.ContainerKind != ArchiveContainerKind.ArchiveBox
                    || fact.ContainerId <= 0)
                {
                    continue;
                }

                boxIds.Add(fact.ContainerId);
            }

            return boxIds;
        }

        private static string BuildCompleteSuccessMessage(
            YearlyArchiveReturnRecord record,
            IReadOnlyList<EmptiedArchiveBoxHint> emptiedByLoss)
        {
            string message = $"已办结入库，单据 {record.ReturnNo} 已收回入库。";
            if (emptiedByLoss.Count == 0)
            {
                return message;
            }

            string details = string.Join(
                "；",
                emptiedByLoss.Select(box =>
                {
                    string code = string.IsNullOrWhiteSpace(box.ArchiveSequenceNo)
                        ? "（无编号）"
                        : box.ArchiveSequenceNo.Trim();
                    string location = string.IsNullOrWhiteSpace(box.LastStorageLocation)
                        ? "原档口未知"
                        : $"原档口 {box.LastStorageLocation.Trim()}";
                    return $"{code}（{location}）";
                }));

            return message
                + $"\n\n以下档案盒因资料灭失已变为空盒，系统已释放档口占位：\n{details}\n\n"
                + "请资料管理员及时对空档案盒进行物理处置（取走、合并或注销）。";
        }
    }
}
