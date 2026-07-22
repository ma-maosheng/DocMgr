using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 模拟介质检索结果：档案盒级展示行，展开后懒加载盒内资料子项分组。
    /// </summary>
    public sealed class FiledArchiveSearchBoxGroupRow : ViewModelBase
    {
        private readonly IReadOnlyList<FiledArchiveSearchGroupHit> _itemGroupHits;
        private readonly RelayCommand<FiledArchiveSearchHitRow> _viewDetailCommand;
        private readonly Action? _onSelectionChanged;
        private bool _itemGroupsLoaded;

        public FiledArchiveSearchBoxGroupRow(
            FiledArchiveSearchBoxGroupHit group,
            RelayCommand<FiledArchiveSearchHitRow> viewDetailCommand,
            Action? onSelectionChanged = null)
        {
            _itemGroupHits = group.ItemGroups;
            _viewDetailCommand = viewDetailCommand;
            _onSelectionChanged = onSelectionChanged;

            ArchiveSequenceNo = group.ArchiveSequenceNo;
            ProjectName = group.ProjectName;
            Year = group.Year;
            StorageLocation = group.StorageLocation;
            CurrentStorageLocation = group.CurrentStorageLocation;
            Specifications = group.Specifications;
            PlacementModeDisplay = FormatPlacementMode(group.PlacementMode);
            ArchivedBy = group.ArchivedBy;
            ArchivedDateText = group.ArchivedDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            Remarks = group.Remarks;
            ContainerLifecycleStatusDisplay = CirculationLedgerDisplayValues.MapContainerStatusDisplay(
                group.ContainerLifecycleStatus);
            MatchedItemCount = group.MatchedItemCount;

            ToggleExpandCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
            SelectAllItemGroupsCommand = new RelayCommand(_ => SelectAllItemGroups());
            ClearItemGroupSelectionCommand = new RelayCommand(_ => ClearItemGroupSelection());
            ToggleItemGroupSelectionCommand = new RelayCommand(_ => ToggleItemGroupSelection());
        }

        public string ArchiveSequenceNo { get; }

        public string ProjectName { get; }

        public string Year { get; }

        public string StorageLocation { get; }

        public string CurrentStorageLocation { get; }

        public string Specifications { get; }

        public string PlacementModeDisplay { get; }

        public string ArchivedBy { get; }

        public string ArchivedDateText { get; }

        public string Remarks { get; }

        /// <summary>档案盒容器状态展示（在用/已清空/已销号等）。</summary>
        public string ContainerLifecycleStatusDisplay { get; }

        public int MatchedItemCount { get; }

        public IReadOnlyList<FiledArchiveSearchGroupHit> ItemGroupHits => _itemGroupHits;

        public ObservableCollection<FiledArchiveSearchGroupRow> ItemGroups { get; } = new();

        public string SummaryText => $"盒内命中 {MatchedItemCount} 条资料子项";

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

        private void SelectAllItemGroups()
        {
            IsExpanded = true;
            EnsureItemGroupsLoaded();
            SetAllPrimarySelection(true);
        }

        private void ClearItemGroupSelection()
        {
            if (!_itemGroupsLoaded)
            {
                return;
            }

            SetAllPrimarySelection(false);
            SetAllBackupSelection(false);
        }

        private void ToggleItemGroupSelection()
        {
            IsExpanded = true;
            EnsureItemGroupsLoaded();
            foreach (var itemGroup in ItemGroups)
            {
                itemGroup.Primary.IsSelected = !itemGroup.Primary.IsSelected;
            }

            _onSelectionChanged?.Invoke();
        }

        private void SetAllPrimarySelection(bool isSelected)
        {
            foreach (var itemGroup in ItemGroups)
            {
                itemGroup.Primary.IsSelected = isSelected;
            }

            _onSelectionChanged?.Invoke();
        }

        private void SetAllBackupSelection(bool isSelected)
        {
            foreach (var itemGroup in ItemGroups)
            {
                foreach (var backupRow in itemGroup.BackupRows)
                {
                    backupRow.IsSelected = isSelected;
                }
            }
        }

        public RelayCommand ToggleExpandCommand { get; }

        public RelayCommand SelectAllItemGroupsCommand { get; }

        public RelayCommand ClearItemGroupSelectionCommand { get; }

        public RelayCommand ToggleItemGroupSelectionCommand { get; }

        public bool ShowItemGroupSelectionCommands => MatchedItemCount > 0;

        /// <summary>
        /// 确保已创建盒内资料子项展示行（首次展开时加载）。
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
                    isElectronicSearch: false,
                    loadAllContentEntriesAsync: null,
                    _onSelectionChanged));
            }
        }

        private static string FormatPlacementMode(string? placementMode)
        {
            return string.Equals(placementMode, "FrontOut", StringComparison.OrdinalIgnoreCase)
                ? "盒面向外"
                : string.IsNullOrWhiteSpace(placementMode)
                    ? string.Empty
                    : "盒脊向外";
        }
    }
}
