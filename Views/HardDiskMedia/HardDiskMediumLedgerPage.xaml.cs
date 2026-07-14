using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediumLedgerPage : Page
    {
        private readonly IServiceScope _pageScope;

        public HardDiskMediumLedgerPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskMediumLedgerViewModel>();

            Loaded += HardDiskMediumLedgerPage_Loaded;
            Unloaded += HardDiskMediumLedgerPage_Unloaded;
        }

        private async void HardDiskMediumLedgerPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskMediumLedgerViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void HardDiskMediumLedgerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediumLedgerPage_Loaded;
            Unloaded -= HardDiskMediumLedgerPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgMedia_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HardDiskMediumLedgerViewModel viewModel &&
                viewModel.EditCommand.CanExecute(null))
            {
                viewModel.EditCommand.Execute(null);
            }
        }
    }
}
