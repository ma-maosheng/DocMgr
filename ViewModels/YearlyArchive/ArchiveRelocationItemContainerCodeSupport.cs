using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>迁档资料清单中「迁档前/后编号」——物理位置编码展示约定。</summary>
    internal static class ArchiveRelocationItemContainerCodeSupport
    {
        public const string UnspecifiedStorageLocation = "—";

        public static string NormalizeStorageLocation(string? storageLocation)
        {
            string trimmed = storageLocation?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? UnspecifiedStorageLocation : trimmed;
        }

        public static ArchiveRelocationItemSummary WithStorageLocations(
            ArchiveRelocationItemSummary item,
            string beforeStorageLocation,
            string afterStorageLocation)
        {
            return new ArchiveRelocationItemSummary
            {
                MediaItemId = item.MediaItemId,
                FormNo = item.FormNo,
                Year = item.Year,
                ProjectName = item.ProjectName,
                MaterialName = item.MaterialName,
                ItemName = item.ItemName,
                BeforeStorageLocation = beforeStorageLocation,
                AfterStorageLocation = afterStorageLocation
            };
        }
    }
}
