using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using DocMgr.ViewModels.SystemSettings;

namespace DocMgr.Views.SystemSettings
{
    public partial class ServerPathSettingPage : Page
    {
        private readonly IServiceScope _pageScope;

        public ServerPathSettingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<ServerPathSettingViewModel>();

            Unloaded += ServerPathSettingPage_Unloaded;
        }

        private void ServerPathSettingPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Unloaded -= ServerPathSettingPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
