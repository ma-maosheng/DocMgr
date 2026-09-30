using DocMgr.ViewModels.Inventory;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DocMgr.Views.Inventory
{
    public partial class StockContainerOverviewPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly StockContainerOverviewViewModel _viewModel;

        public StockContainerOverviewPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<StockContainerOverviewViewModel>();
            DataContext = _viewModel;

            _viewModel.OpenFilingLedgerRequested += ViewModel_OpenFilingLedgerRequested;
            Loaded += StockContainerOverviewPage_Loaded;
            Unloaded += StockContainerOverviewPage_Unloaded;
        }

        private async void StockContainerOverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await _viewModel.InitializeAsync();
        }

        private void StockContainerOverviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StockContainerOverviewPage_Loaded;
            Unloaded -= StockContainerOverviewPage_Unloaded;
            _viewModel.OpenFilingLedgerRequested -= ViewModel_OpenFilingLedgerRequested;
            _pageScope.Dispose();
        }

        private void ViewModel_OpenFilingLedgerRequested(int filingFactId)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToArchiveFilingLedger(filingFactId);
                return;
            }

            MessageBox.Show("当前无法打开立档台账。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
