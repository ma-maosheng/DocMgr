using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveDisposalPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly bool _pendingInProgress;
        private readonly bool _matchAllYears;

        public string MediaKind { get; }

        public ArchiveDisposalPage(string mediaKind)
            : this(mediaKind, false, false)
        {
        }

        public ArchiveDisposalPage(string mediaKind, bool pendingInProgress, bool matchAllYears = false)
        {
            InitializeComponent();
            MediaKind = mediaKind?.Trim() ?? string.Empty;
            _pendingInProgress = pendingInProgress;
            _matchAllYears = matchAllYears;
            _pageScope = App.CurrentProvider.CreateScope();
            var viewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveDisposalPageViewModel>();
            viewModel.Configure(MediaKind);
            DataContext = viewModel;
            Loaded += ArchiveDisposalPage_Loaded;
            Unloaded += ArchiveDisposalPage_Unloaded;
        }

        private async void ArchiveDisposalPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ArchiveDisposalPageViewModel viewModel)
            {
                await viewModel.InitializeAsync(_pendingInProgress, _matchAllYears);
            }
        }

        private void ArchiveDisposalPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ArchiveDisposalPage_Loaded;
            Unloaded -= ArchiveDisposalPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ArchiveDisposalPageViewModel viewModel &&
                viewModel.OpenCommand.CanExecute(null))
            {
                viewModel.OpenCommand.Execute(null);
            }
        }
    }
}
