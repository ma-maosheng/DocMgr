using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveElectronicRelocationPage : Page
    {
        private readonly IServiceScope _pageScope;
        public ArchiveElectronicRelocationViewModel ViewModel { get; }

        public ArchiveElectronicRelocationPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            ViewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveElectronicRelocationViewModel>();
            DataContext = ViewModel;
            Loaded += ArchiveElectronicRelocationPage_Loaded;
            Unloaded += ArchiveElectronicRelocationPage_Unloaded;
        }

        private async void ArchiveElectronicRelocationPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();
        }

        private void ArchiveElectronicRelocationPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= ArchiveElectronicRelocationPage_Loaded;
            Unloaded -= ArchiveElectronicRelocationPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
