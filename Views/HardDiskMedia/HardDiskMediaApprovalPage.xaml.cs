using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediaApprovalPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly int? _initialApplicationId;

        public HardDiskMediaApprovalPage()
            : this(null)
        {
        }

        public HardDiskMediaApprovalPage(int? initialApplicationId)
        {
            InitializeComponent();

            _initialApplicationId = initialApplicationId;
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskMediaApprovalPageViewModel>();

            Loaded += HardDiskMediaApprovalPage_Loaded;
            Unloaded += HardDiskMediaApprovalPage_Unloaded;
        }

        private async void HardDiskMediaApprovalPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskMediaApprovalPageViewModel viewModel)
            {
                await viewModel.InitializeAsync(_initialApplicationId);
            }
        }

        private void HardDiskMediaApprovalPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediaApprovalPage_Loaded;
            Unloaded -= HardDiskMediaApprovalPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
