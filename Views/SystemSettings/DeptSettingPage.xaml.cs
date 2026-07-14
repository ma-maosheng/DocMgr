using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using System.Windows;

namespace DocMgr.Views.SystemSettings
{
    public partial class DeptSettingPage : Page
    {
        private readonly IServiceScope _pageScope;
        public DeptSettingPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<DeptSettingViewModel>();

            Unloaded += DeptSettingPage_Unloaded;
        }

        private void DeptSettingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= DeptSettingPage_Unloaded;
            _pageScope.Dispose();
        }

    }
}