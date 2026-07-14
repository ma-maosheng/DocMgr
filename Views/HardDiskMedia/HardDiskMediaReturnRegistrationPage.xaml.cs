using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediaReturnRegistrationPage : Page
    {
        private readonly IServiceScope _pageScope;

        public HardDiskMediaReturnRegistrationPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskMediaReturnRegistrationPageViewModel>();

            Loaded += HardDiskMediaReturnRegistrationPage_Loaded;
            Unloaded += HardDiskMediaReturnRegistrationPage_Unloaded;
        }

        private async void HardDiskMediaReturnRegistrationPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskMediaReturnRegistrationPageViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void HardDiskMediaReturnRegistrationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediaReturnRegistrationPage_Loaded;
            Unloaded -= HardDiskMediaReturnRegistrationPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgApplications_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HardDiskMediaReturnRegistrationPageViewModel viewModel &&
                viewModel.OpenReturnCommand.CanExecute(null))
            {
                viewModel.OpenReturnCommand.Execute(null);
            }
        }

        private void DgCandidates_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HardDiskMediaReturnRegistrationPageViewModel viewModel &&
                viewModel.StartReturnCommand.CanExecute(null))
            {
                viewModel.StartReturnCommand.Execute(null);
            }
        }
    }
}
