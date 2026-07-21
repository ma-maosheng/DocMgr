using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class SearchPoolItemRow : ViewModelBase
    {
        public SearchPoolItemRow(
            int resultSetItemId,
            int filingFactId,
            string formNo,
            string materialName,
            string itemName,
            string containerCode,
            string storageLocation,
            string currentStorageLocation,
            string lifecycleStatusDisplay,
            string borrowHintDisplay,
            string selectionScopeKind,
            string contentEntryKind,
            string contentEntryName,
            string contentEntryRelativePath,
            int? contentEntryId = null,
            string confidentialLevel = "",
            int requestedCopyCount = 1,
            bool isSimulatedMedia = false,
            int filedCopyCount = 1,
            int currentInArchiveCopyCount = 1,
            int lostCopyCount = 0)
        {
            ResultSetItemId = resultSetItemId;
            FilingFactId = filingFactId;
            FormNo = formNo;
            MaterialName = materialName;
            ItemName = itemName;
            ContainerCode = containerCode;
            StorageLocation = storageLocation;
            CurrentStorageLocation = currentStorageLocation;
            LifecycleStatusDisplay = lifecycleStatusDisplay;
            BorrowHintDisplay = borrowHintDisplay;
            SelectionScopeKind = selectionScopeKind;
            ContentEntryId = contentEntryId;
            ContentEntryKind = contentEntryKind;
            ContentEntryName = contentEntryName;
            ContentEntryRelativePath = contentEntryRelativePath;
            ConfidentialLevel = confidentialLevel;
            RequestedCopyCount = requestedCopyCount > 0 ? requestedCopyCount : 1;
            IsSimulatedMedia = isSimulatedMedia;
            FiledCopyCount = SimulatedInArchiveCopyCountSupport.ResolveFiledCopyCount(filedCopyCount);
            CurrentInArchiveCopyCount = Math.Max(0, currentInArchiveCopyCount);
            LostCopyCount = Math.Max(0, lostCopyCount);
        }

        public int ResultSetItemId { get; }

        public int FilingFactId { get; }

        public string FormNo { get; }

        public string MaterialName { get; }

        public string ItemName { get; }

        public string ContainerCode { get; }

        public string StorageLocation { get; }

        public string CurrentStorageLocation { get; }

        public string LifecycleStatusDisplay { get; }

        public string BorrowHintDisplay { get; }

        public string ConfidentialLevel { get; }

        /// <summary>保存结果集时写入的筛选份数。</summary>
        public int RequestedCopyCount { get; }

        public string RequestedCopyCountDisplay => $"{RequestedCopyCount} 份";

        public bool IsSimulatedMedia { get; }

        public int FiledCopyCount { get; }

        public int CurrentInArchiveCopyCount { get; }

        public int LostCopyCount { get; }

        public string CurrentInArchiveCopyCountDisplay =>
            SimulatedInArchiveCopyCountSupport.FormatCurrentVsFiled(CurrentInArchiveCopyCount, FiledCopyCount);

        public string StatusColumnDisplay => IsSimulatedMedia
            ? CurrentInArchiveCopyCountDisplay
            : LifecycleStatusDisplay;

        public string SelectionScopeKind { get; }

        public int? ContentEntryId { get; }

        public string ContentEntryKind { get; }

        public string ContentEntryName { get; }

        public string ContentEntryRelativePath { get; }

        public string SelectionScopeDisplay => ArchiveSearchPoolSupport.FormatScopeDisplay(
            SelectionScopeKind,
            ContentEntryKind,
            ContentEntryName,
            ContentEntryRelativePath);

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
