using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using DocMgr.ViewModels.Cabinets;

namespace DocMgr.Views.Cabinets
{
    public partial class CabinetSearchPage : Page
    {
        private readonly IServiceScope _pageScope;

        public CabinetSearchPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<CabinetSearchViewModel>();

            Unloaded += CabinetSearchPage_Unloaded;
        }

        private void CabinetSearchPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Unloaded -= CabinetSearchPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
