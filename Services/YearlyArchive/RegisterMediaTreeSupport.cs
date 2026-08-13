using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 登记申请介质树与实体之间的映射辅助方法。
    /// </summary>
    public static class RegisterMediaTreeSupport
    {
        /// <summary>
        /// 将资料子项 ViewModel 映射为登记介质明细实体。
        /// </summary>
        public static YearlyArchiveRegisterMediaItem MapMediaItemEntity(MediaItemViewModel item, bool isElectronic, int index)
        {
            var entity = new YearlyArchiveRegisterMediaItem
            {
                ItemType = item.ItemType,
                ContentDesc = item.ContentDesc,
                ContentCount = isElectronic ? 1 : item.ContentCount,
                StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(item.StoragePath),
                Note = item.Note,
                ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(item.ConfidentialLevel)
            };

            if (!isElectronic)
            {
                return entity;
            }

            entity.ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
            {
                MaterialCategory = item.MaterialCategory?.Trim() ?? string.Empty,
                SubCategory = item.SubCategory?.Trim() ?? string.Empty,
                DataOrganizationForm = item.DataOrganizationForm?.Trim() ?? string.Empty,
                DataSizeMb = item.DataSizeMb,
                Entries = item.ContentEntries
                    .Select((entry, entryIndex) => new YearlyArchiveRegisterElectronicMediaItemEntry
                    {
                        EntryKind = string.IsNullOrWhiteSpace(entry.EntryKind)
                            ? ElectronicMediaItemSupport.ResolveEntryKind(item.DataOrganizationForm)
                            : entry.EntryKind,
                        EntryName = entry.EntryName?.Trim() ?? string.Empty,
                        RelativePath = entry.RelativePath?.Trim() ?? string.Empty,
                        SizeMb = entry.SizeMb,
                        CreatedAt = entry.CreatedAt,
                        ModifiedAt = entry.ModifiedAt,
                        SortOrder = (entryIndex + 1) * 10
                    })
                    .ToList()
            };

            return entity;
        }

        /// <summary>
        /// 将登记介质实体映射为介质组 ViewModel。
        /// </summary>
        public static MediaEntryViewModel CreateMediaEntryViewModel(
            YearlyArchiveRegisterMedia media,
            Func<string?, string> resolveConfidentialLevel,
            Action<MediaItemViewModel>? configureElectronicMediaItem = null)
        {
            var vm = new MediaEntryViewModel
            {
                MediaKind = media.MediaKind,
                MediaType = media.MediaType,
                Disposition = media.Disposition,
                IsBorrowedHardDisk = media.IsBorrowedHardDisk,
                BorrowedHardDiskCode = media.BorrowedHardDiskCode
            };

            if (media.Items == null)
            {
                return vm;
            }

            foreach (var item in media.Items)
            {
                var itemVm = new MediaItemViewModel
                {
                    ItemType = item.ItemType,
                    ContentDesc = item.ContentDesc,
                    ContentCount = item.ContentCount > 0 ? item.ContentCount : 1,
                    StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(item.StoragePath),
                    Note = item.Note,
                    ConfidentialLevel = resolveConfidentialLevel(item.ConfidentialLevel)
                };

                if (item.ElectronicDetail != null)
                {
                    itemVm.MaterialCategory = item.ElectronicDetail.MaterialCategory;
                    itemVm.SubCategory = item.ElectronicDetail.SubCategory;
                    itemVm.DataOrganizationForm = item.ElectronicDetail.DataOrganizationForm;
                    itemVm.DataSizeMb = item.ElectronicDetail.DataSizeMb;

                    foreach (var entry in item.ElectronicDetail.Entries.OrderBy(e => e.SortOrder))
                    {
                        itemVm.ContentEntries.Add(new ElectronicMediaItemEntryViewModel
                        {
                            EntryKind = entry.EntryKind,
                            EntryName = entry.EntryName,
                            RelativePath = entry.RelativePath,
                            SizeMb = entry.SizeMb,
                            CreatedAt = entry.CreatedAt,
                            ModifiedAt = entry.ModifiedAt
                        });
                    }
                }

                configureElectronicMediaItem?.Invoke(itemVm);
                itemVm.RefreshContentScanSummary();
                vm.Items.Add(itemVm);
            }

            return vm;
        }

        /// <summary>
        /// 由电子介质 ViewModel 集合构建登记介质实体列表。
        /// </summary>
        public static List<YearlyArchiveRegisterMedia> BuildElectronicMediaEntities(
            IEnumerable<MediaEntryViewModel> entries,
            Func<string> resolveSelectedMediaType,
            Func<string> resolveSelectedDisposition)
        {
            return entries
                .Where(IsDataElectronic)
                .Select(m => new YearlyArchiveRegisterMedia
                {
                    MediaKind = m.MediaKind,
                    MediaType = resolveSelectedMediaType(),
                    MediaCount = 1,
                    Disposition = resolveSelectedDisposition(),
                    IsBorrowedHardDisk = m.IsRetainedHardDiskScenario && m.IsBorrowedHardDisk,
                    BorrowedHardDiskCode = m.IsRetainedHardDiskScenario && m.IsBorrowedHardDisk
                        ? (m.BorrowedHardDiskCode?.Trim() ?? string.Empty)
                        : string.Empty,
                    Items = m.Items.Select((item, index) => MapMediaItemEntity(item, isElectronic: true, index)).ToList()
                })
                .ToList();
        }

        /// <summary>
        /// 判断登记介质实体是否为数据电子介质（排除历史证明材料行）。
        /// </summary>
        public static bool IsElectronicMediaEntity(YearlyArchiveRegisterMedia? media) =>
            media != null
            && string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
            && !IsProofMediaEntity(media);

        /// <summary>
        /// 判断介质组 ViewModel 是否为数据电子介质。
        /// </summary>
        public static bool IsDataElectronic(MediaEntryViewModel? media) =>
            media != null
            && string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
            && !IsProofMedia(media);

        private static bool IsProofMedia(MediaEntryViewModel? media) =>
            media?.Items.Any(i => string.Equals(i.ItemType, ArchiveRegisterDomainValues.ItemTypeProof, StringComparison.Ordinal)) == true;

        private static bool IsProofMediaEntity(YearlyArchiveRegisterMedia? media) =>
            media?.Items?.Any(i => string.Equals(i.ItemType, ArchiveRegisterDomainValues.ItemTypeProof, StringComparison.Ordinal)) == true;
    }
}
