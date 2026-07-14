using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using DocMgr.ViewModels.Cabinets;
using System.Windows;

namespace DocMgr.Views.Cabinets
{
    public partial class CabinetLayoutPage : Page
    {
        private readonly IServiceScope _pageScope;
        public CabinetLayoutPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<CabinetLayoutViewModel>();

            Unloaded += CabinetLayoutPage_Unloaded;
        }

        private void CabinetLayoutPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= CabinetLayoutPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}