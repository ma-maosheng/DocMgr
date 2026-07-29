using DocMgr.Models.OpticalDiscMedia;
using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class OpticalDiscMediumLedgerPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly string? _initialStatus;
        private readonly OpticalDiscLedgerQuickFilter _quickFilter;
        private readonly bool _recentTransactionsOnly;

        public OpticalDiscMediumLedgerPage()
            : this(null, OpticalDiscLedgerQuickFilter.None, false)
        {
        }

        public OpticalDiscMediumLedgerPage(
            string? initialStatus,
            OpticalDiscLedgerQuickFilter quickFilter = OpticalDiscLedgerQuickFilter.None,
            bool recentTransactionsOnly = false)
        {
            InitializeComponent();

            _initialStatus = initialStatus;
            _quickFilter = quickFilter;
            _recentTransactionsOnly = recentTransactionsOnly;
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<OpticalDiscMediumLedgerViewModel>();

            Loaded += OpticalDiscMediumLedgerPage_Loaded;
            Unloaded += OpticalDiscMediumLedgerPage_Unloaded;
        }

        private async void OpticalDiscMediumLedgerPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is OpticalDiscMediumLedgerViewModel viewModel)
            {
                await viewModel.InitializeAsync(_initialStatus, _quickFilter, _recentTransactionsOnly);
            }
        }

        private void OpticalDiscMediumLedgerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OpticalDiscMediumLedgerPage_Loaded;
            Unloaded -= OpticalDiscMediumLedgerPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
