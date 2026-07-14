using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveOutboundService
    {
        /// <summary>
        /// 校验「拷贝 + 库内空盘」场景下，所选硬盘可用容量是否满足拟拷贝资料数据量。
        /// </summary>
        private async Task<List<string>> CollectCopyDiskCapacityErrorsAsync(
            IReadOnlyList<YearlyArchiveOutboundItem> items)
        {
            var errors = new List<string>();
            var copyDiskGroups = items
                .Where(ArchiveOutboundSharedDiskSettingsSupport.UsesInStockBlankDisk)
                .GroupBy(item => item.RequisitionedMediumId!.Value)
                .ToList();

            if (copyDiskGroups.Count == 0)
            {
                return errors;
            }

            var filingFactIds = copyDiskGroups
                .SelectMany(group => group)
                .Select(item => item.FilingFactId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var filingFactsById = filingFactIds.Count == 0
                ? new Dictionary<int, YearlyArchiveFilingFact>()
                : await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(filingFactIds);

            foreach (var group in copyDiskGroups)
            {
                var groupItems = group.ToList();
                string diskLabel = ArchiveOutboundCopyDiskCapacitySupport.FormatDiskLabel(
                    groupItems[0].RequisitionedDiskCode);

                decimal pendingMb = groupItems.Sum(item =>
                    ArchiveOutboundCopyDiskCapacitySupport.ResolveCopyDataSizeMb(item, filingFactsById));

                if (pendingMb <= 0)
                {
                    errors.Add(ArchiveOutboundCopyDiskCapacitySupport.BuildMissingCopyDataSizeError(diskLabel));
                    continue;
                }

                HardDiskMedium? medium = await _hardDiskMediaRepository
                    .GetActiveMediumWithLedgerByIdAsync(group.Key);
                if (medium == null)
                {
                    errors.Add(ArchiveOutboundCopyDiskCapacitySupport.BuildMissingMediumError(diskLabel));
                    continue;
                }

                decimal totalMb = ArchiveOutboundCopyDiskCapacitySupport.ResolveTotalCapacityMb(medium);
                if (totalMb <= 0)
                {
                    errors.Add(ArchiveOutboundCopyDiskCapacitySupport.BuildMissingCapacityRegistrationError(diskLabel));
                    continue;
                }

                string diskCode = medium.DiskCode?.Trim() ?? string.Empty;
                decimal usedMb = string.IsNullOrWhiteSpace(diskCode)
                    ? 0m
                    : await _outboundRepository.GetUsedDataSizeMbByHardDiskCodeAsync(diskCode);
                decimal availableMb = totalMb - usedMb;

                if (availableMb < pendingMb)
                {
                    errors.Add(ArchiveOutboundCopyDiskCapacitySupport.BuildInsufficientCapacityError(
                        diskLabel,
                        availableMb,
                        pendingMb,
                        totalMb,
                        usedMb));
                }
            }

            return errors;
        }
    }
}
