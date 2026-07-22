using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskDisposalPage : Page
    {
        private readonly IServiceScope _pageScope;

        public HardDiskDisposalPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskDisposalPageViewModel>();
            Loaded += HardDiskDisposalPage_Loaded;
            Unloaded += HardDiskDisposalPage_Unloaded;
        }

        private async void HardDiskDisposalPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskDisposalPageViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void HardDiskDisposalPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskDisposalPage_Loaded;
            Unloaded -= HardDiskDisposalPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HardDiskDisposalPageViewModel viewModel &&
                viewModel.OpenCommand.CanExecute(null))
            {
                viewModel.OpenCommand.Execute(null);
            }
        }
    }
}
