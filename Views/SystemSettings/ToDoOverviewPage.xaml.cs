using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings;

public partial class ToDoOverviewPage : Page
{
    private readonly IServiceScope _pageScope;

    public ToDoOverviewPage()
    {
        InitializeComponent();
        _pageScope = App.CurrentProvider.CreateScope();
        var vm = _pageScope.ServiceProvider.GetRequiredService<ToDoOverviewPageViewModel>();
        DataContext = vm;

        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
        {
            vm.OpenItemHandler = mainWindow.OpenToDoItemAsync;
        }

        Loaded += async (_, _) => await vm.InitializeAsync();
        Unloaded += (_, _) => _pageScope.Dispose();
    }

    private void DgItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ToDoOverviewPageViewModel vm && vm.OpenCommand.CanExecute(null))
        {
            vm.OpenCommand.Execute(null);
        }
    }
}
