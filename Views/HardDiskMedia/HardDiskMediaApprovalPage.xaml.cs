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
        private readonly string? _initialStatusLabel;
        private readonly bool? _signedAttachmentUploadedFilter;
        private readonly bool _matchAllYears;

        public HardDiskMediaApprovalPage()
            : this(null)
        {
        }

        public HardDiskMediaApprovalPage(int? initialApplicationId)
            : this(initialApplicationId, null, null, false)
        {
        }

        public HardDiskMediaApprovalPage(
            int? initialApplicationId,
            string? initialStatusLabel,
            bool? signedAttachmentUploadedFilter = null,
            bool matchAllYears = false)
        {
            InitializeComponent();

            _initialApplicationId = initialApplicationId;
            _initialStatusLabel = initialStatusLabel;
            _signedAttachmentUploadedFilter = signedAttachmentUploadedFilter;
            _matchAllYears = matchAllYears;
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HardDiskMediaApprovalPageViewModel>();

            Loaded += HardDiskMediaApprovalPage_Loaded;
            Unloaded += HardDiskMediaApprovalPage_Unloaded;
        }

        private async void HardDiskMediaApprovalPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskMediaApprovalPageViewModel viewModel)
            {
                await viewModel.InitializeAsync(
                    _initialApplicationId,
                    _initialStatusLabel,
                    _signedAttachmentUploadedFilter,
                    _matchAllYears);
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
