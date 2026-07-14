using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    internal static class ElectronicArchiveMediaItemCloneSupport
    {
        internal static YearlyArchiveRegisterMediaItem CloneForBackup(YearlyArchiveRegisterMediaItem source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var clone = new YearlyArchiveRegisterMediaItem
            {
                YearlyArchiveRegisterMediaId = source.YearlyArchiveRegisterMediaId,
                ItemType = source.ItemType,
                ContentDesc = source.ContentDesc,
                ContentCount = source.ContentCount,
                StoragePath = source.StoragePath,
                Note = source.Note,
                ConfidentialLevel = source.ConfidentialLevel
            };

            if (source.ElectronicDetail != null)
            {
                clone.ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
                {
                    MaterialCategory = source.ElectronicDetail.MaterialCategory,
                    SubCategory = source.ElectronicDetail.SubCategory,
                    DataOrganizationForm = source.ElectronicDetail.DataOrganizationForm,
                    DataSizeMb = source.ElectronicDetail.DataSizeMb,
                    Entries = source.ElectronicDetail.Entries
                        .Select(entry => new YearlyArchiveRegisterElectronicMediaItemEntry
                        {
                            EntryKind = entry.EntryKind,
                            EntryName = entry.EntryName,
                            RelativePath = entry.RelativePath,
                            SizeMb = entry.SizeMb,
                            SortOrder = entry.SortOrder
                        })
                        .ToList()
                };
            }

            return clone;
        }
    }
}
