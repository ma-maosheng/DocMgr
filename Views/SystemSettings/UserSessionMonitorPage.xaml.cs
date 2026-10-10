using System.Windows.Controls;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings;

public partial class UserSessionMonitorPage : Page
{
    private readonly IServiceScope _pageScope;

    public UserSessionMonitorPage()
    {
        InitializeComponent();

        _pageScope = App.CurrentProvider.CreateScope();
        DataContext = _pageScope.ServiceProvider.GetRequiredService<UserSessionMonitorViewModel>();

        var logContext = _pageScope.ServiceProvider.GetRequiredService<IDbOperationLogContextService>();
        logContext.SetCurrentPage("系统设置（在线用户与登录记录）");

        Loaded += UserSessionMonitorPage_Loaded;
        Unloaded += UserSessionMonitorPage_Unloaded;
    }

    private async void UserSessionMonitorPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Loaded -= UserSessionMonitorPage_Loaded;

        if (DataContext is UserSessionMonitorViewModel viewModel)
        {
            await viewModel.LoadOnPageDisplayedAsync();
        }
    }

    private void UserSessionMonitorPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Unloaded -= UserSessionMonitorPage_Unloaded;
        _pageScope.Dispose();
    }
}
