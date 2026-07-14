using DocMgr.Models.YearlyArchive;

using DocMgr.ViewModels.Base;

using System;

using System.Collections.ObjectModel;

using System.Collections.Generic;

using System.Linq;

using System.Threading.Tasks;

using System.Windows.Input;



namespace DocMgr.ViewModels.YearlyArchive

{

    public sealed class FiledArchiveSearchBackupRow : ViewModelBase

    {

        public FiledArchiveSearchBackupRow(FiledArchiveSearchHit hit, Action? selectionChanged = null)

        {

            Hit = hit;

            _selectionChanged = selectionChanged;

        }



        public FiledArchiveSearchHit Hit { get; }



        private readonly Action? _selectionChanged;



        private bool _isSelected;



        public bool IsSelected

        {

            get => _isSelected;

            set

            {

                if (SetProperty(ref _isSelected, value))

                {

                    _selectionChanged?.Invoke();

                }

            }

        }



        public string ContainerCode => Hit.ContainerCode;



        public string StorageLocation => Hit.StorageLocation;



        public string CurrentStorageLocation => Hit.CurrentStorageLocation;



        public string MediumCode => Hit.MediumCode;



        public string FiledAtDisplay => Hit.FiledAt.ToString("yyyy-MM-dd");



        public string ArchiveCopyRoleDisplay => Hit.ArchiveCopyRoleDisplay;

    }



    public sealed class FiledArchiveSearchGroupRow : ViewModelBase

    {

        private readonly Func<int, string?, Task<IReadOnlyList<MatchedContentEntryInfo>>>? _loadAllContentEntriesAsync;

        private readonly Action? _onSelectionChanged;

        private bool _allContentEntriesLoaded;



        public FiledArchiveSearchGroupRow(

            FiledArchiveSearchGroupHit group,

            RelayCommand<FiledArchiveSearchHitRow> viewDetailCommand,

            bool isElectronicSearch,

            Func<int, string?, Task<IReadOnlyList<MatchedContentEntryInfo>>>? loadAllContentEntriesAsync = null,

            Action? onSelectionChanged = null)

