using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ElectronicMediaItemEntriesDialogViewModel : ViewModelBase
    {
        private const int PageSize = 100;

        private readonly IReadOnlyList<ElectronicMediaItemEntryDisplayItem> _allEntries;
        private int _currentPage = 1;

        public ElectronicMediaItemEntriesDialogViewModel(
            string title,
            IReadOnlyList<ElectronicMediaItemEntryDisplayItem> entries,
            string summaryText)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "目录/文件明细" : title.Trim();
            SummaryText = summaryText ?? string.Empty;
            _allEntries = entries ?? Array.Empty<ElectronicMediaItemEntryDisplayItem>();
            PageEntries = new ObservableCollection<ElectronicMediaItemEntryDisplayItem>();

            FirstPageCommand = new RelayCommand(_ => GoToPage(1), _ => CanGoPrevious);
            PreviousPageCommand = new RelayCommand(_ => GoToPage(CurrentPage - 1), _ => CanGoPrevious);
            NextPageCommand = new RelayCommand(_ => GoToPage(CurrentPage + 1), _ => CanGoNext);
            LastPageCommand = new RelayCommand(_ => GoToPage(TotalPages), _ => CanGoNext);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
            LoadCurrentPage();
        }

        public string Title { get; }

        public string SummaryText { get; }

        public bool HasSummaryText => !string.IsNullOrWhiteSpace(SummaryText);

        public ObservableCollection<ElectronicMediaItemEntryDisplayItem> PageEntries { get; }

        public int TotalCount => _allEntries.Count;

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

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

        public string PageInfo => TotalCount == 0
            ? "暂无明细"
            : $"第 {CurrentPage} / {TotalPages} 页，共 {TotalCount} 条";

        public bool CanGoPrevious => CurrentPage > 1;

        public bool CanGoNext => CurrentPage < TotalPages;

        public ICommand FirstPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }
        public ICommand CloseCommand { get; }

        public event Action? RequestClose;

        public void LoadCurrentPage()
        {
            PageEntries.Clear();

            if (TotalCount == 0)
            {
                CurrentPage = 1;
                return;
            }

            int safePage = Math.Clamp(CurrentPage, 1, TotalPages);
            if (safePage != CurrentPage)
            {
                CurrentPage = safePage;
            }

            int startIndex = (CurrentPage - 1) * PageSize;
            foreach (var entry in _allEntries.Skip(startIndex).Take(PageSize))
            {
                PageEntries.Add(entry);
            }
        }

        private void GoToPage(int page)
        {
            CurrentPage = Math.Clamp(page, 1, TotalPages);
            LoadCurrentPage();
        }
    }
}
