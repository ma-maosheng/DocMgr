using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveSimulatedRelocationPage : Page
    {
        private readonly IServiceScope _pageScope;
        public ArchiveSimulatedRelocationViewModel ViewModel { get; }

        public ArchiveSimulatedRelocationPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            ViewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveSimulatedRelocationViewModel>();
            DataContext = ViewModel;
            Loaded += ArchiveSimulatedRelocationPage_Loaded;
            Unloaded += ArchiveSimulatedRelocationPage_Unloaded;
        }

        private async void ArchiveSimulatedRelocationPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();
        }

        private void ArchiveSimulatedRelocationPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= ArchiveSimulatedRelocationPage_Loaded;
            Unloaded -= ArchiveSimulatedRelocationPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
