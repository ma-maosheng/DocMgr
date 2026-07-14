using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    public partial class ArchiveFilingService
    {
        private static List<int> NormalizeElectronicSubmissionMediaItemIds(IEnumerable<int>? mediaItemIds)
        {
            return mediaItemIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList()
                ?? [];
        }

        private async Task<(List<int> MediaItemIds, List<YearlyArchiveRegisterMediaItem> MediaItems, List<YearlyArchiveRegisterMedia> MediaEntries)> ResolveElectronicSubmissionAsync(
            ElectronicArchiveSubmissionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<int> mediaItemIds = NormalizeElectronicSubmissionMediaItemIds(request.MediaItemIds);
            if (mediaItemIds.Count == 0)
            {
                List<int> mediaEntryIds = NormalizeElectronicSubmissionMediaEntryIds(request.MediaEntryIds);
                if (mediaEntryIds.Count > 0)
                {
                    var legacyEntries = await _archiveFilingRepository.GetElectronicMediaEntriesForArchivingAsync(mediaEntryIds);
                    mediaItemIds = legacyEntries
                        .SelectMany(entry => entry.Items)
                        .Select(item => item.Id)
                        .Distinct()
                        .ToList();
                }
            }

            var mediaItems = await LoadElectronicMediaItemsForArchivingAsync(mediaItemIds);
            var mediaEntries = mediaItems
                .Select(item => item.MediaEntry!)
                .DistinctBy(entry => entry.Id)
                .ToList();

            return (mediaItemIds, mediaItems, mediaEntries);
        }

        private async Task<List<YearlyArchiveRegisterMediaItem>> LoadElectronicMediaItemsForArchivingAsync(List<int> mediaItemIds)
        {
            if (mediaItemIds == null || mediaItemIds.Count == 0)
            {
                throw new ArgumentException("至少需要一个资料子项。", nameof(mediaItemIds));
            }

            var items = await _archiveFilingRepository.GetElectronicMediaItemsForArchivingAsync(mediaItemIds);
            if (items.Count != mediaItemIds.Distinct().Count())
            {
                throw new InvalidOperationException("One or more archive register media items were not found.");
            }

            if (items.Any(item => item.MediaEntry?.RegisterRecord == null))
            {
                throw new InvalidOperationException("资料子项缺少登记单关联信息。");
            }

            if (items.Any(item => !string.Equals(item.MediaEntry!.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("仅电子介质资料子项允许归入电子介质袋。");
            }

            var archivedItems = items
                .Where(item => item.ElectronicArchiveUnitMediaItemLinks.Any())
                .Select(item => item.MediaEntry!.RegisterRecord!.FormNo + "/" + item.ContentDesc)
                .ToList();

            if (archivedItems.Count > 0)
            {
                throw new InvalidOperationException($"所选资料明细中包含已立档内容，请刷新后重试：{string.Join("；", archivedItems)}");
            }

            return items;
        }

        private static string ResolveSubmissionMediumCode(ElectronicArchiveSubmissionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!string.IsNullOrWhiteSpace(request.ArchiveUnit.LinkedMediumCodes))
            {
                return request.ArchiveUnit.LinkedMediumCodes.Trim();
            }

            if (UsesOpticalDiscCarrier(request.SubmissionMode))
            {
                return request.ArchiveUnit.ElectronicArchiveNo?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        private async Task ValidateCopySubmissionMediumCapacityAsync(
            ElectronicArchiveSubmissionRequest request,
            IReadOnlyCollection<YearlyArchiveRegisterMediaItem> mediaItems,
            YearlyElectronicArchiveUnit? appendTargetUnit = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(mediaItems);

            if (!ArchiveFilingBusinessRules.IsCopySubmissionMode(request.SubmissionMode))
            {
                return;
            }

            if (appendTargetUnit == null && request.ExistingElectronicArchiveUnitId is int unitId and > 0)
            {
                appendTargetUnit = await _archiveFilingRepository.GetElectronicArchiveUnitWithDetailsAsync(unitId);
                if (appendTargetUnit == null)
                {
                    throw new InvalidOperationException($"未找到指定电子立档单元：{unitId}");
                }
            }

            string mediumCode = ResolveCopyValidationMediumCode(request, appendTargetUnit);
            if (string.IsNullOrWhiteSpace(mediumCode) || mediumCode.StartsWith('待'))
            {
                throw new InvalidOperationException("拷贝型硬盘立档请先确定目标硬盘后再提交。");
            }

            decimal pendingMb = mediaItems.Sum(ElectronicMediaItemSupport.ResolveMediaItemDataSizeMb);
            if (pendingMb <= 0)
            {
                return;
            }

            decimal totalMb = await ResolveMediumTotalCapacityMbAsync(mediumCode);
            if (totalMb <= 0)
            {
                throw new InvalidOperationException(
                    $"无法获取目标介质 [{mediumCode}] 的容量信息，请先在台账中登记容量后再提交。");
            }

            var existingLinks = await _archiveFilingRepository
                .GetElectronicArchiveUnitMediaItemLinksByMediumCodeAsync(mediumCode);
            decimal usedMb = existingLinks.Sum(link => link.DataSizeMb);
            decimal availableMb = totalMb - usedMb;

            if (availableMb < pendingMb)
            {
                throw new InvalidOperationException(
                    $"目标介质可用容量不足：可用 {ElectronicMediaCapacitySupport.FormatCapacityMb(Math.Max(0, availableMb))}，"
                    + $"本次资料数据量 {ElectronicMediaCapacitySupport.FormatCapacityMb(pendingMb)}。"
                    + $"（介质 [{mediumCode}] 总容量 {ElectronicMediaCapacitySupport.FormatCapacityMb(totalMb)}，"
                    + $"已占用 {ElectronicMediaCapacitySupport.FormatCapacityMb(usedMb)}）");
            }
        }

        private static string ResolveCopyValidationMediumCode(
            ElectronicArchiveSubmissionRequest request,
            YearlyElectronicArchiveUnit? appendTargetUnit)
        {
            if (appendTargetUnit != null
                && !string.IsNullOrWhiteSpace(appendTargetUnit.LinkedMediumCodes))
            {
                return ParseMediumCodes(appendTargetUnit.LinkedMediumCodes).FirstOrDefault() ?? string.Empty;
            }

            return ResolveSubmissionMediumCode(request);
        }

        private async Task<decimal> ResolveMediumTotalCapacityMbAsync(string mediumCode)
        {
            var hardDisk = await _archiveFilingRepository.GetHardDiskMediumByDiskCodeWithLedgerAsync(mediumCode);
            return hardDisk == null ? 0 : ElectronicMediaCapacitySupport.ParseCapacityTextToMb(hardDisk.Capacity);
        }
    }
}
