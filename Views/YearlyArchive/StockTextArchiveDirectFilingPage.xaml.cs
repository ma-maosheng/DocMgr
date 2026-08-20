using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class StockTextArchiveDirectFilingPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly StockTextArchiveDirectFilingViewModel _viewModel;

        public StockTextArchiveDirectFilingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<StockTextArchiveDirectFilingViewModel>();
            DataContext = _viewModel;

            Loaded += StockTextArchiveDirectFilingPage_Loaded;
            Unloaded += StockTextArchiveDirectFilingPage_Unloaded;
        }

        private async void StockTextArchiveDirectFilingPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void StockTextArchiveDirectFilingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StockTextArchiveDirectFilingPage_Loaded;
            Unloaded -= StockTextArchiveDirectFilingPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
