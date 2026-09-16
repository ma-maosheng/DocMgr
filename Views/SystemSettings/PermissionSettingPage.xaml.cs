using System.Windows;
using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings
{
    /// <summary>
    /// 权限设置页：展示角色列表与各角色类别的菜单权限矩阵（只读查阅）。
    /// </summary>
    public partial class PermissionSettingPage : Page
    {
        private readonly IServiceScope _pageScope;

        public PermissionSettingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<PermissionSettingViewModel>();

            Unloaded += PermissionSettingPage_Unloaded;
        }

        private void PermissionSettingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= PermissionSettingPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
