using DocMgr.ViewModels.NetworkTransfer;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.NetworkTransfer
{
    public partial class NetworkOnNetDisposalPage : Page
    {
        private readonly IServiceScope _pageScope;

        public NetworkOnNetDisposalPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<NetworkOnNetDisposalPageViewModel>();
            Loaded += async (_, _) =>
            {
                if (DataContext is NetworkOnNetDisposalPageViewModel vm)
                    await vm.InitializeAsync();
            };
            Unloaded += (_, _) => _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is NetworkOnNetDisposalPageViewModel vm && vm.OpenDisposalCommand.CanExecute(null))
                vm.OpenDisposalCommand.Execute(null);
        }
    }
}
