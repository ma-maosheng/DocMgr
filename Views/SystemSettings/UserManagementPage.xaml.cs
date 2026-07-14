using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection; // 用于解析服务
using DocMgr.ViewModels.SystemSettings;
using System.Windows; // 引用 VM

namespace DocMgr.Views.SystemSettings
{
    public partial class UserManagementPage : Page
    {
        private readonly IServiceScope _pageScope;
        public UserManagementPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<UserManagementViewModel>();

            Unloaded += UserManagementPage_Unloaded;
        }

        private void UserManagementPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= UserManagementPage_Unloaded;
            _pageScope.Dispose();
        }
    }

}