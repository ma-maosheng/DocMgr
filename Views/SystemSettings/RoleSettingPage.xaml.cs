using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using DocMgr.ViewModels.SystemSettings;
using System.Windows;

namespace DocMgr.Views.SystemSettings
{
    public partial class RoleSettingPage : Page
    {
        private readonly IServiceScope _pageScope;
        public RoleSettingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<RoleSettingViewModel>();

            Unloaded += RoleSettingPage_Unloaded;
        }

        private void RoleSettingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= RoleSettingPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}