using DocMgr.ViewModels.HistoryArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace DocMgr.Views.HistoryArchive;

public partial class HistoryArchiveBoxLedgerPage : Page
{
    private readonly IServiceScope _pageScope;

    public HistoryArchiveBoxLedgerPage()
    {
        InitializeComponent();
        _pageScope = App.CurrentProvider.CreateScope();
        DataContext = _pageScope.ServiceProvider.GetRequiredService<HistoryArchiveBoxLedgerPageViewModel>();
        Loaded += async (_, _) =>
        {
            if (DataContext is HistoryArchiveBoxLedgerPageViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
        Unloaded += (_, _) => _pageScope.Dispose();
    }
}
