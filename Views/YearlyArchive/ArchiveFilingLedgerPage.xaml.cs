using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveFilingLedgerPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly ArchiveFilingLedgerViewModel _viewModel;
        private bool _preserveStateOnUnload;

        public ArchiveFilingLedgerPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveFilingLedgerViewModel>();
            DataContext = _viewModel;

            _viewModel.ViewRegisterDetailRequested += ViewModel_ViewRegisterDetailRequested;

            Loaded += ArchiveFilingLedgerPage_Loaded;
            Unloaded += ArchiveFilingLedgerPage_Unloaded;
        }

        private async void ArchiveFilingLedgerPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
            await _viewModel.ApplyPendingNavigationFocusAsync();
        }

        private void ArchiveFilingLedgerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_preserveStateOnUnload)
            {
                _preserveStateOnUnload = false;
                return;
            }

            Loaded -= ArchiveFilingLedgerPage_Loaded;
            Unloaded -= ArchiveFilingLedgerPage_Unloaded;
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
    }
}