        {

            _loadAllContentEntriesAsync = loadAllContentEntriesAsync;

            _onSelectionChanged = onSelectionChanged;

            IsElectronicSearch = isElectronicSearch;

            Primary = new FiledArchiveSearchHitRow(group.PrimaryHit);
            Primary.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FiledArchiveSearchHitRow.IsSelected))
                {
                    NotifySelectionChanged();
                }
            };

            HasMatchingBackup = group.HasMatchingBackup;

            IsExpanded = group.ExpandByDefault;



            foreach (var backupHit in group.BackupHits)

            {

                BackupRows.Add(new FiledArchiveSearchBackupRow(backupHit, NotifySelectionChanged));

            }



            InitializeMatchedContentEntries(group.PrimaryHit);



            ToggleExpandCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);

            ToggleContentExpandCommand = new RelayCommand(async _ => await ToggleContentExpandAsync());

            SelectAllContentEntriesCommand = new RelayCommand(async _ => await SelectAllContentEntriesAsync());

            ClearContentEntrySelectionCommand = new RelayCommand(_ => SetAllContentEntrySelection(false));

            ToggleContentEntrySelectionCommand = new RelayCommand(async _ => await ToggleContentEntrySelectionAsync());



            ViewPrimaryDetailCommand = new RelayCommand(_ => viewDetailCommand.Execute(Primary));

        }



        public bool IsElectronicSearch { get; }

        /// <summary>是否在子项行展示档案盒/位置（模拟介质按盒分组时由盒级行承担）。</summary>
        public bool ShowArchiveContainerSummary => IsElectronicSearch;

        public FiledArchiveSearchHitRow Primary { get; }



        public ObservableCollection<FiledArchiveSearchBackupRow> BackupRows { get; } = new();



        public ObservableCollection<FiledArchiveSearchContentEntryRow> ContentEntryRows { get; } = new();



        public int BackupCount => BackupRows.Count;



        public bool HasBackups => BackupCount > 0;



        public bool HasMatchingBackup { get; }



        public bool HasMatchedContentEntries => Primary.Hit.MatchedContentEntries.Count > 0;



        public bool HasContentEntries => ContentEntryRows.Count > 0;



        public bool CanExpandContentEntries => IsElectronicSearch && Primary.Hit.MediaItemId > 0;



        public bool ShowContentEntrySelectionCommands => CanExpandContentEntries;



        public string ContentSummaryText

        {

            get

            {

                if (!CanExpandContentEntries)

                {

                    return string.Empty;

                }



                if (HasMatchedContentEntries && !_allContentEntriesLoaded)

                {

                    return $"命中 {Primary.Hit.MatchedContentEntries.Count} 条目录/文件";

                }



                return HasContentEntries

                    ? $"共 {ContentEntryRows.Count} 条目录/文件"

                    : "可展开查看目录/文件明细";

            }

        }



        public string ContentExpandToggleText => IsContentExpanded ? "收起目录/文件" : "展开目录/文件";



        private bool _isContentExpanded;



        public bool IsContentExpanded

        {

            get => _isContentExpanded;

            set

            {

                if (SetProperty(ref _isContentExpanded, value))

                {

                    OnPropertyChanged(nameof(ContentExpandToggleText));

                }

            }

        }



        public string BackupSummaryText => HasMatchingBackup

            ? $"另有 {BackupCount} 份备份（本次检索匹配备份）"

            : $"另有 {BackupCount} 份备份";



        public string ExpandToggleText => IsExpanded ? "收起备份" : "展开备份";



        private bool _isExpanded;



        public bool IsExpanded

        {

            get => _isExpanded;

            set

            {

                if (SetProperty(ref _isExpanded, value))

                {

                    OnPropertyChanged(nameof(ExpandToggleText));

                }

            }

        }



        public RelayCommand ToggleExpandCommand { get; }



        public RelayCommand ToggleContentExpandCommand { get; }



        public RelayCommand SelectAllContentEntriesCommand { get; }



        public RelayCommand ClearContentEntrySelectionCommand { get; }



        public RelayCommand ToggleContentEntrySelectionCommand { get; }



        public RelayCommand ViewPrimaryDetailCommand { get; }



        /// <summary>

        /// 确保已加载资料子项下的全部目录/文件，供整子项/部分子项判定与批量选择使用。

        /// </summary>

        public async Task EnsureAllContentEntriesLoadedAsync()

        {

            if (!CanExpandContentEntries || _allContentEntriesLoaded || _loadAllContentEntriesAsync == null)

            {

                return;

            }



            var entries = await _loadAllContentEntriesAsync(

                Primary.Hit.MediaItemId,

                Primary.Hit.FilingStoragePath);

            ReplaceContentEntryRows(entries);

            _allContentEntriesLoaded = true;

            OnPropertyChanged(nameof(ContentSummaryText));

        }



        private void InitializeMatchedContentEntries(FiledArchiveSearchHit hit)

        {

            _allContentEntriesLoaded = false;

            ContentEntryRows.Clear();

            foreach (var entry in hit.MatchedContentEntries)

            {

                ContentEntryRows.Add(CreateContentEntryRow(entry, hit.FilingFactId));

            }



            if (HasMatchedContentEntries)

            {

                IsContentExpanded = true;

            }



            OnPropertyChanged(nameof(HasContentEntries));

            OnPropertyChanged(nameof(HasMatchedContentEntries));

            OnPropertyChanged(nameof(ContentSummaryText));

        }



        private FiledArchiveSearchContentEntryRow CreateContentEntryRow(MatchedContentEntryInfo entry, int filingFactId)

        {

            return new FiledArchiveSearchContentEntryRow(entry, filingFactId, NotifySelectionChanged);

        }



        private void ReplaceContentEntryRows(IReadOnlyList<MatchedContentEntryInfo> entries)

        {

            var selectedIds = ContentEntryRows

                .Where(row => row.IsSelected)

                .Select(row => row.EntryId)

                .ToHashSet();



            ContentEntryRows.Clear();

            foreach (var entry in entries)

            {

                var row = CreateContentEntryRow(entry, Primary.Hit.FilingFactId);

                if (selectedIds.Contains(entry.EntryId))

                {

                    row.IsSelected = true;

                }



                ContentEntryRows.Add(row);

            }



            OnPropertyChanged(nameof(HasContentEntries));

            OnPropertyChanged(nameof(ContentSummaryText));

        }



        private async Task ToggleContentExpandAsync()

        {

            if (!CanExpandContentEntries)

            {

                return;

            }



            if (IsContentExpanded)

            {

                IsContentExpanded = false;

                return;

            }



            if (!_allContentEntriesLoaded)

            {

                await EnsureAllContentEntriesLoadedAsync();

            }



            IsContentExpanded = true;

        }



        private async Task SelectAllContentEntriesAsync()

        {

            if (!CanExpandContentEntries)

            {

                return;

            }



            await EnsureAllContentEntriesLoadedAsync();

            SetAllContentEntrySelection(true);

            IsContentExpanded = true;

        }



        private async Task ToggleContentEntrySelectionAsync()

        {

            if (!CanExpandContentEntries)

            {

                return;

            }



            await EnsureAllContentEntriesLoadedAsync();

            foreach (var row in ContentEntryRows)

            {

                row.IsSelected = !row.IsSelected;

            }



            IsContentExpanded = true;

        }



        private void SetAllContentEntrySelection(bool isSelected)

        {

            foreach (var row in ContentEntryRows)

            {

                row.IsSelected = isSelected;

            }

        }



        private void NotifySelectionChanged()

        {

            _onSelectionChanged?.Invoke();

        }

    }

}


