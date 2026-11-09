using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子介质检索结果：电子介质袋级展示行，展开后懒加载袋内资料子项。
    /// </summary>
    public sealed class FiledArchiveSearchElectronicBagGroupRow : ViewModelBase
    {
        private readonly IReadOnlyList<FiledArchiveSearchGroupHit> _itemGroupHits;
        private readonly RelayCommand<FiledArchiveSearchHitRow> _viewDetailCommand;
        private readonly Func<int, string?, Task<IReadOnlyList<MatchedContentEntryInfo>>>? _loadAllContentEntriesAsync;
        private readonly Action? _onSelectionChanged;
        private readonly Action? _onContainerSelectionChanged;
        private bool _itemGroupsLoaded;

        public FiledArchiveSearchElectronicBagGroupRow(
            FiledArchiveSearchElectronicBagGroupHit group,
            RelayCommand<FiledArchiveSearchHitRow> viewDetailCommand,
            Func<int, string?, Task<IReadOnlyList<MatchedContentEntryInfo>>>? loadAllContentEntriesAsync,
            Action? onSelectionChanged = null,
            Action? onContainerSelectionChanged = null)
        {
            _itemGroupHits = group.ItemGroups;
            _viewDetailCommand = viewDetailCommand;
            _loadAllContentEntriesAsync = loadAllContentEntriesAsync;
            _onSelectionChanged = onSelectionChanged;
            _onContainerSelectionChanged = onContainerSelectionChanged;

            ElectronicArchiveNo = group.ElectronicArchiveNo;
            ProjectName = group.ProjectName;
            Year = group.Year;
            StorageLocation = group.StorageLocation;
            CurrentStorageLocation = group.CurrentStorageLocation;
            StorageCarrierTypeDisplay = group.StorageCarrierTypeDisplay;
            LinkedMediumCodes = group.LinkedMediumCodes;
            ArchivedBy = group.ArchivedBy;
            ArchivedDateText = group.ArchivedDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            Remarks = group.Remarks;
            UnitLifecycleStatusDisplay = CirculationLedgerDisplayValues.MapContainerStatusDisplay(
                group.UnitLifecycleStatus);
            ItemCount = group.ItemCount;

            ToggleExpandCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
        }

        private bool _isSelected;

        /// <summary>勾选后可将本介质袋内全部可提档资料加入筛选池。</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    _onContainerSelectionChanged?.Invoke();
                }
            }
        }

        public string ElectronicArchiveNo { get; }

        public string ProjectName { get; }

        public string Year { get; }

        public string StorageLocation { get; }

        public string CurrentStorageLocation { get; }

        public string StorageCarrierTypeDisplay { get; }

        public string LinkedMediumCodes { get; }

        public string ArchivedBy { get; }

        public string ArchivedDateText { get; }

        public string Remarks { get; }

        public string UnitLifecycleStatusDisplay { get; }

        public int ItemCount { get; }

        public IReadOnlyList<FiledArchiveSearchGroupHit> ItemGroupHits => _itemGroupHits;

        public ObservableCollection<FiledArchiveSearchGroupRow> ItemGroups { get; } = new();

        public string SummaryText => $"袋内 {ItemCount} 条资料";

        public string ExpandToggleText => IsExpanded ? "收起资料子项" : "展开资料子项";

        private bool _isExpanded;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                {
                    if (value)
                    {
                        EnsureItemGroupsLoaded();
                    }

                    OnPropertyChanged(nameof(ExpandToggleText));
                }
            }
        }

        public RelayCommand ToggleExpandCommand { get; }

        /// <summary>
        /// 确保已创建袋内资料子项展示行（首次展开时加载）。
        /// </summary>
        public void EnsureItemGroupsLoaded()
        {
            if (_itemGroupsLoaded)
            {
                return;
            }

            _itemGroupsLoaded = true;
            foreach (var itemGroup in _itemGroupHits)
            {
                ItemGroups.Add(new FiledArchiveSearchGroupRow(
                    itemGroup,
                    _viewDetailCommand,
                    isElectronicSearch: true,
                    loadAllContentEntriesAsync: _loadAllContentEntriesAsync,
                    onSelectionChanged: _onSelectionChanged,
                    showContainerSummary: false));
            }
        }
    }
}
