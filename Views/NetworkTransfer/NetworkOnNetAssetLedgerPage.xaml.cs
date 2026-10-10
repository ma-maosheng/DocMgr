using DocMgr.ViewModels.NetworkTransfer;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace DocMgr.Views.NetworkTransfer;

public partial class NetworkOnNetAssetLedgerPage : Page
{
    private readonly IServiceScope _pageScope;

    public NetworkOnNetAssetLedgerPage()
    {
        InitializeComponent();
        _pageScope = App.CurrentProvider.CreateScope();
        DataContext = _pageScope.ServiceProvider.GetRequiredService<NetworkOnNetAssetLedgerPageViewModel>();
        Loaded += async (_, _) =>
        {
            if (DataContext is NetworkOnNetAssetLedgerPageViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
        Unloaded += (_, _) => _pageScope.Dispose();
    }
}
