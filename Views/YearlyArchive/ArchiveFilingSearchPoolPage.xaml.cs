using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveFilingSearchPoolPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly ArchiveFilingSearchPoolViewModel _viewModel;
        private bool _preserveStateOnUnload;

        public ArchiveFilingSearchPoolPage(string mediaKind)
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            var createViewModel = _pageScope.ServiceProvider.GetRequiredService<Func<string, ArchiveFilingSearchPoolViewModel>>();
            _viewModel = createViewModel(mediaKind);
            DataContext = _viewModel;

            _viewModel.ViewRegisterDetailRequested += ViewModel_ViewRegisterDetailRequested;
            _viewModel.CreateOutboundRequested += ViewModel_CreateOutboundRequested;
            _viewModel.CreateInboundRequested += ViewModel_CreateInboundRequested;

            Loaded += ArchiveFilingSearchPoolPage_Loaded;
            Unloaded += ArchiveFilingSearchPoolPage_Unloaded;
        }

        private async void ArchiveFilingSearchPoolPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void ArchiveFilingSearchPoolPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_preserveStateOnUnload)
            {
                _preserveStateOnUnload = false;
                return;
            }

            Loaded -= ArchiveFilingSearchPoolPage_Loaded;
            Unloaded -= ArchiveFilingSearchPoolPage_Unloaded;
            _viewModel.ViewRegisterDetailRequested -= ViewModel_ViewRegisterDetailRequested;
            _viewModel.CreateOutboundRequested -= ViewModel_CreateOutboundRequested;
            _viewModel.CreateInboundRequested -= ViewModel_CreateInboundRequested;
            _pageScope.Dispose();
        }

        private void ViewModel_CreateInboundRequested(int recordId)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                _preserveStateOnUnload = true;
                mainWindow.NavigateToNetworkInboundApplyPage(recordId);
            }
        }

        private void ViewModel_CreateOutboundRequested(int recordId)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                _preserveStateOnUnload = true;
                mainWindow.NavigateToArchiveOutboundApplyPage(recordId);
            }
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
    }
}
