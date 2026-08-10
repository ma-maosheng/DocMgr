using DocMgr.Models.NetworkTransfer;
using DocMgr.ViewModels.NetworkTransfer;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.NetworkTransfer
{
    public partial class NetworkInboundApprovalPage : Page
    {
        private readonly IServiceScope _pageScope;

        public NetworkInboundApprovalPage(int initialRecordId = 0)
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            var factory = _pageScope.ServiceProvider
                .GetRequiredService<Func<NetworkTransferWorkspaceMode, int, NetworkInboundWorkbenchPageViewModel>>();
            DataContext = factory(NetworkTransferWorkspaceMode.Approval, initialRecordId);
            Loaded += async (_, _) =>
            {
                if (DataContext is NetworkInboundWorkbenchPageViewModel vm)
                    await vm.InitializeAsync();
            };
            Unloaded += (_, _) => _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is NetworkInboundWorkbenchPageViewModel vm && vm.OpenCommand.CanExecute(null))
                vm.OpenCommand.Execute(null);
        }
    }
}
