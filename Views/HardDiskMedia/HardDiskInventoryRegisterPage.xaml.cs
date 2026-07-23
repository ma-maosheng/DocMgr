using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskInventoryRegisterPage : Page
    {
        private readonly IServiceScope _pageScope;

        public HardDiskInventoryRegisterPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskInventoryRegisterPageViewModel>();
            Loaded += HardDiskInventoryRegisterPage_Loaded;
            Unloaded += HardDiskInventoryRegisterPage_Unloaded;
        }

        private async void HardDiskInventoryRegisterPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskInventoryRegisterPageViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void HardDiskInventoryRegisterPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskInventoryRegisterPage_Loaded;
            Unloaded -= HardDiskInventoryRegisterPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HardDiskInventoryRegisterPageViewModel viewModel &&
                viewModel.OpenCommand.CanExecute(null))
            {
                viewModel.OpenCommand.Execute(null);
            }
        }
    }
}
