using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveCrossDomainTransferLedgerPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly ArchiveCrossDomainTransferLedgerViewModel _viewModel;
        private bool _preserveStateOnUnload;

        public ArchiveCrossDomainTransferLedgerPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveCrossDomainTransferLedgerViewModel>();
            DataContext = _viewModel;

            _viewModel.NavigateToFilingLedgerRequested += ViewModel_NavigateToFilingLedgerRequested;

            Loaded += ArchiveCrossDomainTransferLedgerPage_Loaded;
            Unloaded += ArchiveCrossDomainTransferLedgerPage_Unloaded;
        }

        private async void ArchiveCrossDomainTransferLedgerPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void ArchiveCrossDomainTransferLedgerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // 跳转立档台账后可通过「返回」回到本页，需保留事件订阅与页面 scope。
            if (_preserveStateOnUnload)
            {
                _preserveStateOnUnload = false;
                return;
            }

            Loaded -= ArchiveCrossDomainTransferLedgerPage_Loaded;
            Unloaded -= ArchiveCrossDomainTransferLedgerPage_Unloaded;
            _viewModel.NavigateToFilingLedgerRequested -= ViewModel_NavigateToFilingLedgerRequested;
            _pageScope.Dispose();
        }

        private void ViewModel_NavigateToFilingLedgerRequested(int filingFactId)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                _preserveStateOnUnload = true;
                mainWindow.NavigateToArchiveFilingLedger(filingFactId);
                return;
            }

            MessageBox.Show("当前无法打开立档台账。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LedgerGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.NavigateToFilingLedgerCommand.CanExecute(null))
            {
                _viewModel.NavigateToFilingLedgerCommand.Execute(null);
            }
        }
    }
}
