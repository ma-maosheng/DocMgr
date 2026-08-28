using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings;

/// <summary>
/// 帮助文档页：覆盖安装、备份与当前库路径。
/// </summary>
public partial class HelpPage : Page
{
    private readonly IServiceScope _pageScope;

    public HelpPage()
    {
        InitializeComponent();

        _pageScope = App.CurrentProvider.CreateScope();
        DataContext = _pageScope.ServiceProvider.GetRequiredService<HelpPageViewModel>();

        Unloaded += HelpPage_Unloaded;
    }

    private void HelpPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Unloaded -= HelpPage_Unloaded;
        _pageScope.Dispose();
    }
}
