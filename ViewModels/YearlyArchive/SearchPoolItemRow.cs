using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class SearchPoolItemRow : ViewModelBase
    {
        private readonly string _matchedContentEntrySummaryFromHit;

        public SearchPoolItemRow(
            YearlyArchiveSearchResultSetItem item,
            FiledArchiveSearchHit hit,
            string currentStorageLocation)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(hit);

            ResultSetItemId = item.Id;
            FilingFactId = item.FilingFactId;
            FormNo = item.FormNo;
            MaterialName = item.MaterialName;
            ItemName = item.ItemName;
            ContainerCode = item.ContainerCode;
            StorageLocation = item.StorageLocation;
            CurrentStorageLocation = currentStorageLocation;
            SelectionScopeKind = item.SelectionScopeKind;
            ContentEntryId = item.ContentEntryId;
            ContentEntryKind = item.ContentEntryKind;
            ContentEntryName = item.ContentEntryName;
            ContentEntryRelativePath = item.ContentEntryRelativePath;
            ConfidentialLevel = hit.ConfidentialLevel;
            RequestedCopyCount = item.RequestedCopyCount > 0 ? item.RequestedCopyCount : 1;

            ProjectName = hit.ProjectName;
            ProjectYear = hit.ProjectYear;
            ArchivePurpose = hit.ArchivePurpose;
            StorageCarrierTypeDisplay = hit.StorageCarrierTypeDisplay;
            FilingDirectoryDisplay = hit.FilingStoragePath?.Trim() ?? string.Empty;
            MaterialCategory = hit.MaterialCategory;
            SubCategory = hit.SubCategory;
            DataOrganizationForm = hit.DataOrganizationForm;
            DataSizeDisplay = hit.DataSizeDisplay;
            LifecycleStatusDisplay = hit.LifecycleStatusDisplay;
            BorrowHintDisplay = hit.BorrowHintDisplay;
            _matchedContentEntrySummaryFromHit = hit.MatchedContentEntrySummary;

            IsSimulatedMedia = string.Equals(
                hit.MediaKind,
                ArchiveRegisterDomainValues.MediaKindSimulated,
                StringComparison.Ordinal);
        }

        public int ResultSetItemId { get; }

        public int FilingFactId { get; }

        public string FormNo { get; }

        public string MaterialName { get; }

        public string ProjectName { get; }

        public string ProjectYear { get; }

        public string ArchivePurpose { get; }

        public string ItemName { get; }

        public string ContainerCode { get; }

        public string StorageLocation { get; }

        public string CurrentStorageLocation { get; }

        public string LifecycleStatusDisplay { get; }

        public string BorrowHintDisplay { get; }

        public string ConfidentialLevel { get; }

        /// <summary>保存结果集时写入的筛选份数。</summary>
        public int RequestedCopyCount { get; }

        public bool IsSimulatedMedia { get; }

        public string StorageCarrierTypeDisplay { get; }

        public string FilingDirectoryDisplay { get; }

        public string MaterialCategory { get; }

        public string SubCategory { get; }

        public string DataOrganizationForm { get; }

        public string DataSizeDisplay { get; }

        public string SelectionScopeKind { get; }

        public int? ContentEntryId { get; }

        public string ContentEntryKind { get; }

        public string ContentEntryName { get; }

        public string ContentEntryRelativePath { get; }

        public bool IsWholeMediaItem => string.Equals(
            SelectionScopeKind,
            ArchiveSearchSelectionScopeKind.WholeMediaItem,
            StringComparison.Ordinal);

        public bool IsContentEntry => string.Equals(
            SelectionScopeKind,
            ArchiveSearchSelectionScopeKind.ContentEntry,
            StringComparison.Ordinal);

        public bool IsCopyCountEditable => ArchiveSearchPoolCopyCountSupport.IsEditableSimulatedWholeItem(
            IsSimulatedMedia
                ? ArchiveRegisterDomainValues.MediaKindSimulated
                : ArchiveRegisterDomainValues.MediaKindElectronic,
            new ArchiveSearchPoolSelection
            {
                FilingFactId = FilingFactId,
                SelectionScopeKind = SelectionScopeKind,
                ContentEntryId = ContentEntryId,
                RequestedCopyCount = RequestedCopyCount
            });

        public string SelectionScopeDisplay => IsWholeMediaItem
            ? ArchiveSearchPoolSupport.FormatScopeDisplay(
                SelectionScopeKind,
                string.Empty,
                string.Empty,
                string.Empty)
            : IsContentEntry
                ? "部分子项"
                : ArchiveSearchPoolSupport.FormatScopeDisplay(
                    SelectionScopeKind,
                    ContentEntryKind,
                    ContentEntryName,
                    ContentEntryRelativePath);

        public string MatchedContentEntrySummary => IsWholeMediaItem
            ? _matchedContentEntrySummaryFromHit
            : IsContentEntry
                ? ArchiveSearchPoolSupport.FormatScopeDisplay(
                    SelectionScopeKind,
                    ContentEntryKind,
                    ContentEntryName,
                    ContentEntryRelativePath)
                : SelectionScopeDisplay;

    }
}
