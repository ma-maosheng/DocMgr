using DocMgr.Models.NetworkTransfer;
using DocMgr.ViewModels.NetworkTransfer;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.NetworkTransfer
{
    public partial class NetworkOutboundApplicationPage : Page
    {
        private readonly IServiceScope _pageScope;

        public NetworkOutboundApplicationPage(int initialRecordId = 0)
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            var factory = _pageScope.ServiceProvider
                .GetRequiredService<Func<NetworkTransferWorkspaceMode, int, NetworkOutboundWorkbenchPageViewModel>>();
            DataContext = factory(NetworkTransferWorkspaceMode.Application, initialRecordId);
            Loaded += async (_, _) =>
            {
                if (DataContext is NetworkOutboundWorkbenchPageViewModel vm)
                    await vm.InitializeAsync();
            };
            Unloaded += (_, _) => _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is NetworkOutboundWorkbenchPageViewModel vm && vm.OpenCommand.CanExecute(null))
                vm.OpenCommand.Execute(null);
        }
    }
}
