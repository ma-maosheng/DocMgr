using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveOutboundApplyPage : Page
    {
        private readonly IServiceScope _pageScope;

        public ArchiveOutboundApplyPage(int initialRecordId = 0)
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            var vmFactory = _pageScope.ServiceProvider
                .GetRequiredService<Func<ArchiveOutboundWorkspaceMode, int, ArchiveOutboundWorkbenchPageViewModel>>();
            DataContext = vmFactory(ArchiveOutboundWorkspaceMode.Application, initialRecordId);
            Loaded += async (_, _) =>
            {
                if (DataContext is ArchiveOutboundWorkbenchPageViewModel vm)
                {
                    await vm.InitializeAsync();
                    if (initialRecordId > 0)
                    {
                        await vm.OpenRecordByIdAsync(initialRecordId);
                    }
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
