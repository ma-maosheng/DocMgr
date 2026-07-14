using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 资料明细列表：智能默认折叠 + 分页展示。
    /// </summary>
    public sealed class ItemDetailsListPresenter<T> : ViewModelBase where T : class
    {
        private readonly Func<IReadOnlyList<T>, string> _summaryBuilder;
        private readonly int _pageSize;
        private readonly int _smartExpandThreshold;
        private IReadOnlyList<T> _allItems = Array.Empty<T>();
        private bool _isExpanded = true;
        private bool _expandStateInitialized;
        private int _currentPage = 1;

        public ItemDetailsListPresenter(
            string title = "资料明细",
            int? pageSize = null,
            int? smartExpandThreshold = null,
            Func<IReadOnlyList<T>, string>? summaryBuilder = null)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "资料明细" : title.Trim();
            _pageSize = pageSize ?? ItemDetailsPanelDomainValues.DefaultPageSize;
            _smartExpandThreshold = smartExpandThreshold ?? ItemDetailsPanelDomainValues.SmartExpandThreshold;
            _summaryBuilder = summaryBuilder ?? ItemDetailsPanelSummarySupport.BuildGenericCountSummary;

            PageItems = new ObservableCollection<T>();

            ToggleExpandCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
            FirstPageCommand = new RelayCommand(_ => GoToPage(1), _ => CanGoPrevious);
            PreviousPageCommand = new RelayCommand(_ => GoToPage(CurrentPage - 1), _ => CanGoPrevious);
            NextPageCommand = new RelayCommand(_ => GoToPage(CurrentPage + 1), _ => CanGoNext);
            LastPageCommand = new RelayCommand(_ => GoToPage(TotalPages), _ => CanGoNext);
        }

        public string Title { get; }

        public ObservableCollection<T> PageItems { get; }

        public int ItemCount => _allItems.Count;

        public string ItemCountDisplay => $"{ItemCount} 条";

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                {
                    _expandStateInitialized = true;
                    OnPropertyChanged(nameof(ExpandToggleText));
                    OnPropertyChanged(nameof(CollapsedSummary));
                    OnPropertyChanged(nameof(ShowCollapsedSummary));
                    OnPropertyChanged(nameof(ShowPagination));
                }
            }
        }

        public string ExpandToggleText => IsExpanded ? "收起明细" : "展开明细";

        public string CollapsedSummary => _summaryBuilder(_allItems);

        public bool ShowCollapsedSummary => !IsExpanded && ItemCount > 0;

        public bool ShowPagination => IsExpanded && ItemCount > _pageSize;

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(ItemCount / (double)_pageSize));

        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(CanGoPrevious));
                    OnPropertyChanged(nameof(CanGoNext));
                }
            }
        }

        public string PageInfo => ItemCount == 0
            ? "暂无明细"
            : $"第 {CurrentPage} / {TotalPages} 页，共 {ItemCount} 条";

        public bool CanGoPrevious => CurrentPage > 1;

        public bool CanGoNext => CurrentPage < TotalPages;

        public ICommand ToggleExpandCommand { get; }

        public ICommand FirstPageCommand { get; }

        public ICommand PreviousPageCommand { get; }

        public ICommand NextPageCommand { get; }

        public ICommand LastPageCommand { get; }

        /// <summary>
        /// 用最新明细刷新分页；未手动切换折叠状态时会按条数智能默认展开/折叠。
        /// </summary>
        public void RefreshItems(IEnumerable<T>? items, bool? preserveExpanded = null)
        {
            _allItems = items?.ToList() ?? new List<T>();

            if (preserveExpanded.HasValue)
            {
                _isExpanded = preserveExpanded.Value;
                _expandStateInitialized = true;
            }
            else if (!_expandStateInitialized)
            {
                _isExpanded = _allItems.Count <= _smartExpandThreshold;
                _expandStateInitialized = _allItems.Count > 0;
            }

            CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);
            LoadCurrentPage();

            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(ItemCountDisplay));
            OnPropertyChanged(nameof(IsExpanded));
            OnPropertyChanged(nameof(ExpandToggleText));
            OnPropertyChanged(nameof(CollapsedSummary));
            OnPropertyChanged(nameof(ShowCollapsedSummary));
            OnPropertyChanged(nameof(ShowPagination));
            OnPropertyChanged(nameof(PageInfo));
        }

        public void SetExpanded(bool expanded)
        {
            IsExpanded = expanded;
            _expandStateInitialized = true;
        }

        public void ResetExpandState()
        {
            _expandStateInitialized = false;
            RefreshItems(_allItems);
        }

        private void GoToPage(int page)
        {
            CurrentPage = Math.Clamp(page, 1, TotalPages);
            LoadCurrentPage();
        }

        private void LoadCurrentPage()
        {
            PageItems.Clear();
            if (ItemCount == 0)
            {
                CurrentPage = 1;
                return;
            }

            int startIndex = (CurrentPage - 1) * _pageSize;
            foreach (var item in _allItems.Skip(startIndex).Take(_pageSize))
            {
                PageItems.Add(item);
            }
        }
    }
}
