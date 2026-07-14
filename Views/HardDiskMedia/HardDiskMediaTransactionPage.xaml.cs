using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediaTransactionPage : Page
    {
        private readonly IServiceScope _pageScope;

        public HardDiskMediaTransactionPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskMediaTransactionPageViewModel>();

            Loaded += HardDiskMediaTransactionPage_Loaded;
            Unloaded += HardDiskMediaTransactionPage_Unloaded;
        }

        private async void HardDiskMediaTransactionPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskMediaTransactionPageViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void HardDiskMediaTransactionPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediaTransactionPage_Loaded;
            Unloaded -= HardDiskMediaTransactionPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
