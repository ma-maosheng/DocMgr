using DocMgr.ViewModels.HistoryArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HistoryArchive
{
    public partial class HistoryArchiveDisposalPage : Page
    {
        private readonly IServiceScope _pageScope;

        public HistoryArchiveDisposalPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<HistoryArchiveDisposalPageViewModel>();
            Loaded += async (_, _) =>
            {
                if (DataContext is HistoryArchiveDisposalPageViewModel vm)
                {
                    await vm.InitializeAsync();
                }
            };
            Unloaded += (_, _) => _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HistoryArchiveDisposalPageViewModel vm && vm.OpenDisposalCommand.CanExecute(null))
            {
                vm.OpenDisposalCommand.Execute(null);
            }
        }
    }
}
