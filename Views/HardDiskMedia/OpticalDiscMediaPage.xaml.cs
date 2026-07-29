using DocMgr.Models.OpticalDiscMedia;
using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class OpticalDiscMediaPage : Page
    {
        private readonly IServiceScope _pageScope;

        public OpticalDiscMediaPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<OpticalDiscMediaPageViewModel>();

            Loaded += OpticalDiscMediaPage_Loaded;
            Unloaded += OpticalDiscMediaPage_Unloaded;
        }

        private async void OpticalDiscMediaPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is OpticalDiscMediaPageViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void OpticalDiscMediaPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OpticalDiscMediaPage_Loaded;
            Unloaded -= OpticalDiscMediaPage_Unloaded;
            _pageScope.Dispose();
        }

        private void KpiCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: OpticalDiscOverviewKpiKind kind })
            {
                return;
            }

            if (DataContext is OpticalDiscMediaPageViewModel viewModel
                && viewModel.NavigateKpiCommand.CanExecute(kind))
            {
                viewModel.NavigateKpiCommand.Execute(kind);
            }
        }
    }
}
