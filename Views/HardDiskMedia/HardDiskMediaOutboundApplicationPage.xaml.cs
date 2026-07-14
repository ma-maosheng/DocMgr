using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediaOutboundApplicationPage : Page
    {
        private readonly IServiceScope _pageScope;

        public HardDiskMediaOutboundApplicationPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskMediaOutboundApplicationPageViewModel>();

            Loaded += HardDiskMediaOutboundApplicationPage_Loaded;
            Unloaded += HardDiskMediaOutboundApplicationPage_Unloaded;
        }

        private async void HardDiskMediaOutboundApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskMediaOutboundApplicationPageViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void HardDiskMediaOutboundApplicationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediaOutboundApplicationPage_Loaded;
            Unloaded -= HardDiskMediaOutboundApplicationPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgApplications_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HardDiskMediaOutboundApplicationPageViewModel viewModel &&
                viewModel.EditCommand.CanExecute(null))
            {
                viewModel.EditCommand.Execute(null);
            }
        }
    }
}
