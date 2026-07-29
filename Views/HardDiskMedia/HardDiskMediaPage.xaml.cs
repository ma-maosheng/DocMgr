using DocMgr.Models.HardDiskMedia;
using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediaPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly HardDiskMediaWorkbenchSection _initialSection;

        public HardDiskMediaPage()
            : this(HardDiskMediaWorkbenchSection.Ledger)
        {
        }

        public HardDiskMediaPage(HardDiskMediaWorkbenchSection initialSection)
        {
            InitializeComponent();

            _initialSection = initialSection;
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskMediaPageViewModel>();

            Loaded += HardDiskMediaPage_Loaded;
            Unloaded += HardDiskMediaPage_Unloaded;
        }

        private async void HardDiskMediaPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskMediaPageViewModel viewModel)
            {
                await viewModel.InitializeAsync(_initialSection);
            }
        }

        private void HardDiskMediaPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediaPage_Loaded;
            Unloaded -= HardDiskMediaPage_Unloaded;
            _pageScope.Dispose();
        }

        private void KpiCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: HardDiskOverviewKpiKind kind })
            {
                return;
            }

            if (DataContext is HardDiskMediaPageViewModel viewModel
                && viewModel.NavigateKpiCommand.CanExecute(kind))
            {
                viewModel.NavigateKpiCommand.Execute(kind);
            }
        }
    }
}
