using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 模拟介质待立档资料子项选择项。
    /// </summary>
    public sealed class SelectableSimulatedArchiveItemViewModel : ViewModelBase
    {
        private bool _isSelected;

        public int MediaItemId { get; init; }

        public int RecordId { get; init; }

        public string FormNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string MediaType { get; init; } = string.Empty;

        public string ItemType { get; init; } = string.Empty;

        public string ContentDesc { get; init; } = string.Empty;

        public int ContentCount { get; init; }

        public string Note { get; init; } = string.Empty;

        public bool CanSelect { get; init; } = true;

        public string ArchiveStatusText { get; init; } = string.Empty;

        public string ArchiveSequenceNo { get; init; } = string.Empty;

        public string ArchiveLocationCode { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
