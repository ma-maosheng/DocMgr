using System.Collections.ObjectModel;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveSearchResultSetPickDialogViewModel : ViewModelBase
    {
        private readonly IArchiveFilingSearchService _searchService;
        private readonly IUserContextService _userContextService;
        private readonly IArchiveOutboundService _outboundService;
        private readonly IDialogService _dialogService;
        private readonly HashSet<int> _excludedResultSetIds;

        private SearchPoolListItem? _selectedPool;
        private string _keyword = string.Empty;
        private bool _isInitialized;

        public ArchiveSearchResultSetPickDialogViewModel(
            IArchiveFilingSearchService searchService,
            IUserContextService userContextService,
            IArchiveOutboundService outboundService,
            IDialogService dialogService,
            IEnumerable<int>? excludedResultSetIds = null)
        {
            _searchService = searchService;
            _userContextService = userContextService;
            _outboundService = outboundService;
            _dialogService = dialogService;
            _excludedResultSetIds = excludedResultSetIds?.ToHashSet() ?? new HashSet<int>();

            Pools = new ObservableCollection<SearchPoolListItem>();
            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => SelectedPool != null);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public ObservableCollection<SearchPoolListItem> Pools { get; }

        public SearchPoolListItem? SelectedPool
        {
            get => _selectedPool;
            set
            {
                if (SetProperty(ref _selectedPool, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        public int? SelectedResultSetId { get; private set; }

        public RelayCommand SearchCommand { get; }

        public RelayCommand ConfirmCommand { get; }

        public RelayCommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            await SearchAsync();
        }

        private async Task SearchAsync()
        {
            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                Pools.Clear();
                SelectedPool = null;
                return;
            }

            try
            {
                bool isAdmin = _outboundService.IsArchiveAdminUser(user);
                var pools = await ListConfirmedPoolsAsync(user, isAdmin);

                Pools.Clear();
                foreach (var pool in pools.Where(item => item.ItemCount > 0))
                {
                    Pools.Add(pool);
                }

                SelectedPool = Pools.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Pools.Clear();
                SelectedPool = null;
                _dialogService.ShowError($"加载检索集失败：{ex.Message}");
            }
        }

        private async Task<List<SearchPoolListItem>> ListConfirmedPoolsAsync(User user, bool isArchiveAdmin)
        {
            var criteria = new SearchPoolListCriteria
            {
                Keyword = Keyword?.Trim() ?? string.Empty,
                Status = ArchiveSearchResultSetStatus.Confirmed,
                OnlyMine = !isArchiveAdmin
            };

            var simulatedPools = await _searchService.ListSearchPoolsAsync(
                new SearchPoolListCriteria
                {
                    MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                    Keyword = criteria.Keyword,
                    Status = criteria.Status,
                    OnlyMine = criteria.OnlyMine
                },
                user,
                isArchiveAdmin);

            var electronicPools = await _searchService.ListSearchPoolsAsync(
                new SearchPoolListCriteria
                {
                    MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                    Keyword = criteria.Keyword,
                    Status = criteria.Status,
                    OnlyMine = criteria.OnlyMine
                },
                user,
                isArchiveAdmin);

            return simulatedPools
                .Concat(electronicPools)
                .OrderByDescending(pool => pool.UpdatedAt ?? pool.CreatedAt)
                .ThenByDescending(pool => pool.Id)
                .ToList();
        }

        private void Confirm()
        {
            if (SelectedPool == null)
            {
                _dialogService.ShowMessage("请选择一个检索集。", "提示");
                return;
            }

            SelectedResultSetId = SelectedPool.Id;
            RequestClose?.Invoke(true);
        }
    }
}
