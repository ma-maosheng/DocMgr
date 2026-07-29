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
        private readonly bool _pendingInProgress;
        private readonly bool _matchAllYears;

        public HardDiskDisposalPage()
            : this(false, false)
        {
        }

        public HardDiskDisposalPage(bool pendingInProgress, bool matchAllYears = false)
        {
            InitializeComponent();
            _pendingInProgress = pendingInProgress;
            _matchAllYears = matchAllYears;
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskDisposalPageViewModel>();
            Loaded += HardDiskDisposalPage_Loaded;
            Unloaded += HardDiskDisposalPage_Unloaded;
        }

        private async void HardDiskDisposalPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskDisposalPageViewModel viewModel)
            {
                await viewModel.InitializeAsync(_pendingInProgress, _matchAllYears);
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
