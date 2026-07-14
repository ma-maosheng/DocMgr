using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class OpticalDiscMediumLedgerPage : Page
    {
        private readonly IServiceScope _pageScope;

        public OpticalDiscMediumLedgerPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<OpticalDiscMediumLedgerViewModel>();

            Loaded += OpticalDiscMediumLedgerPage_Loaded;
            Unloaded += OpticalDiscMediumLedgerPage_Unloaded;
        }

        private async void OpticalDiscMediumLedgerPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is OpticalDiscMediumLedgerViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void OpticalDiscMediumLedgerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OpticalDiscMediumLedgerPage_Loaded;
            Unloaded -= OpticalDiscMediumLedgerPage_Unloaded;
            _pageScope.Dispose();
        }

        private async void BtnSearchTransactions_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is OpticalDiscMediumLedgerViewModel viewModel)
            {
                await viewModel.SearchTransactionsAsync();
            }
        }
    }
}
