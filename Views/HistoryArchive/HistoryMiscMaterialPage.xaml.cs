using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.ViewModels.HistoryArchive;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.HistoryArchive;

public partial class HistoryMiscMaterialPage : Page
{
    private readonly IServiceScope _pageScope;

    public HistoryMiscMaterialPage()
    {
        InitializeComponent();
        _pageScope = App.CurrentProvider.CreateScope();
        DataContext = _pageScope.ServiceProvider.GetRequiredService<HistoryMiscMaterialPageViewModel>();
        Unloaded += HistoryMiscMaterialPage_Unloaded;
    }

    private void HistoryMiscMaterialPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Unloaded -= HistoryMiscMaterialPage_Unloaded;
        _pageScope.Dispose();
    }

    private void DgItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is HistoryMiscMaterialPageViewModel vm && vm.OpenCommand.CanExecute(null))
        {
            vm.OpenCommand.Execute(null);
        }
    }
}
