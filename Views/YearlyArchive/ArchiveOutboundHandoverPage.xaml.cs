using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveOutboundHandoverPage : Page
    {
        private readonly IServiceScope _pageScope;

        public ArchiveOutboundHandoverPage(int initialRecordId = 0)
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            var vmFactory = _pageScope.ServiceProvider
                .GetRequiredService<Func<ArchiveOutboundWorkspaceMode, int, ArchiveOutboundWorkbenchPageViewModel>>();
            DataContext = vmFactory(ArchiveOutboundWorkspaceMode.Handover, initialRecordId);
            Loaded += async (_, _) =>
            {
                if (DataContext is ArchiveOutboundWorkbenchPageViewModel vm)
                {
                    await vm.InitializeAsync();
                }
            };
            Unloaded += (_, _) => _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ArchiveOutboundWorkbenchPageViewModel vm && vm.OpenCommand.CanExecute(null))
            {
                vm.OpenCommand.Execute(null);
            }
        }
    }
}
