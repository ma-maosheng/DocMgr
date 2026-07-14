using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveDetailPage : Page
    {
        private const double ElectronicMediaItemRowHeight = 32d;
        private const double ContentEntryRowHeight = 32d;
        private const double GenericMediaItemRowHeight = 32d;

        private readonly IServiceScope _pageScope;
        private readonly int _recordId;
        private readonly ArchiveDetailHighlightContext? _searchHighlight;
        private readonly string? _filterPoolMediaKind;
        private readonly int? _filingFactId;

        public double ElectronicMediaItemsViewportHeight => ElectronicMediaItemRowHeight * 8;

        public double ElectronicContentEntriesViewportHeight => ContentEntryRowHeight * 6 + 36;

        public double GenericMediaItemsViewportHeight => GenericMediaItemRowHeight * 7;

        public ArchiveDetailPage(
            int recordId,
            ArchiveDetailHighlightContext? searchHighlight = null,
            string? filterPoolMediaKind = null,
            int? filingFactId = null)
        {
            InitializeComponent();

            _recordId = recordId;
            _searchHighlight = searchHighlight;
            _filterPoolMediaKind = filterPoolMediaKind;
            _filingFactId = filingFactId;
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<ArchiveDetailViewModel>();

            Loaded += ArchiveDetailPage_Loaded;
            Unloaded += ArchiveDetailPage_Unloaded;
        }

        private async void ArchiveDetailPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ArchiveDetailViewModel vm)
            {
                await vm.InitializeAsync(_recordId, _searchHighlight, _filterPoolMediaKind, _filingFactId);
            }
        }

        private void ArchiveDetailPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ArchiveDetailPage_Loaded;
            Unloaded -= ArchiveDetailPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
