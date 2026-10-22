using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class StockOpticalDiscDirectFilingPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly StockOpticalDiscDirectFilingViewModel _viewModel;

        public StockOpticalDiscDirectFilingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<StockOpticalDiscDirectFilingViewModel>();
            DataContext = _viewModel;

            Loaded += StockOpticalDiscDirectFilingPage_Loaded;
            Unloaded += StockOpticalDiscDirectFilingPage_Unloaded;
        }

        private async void StockOpticalDiscDirectFilingPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void StockOpticalDiscDirectFilingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StockOpticalDiscDirectFilingPage_Loaded;
            Unloaded -= StockOpticalDiscDirectFilingPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
