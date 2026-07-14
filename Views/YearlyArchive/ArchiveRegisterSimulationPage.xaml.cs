using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveRegisterSimulationPage : Page
    {
        private readonly IServiceScope _pageScope;

        public ArchiveRegisterSimulationPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<ArchiveRegisterSimulationViewModel>();
            Unloaded += ArchiveRegisterSimulationPage_Unloaded;
        }

        private void ArchiveRegisterSimulationPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Unloaded -= ArchiveRegisterSimulationPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
