using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveFilingSearchPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly ArchiveFilingSearchViewModel _viewModel;
        private bool _preserveStateOnUnload;

        public ArchiveFilingSearchPage(string mediaKind)
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            var createViewModel = _pageScope.ServiceProvider.GetRequiredService<Func<string, ArchiveFilingSearchViewModel>>();
            _viewModel = createViewModel(mediaKind);
            DataContext = _viewModel;

            _viewModel.ViewRegisterDetailRequested += ViewModel_ViewRegisterDetailRequested;

            Loaded += ArchiveFilingSearchPage_Loaded;
            Unloaded += ArchiveFilingSearchPage_Unloaded;
        }

        private async void ArchiveFilingSearchPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void ArchiveFilingSearchPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_preserveStateOnUnload)
            {
                _preserveStateOnUnload = false;
                return;
            }

            Loaded -= ArchiveFilingSearchPage_Loaded;
            Unloaded -= ArchiveFilingSearchPage_Unloaded;
            _viewModel.ViewRegisterDetailRequested -= ViewModel_ViewRegisterDetailRequested;
            _pageScope.Dispose();
        }

        private void ViewModel_ViewRegisterDetailRequested(ArchiveDetailOpenRequest request)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                _preserveStateOnUnload = true;
                mainWindow.NavigateToArchiveDetailPage(
                    request.RegisterRecordId,
                    request.SearchHighlight,
                    request.FilterPoolMediaKind,
                    request.FilingFactId);
                return;
            }

            MessageBox.Show("当前无法打开资料查看页。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenSearchPool_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                _preserveStateOnUnload = true;
                mainWindow.NavigateToArchiveFilingSearchPoolPage(_viewModel.MediaKind);
                return;
            }

            MessageBox.Show("当前无法打开检索池页面。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
