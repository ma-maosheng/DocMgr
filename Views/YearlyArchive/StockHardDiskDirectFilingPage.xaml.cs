using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class StockHardDiskDirectFilingPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly StockHardDiskDirectFilingViewModel _viewModel;

        public StockHardDiskDirectFilingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<StockHardDiskDirectFilingViewModel>();
            DataContext = _viewModel;

            Loaded += StockHardDiskDirectFilingPage_Loaded;
            Unloaded += StockHardDiskDirectFilingPage_Unloaded;
        }

        private async void StockHardDiskDirectFilingPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void StockHardDiskDirectFilingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StockHardDiskDirectFilingPage_Loaded;
            Unloaded -= StockHardDiskDirectFilingPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
