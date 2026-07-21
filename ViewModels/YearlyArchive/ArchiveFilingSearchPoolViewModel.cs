using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveFilingSearchPoolViewModel : ViewModelBase
    {
        private readonly IArchiveFilingSearchService _searchService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveOutboundService _outboundService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private string _mediaKind;

        private SearchPoolListItem? _selectedPool;
        private SearchPoolItemRow? _selectedPoolItem;
        private bool _isArchiveAdmin;
        private bool _isInitialized;
        private string _filterKeyword = string.Empty;
        private string _filterStatus = string.Empty;
        private bool _onlyMine;
        private string _editableName = string.Empty;
        private string _editableRemarks = string.Empty;
        private string _editableStatus = ArchiveSearchResultSetStatus.Confirmed;
        private string _detailSummary = "请从左侧选择一个检索池。";

        public event Action<ArchiveDetailOpenRequest>? ViewRegisterDetailRequested;

        public event Action<int>? CreateOutboundRequested;

        public ArchiveFilingSearchPoolViewModel(
            string mediaKind,
            IArchiveFilingSearchService searchService,
            IArchiveRegisterService archiveRegisterService,
            IArchiveOutboundService outboundService,
            IUserContextService userContextService,
            IDialogService dialogService)
        {
            _mediaKind = mediaKind;
            _searchService = searchService;
            _archiveRegisterService = archiveRegisterService;
            _outboundService = outboundService;
            _userContextService = userContextService;
            _dialogService = dialogService;

            MediaKindOptions =
            [
                new MediaKindOption
                {
                    Label = "电子介质",
                    Value = ArchiveRegisterDomainValues.MediaKindElectronic
                },
                new MediaKindOption
                {
                    Label = "模拟介质",
                    Value = ArchiveRegisterDomainValues.MediaKindSimulated
                }
            ];

            SearchCommand = new RelayCommand(async _ => await SearchPoolsAsync());
            ResetCommand = new RelayCommand(_ => ResetFilter());
            SaveCommand = new RelayCommand(async _ => await SavePoolAsync(), _ => SelectedPool != null);
            RemoveSelectedItemsCommand = new RelayCommand(_ => RemoveSelectedItems(), _ => PoolItems.Any(item => item.IsSelected));
            ViewDetailCommand = new RelayCommand(async _ => await ViewDetailAsync(), _ => SelectedPoolItem != null);
            CreateOutboundFromPoolCommand = new RelayCommand(
                async _ => await CreateOutboundFromPoolAsync(),
                _ => SelectedPool != null
                     && PoolItems.Count > 0
                     && _outboundService.CanSubmitApplication(_userContextService.CurrentUser));

            StatusFilterOptions =
            [
                new StatusFilterOption { Label = "全部状态", Value = string.Empty },
                new StatusFilterOption { Label = "草稿", Value = ArchiveSearchResultSetStatus.Draft },
                new StatusFilterOption { Label = "已确认", Value = ArchiveSearchResultSetStatus.Confirmed },
                new StatusFilterOption { Label = "已引用", Value = ArchiveSearchResultSetStatus.Referenced }
            ];

            EditableStatusOptions =
            [
                new StatusFilterOption { Label = "草稿", Value = ArchiveSearchResultSetStatus.Draft },
                new StatusFilterOption { Label = "已确认", Value = ArchiveSearchResultSetStatus.Confirmed },
                new StatusFilterOption { Label = "已引用", Value = ArchiveSearchResultSetStatus.Referenced }
            ];
        }

        public ObservableCollection<SearchPoolListItem> Pools { get; } = new();

        public ObservableCollection<SearchPoolItemRow> PoolItems { get; } = new();

        public ObservableCollection<StatusFilterOption> StatusFilterOptions { get; }

        public ObservableCollection<StatusFilterOption> EditableStatusOptions { get; }

        public ObservableCollection<MediaKindOption> MediaKindOptions { get; }

        public string SelectedMediaKind
        {
            get => _mediaKind;
            set
            {
                if (string.IsNullOrWhiteSpace(value)
                    || string.Equals(_mediaKind, value, StringComparison.Ordinal))
                {
                    return;
                }

                SetProperty(ref _mediaKind, value);
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(IsSimulatedMediaPool));
                OnPropertyChanged(nameof(PoolItemStatusColumnHeader));
                _selectedPool = null;
                OnPropertyChanged(nameof(SelectedPool));
                ClearDetail();
                _ = SearchPoolsAsync();
            }
        }

        public string PageTitle => string.Equals(_mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
            ? "电子介质检索池"
            : "模拟介质检索池";

        public bool IsSimulatedMediaPool => string.Equals(
            _mediaKind,
            ArchiveRegisterDomainValues.MediaKindSimulated,
            StringComparison.Ordinal);

        public string PoolItemStatusColumnHeader => IsSimulatedMediaPool ? "当前在库份数" : "状态";

        public string FilterKeyword
        {
            get => _filterKeyword;
            set => SetProperty(ref _filterKeyword, value);
        }

        public string FilterStatus
        {
            get => _filterStatus;
            set => SetProperty(ref _filterStatus, value);
        }

        public bool OnlyMine
        {
            get => _onlyMine;
            set => SetProperty(ref _onlyMine, value);
        }

        public bool CanBrowseAllPools
        {
            get => _isArchiveAdmin;
            private set
            {
                if (SetProperty(ref _isArchiveAdmin, value))
                {
                    OnPropertyChanged(nameof(CanBrowseAllPools));
                }
            }
        }

        public SearchPoolListItem? SelectedPool
        {
            get => _selectedPool;
            set
            {
                if (!SetProperty(ref _selectedPool, value))
                {
                    return;
                }

                CommandManager.InvalidateRequerySuggested();
                _ = LoadSelectedPoolAsync();
            }
        }

        public SearchPoolItemRow? SelectedPoolItem
        {
            get => _selectedPoolItem;
            set => SetProperty(ref _selectedPoolItem, value);
        }

        public string EditableName
        {
            get => _editableName;
            set => SetProperty(ref _editableName, value);
        }

        public string EditableRemarks
        {
            get => _editableRemarks;
            set => SetProperty(ref _editableRemarks, value);
        }

        public string EditableStatus
        {
            get => _editableStatus;
            set => SetProperty(ref _editableStatus, value);
        }

        public string DetailSummary
        {
            get => _detailSummary;
            private set => SetProperty(ref _detailSummary, value);
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand RemoveSelectedItemsCommand { get; }
        public RelayCommand ViewDetailCommand { get; }

        public RelayCommand CreateOutboundFromPoolCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                _dialogService.ShowError("请先登录。");
                return;
            }

            CanBrowseAllPools = _archiveRegisterService.IsArchiveAdminUser(user);
            OnlyMine = !CanBrowseAllPools;
            await SearchPoolsAsync();
        }

        private async Task SearchPoolsAsync()
        {
            var user = RequireCurrentUser();

            try
            {
                var pools = await _searchService.ListSearchPoolsAsync(
                    new SearchPoolListCriteria
                    {
                        MediaKind = _mediaKind,
                        Keyword = FilterKeyword,
                        Status = string.IsNullOrWhiteSpace(FilterStatus) ? null : FilterStatus,
                        OnlyMine = OnlyMine || !CanBrowseAllPools
                    },
                    user,
                    CanBrowseAllPools);

                Pools.Clear();
                foreach (var pool in pools)
                {
                    Pools.Add(pool);
                }

                if (SelectedPool != null && Pools.All(pool => pool.Id != SelectedPool.Id))
                {
                    _selectedPool = null;
                    OnPropertyChanged(nameof(SelectedPool));
                    ClearDetail();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载检索池失败：{ex.Message}");
            }
        }

        private void ResetFilter()
        {
            FilterKeyword = string.Empty;
            FilterStatus = string.Empty;
            OnlyMine = !CanBrowseAllPools;
            _ = SearchPoolsAsync();
        }

        private async Task LoadSelectedPoolAsync()
        {
            if (SelectedPool == null)
            {
                ClearDetail();
                return;
            }

            var user = RequireCurrentUser();

            try
            {
                var resultSet = await _searchService.GetSearchPoolAsync(SelectedPool.Id, user, CanBrowseAllPools);
                if (resultSet == null)
                {
                    ClearDetail();
                    return;
                }

                EditableName = resultSet.Name;
                EditableRemarks = resultSet.Remarks;
                EditableStatus = resultSet.Status;
                DetailSummary = $"{resultSet.ResultSetNo} · {resultSet.Name} · 共 {resultSet.Items.Count} 条";

                var factIds = resultSet.Items.Select(item => item.FilingFactId).Distinct().ToList();
                var currentLocations = await _searchService.GetCurrentStorageLocationsByFilingFactIdsAsync(factIds);

                IReadOnlyDictionary<int, SimulatedInArchiveCopyCountInfo> inArchiveCopyCountInfo = new Dictionary<int, SimulatedInArchiveCopyCountInfo>();
                if (IsSimulatedMediaPool)
                {
                    inArchiveCopyCountInfo = await _searchService.GetSimulatedInArchiveCopyCountInfoByFilingFactIdsAsync(factIds);
                }

                PoolItems.Clear();
                foreach (var item in resultSet.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
                {
                    currentLocations.TryGetValue(item.FilingFactId, out string? currentLocation);
                    int filedCopyCount = 1;
                    int currentInArchiveCopyCount = 1;
                    int lostCopyCount = 0;
                    if (IsSimulatedMediaPool
                        && inArchiveCopyCountInfo.TryGetValue(item.FilingFactId, out SimulatedInArchiveCopyCountInfo? copyCountInfo))
                    {
                        filedCopyCount = copyCountInfo.FiledCopyCount;
                        currentInArchiveCopyCount = copyCountInfo.CurrentInArchiveCopyCount;
                        lostCopyCount = copyCountInfo.LostCopyCount;
                    }

                    PoolItems.Add(new SearchPoolItemRow(
                        item.Id,
                        item.FilingFactId,
                        item.FormNo,
                        item.MaterialName,
                        item.ItemName,
                        item.ContainerCode,
                        item.StorageLocation,
                        currentLocation ?? item.StorageLocation,
                        MapLifecycleStatus(item.LifecycleStatus),
                        MapBorrowHint(item.BorrowHintLevel, item.BorrowHintText),
                        item.SelectionScopeKind,
                        item.ContentEntryKind,
                        item.ContentEntryName,
                        item.ContentEntryRelativePath,
                        item.ContentEntryId,
                        confidentialLevel: string.Empty,
                        requestedCopyCount: item.RequestedCopyCount,
                        isSimulatedMedia: IsSimulatedMediaPool,
                        filedCopyCount: filedCopyCount,
                        currentInArchiveCopyCount: currentInArchiveCopyCount,
                        lostCopyCount: lostCopyCount));
                }

                SelectedPoolItem = PoolItems.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载检索池明细失败：{ex.Message}");
                ClearDetail();
            }
        }

        private async Task SavePoolAsync()
        {
            if (SelectedPool == null)
            {
                return;
            }

            var user = RequireCurrentUser();

            if (string.IsNullOrWhiteSpace(EditableName))
            {
                _dialogService.ShowError("请填写检索池名称。");
                return;
            }

            if (PoolItems.Count == 0)
            {
                _dialogService.ShowError("检索池至少应保留一条记录。");
                return;
            }

            try
            {
                await _searchService.UpdateSearchPoolAsync(
                    new UpdateSearchPoolRequest
                    {
                        ResultSetId = SelectedPool.Id,
                        Name = EditableName,
                        Remarks = EditableRemarks,
                        Status = EditableStatus,
                        RemainingResultSetItemIds = PoolItems.Select(item => item.ResultSetItemId).ToList()
                    },
                    user,
                    CanBrowseAllPools);

                _dialogService.ShowMessage("检索池已保存。", "完成");
                int savedPoolId = SelectedPool.Id;
                await SearchPoolsAsync();
                SelectedPool = Pools.FirstOrDefault(pool => pool.Id == savedPoolId) ?? Pools.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"保存失败：{ex.Message}");
            }
        }

        private void RemoveSelectedItems()
        {
            var toRemove = PoolItems.Where(item => item.IsSelected).ToList();
            if (toRemove.Count == 0)
            {
                return;
            }

            if (PoolItems.Count - toRemove.Count <= 0)
            {
                _dialogService.ShowMessage("检索池至少应保留一条记录。", "提示");
                return;
            }

            foreach (var item in toRemove)
            {
                PoolItems.Remove(item);
            }

            if (SelectedPoolItem != null && !PoolItems.Contains(SelectedPoolItem))
            {
                SelectedPoolItem = PoolItems.FirstOrDefault();
            }

            DetailSummary = SelectedPool == null
                ? DetailSummary
                : $"{SelectedPool.ResultSetNo} · {EditableName} · 共 {PoolItems.Count} 条（未保存）";
        }

        private async Task ViewDetailAsync()
        {
            if (SelectedPoolItem == null)
            {
                return;
            }

            try
            {
                var hit = await _searchService.GetSearchHitByFilingFactIdAsync(SelectedPoolItem.FilingFactId);
                if (hit == null || hit.RegisterRecordId <= 0)
                {
                    _dialogService.ShowMessage("无法定位该条立档记录对应的登记资料。", "提示");
                    return;
                }

                ViewRegisterDetailRequested?.Invoke(new ArchiveDetailOpenRequest(
                    hit.RegisterRecordId,
                    BuildHighlightContext(hit, SelectedPoolItem),
                    SelectedMediaKind,
                    SelectedPoolItem.FilingFactId));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"打开资料详情失败：{ex.Message}");
            }
        }

        private void ClearDetail()
        {
            SelectedPoolItem = null;
            PoolItems.Clear();
            EditableName = string.Empty;
            EditableRemarks = string.Empty;
            EditableStatus = ArchiveSearchResultSetStatus.Confirmed;
            DetailSummary = "请从左侧选择一个检索池。";
        }

        private static ArchiveDetailHighlightContext BuildHighlightContext(
            FiledArchiveSearchHit hit,
            SearchPoolItemRow poolItem)
        {
            var context = ArchiveDetailHighlightContext.FromHit(hit);
            if (!string.Equals(
                    poolItem.SelectionScopeKind,
                    ArchiveSearchSelectionScopeKind.ContentEntry,
                    StringComparison.Ordinal)
                || poolItem.ContentEntryId is not int contentEntryId
                || contentEntryId <= 0)
            {
                return context;
            }

            return new ArchiveDetailHighlightContext
            {
                MediaKind = context.MediaKind,
                RegisterMediaId = context.RegisterMediaId,
                MediaItemId = context.MediaItemId,
                ItemType = context.ItemType,
                ItemName = context.ItemName,
                ContainerCode = context.ContainerCode,
                ContentEntryKeyword = context.ContentEntryKeyword,
                ContentEntryKindFilter = context.ContentEntryKindFilter,
                MatchedContentEntryIds = new[] { contentEntryId }
            };
        }

        private User RequireCurrentUser()
        {
            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                throw new InvalidOperationException("请先登录。");
            }

            return user;
        }

        private async Task CreateOutboundFromPoolAsync()
        {
            if (SelectedPool == null)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                _dialogService.ShowError("请先登录。");
                return;
            }

            var selectedItemIds = PoolItems
                .Where(item => item.IsSelected)
                .Select(item => item.ResultSetItemId)
                .ToList();

            if (selectedItemIds.Count == 0)
            {
                selectedItemIds = PoolItems.Select(item => item.ResultSetItemId).ToList();
            }

            try
            {
                var record = await _outboundService.CreateDraftFromSearchPoolAsync(new CreateOutboundFromPoolRequest
                {
                    ResultSetId = SelectedPool.Id,
                    ResultSetItemIds = selectedItemIds
                }, user);

                var saveResult = await _outboundService.SaveDraftFlowAsync(new SaveOutboundDraftRequest
                {
                    Record = record,
                    Items = record.Items
                }, user);

                if (!saveResult.Success)
                {
                    _dialogService.ShowError(saveResult.Message);
                    return;
                }

                var saved = await _outboundService.GetRecordAsync(record.Id > 0 ? record.Id : 0);
                if (saved == null && !string.IsNullOrWhiteSpace(record.OutboundNo))
                {
                    saved = (await _outboundService.ListRecordsAsync(new OutboundListCriteria
                    {
                        Year = DateTime.Today.Year,
                        WorkspaceMode = ArchiveOutboundWorkspaceMode.Application,
                        OnlyMine = true
                    }, user)).FirstOrDefault(r => r.OutboundNo == record.OutboundNo);
                }

                if (saved == null)
                {
                    _dialogService.ShowError("借出申请已创建，但未能定位记录 Id。");
                    return;
                }

                // 跳转借出申请页并自动打开编辑框，由申请人在弹窗中填写原由等信息。
                CreateOutboundRequested?.Invoke(saved.Id);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"发起借出申请失败：{ex.Message}");
            }
        }

        private static string MapLifecycleStatus(string status) => status switch
        {
            FilingFactLifecycleStatus.InArchive => "在库",
            FilingFactLifecycleStatus.Borrowed => "借出中",
            FilingFactLifecycleStatus.Transferred => "已转移",
            FilingFactLifecycleStatus.Destroyed => "已销毁",
            FilingFactLifecycleStatus.Disposed => "已处置",
            _ => status
        };

        private static string MapBorrowHint(string level, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return level switch
            {
                FilingFactBorrowHintLevel.CopyBorrowed => "有拷贝借出",
                FilingFactBorrowHintLevel.OriginalBorrowed => "原件借出中",
                FilingFactBorrowHintLevel.PartialAvailable => "多备份，部分在库",
                FilingFactBorrowHintLevel.Unknown => "借出状态待确认",
                _ => string.Empty
            };
        }

        public sealed class StatusFilterOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }

        public sealed class MediaKindOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }
    }
}
