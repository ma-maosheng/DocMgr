using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveFilingSearchViewModel : ViewModelBase
    {
        private readonly string _mediaKind;
        private readonly IArchiveFilingSearchService _searchService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly IProjectService _projectService;
        private readonly IArchiveFilingSearchPoolSession _poolSession;

        private readonly ObservableCollection<ArchiveSearchPoolItemRow> _filterPool = new();
        private bool _isInitialized;
        private bool _isSearching;

        private ObservableCollection<FiledArchiveSearchGroupRow> _searchResultGroups = new();
        private ObservableCollection<FiledArchiveSearchBoxGroupRow> _searchResultBoxGroups = new();
        private List<FiledArchiveSearchGroupHit> _simulatedItemGroupHits = new();

        public event Action<ArchiveDetailOpenRequest>? ViewRegisterDetailRequested;

        public ArchiveFilingSearchViewModel(
            string mediaKind,
            IArchiveFilingSearchService searchService,
            IArchiveRegisterService archiveRegisterService,
            IUserContextService userContextService,
            IDialogService dialogService,
            IProjectService projectService,
            IArchiveFilingSearchPoolSession poolSession)
        {
            _mediaKind = mediaKind;
            _searchService = searchService;
            _archiveRegisterService = archiveRegisterService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            _projectService = projectService;
            _poolSession = poolSession;
            _poolSession.PoolChanged += HandlePoolSessionChanged;

            PageTitle = _mediaKind == ArchiveRegisterDomainValues.MediaKindElectronic
                ? "电子介质资料检索"
                : "模拟介质资料检索";

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            ResetCommand = new RelayCommand(_ => ResetCriteria());
            AddSelectedToPoolCommand = new RelayCommand(async _ => await AddSelectedToPoolAsync(), _ => HasSelectableSearchResults);
            AddAllResultsToPoolCommand = new RelayCommand(_ => AddAllResultsToPool(), _ => HasSearchResults);
            RemoveSelectedFromPoolCommand = new RelayCommand(_ => RemoveSelectedFromPool(), _ => FilterPool.Any(r => r.IsSelected));
            ClearPoolCommand = new RelayCommand(_ => ClearPool(), _ => FilterPool.Count > 0);
            SaveResultSetCommand = new RelayCommand(async _ => await SaveResultSetAsync(), _ => FilterPool.Count > 0);
            ViewDetailCommand = new RelayCommand<FiledArchiveSearchHitRow>(ViewDetail);
            ViewBackupDetailCommand = new RelayCommand<FiledArchiveSearchBackupRow>(ViewBackupDetail);
        }

        public string PageTitle { get; }

        public string MediaKind => _mediaKind;

        public ObservableCollection<string> Years { get; } = new() { "全部年份" };

        private string _selectedYear = "全部年份";

        public string SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (!SetProperty(ref _selectedYear, value))
                {
                    return;
                }

                SelectedProjectId = null;
                LoadProjectOptions();
            }
        }

        public string RegisterKeyword { get; set; } = string.Empty;

        public string ContentEntryKeyword { get; set; } = string.Empty;

        public ObservableCollection<ContentEntryKindOption> ContentEntryKindOptions { get; } =
        [
            new ContentEntryKindOption { Label = "全部", Value = string.Empty },
            new ContentEntryKindOption
            {
                Label = ArchiveRegisterDomainValues.ElectronicEntryKindDirectory,
                Value = ArchiveRegisterDomainValues.ElectronicEntryKindDirectory
            },
            new ContentEntryKindOption
            {
                Label = ArchiveRegisterDomainValues.ElectronicEntryKindFile,
                Value = ArchiveRegisterDomainValues.ElectronicEntryKindFile
            }
        ];

        private string _selectedContentEntryKind = string.Empty;

        public string SelectedContentEntryKind
        {
            get => _selectedContentEntryKind;
            set => SetProperty(ref _selectedContentEntryKind, value);
        }

        public bool IsElectronicSearch => string.Equals(
            _mediaKind,
            ArchiveRegisterDomainValues.MediaKindElectronic,
            StringComparison.Ordinal);

        public ObservableCollection<ProjectFilterOption> ProjectOptions { get; } = new();

        private int? _selectedProjectId;

        public int? SelectedProjectId
        {
            get => _selectedProjectId;
            set => SetProperty(ref _selectedProjectId, value);
        }

        public ObservableCollection<LifecycleStatusOption> LifecycleStatusOptions { get; } =
        [
            new LifecycleStatusOption { Label = "全部", Value = string.Empty },
            new LifecycleStatusOption { Label = "在库", Value = FilingFactLifecycleStatus.InArchive },
            new LifecycleStatusOption { Label = "借出中", Value = FilingFactLifecycleStatus.Borrowed },
            new LifecycleStatusOption { Label = "已转移", Value = FilingFactLifecycleStatus.Transferred },
            new LifecycleStatusOption { Label = "已销毁", Value = FilingFactLifecycleStatus.Destroyed },
            new LifecycleStatusOption { Label = "已处置", Value = FilingFactLifecycleStatus.Disposed }
        ];

        private string _selectedLifecycleStatus = string.Empty;

        public string SelectedLifecycleStatus
        {
            get => _selectedLifecycleStatus;
            set => SetProperty(ref _selectedLifecycleStatus, value);
        }

        public ObservableCollection<FiledArchiveSearchHitRow> SearchResults { get; } = new();

        public ObservableCollection<FiledArchiveSearchGroupRow> SearchResultGroups => _searchResultGroups;

        public ObservableCollection<FiledArchiveSearchBoxGroupRow> SearchResultBoxGroups => _searchResultBoxGroups;

        public bool UseArchiveBoxGroupedResults => !IsElectronicSearch;

        public ObservableCollection<ArchiveSearchPoolItemRow> FilterPool => _filterPool;

        public int FilterPoolCount => _filterPool.Count;

        public string PoolSummary => $"筛选池：{_filterPool.Count} 条";

        public string ResultSetName { get; set; } = string.Empty;

        public string ResultSetRemarks { get; set; } = string.Empty;

        private string _lastSaveResultSetNotice = string.Empty;

        public string LastSaveResultSetNotice
        {
            get => _lastSaveResultSetNotice;
            private set
            {
                if (SetProperty(ref _lastSaveResultSetNotice, value))
                {
                    OnPropertyChanged(nameof(HasLastSaveResultSetNotice));
                }
            }
        }

        public bool HasLastSaveResultSetNotice => !string.IsNullOrWhiteSpace(LastSaveResultSetNotice);

        public string ResultSetSaveLimitHint =>
            $"每位用户每种介质类型最多保存 {SearchPoolLimits.MaxResultSetsPerUserPerMediaKind} 个结果集，超出时将自动删除最早保存的记录。";

        public bool HasSearchResults => UseArchiveBoxGroupedResults
            ? _searchResultBoxGroups.Count > 0
            : _searchResultGroups.Count > 0;

        private bool _hasSelectableSearchResults;

        public bool HasSelectableSearchResults => _hasSelectableSearchResults;

        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand AddSelectedToPoolCommand { get; }
        public RelayCommand AddAllResultsToPoolCommand { get; }
        public RelayCommand RemoveSelectedFromPoolCommand { get; }
        public RelayCommand ClearPoolCommand { get; }
        public RelayCommand SaveResultSetCommand { get; }
        public RelayCommand<FiledArchiveSearchHitRow> ViewDetailCommand { get; }

        public RelayCommand<FiledArchiveSearchBackupRow> ViewBackupDetailCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            RestoreFilterPoolFromSession();
            await LoadYearsAsync();
        }

        private void HandlePoolSessionChanged(string mediaKind)
        {
            if (!string.Equals(mediaKind, _mediaKind, StringComparison.Ordinal))
            {
                return;
            }

            RestoreFilterPoolFromSession();
        }

        private void RestoreFilterPoolFromSession()
        {
            _filterPool.Clear();
            foreach (var row in _poolSession.GetPool(_mediaKind))
            {
                _filterPool.Add(row);
            }

            NotifyPoolChanged();
        }

        private async Task LoadYearsAsync()
        {
            try
            {
                var yearsList = await _archiveRegisterService.GetExistingYearsAsync();
                Years.Clear();
                Years.Add("全部年份");
                foreach (int year in yearsList)
                {
                    Years.Add(year.ToString());
                }

                SelectedYear = Years.FirstOrDefault(x => x != "全部年份") ?? "全部年份";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载年份失败：{ex.Message}");
            }
        }

        private void LoadProjectOptions()
        {
            try
            {
                ProjectFilterOptionFactory.Reload(ProjectOptions, _projectService, SelectedYear);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载项目列表失败：{ex.Message}");
            }
        }

        private async Task SearchAsync()
        {
            if (_isSearching)
            {
                return;
            }

            _isSearching = true;
            try
            {
                string? year = SelectedYear == "全部年份" ? null : SelectedYear;
                var criteria = BuildSearchCriteria(year);

                if (IsElectronicSearch)
                {
                    var groups = await _searchService.SearchByRegisterGroupedAsync(_mediaKind, criteria)
                        .ConfigureAwait(true);
                    var itemRows = groups
                        .Select(group => CreateItemGroupRow(group))
                        .ToList();
                    var hitRows = groups
                        .Select(group => new FiledArchiveSearchHitRow(group.PrimaryHit))
                        .ToList();

                    ApplyElectronicSearchResults(itemRows, hitRows);
                }
                else
                {
                    var boxGroups = await _searchService.SearchByRegisterGroupedByArchiveBoxAsync(_mediaKind, criteria)
                        .ConfigureAwait(true);
                    var boxRows = boxGroups
                        .Select(boxGroup => new FiledArchiveSearchBoxGroupRow(
                            boxGroup,
                            ViewDetailCommand,
                            NotifySearchSelectionChanged))
                        .ToList();
                    var hitRows = boxGroups
                        .SelectMany(boxGroup => boxGroup.ItemGroups)
                        .Select(itemGroup => new FiledArchiveSearchHitRow(itemGroup.PrimaryHit))
                        .ToList();

                    ApplySimulatedSearchResults(boxRows, hitRows);
                }

                RefreshSearchSelectionState();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查询失败：{ex.Message}");
            }
            finally
            {
                _isSearching = false;
            }
        }

        private void ApplyElectronicSearchResults(
            IReadOnlyList<FiledArchiveSearchGroupRow> itemRows,
            IReadOnlyList<FiledArchiveSearchHitRow> hitRows)
        {
            _searchResultGroups = new ObservableCollection<FiledArchiveSearchGroupRow>(itemRows);
            _searchResultBoxGroups = new ObservableCollection<FiledArchiveSearchBoxGroupRow>();
            ReplaceSearchHitRows(hitRows);
            OnPropertyChanged(nameof(SearchResultGroups));
            OnPropertyChanged(nameof(SearchResultBoxGroups));
        }

        private void ApplySimulatedSearchResults(
            IReadOnlyList<FiledArchiveSearchBoxGroupRow> boxRows,
            IReadOnlyList<FiledArchiveSearchHitRow> hitRows)
        {
            _searchResultBoxGroups = new ObservableCollection<FiledArchiveSearchBoxGroupRow>(boxRows);
            _searchResultGroups = new ObservableCollection<FiledArchiveSearchGroupRow>();
            _simulatedItemGroupHits = boxRows
                .SelectMany(boxRow => boxRow.ItemGroupHits)
                .ToList();
            ReplaceSearchHitRows(hitRows);
            OnPropertyChanged(nameof(SearchResultBoxGroups));
            OnPropertyChanged(nameof(SearchResultGroups));
        }

        private void ReplaceSearchHitRows(IReadOnlyList<FiledArchiveSearchHitRow> hitRows)
        {
            SearchResults.Clear();
            foreach (var hitRow in hitRows)
            {
                SearchResults.Add(hitRow);
            }
        }

        private void RefreshSearchSelectionState()
        {
            OnPropertyChanged(nameof(HasSearchResults));
            NotifySearchSelectionChanged();
        }

        private IEnumerable<FiledArchiveSearchGroupHit> EnumerateSimulatedItemGroupHits()
        {
            return _simulatedItemGroupHits;
        }

        private RegisterDirectionSearchCriteria BuildSearchCriteria(string? year)
        {
            return new RegisterDirectionSearchCriteria
            {
                Year = year,
                ProjectId = SelectedProjectId,
                Keyword = RegisterKeyword?.Trim() ?? string.Empty,
                ContentEntryKeyword = ContentEntryKeyword?.Trim() ?? string.Empty,
                ContentEntryKindFilter = SelectedContentEntryKind?.Trim() ?? string.Empty,
                LifecycleStatus = string.IsNullOrWhiteSpace(SelectedLifecycleStatus)
                    ? null
                    : SelectedLifecycleStatus
            };
        }

        private FiledArchiveSearchGroupRow CreateItemGroupRow(FiledArchiveSearchGroupHit group)
        {
            return new FiledArchiveSearchGroupRow(
                group,
                ViewDetailCommand,
                IsElectronicSearch,
                IsElectronicSearch ? LoadContentEntriesAsync : null,
                NotifySearchSelectionChanged);
        }

        private IEnumerable<FiledArchiveSearchGroupRow> EnumerateLoadedItemGroups()
        {
            if (UseArchiveBoxGroupedResults)
            {
                foreach (var boxGroup in _searchResultBoxGroups)
                {
                    foreach (var itemGroup in boxGroup.ItemGroups)
                    {
                        yield return itemGroup;
                    }
                }

                yield break;
            }

            foreach (var itemGroup in _searchResultGroups)
            {
                yield return itemGroup;
            }
        }

        private void ClearSearchResults()
        {
            _searchResultGroups = new ObservableCollection<FiledArchiveSearchGroupRow>();
            _searchResultBoxGroups = new ObservableCollection<FiledArchiveSearchBoxGroupRow>();
            _simulatedItemGroupHits = new List<FiledArchiveSearchGroupHit>();
            SearchResults.Clear();
            OnPropertyChanged(nameof(SearchResultGroups));
            OnPropertyChanged(nameof(SearchResultBoxGroups));
            RefreshSearchSelectionState();
        }

        private void ResetCriteria()
        {
            SelectedYear = Years.FirstOrDefault(x => x != "全部年份") ?? "全部年份";
            RegisterKeyword = string.Empty;
            ContentEntryKeyword = string.Empty;
            SelectedContentEntryKind = string.Empty;
            SelectedProjectId = null;
            SelectedLifecycleStatus = string.Empty;
            OnPropertyChanged(nameof(RegisterKeyword));
            OnPropertyChanged(nameof(ContentEntryKeyword));
            ClearSearchResults();
        }

        private async Task<IReadOnlyList<MatchedContentEntryInfo>> LoadContentEntriesAsync(
            int mediaItemId,
            string? filingStoragePath)
        {
            try
            {
                return await _searchService.GetContentEntriesByMediaItemIdAsync(mediaItemId, filingStoragePath);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载目录/文件明细失败：{ex.Message}");
                return Array.Empty<MatchedContentEntryInfo>();
            }
        }

        private async Task AddSelectedToPoolAsync()
        {
            var selections = new List<ArchiveSearchPoolSelection>();
            var hitsByFactId = new Dictionary<int, FiledArchiveSearchHit>();
            var entriesById = new Dictionary<int, MatchedContentEntryInfo>();

            foreach (var group in EnumerateLoadedItemGroups())
            {
                CollectHit(hitsByFactId, group.Primary.Hit);

                if (IsElectronicSearch && group.CanExpandContentEntries)
                {
                    var selectedEntries = group.ContentEntryRows.Where(row => row.IsSelected).ToList();
                    if (selectedEntries.Count > 0)
                    {
                        await group.EnsureAllContentEntriesLoadedAsync();
                        selectedEntries = group.ContentEntryRows.Where(row => row.IsSelected).ToList();

                        if (selectedEntries.Count == group.ContentEntryRows.Count)
                        {
                            selections.Add(ArchiveSearchPoolSupport.CreateWholeMediaItem(group.Primary.Hit.FilingFactId));
                        }
                        else
                        {
                            foreach (var entryRow in selectedEntries)
                            {
                                selections.Add(ArchiveSearchPoolSupport.CreateContentEntry(
                                    entryRow.FilingFactId,
                                    entryRow.EntryId));
                                entriesById[entryRow.EntryId] = entryRow.Entry;
                            }
                        }
                    }
                }
                else if (group.Primary.IsSelected)
                {
                    selections.Add(ArchiveSearchPoolSupport.CreateWholeMediaItem(group.Primary.Hit.FilingFactId));
                }
                else
                {
                    foreach (var entryRow in group.ContentEntryRows.Where(row => row.IsSelected))
                    {
                        selections.Add(ArchiveSearchPoolSupport.CreateContentEntry(
                            entryRow.FilingFactId,
                            entryRow.EntryId));
                        entriesById[entryRow.EntryId] = entryRow.Entry;
                    }
                }

                foreach (var backup in group.BackupRows.Where(row => row.IsSelected))
                {
                    CollectHit(hitsByFactId, backup.Hit);
                    selections.Add(ArchiveSearchPoolSupport.CreateWholeMediaItem(backup.Hit.FilingFactId));
                }
            }

            MergeIntoPool(selections, hitsByFactId, entriesById);

            foreach (var group in EnumerateLoadedItemGroups())
            {
                group.Primary.IsSelected = false;
                foreach (var entryRow in group.ContentEntryRows)
                {
                    entryRow.IsSelected = false;
                }

                foreach (var backup in group.BackupRows)
                {
                    backup.IsSelected = false;
                }
            }

            NotifySearchSelectionChanged();
        }

        private void NotifySearchSelectionChanged()
        {
            bool hasSelectable = EnumerateLoadedItemGroups().Any(group =>
                group.BackupRows.Any(row => row.IsSelected)
                || group.ContentEntryRows.Any(row => row.IsSelected)
                || (!IsElectronicSearch && group.Primary.IsSelected));

            SetProperty(ref _hasSelectableSearchResults, hasSelectable, nameof(HasSelectableSearchResults));
        }

        private void AddAllResultsToPool()
        {
            List<ArchiveSearchPoolSelection> selections;
            Dictionary<int, FiledArchiveSearchHit> hitsByFactId;

            if (UseArchiveBoxGroupedResults)
            {
                selections = EnumerateSimulatedItemGroupHits()
                    .Select(group => ArchiveSearchPoolSupport.CreateWholeMediaItem(group.PrimaryHit.FilingFactId))
                    .ToList();
                hitsByFactId = EnumerateSimulatedItemGroupHits()
                    .Select(group => group.PrimaryHit)
                    .ToDictionary(hit => hit.FilingFactId);
            }
            else
            {
                selections = EnumerateLoadedItemGroups()
                    .Select(group => ArchiveSearchPoolSupport.CreateWholeMediaItem(group.Primary.Hit.FilingFactId))
                    .ToList();
                hitsByFactId = EnumerateLoadedItemGroups()
                    .Select(group => group.Primary.Hit)
                    .ToDictionary(hit => hit.FilingFactId);
            }

            MergeIntoPool(selections, hitsByFactId, new Dictionary<int, MatchedContentEntryInfo>());
        }

        private static void CollectHit(Dictionary<int, FiledArchiveSearchHit> hitsByFactId, FiledArchiveSearchHit hit)
        {
            hitsByFactId[hit.FilingFactId] = hit;
        }

        private void MergeIntoPool(
            IReadOnlyList<ArchiveSearchPoolSelection> incoming,
            IReadOnlyDictionary<int, FiledArchiveSearchHit> hitsByFactId,
            IReadOnlyDictionary<int, MatchedContentEntryInfo> entriesById)
        {
            if (incoming.Count == 0)
            {
                return;
            }

            var hitByFactId = new Dictionary<int, FiledArchiveSearchHit>();
            foreach (var row in _filterPool)
            {
                hitByFactId[row.FilingFactId] = row.Hit;
            }

            foreach (var pair in hitsByFactId)
            {
                hitByFactId[pair.Key] = pair.Value;
            }

            var target = _filterPool.Select(row => row.Selection).ToList();
            var mergeResult = ArchiveSearchPoolSupport.MergeSelections(target, incoming);

            _filterPool.Clear();
            foreach (var selection in target)
            {
                if (!hitByFactId.TryGetValue(selection.FilingFactId, out var hit))
                {
                    continue;
                }

                MatchedContentEntryInfo? contentEntry = null;
                if (selection.IsContentEntry && selection.ContentEntryId is int entryId)
                {
                    if (!entriesById.TryGetValue(entryId, out contentEntry))
                    {
                        contentEntry = hit.MatchedContentEntries.FirstOrDefault(entry => entry.EntryId == entryId);
                    }
                }

                _filterPool.Add(new ArchiveSearchPoolItemRow(hit, selection, contentEntry));
            }

            _poolSession.Replace(_mediaKind, _filterPool.ToList());

            NotifyPoolChanged();
            ShowMergeResultMessage(mergeResult, incoming.Count);
        }

        private void ShowMergeResultMessage(ArchiveSearchPoolSupport.MergeResult mergeResult, int incomingCount)
        {
            if (mergeResult.AddedCount > 0)
            {
                return;
            }

            if (mergeResult.SkippedDuplicateCount > 0 && incomingCount == mergeResult.SkippedDuplicateCount)
            {
                _dialogService.ShowMessage("所选条目已在筛选池中。", "提示");
                return;
            }

            if (mergeResult.SkippedWholeExistsCount > 0)
            {
                _dialogService.ShowMessage("对应资料子项已以「整子项」在筛选池中，无法再单独加入目录/文件。", "提示");
            }
        }

        private void RemoveSelectedFromPool()
        {
            var toRemove = _filterPool.Where(row => row.IsSelected).ToList();
            foreach (var row in toRemove)
            {
                _filterPool.Remove(row);
            }

            _poolSession.Replace(_mediaKind, _filterPool.ToList());
            NotifyPoolChanged();
        }

        private void ClearPool()
        {
            _filterPool.Clear();
            _poolSession.Clear(_mediaKind);
            NotifyPoolChanged();
        }

        private async Task SaveResultSetAsync()
        {
            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                _dialogService.ShowError("请先登录。");
                return;
            }

            if (string.IsNullOrWhiteSpace(ResultSetName))
            {
                _dialogService.ShowError("请填写结果集名称。");
                return;
            }

            if (!IsElectronicSearch)
            {
                var copyCountErrors = _filterPool
                    .Where(row => row.IsCopyCountEditable)
                    .Select(row => ArchiveSearchPoolCopyCountSupport.ValidateSimulatedRequestedCopyCount(
                        row.RequestedCopyCount,
                        row.Hit.ContentCount,
                        row.ItemName))
                    .Where(error => error != null)
                    .Cast<string>()
                    .ToList();

                if (copyCountErrors.Count > 0)
                {
                    _dialogService.ShowError(
                        "筛选池份数校验未通过：" + Environment.NewLine + string.Join(Environment.NewLine, copyCountErrors));
                    return;
                }
            }

            int borrowedCount = _filterPool.Count(row =>
                row.Hit.BorrowHintLevel is FilingFactBorrowHintLevel.OriginalBorrowed
                    or FilingFactBorrowHintLevel.CopyBorrowed);

            if (borrowedCount > 0)
            {
                _dialogService.ShowMessage(
                    $"即将保存 {_filterPool.Count} 条，其中 {borrowedCount} 条含借出提示。保存不会排除这些条目。",
                    "保存确认");
            }

            try
            {
                bool isAdmin = _archiveRegisterService.IsArchiveAdminUser(user);
                string savedName = ResultSetName.Trim();
                var saveResult = await _searchService.SaveResultSetAsync(new SaveArchiveSearchResultSetRequest
                {
                    Name = savedName,
                    Remarks = ResultSetRemarks?.Trim() ?? string.Empty,
                    MediaKind = _mediaKind,
                    Selections = _filterPool.Select(row => row.Selection).ToList()
                }, user, isAdmin);

                string notice = $"已保存结果集「{saveResult.ResultSet.Name}」（编号 {saveResult.ResultSet.ResultSetNo}）。";
                if (saveResult.AutoRemovedResultSetNames.Count > 0)
                {
                    notice += $" 已自动删除最早的结果集：{string.Join("、", saveResult.AutoRemovedResultSetNames)}。";
                }

                LastSaveResultSetNotice = notice;
                _dialogService.ShowMessage(notice, "保存完成");

                ResultSetName = string.Empty;
                ResultSetRemarks = string.Empty;
                OnPropertyChanged(nameof(ResultSetName));
                OnPropertyChanged(nameof(ResultSetRemarks));
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException?.Message;
                var message = string.IsNullOrWhiteSpace(detail)
                    ? ex.Message
                    : $"{ex.Message} {detail}";
                _dialogService.ShowError($"保存失败：{message}");
            }
        }

        private void ViewDetail(FiledArchiveSearchHitRow? row)
        {
            if (row == null || row.Hit.RegisterRecordId <= 0)
            {
                return;
            }

            ViewRegisterDetailRequested?.Invoke(new ArchiveDetailOpenRequest(
                row.Hit.RegisterRecordId,
                ArchiveDetailHighlightContext.FromHit(row.Hit),
                _mediaKind,
                row.Hit.FilingFactId));
        }

        private void ViewBackupDetail(FiledArchiveSearchBackupRow? row)
        {
            if (row == null)
            {
                return;
            }

            ViewDetail(new FiledArchiveSearchHitRow(row.Hit));
        }

        private void NotifyPoolChanged()
        {
            OnPropertyChanged(nameof(FilterPoolCount));
            OnPropertyChanged(nameof(PoolSummary));
        }

        public sealed class LifecycleStatusOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }

        public sealed class ContentEntryKindOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }
    }
}
