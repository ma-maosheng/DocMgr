using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveInventoryRegisterPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly string? _initialStatus;
        private readonly bool _matchAllYears;

        public string MediaKind { get; }

        public ArchiveInventoryRegisterPage(string mediaKind)
            : this(mediaKind, null, false)
        {
        }

        public ArchiveInventoryRegisterPage(string mediaKind, string? initialStatus, bool matchAllYears = false)
        {
            InitializeComponent();
            MediaKind = mediaKind?.Trim() ?? string.Empty;
            _initialStatus = initialStatus;
            _matchAllYears = matchAllYears;
            _pageScope = App.CurrentProvider.CreateScope();
            var viewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveInventoryRegisterPageViewModel>();
            viewModel.Configure(MediaKind);
            DataContext = viewModel;
            Loaded += ArchiveInventoryRegisterPage_Loaded;
            Unloaded += ArchiveInventoryRegisterPage_Unloaded;
        }

        private async void ArchiveInventoryRegisterPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ArchiveInventoryRegisterPageViewModel viewModel)
            {
                await viewModel.InitializeAsync(_initialStatus, _matchAllYears);
            }
        }

        private void ArchiveInventoryRegisterPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ArchiveInventoryRegisterPage_Loaded;
            Unloaded -= ArchiveInventoryRegisterPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ArchiveInventoryRegisterPageViewModel viewModel &&
                viewModel.OpenCommand.CanExecute(null))
            {
                viewModel.OpenCommand.Execute(null);
            }
        }
    }
}
